using System;

namespace WebSocketProxy
{
    static class UriExtensions
    {
        public static Uri ToWebSocketAddress(this Uri uri)
        {
            string replacementScheme;

            if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
            {
                replacementScheme = "ws";
            }
            else if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                replacementScheme = "wss";
            }
            else
            {
                return uri;
            }

            var builder = new UriBuilder(uri)
            {
                Scheme = replacementScheme
            };
            return builder.Uri;
        }
    }
}
