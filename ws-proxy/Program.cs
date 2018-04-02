using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace ws_proxy
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {

            void PrintUsage() => Console.Error.WriteLine("Usage: dotnet ws-proxy.dll <proxy server> <target host> <target port>");

            if (args.Length != 3)
            {
                PrintUsage();
                return -1;
            }

            var (proxyServer, targetHost, targetPort) = (args[0], args[1], args[2]);


            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = cts.IsCancellationRequested;
                        cts.Cancel();
                    };

                    await ConnectAndRun(proxyServer, targetHost, targetPort, cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return -1;
            }

            return 0;
        }

        static async Task ConnectAndRun(string proxyServer, string targetHost, string targetPort, CancellationToken cancellationToken)
        {

            var sock = new Socket(SocketType.Stream, ProtocolType.Tcp);
            sock.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            sock.Listen(50);

            var tasks = new ConcurrentDictionary<Task, object>();

            Console.WriteLine("Listening on {0}", sock.LocalEndPoint);

            while (!cancellationToken.IsCancellationRequested)
            {
                var socket = await sock.AcceptTaskAsync().ConfigureAwait(false);
                Console.WriteLine("got socket.");
                var task = HandleSocketAsync(socket, proxyServer, targetHost, targetPort, cancellationToken);
                tasks.TryAdd(task, null);
                _ = task.ContinueWith(t =>
                {
                    tasks.TryRemove(t, out _);

                    if (t.IsFaulted)
                    {
                        Console.WriteLine(t.Exception);
                        ExceptionDispatchInfo.Throw(t.Exception);
                    }
                });
            }

            await Task.WhenAll(tasks.Keys);
        }

        static long id = 0;

        static async Task HandleSocketAsync(Socket socket, string proxyServer, string targetHost, string targetPort, CancellationToken cancellationToken)
        {
            var id = Interlocked.Increment(ref Program.id);

            using (socket)
            using (var ws = new ClientWebSocket())
            {
                Console.WriteLine("[{0}] Accepted connection from {1}", id, socket.RemoteEndPoint);

                ws.Options.SetRequestHeader("WS-Proxy-Target-Host", targetHost);
                ws.Options.SetRequestHeader("WS-Proxy-Target-Port", targetPort);

                Console.WriteLine("[{0}] Connecting to: {1}", id, proxyServer);

                await ws.ConnectAsync(new Uri(proxyServer, UriKind.Absolute), default).ConfigureAwait(false);

                Console.WriteLine("[{0}] Connected to proxy server.", id);

                var uploadTask = CopyToAsync(socket, ws, cancellationToken);
                var downloadTask = CopyToAsync(ws, socket, cancellationToken);

                await Task.WhenAny(uploadTask, downloadTask).ConfigureAwait(false);
            }
        }

        static async Task CopyToAsync(Socket sock, ClientWebSocket ws, CancellationToken cancellationToken)
        {
            var buffer = new byte[1024];
            var readSegment = new ArraySegment<byte>(buffer, 0, buffer.Length);

            int read;
            do
            {
                read = await sock.ReceiveAsync(readSegment, SocketFlags.None).ConfigureAwait(false);
                if (read > 0)
                {
                    Console.WriteLine("Read {0} bytes from TCP socket.", read);
                    await ws.SendAsync(new ArraySegment<byte>(buffer, 0, read), WebSocketMessageType.Binary, false, default);
                }
            }
            while (read > 0);
        }

        static async Task CopyToAsync(ClientWebSocket ws, Socket sock, CancellationToken cancellationToken)
        {
            var buffer = new byte[1024];
            var readSegment = new ArraySegment<byte>(buffer, 0, buffer.Length);

            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(readSegment, cancellationToken).ConfigureAwait(false);
                if (result.CloseStatus == null && result.Count > 0 && result.MessageType == WebSocketMessageType.Binary)
                {
                    Console.WriteLine("Read {0} bytes from websocket.", result.Count);
                    await sock.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), SocketFlags.None);
                }
            }
            while (result.CloseStatus == null && result.Count > 0);
        }
    }

    static class SocketExtensions
    {
        public static Task<Socket> AcceptTaskAsync(this Socket socket)
        {
            var task = Task.Factory.FromAsync(socket.BeginAccept, socket.EndAccept, null);
            return task;
        }
    }
}
