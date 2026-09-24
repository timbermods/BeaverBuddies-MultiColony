using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TimberNet
{

    public class TCPListenerWrapper : ISocketListener
    {
        private readonly TcpListener listener;

        public TCPListenerWrapper(int port)
        {
            // Listen on IPv6 and IPv4
            listener = new TcpListener(IPAddress.IPv6Any, port);
            listener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            // A host moving its game to a waiting room binds the port again the moment its game's server has closed, with
            // that server's connections just closed on it. macOS and Linux refuse the port while such a connection lingers
            // (TIME_WAIT) unless the socket may reuse the address; there that is all it allows. Windows already lets the
            // port be taken again, and there the same option would let another program listen on it too, so it is not set.
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        public ISocketStream AcceptClient()
        {
            return new TCPClientWrapper(listener.AcceptTcpClient());
        }

        public void Start()
        {
            listener.Start();
        }

        public void Stop()
        {
            listener.Stop();
        }
    }
}
