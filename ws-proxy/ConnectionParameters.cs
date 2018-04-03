namespace WebSocketProxy
{
    readonly struct ConnectionParameters
    {
        public ConnectionParameters(string proxyServerAddress, string password)
        {
            ProxyServerAddress = proxyServerAddress;
            Password = password;
        }

        public readonly string ProxyServerAddress;
        public readonly string Password;
    }
}
