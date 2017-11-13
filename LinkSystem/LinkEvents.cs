using System;
using System.Net.Sockets;

namespace LinkSystem
{
    public class LinkConnectionEvent : EventArgs
    {
        public TcpClient Client { get; private set; }

        public LinkConnectionEvent(TcpClient client)
        {
            this.Client = client;
        }
    }
}
