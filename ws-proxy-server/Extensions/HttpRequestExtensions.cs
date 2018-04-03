using Microsoft.AspNetCore.Http;

namespace ws_proxy_server
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
    }
}
