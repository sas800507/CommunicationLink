using System;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using Logger;

namespace LinkSystem
{
    public class TCPServer : ILinkPort
    {
        public Log Log { get; set; }

        private readonly TcpListener _port;
        private readonly int _portNum;
        private readonly Thread _thread;
        private bool _stopThread;
        private readonly LinkBuffer _rx = new LinkBuffer();
        private readonly LinkBuffer _tx = new LinkBuffer {PushOnOverflow = true };
        private bool _connected = false;

        private void AddToLog(string str)
        {
            if (Log != null) Log.AddLine(str, "[TCPServer]");
        }

        public TCPServer(int port)
        {
            _portNum = port;
            _port = new TcpListener(IPAddress.Any, port);
            _thread = new Thread(new ThreadStart(Listener));
            _thread.Start();
        }

        private void Listener()
        {
            try
            {
                _port.Start();
            }
            catch (Exception)
            {
                AddToLog(string.Format("Порт {0} занят", _portNum));
                _thread.Abort();
                return;
            }

            while (!_stopThread)
            {
                try
                {
                    var client = _port.AcceptTcpClient();
                    var clientIp = IPAddress.Parse(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString());
                    AddToLog("Connect to emul <-> ip: " + clientIp);
                    if (ConnectEvent != null) ConnectEvent(this, new LinkConnectionEvent(client));
                    _connected = true;
                    _tx.Clear();
                    var cycle = true;
                    while (cycle)
                    {
                        cycle = ClientCycle(client);
                        Thread.Sleep(10);
                    }
                    if (DisconectEvent != null) DisconectEvent(this, new LinkConnectionEvent(client));
                    AddToLog("Disconnect from emul");
                    _connected = false;
                }
                catch (Exception ex)
                {
                    AddToLog(ex.Message);
                    break;
                }
            }

            _thread.Abort();
        }

        private static bool CheckConnection(TcpClient client)
        {
            if (!client.Client.Poll(0, SelectMode.SelectRead)) return false;
            var buff = new byte[1];
            return client.Client.Receive(buff, SocketFlags.Peek) == 0;
        }

        private bool ClientCycle(TcpClient client)
        {
            var tcpClient = client;
            var cliStream = tcpClient.GetStream();
            var message = new byte[_rx.Size];

            while (!_stopThread)
            {
                try
                {
                    if (CheckConnection(tcpClient)) break;
                    if (cliStream.DataAvailable)
                    {
                        var bytesRead = cliStream.Read(message, 0, _rx.Size);
                        if (bytesRead != 0)
                        {
                            _rx.Add(message, bytesRead);
                            if (RecieveEvent != null) RecieveEvent(this, EventArgs.Empty);
                        }
                    }
                    if (_tx.Length != 0)
                    {
                        var length = _tx.Length;
                        cliStream.Write(_tx.Get(length), 0, length);
                        cliStream.Flush();
                    }
                }
                catch
                {
                    break;
                }
                Thread.Sleep(10);
            }

            tcpClient.Close();
            return false;
        }

        #region ILinkPort implementation
        public event EventHandler RecieveEvent;
        public event EventHandler<LinkConnectionEvent> ConnectEvent;
        public event EventHandler<LinkConnectionEvent> DisconectEvent;

        public void PortState(bool state)
        {
        }

        public bool GetPortState()
        {
            return _connected;
        }

        public void Send(LinkData data)
        {
            _tx.Add(data.Data, data.Data.Length);
        }

        public LinkData Receive()
        {
            return new LinkData(_rx.Get(_rx.Length));
        }

        public PortSignals GetSignals()
        {
            return new PortSignals()
            { 
                CD = false,
                CTS = true,
                DSR = true,
                DTR = true,
                RI = false,
                RTS = true
            };
        }

        public void SetSignals(PortSignals signals)
        {
            return;
        }

        public void Dispose()
        {
            _stopThread = true;
            _port.Stop();
        }

        public string Name()
        {
            return string.Format("TCPServer ({0})", _portNum);
        }
        #endregion
    }
}

