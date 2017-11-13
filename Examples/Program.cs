using System;
using System.Threading;
using LinkSystem;
using Logger;

namespace Examples
{
    class MainClass
    {
        public static Log log = new Log();
        public static void Main(string[] args)
        {
            const string helpString = "Press Q, Space or Enter key to exit!";

            log.ConsoleOutput = true;

            var link = new Link(new TCPServerEx(1234));
            link.Log = log;
            link.OnConnectEvent += Link_OnConnectEvent;
            link.OnDisconnectEvent += Link_OnDisconnectEvent;
            link.RcvEvent += Link_RcvEvent;

            log.AddLine(helpString);

            var quitPressed = false;
            var cki = new ConsoleKeyInfo();
            while (!quitPressed)
            {
                if (Console.KeyAvailable)
                {
                    cki = Console.ReadKey(true);
                    switch (cki.Key)
                    {
                        case ConsoleKey.Enter:
                        case ConsoleKey.Spacebar:
                        case ConsoleKey.Q:
                            quitPressed = true;
                            break;
                        default:
                            break;
                    }
                }

                Thread.Sleep(10);
            }

            link.Dispose();
        }

        static void Link_OnConnectEvent(object sender, EventArgs e)
        {
            var evnt = e as LinkConnectionEvent;
            if (evnt == null) return;

            var link = sender as Link;
            if (link == null) return;

            log.AddLine("Connect");
        }

        static void Link_OnDisconnectEvent(object sender, EventArgs e)
        {
            var evnt = e as LinkConnectionEvent;
            if (evnt == null) return;

            var link = sender as Link;
            if (link == null) return;

            log.AddLine("Disconnect");
        }

        static void Link_RcvEvent(object sender, EventArgs e)
        {
            var link = sender as Link;
            if (link == null) return;

            var rcv = link.RecieveByte();
            if (rcv.Data.Length > 0)
            {
                log.AddLine(rcv.Data);
                link.Send(new LinkData(new byte[] { 0x41, 0x54, 0x41, 0x0A, 0x0D }, rcv.Identifier));
            }
        }
    }
}
