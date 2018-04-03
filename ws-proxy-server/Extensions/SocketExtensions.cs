using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace WebSocketProxy.Server
{
    static class SocketExtensions
    {
        public static Task ConnectAsyncTask(this Socket socket, EndPoint endPoint)
            => Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, endPoint, null);
    }
}
