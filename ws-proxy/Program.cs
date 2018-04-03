using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WebSocketProxy
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {

            void PrintUsage() => Console.Error.WriteLine("Usage: dotnet ws-proxy.dll <proxy server> <password>");

            if (args.Length != 2)
            {
                PrintUsage();
                return -1;
            }

            var loggerFactory = InitializeLogging();
            var logger = loggerFactory.CreateLogger<Program>();

            var (proxyServer, password) = (args[0], args[1]);

            var parameters = new ConnectionParameters(proxyServer, password);
            logger.LogDebug("Parsed connection parameters: server = {0}, password = {1}", parameters.ProxyServerAddress, new string('*', password.Length));

            try
            {
                using (var cts = ConsoleExtensions.CreateCancellationSourceFromKeyPress(logger))
                {
                    var listener = new SocketListener(loggerFactory.CreateLogger<SocketListener>(), loggerFactory, parameters);
                    await listener.ListenAsync(cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Unhandled exception.");
                return -1;
            }

            return 0;
        }

        static ILoggerFactory InitializeLogging()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole(LogLevel.Trace);

            return loggerFactory;
        }
    }
}
