using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace WebSocketProxy
{
    class ConsoleExtensions
    {
        public static CancellationTokenSource CreateCancellationSourceFromKeyPress(ILogger logger)
        {
            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, e) =>
            {
                if (!cts.IsCancellationRequested)
                {
                    // If this is our first attempt, let user code handle the cancellation.
                    // Otherwse if cancellation is already requested, then allow the process to terminate.
                    logger.LogInformation("Recieved {0}. Exiting...", e.SpecialKey);
                    e.Cancel = true;

                    cts.Cancel();
                }
            };

            return cts;
        }
    }
}
