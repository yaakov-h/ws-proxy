using System;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace WebSocketProxy.Server
{
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

        public static string GetAuthorizationPassword(this HttpRequest request)
        {
            var headers = request.Headers["Authorization"];
            if (headers.Count != 1)
            {
                return null;
            }

            var value = headers[0];
            if (!value.StartsWith("Password ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var encodedPassword = value.Substring("Password ".Length);
            try
            {
                var encodedText = Convert.FromBase64String(encodedPassword);
                var password = Encoding.UTF8.GetString(encodedText);
                return password;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
