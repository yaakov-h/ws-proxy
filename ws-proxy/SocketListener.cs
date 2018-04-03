using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WebSocketProxy
{
    class SocketListener
    {
        public SocketListener(ILogger<SocketListener> logger, ILoggerFactory loggerFactory, ConnectionParameters parameters)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.parameters = parameters;
            this.handlers = new ConcurrentDictionary<Guid,SocketHandler>();
        }

        readonly ILogger logger;
        readonly ILoggerFactory loggerFactory;
        readonly ConnectionParameters parameters;
        readonly ConcurrentDictionary<Guid, SocketHandler> handlers;

        public async Task ListenAsync(CancellationToken cancellationToken)
        {
            var sock = new Socket(SocketType.Stream, ProtocolType.Tcp);
            sock.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            sock.Listen(50);

            cancellationToken.Register(sock.Close);

            logger.LogInformation("Listening on {0}", sock.LocalEndPoint);

            while (!cancellationToken.IsCancellationRequested)
            {
                Socket socket = default;
                try
                {
                    socket = await sock.AcceptTaskAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                }

                if (socket != null)
                {
                    var handler = new SocketHandler(loggerFactory.CreateLogger<SocketHandler>(), parameters, socket);
                    logger.LogDebug("Accepted new socket {0} => {1} for handler {2}", socket.RemoteEndPoint, socket.LocalEndPoint, handler.Identifier);

                    handlers[handler.Identifier] = handler;
                    var task = handler.HandleAsync(cancellationToken);
                    _ = task.ContinueWith(t => FinishConnection(handler.Identifier, t));
                }
            }

            while (!handlers.IsEmpty)
            {
                logger.LogDebug("Waiting for all handlers to clean up. {0} remaining...", handlers.Count);
                await Task.Delay(TimeSpan.FromMilliseconds(10));
            }
        }

        void FinishConnection(Guid identifier, Task task)
        {
            logger.LogTrace("Removing handler {0}", identifier);
            if (!handlers.TryRemove(identifier, out _))
            {
                logger.LogWarning("Handler {0} already removed.", identifier);
            }

            if (task.IsFaulted)
            {
                logger.LogError(task.Exception, "Unhandled exception in handler {0}", identifier);
            }
        }
    }
}
