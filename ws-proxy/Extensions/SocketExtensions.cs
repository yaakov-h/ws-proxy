using System.Net.Sockets;
using System.Threading.Tasks;

namespace WebSocketProxy
{
    static class SocketExtensions
    {
        public static Task<Socket> AcceptTaskAsync(this Socket socket)
        {
            var task = Task.Factory.FromAsync(socket.BeginAccept, socket.EndAccept, null);
            return task;
        }
    }
}
