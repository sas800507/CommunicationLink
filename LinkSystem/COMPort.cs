using System;
using System.IO.Ports;
using Logger;

namespace LinkSystem
{
    public class COMPort : ILinkPort
    {
        public Log Log { get; set; }
        readonly SerialPort _port;

        public COMPort(string com, int speed = 9600, Parity par = Parity.None, int dataBits = 8, StopBits stop = StopBits.One)
        {
            _port = new SerialPort(com, speed, par, dataBits, stop);
            _port.Open();
        }

        public void PortState(bool state)
        {
            if (state)
            {
                if (!_port.IsOpen) _port.Open();
            }
            else
            {
                if (_port.IsOpen) _port.Close();
            }
        }

        public bool GetPortState()
        {
            return _port.IsOpen;
        }

        public void Send(LinkData data)
        {
            _port.Write(data.Data, 0, data.Data.Length);
        }

        public LinkData Receive()
        {
            var tmp = new byte[1024 * 4];
            byte[] result = null;
            var readCnt = _port.BytesToRead;
            if ( readCnt > 0)
            {
                _port.Read(tmp, 0, 1024 * 4);
                result = new byte[readCnt];
                Buffer.BlockCopy(tmp, 0, result, 0, readCnt);
                if (RecieveEvent != null) RecieveEvent(this, EventArgs.Empty);
            }
            return new LinkData(result);
        }

        public event EventHandler RecieveEvent;
        public event EventHandler<LinkConnectionEvent> ConnectEvent;
        public event EventHandler<LinkConnectionEvent> DisconectEvent;

        public PortSignals GetSignals()
        {
            var result = new PortSignals();
            result.DTR = _port.DtrEnable;
            result.RTS = _port.RtsEnable;
            result.CD = _port.CDHolding;
            result.CTS = _port.CtsHolding;
            result.DSR = _port.DsrHolding;
            return result;
        }

        public void SetSignals(PortSignals signals)
        {
            _port.DtrEnable = signals.DTR;
            _port.RtsEnable = signals.RTS;
        }

        public void Dispose()
        {
            _port.Close();
        }

        public string Name()
        {
            return string.Format("COM: {0}", _port);
        }
    }
}

