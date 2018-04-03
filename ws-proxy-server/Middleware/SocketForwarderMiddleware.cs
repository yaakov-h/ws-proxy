using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WebSocketProxy.Server
{
    sealed class SocketForwarderMiddleware
    {
        public SocketForwarderMiddleware(ILogger<SocketForwarderMiddleware> logger, IConfiguration configuration, RequestDelegate next)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.next = next ?? throw new ArgumentNullException(nameof(next));
        }

        readonly ILogger<SocketForwarderMiddleware> logger;
        readonly IConfiguration configuration;
        readonly RequestDelegate next;

        public async Task InvokeAsync(HttpContext context)
        {
            var wsf = context.Features.Get<IHttpWebSocketFeature>();

            if (!context.WebSockets.IsWebSocketRequest)
            {
                logger.LogDebug("Non-websocket request from {0}:{1} to {2}", context.Connection.RemoteIpAddress, context.Connection.RemotePort, context.Request.Path);
                await next(context).ConfigureAwait(false);
                return;
            }

            var targetHost = configuration["WsProxy:TargetHost"];
            var targetPort = int.Parse(configuration["WsProxy:TargetPort"]);
            var expectedPassword = configuration["WsProxy:Password"]?.TrimEnd();

            var password = context.Request.GetAuthorizationPassword();

            if (!string.Equals(password, expectedPassword, StringComparison.Ordinal))
            {
                context.Response.Headers["Www-Authenticate"] = "Password realm=\"ws-proxy\"";
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            var endPoint = IPAddress.TryParse(targetHost, out var address) ? (EndPoint)new IPEndPoint(address, targetPort) : new DnsEndPoint(targetHost, targetPort);

            var ws = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            var sock = new Socket(SocketType.Stream, ProtocolType.Tcp);

            logger.LogTrace("Connecting to {0}", endPoint);

            await sock.ConnectAsyncTask(endPoint).ConfigureAwait(false);

            var uploadTask = CopyToAsync(sock, ws);
            var downloadTask = CopyToAsync(ws, sock);

            await Task.WhenAll(uploadTask, downloadTask).ConfigureAwait(false);

        }

        async Task CopyToAsync(Socket sock, WebSocket ws)
        {
            var buffer = new byte[1024];
            var readSegment = new ArraySegment<byte>(buffer, 0, buffer.Length);

            int read;
            do
            {
                read = await sock.ReceiveAsync(readSegment, SocketFlags.None).ConfigureAwait(false);
                if (read > 0)
                {
                    logger.LogTrace("Read {0} bytes from TCP socket.", read);
                    await ws.SendAsync(new ArraySegment<byte>(buffer, 0, read), WebSocketMessageType.Binary, false, default);
                }
            }
            while (read > 0);
        }

        async Task CopyToAsync(WebSocket ws, Socket sock)
        {
            var buffer = new byte[1024];
            var readSegment = new ArraySegment<byte>(buffer, 0, buffer.Length);

            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(readSegment, default).ConfigureAwait(false);
                if (result.CloseStatus == null && result.Count > 0 && result.MessageType == WebSocketMessageType.Binary)
                {
                    logger.LogTrace("Read {0} bytes from websocket.", result.Count);
                    await sock.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), SocketFlags.None);
                }
            }
            while (result.CloseStatus == null && result.Count > 0);
        }
    }
}
