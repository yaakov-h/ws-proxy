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
        }

        readonly ILogger logger;
        readonly ILoggerFactory loggerFactory;
        readonly ConnectionParameters parameters;

        public async Task ListenAsync(CancellationToken cancellationToken)
        {
            var sock = new Socket(SocketType.Stream, ProtocolType.Tcp);
            sock.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            sock.Listen(50);

            var handlers = new ConcurrentDictionary<SocketHandler, object>();

            logger.LogInformation("Listening on {0}", sock.LocalEndPoint);

            while (!cancellationToken.IsCancellationRequested)
            {
                var socket = await sock.AcceptTaskAsync().ConfigureAwait(false);
                var handler = new SocketHandler(loggerFactory.CreateLogger<SocketHandler>(), parameters, socket);
                logger.LogTrace("Accepted new socket {0} => {1} for handler {2}", socket.RemoteEndPoint, socket.LocalEndPoint, handler.Identifier);

                handlers.TryAdd(handler, null);
                var task = handler.HandleAsync(cancellationToken);
                _ = task.ContinueWith(t =>
                {
                    logger.LogDebug("Removing handler {0}", handler.Identifier);
                    handlers.TryRemove(handler, out _);
                });
            }

            while (!handlers.IsEmpty)
            {
                logger.LogDebug("Waiting for all handlers to clean up. {0} remaining...", handlers.Count);
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }
    }
}
