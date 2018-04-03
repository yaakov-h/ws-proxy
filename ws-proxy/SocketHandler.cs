using System;
using System.ComponentModel;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WebSocketProxy
{
    sealed class SocketHandler
    {
        public SocketHandler(ILogger<SocketHandler> logger, ConnectionParameters parameters, Socket socket)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.parameters = parameters;
            this.socket = socket ?? throw new ArgumentNullException(nameof(socket));
        }

        readonly ILogger logger;
        readonly ConnectionParameters parameters;
        readonly Socket socket;

        public Guid Identifier { get; } = Guid.NewGuid();

        public async Task HandleAsync(CancellationToken cancellationToken)
        {
            using (logger.BeginScope("[Socket {0}]", Identifier))
            using (socket)
            using (var ws = new ClientWebSocket())
            {
                cancellationToken.Register(socket.Close);

                var encodedPassword = Convert.ToBase64String(Encoding.UTF8.GetBytes(parameters.Password));
                ws.Options.SetRequestHeader("Authorization", $"Password {encodedPassword}");

                logger.LogTrace("Connecting to {0}", parameters.ProxyServerAddress);

                await ws.ConnectAsync(new Uri(parameters.ProxyServerAddress, UriKind.Absolute).ToWebSocketAddress(), default).ConfigureAwait(false);

                logger.LogTrace("Connected to proxy server");

                var uploadTask = CopyToAsync(socket, ws, cancellationToken);
                var downloadTask = CopyToAsync(ws, socket, cancellationToken);

                await Task.WhenAny(uploadTask, downloadTask).ConfigureAwait(false);

                try
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection completed.", cancellationToken).ConfigureAwait(false);
                }
                catch (Win32Exception)
                {
                }

                await Task.WhenAll(uploadTask, downloadTask).ConfigureAwait(false);
            }
        }

        async Task CopyToAsync(Socket sock, ClientWebSocket ws, CancellationToken cancellationToken)
        {
            var buffer = new byte[1024];
            var readSegment = new ArraySegment<byte>(buffer, 0, buffer.Length);

            int read;
            do
            {
                read = await sock.ReceiveAsync(readSegment, SocketFlags.None).ConfigureAwait(false);
                if (read <= 0)
                {
                    logger.LogTrace("Read {0} bytes from TCP socket - connection closed.", read);
                }
                else
                {
                    logger.LogTrace("Read {0} bytes from TCP socket.", read);
                    await ws.SendAsync(new ArraySegment<byte>(buffer, 0, read), WebSocketMessageType.Binary, false, default);
                }
            }
            while (read > 0);
        }

        async Task CopyToAsync(ClientWebSocket ws, Socket sock, CancellationToken cancellationToken)
        {
            var buffer = new byte[1024];
            var readSegment = new ArraySegment<byte>(buffer, 0, buffer.Length);

            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(readSegment, cancellationToken).ConfigureAwait(false);
                if (result.CloseStatus != null)
                {
                    logger.LogInformation("The server is closing the connection: {0} ({1})", result.CloseStatus, result.CloseStatusDescription);
                }
                else if (result.MessageType != WebSocketMessageType.Binary)
                {
                    logger.LogWarning("Recieved message type {0}", result.MessageType);
                }
                else if (result.Count <= 0)
                {
                    logger.LogTrace("Read {0} bytes from WebSocket - connection closing?", result.Count);
                }
                else
                {
                    logger.LogTrace("Read {0} bytes from WebSocket.", result.Count);
                    await sock.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), SocketFlags.None);
                }
            }
            while (result.CloseStatus == null && result.Count > 0);
        }
    }
}
