using System;
using System.Net.Sockets;
using System.Threading;
using Logger;

namespace LinkSystem
{
    public class TCPClient : ILinkPort
    {
        public Log Log { get; set; }

        private TcpClient _port;
        private readonly string _ip;
        private readonly int _portNum;

        private Thread _thread;
        private bool _stopThread;
        private readonly LinkBuffer _rx = new LinkBuffer();
        private readonly LinkBuffer _tx = new LinkBuffer { PushOnOverflow = true };

        public bool Connected { get; private set; }

        private void AddLog(string str)
        {
            if (Log != null) Log.AddLine(str, "TCPClient");
        }

        private void PortOpen()
        {
            try
            {
                _port = new TcpClient(_ip, _portNum);
                _thread = new Thread(new ThreadStart(Listener));
                _thread.Start();
            }
            catch
            {
                AddLog("Connection fault!");
                Connected = false;
            }
        }

        private void PortClose()
        {
            _port.Close();
            _stopThread = true;
        }

        public TCPClient(string ip, int port)
        {
            _ip = ip;
            _portNum = port;
            PortOpen();
        }

        private bool checkConnection()
        {
            try
            {
                if (!_port.Client.Poll(0, SelectMode.SelectRead)) return true;
                var buff = new byte[1];
                return _port.Client.Receive(buff, SocketFlags.Peek) != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Listener()
        {
            var cliStream = _port.GetStream();
            _stopThread = false;
            Connected = true;
            AddLog("Connect success.");
            if (ConnectEvent != null) ConnectEvent(this, new LinkConnectionEvent(_port));
            var message = new byte[_rx.Size];
            var msgLen = 0;

            while (!_stopThread)
            {
                try
                {
                    if (!checkConnection()) break;
                    if (cliStream.DataAvailable)
                    {
                        msgLen = cliStream.Read(message, 0, _rx.Size);
                        _rx.Add(message, msgLen);
                        if (msgLen != 0)
                        {
                            if (RecieveEvent != null) RecieveEvent(this, EventArgs.Empty);
                        }
                    }
                    var len = _tx.Length;
                    if (len != 0)
                    {
                        cliStream.Write(_tx.Get(len), 0, len);
                        cliStream.Flush();
                    }
                }
                catch
                {
                    break;
                }
                Thread.Sleep(50);
            }
            Connected = false;
            AddLog("Disconnect");
            if (DisconectEvent != null) DisconectEvent(this, new LinkConnectionEvent(_port));
            _port.Close();
            _thread.Abort();
        }

        #region ILinkPort implementation
        public event EventHandler RecieveEvent;
        public event EventHandler<LinkConnectionEvent> ConnectEvent;
        public event EventHandler<LinkConnectionEvent> DisconectEvent;

        public void PortState(bool state)
        {
            if (state)
            {
                if (!Connected) PortOpen();
            }
            else
            {
                PortClose();
            }
        }

        public bool GetPortState()
        {
            return Connected;
        }

        public void Send(LinkData data)
        {
            _tx.Add(data.Data, data.Data.Length);
        }

        public LinkData Receive()
        {
            return new LinkData(_rx.Get(), _port);
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
        }

        public string Name()
        {
            return string.Format("TCPClient ({0}:{1})", _ip, _portNum);
        }
        #endregion
    }
}

