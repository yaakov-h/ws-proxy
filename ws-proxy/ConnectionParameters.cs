namespace WebSocketProxy
{
    readonly struct ConnectionParameters
    {
        public ConnectionParameters(string proxyServerAddress, string targetHost, int targetPort) : this()
        {
            ProxyServerAddress = proxyServerAddress;
            TargetHost = targetHost;
            TargetPort = targetPort;
        }

        public readonly string ProxyServerAddress;
        public readonly string TargetHost;
        public readonly int TargetPort;
    }
}
