using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ws_proxy_server
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseWebSockets();

            app.Use(HandleRequestAsync);
        }

        static async Task HandleRequestAsync(HttpContext context, Func<Task> next)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<HttpContext>>();

            var wsf = context.Features.Get<IHttpWebSocketFeature>();
            logger.LogInformation("WebSockets are present: {0}", wsf != null ? bool.TrueString : bool.FalseString);
            logger.LogInformation("WebSockets request: {0}", wsf?.IsWebSocketRequest ?? false ? bool.TrueString : bool.FalseString);

            if (!context.WebSockets.IsWebSocketRequest)
            {
                logger.LogInformation("Non-websocket request from {0}:{1} to {2}", context.Connection.RemoteIpAddress, context.Connection.RemotePort, context.Request.Path);
                await next().ConfigureAwait(false);
                return;
            }

            var targetHost = context.Request.GetSingleHeaderValue("WS-Proxy-Target-Host");
            var targetPortString = context.Request.GetSingleHeaderValue("WS-Proxy-Target-Port");

            if (targetHost == null || targetPortString == null)
            {
                logger.LogWarning("Missing one or more WS-Proxy-Targets headers.");
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;

            }

            if (!int.TryParse(targetPortString, out var targetPort) || targetPort <= 0 || targetPort > ushort.MaxValue)
            {
                logger.LogWarning("Target port '{0}' is invalid or out of range.", targetPortString);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            var endPoint = IPAddress.TryParse(targetHost, out var address) ? (EndPoint)new IPEndPoint(address, targetPort) : new DnsEndPoint(targetHost, targetPort);

            //var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
            //var targetHost = configuration["PROXY_TARGET_HOST"];
            //var targetPort = int.Parse(configuration["PROXY_TARGET_PORT"]);
            //var endPoint = IPAddress.TryParse(targetHost, out var address) ? (EndPoint)new IPEndPoint(address, targetPort) : new DnsEndPoint(targetHost, targetPort);

            var ws = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            var sock = new Socket(SocketType.Stream, ProtocolType.Tcp);

            logger.LogTrace("Connecting to {0}", endPoint);

            await sock.ConnectAsyncTask(endPoint).ConfigureAwait(false);

            var uploadTask = CopyToAsync(logger, sock, ws);
            var downloadTask = CopyToAsync(logger, ws, sock);

            await Task.WhenAll(uploadTask, downloadTask).ConfigureAwait(false);

        }

        static async Task CopyToAsync(ILogger logger, Socket sock, WebSocket ws)
        {
            var buffer = new byte[1024];
            var readSegment = new ArraySegment<byte>(buffer, 0, buffer.Length);

            int read;
            do
            {
                read = await sock.ReceiveAsync(readSegment, SocketFlags.None).ConfigureAwait(false);
                if (read > 0)
                {
                    logger.LogDebug("Read {0} bytes from TCP socket.", read);
                    await ws.SendAsync(new ArraySegment<byte>(buffer, 0, read), WebSocketMessageType.Binary, false, default);
                }
            }
            while (read > 0);
        }

        static async Task CopyToAsync(ILogger logger, WebSocket ws, Socket sock)
        {
            var buffer = new byte[1024];
            var readSegment = new ArraySegment<byte>(buffer, 0, buffer.Length);

            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(readSegment, default).ConfigureAwait(false);
                if (result.CloseStatus == null && result.Count > 0 && result.MessageType == WebSocketMessageType.Binary)
                {
                    logger.LogDebug("Read {0} bytes from websocket.", result.Count);
                    await sock.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), SocketFlags.None);
                }
            }
            while (result.CloseStatus == null && result.Count > 0);
        }
    }

    static class HttpRequestExtensions
    {
        public static string GetSingleHeaderValue(this HttpRequest request, string name)
        {
            if (!request.Headers.TryGetValue(name, out var values))
            {
                return null;
            }

            if (values.Count != 1)
            {
                return null;
            }

            return values[0];
        }
    }

    static class SocketExtensions
    {
        public static Task ConnectAsyncTask(this Socket socket, EndPoint endPoint)
            => Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, endPoint, null);
    }
}
