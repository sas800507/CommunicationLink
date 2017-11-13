using System;
using Logger;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;
using System.Collections.Generic;

namespace LinkSystem
{
    public class Link
    {
        //Timer timer = new Timer(new TimerCallback(TimerEvent));
        private System.Timers.Timer _timer;

        private int _eventHandlers = 0;
        private LinkData _tmpRx = null;
        private List<Guid> _subscribers = new List<Guid>();

        private EventHandler _onRcvEvent;

        public event EventHandler RcvEvent
        {
            add { _onRcvEvent += value; _eventHandlers++; }
            // Analysis disable DelegateSubtraction
            remove { _onRcvEvent -= value; _eventHandlers--; }
            // Analysis restore DelegateSubtraction
        }

        void onRcv(object sender, EventArgs e)
        {
            _timer.Start();//    .Change(Convert.ToInt32(timeOut), 0);
        }

        public void TimerEvent(object sender, ElapsedEventArgs args)
        {
            var eh = _onRcvEvent;
            if (eh == null) return;

            eh(this, EventArgs.Empty);
            _timer.Stop();
        }

        private ILinkPort _port;

        public Log Log
        {
            set
            {
                _port.Log = value;
            }
        }

        LinkBuffer _rx = new LinkBuffer();
        LinkDataBuffer _rxData = new LinkDataBuffer();
        double timeOut = 100;
        DateTime _timeOutStart;

        #region init from args

        private static string GetParamValue(string param)
        {
            return param.Split('=')[1].ToLower();
        }

        static int StrToInt(string str, int defValue)
        {
            int result;
            try
            {
                result = Convert.ToInt32(str);
            }
            catch
            {
                result = defValue;
            }

            return result;
        }

        static void formatToConfig(string format, out Parity parity, out int data, out StopBits stop)
        {
            parity = Parity.None;
            data = 8;
            stop = StopBits.One;

            if (string.IsNullOrEmpty(format))
                return;
            try
            {
                data = (int)Char.GetNumericValue(format[0]);
                if (data < 5 || data > 8) data = 8;
            }
            catch
            {
                data = 8;
            }

            if (format.Length > 1)
            {
                switch (format[1])
                {
                    case 'm':
                        parity = Parity.Mark;
                        break;
                    case 'o':
                        parity = Parity.Odd;
                        break;
                    case 'e':
                        parity = Parity.Even;
                        break;
                    case 's':
                        parity = Parity.Space;
                        break;
                    default:
                        parity = Parity.None;
                        break;
                }
                if (format.Length > 2)
                {
                    switch (format[2])
                    {
                        case '0':
                            stop = StopBits.None;
                            break;
                        case '2':
                            stop = StopBits.Two;
                            break;
                        default:
                            stop = StopBits.One;
                            break;
                    }
                    if (format.Length > 3)
                    {
                        stop = StopBits.OnePointFive;
                    }
                }
            }
        }

        void SetTimeOut(int speed)
        {
            if (speed <= 4800) timeOut = 1000;
        }

        void initCom(string[] args)
        {
            var port = GetParamValue(args.FirstOrDefault(x => x.ToLower().Contains("port")));
            var speed = GetParamValue(args.FirstOrDefault(x => x.ToLower().Contains("speed")));
            Parity parity;
            int dataBits;
            StopBits stopBits;
            var format = GetParamValue(args.FirstOrDefault(x => x.ToLower().Contains("format")));
            formatToConfig(format, out parity, out dataBits, out stopBits);

            var setSpeed = StrToInt(speed, 9600);
            SetTimeOut(setSpeed);
            _port = new COMPort(port, setSpeed, parity, dataBits, stopBits);
        }

        void initTCPServer(string[] args)
        {
            var port = GetParamValue(args.FirstOrDefault(x => x.ToLower().Contains("port")));

            _port = new TCPServer(StrToInt(port, 5555));
        }

        void initTCPClient(string[] args)
        {
            var ip = GetParamValue(args.FirstOrDefault(x => x.ToLower().Contains("ip")));
            var port = GetParamValue(args.FirstOrDefault(x => x.ToLower().Contains("port")));

            _port = new TCPClient(ip, StrToInt(port, 5555));
        }

        void initLink(string[] args)
        {
            string type = args.FirstOrDefault(x => x.ToLower().Contains("type"));
            if (!string.IsNullOrEmpty(type))
            {
                switch (GetParamValue(type))
                {
                    case "com":
                        initCom(args);
                        break;
                    case "server":
                        initTCPServer(args);
                        break;
                    case "client":
                        initTCPClient(args);
                        break;
                }
            }
        }
        #endregion

        private void subInit()
        {
            _timer = new System.Timers.Timer(Convert.ToInt32(timeOut));
            _timer.Elapsed += TimerEvent;
            //_timer.Start();
            _port.ConnectEvent += OnConnect;
            _port.DisconectEvent += OnDisconnect;
        }

        public Link()
        {
            _port = new TCPServer(5555);
            _port.RecieveEvent += onRcv;
            subInit();
        }

        public Link(string[] args)
        {
            initLink(args);
            _port.RecieveEvent += onRcv;
            subInit();
        }

        public Link(ILinkPort port)
        {
            _port = port;
            _port.RecieveEvent += onRcv;
            subInit();
        }

        public void Process()
        {
        }

        public event EventHandler OnConnectEvent;
        private void OnConnect(object sender, LinkConnectionEvent e)
        {
            if (OnConnectEvent != null)
                OnConnectEvent(this, e);
        }

        public event EventHandler OnDisconnectEvent;
        private void OnDisconnect(object sender, LinkConnectionEvent e)
        {
            if (OnDisconnectEvent != null)
                OnDisconnectEvent(this, e);
        }

        /// <summary>
        /// Процедура отправки массива байт
        /// </summary>
        /// <param name="arr">Передаваемый массив байт</param>
        public void Send(byte[] arr)
        {
            _port.Send(new LinkData(arr));
        }

        public void Send(LinkData data)
        {
            _port.Send(data);
        }

        /// <summary>
        /// Процедура отправки строкового
        /// </summary>
        /// <param name="str">Передаваемая строка</param>
        public void Send(string str)
        {
            var arr = Encoding.ASCII.GetBytes(str);
            _port.Send(new LinkData(arr));
        }

        public string Recieve()
        {
            var rcv = _port.Receive();
            if (rcv.Data.Length != 0)
            {
                _timeOutStart = DateTime.Now;
                _rx.Add(rcv.Data);
            }
            if (_rx.Length == 0)
                return string.Empty;
            if (_onRcvEvent == null && (DateTime.Now - _timeOutStart).TotalMilliseconds < timeOut)
                return string.Empty;
            var len = _rx.Length;
            var result = Encoding.ASCII.GetString(_rx.Get(), 0, len);
            return result;
        }


        public LinkData RecieveByte(Guid subcriberGuid)
        {
            lock (this)
            {
                if (!_subscribers.Any(x => x.Equals(subcriberGuid)))
                {
                    _subscribers.Add(subcriberGuid);
                    if (_tmpRx == null) _tmpRx = RecieveByte();
                }
                var tmp = _tmpRx;
                if (_subscribers.Count == _eventHandlers)
                {
                    _subscribers.Clear();
                    _tmpRx = null;
                }
                return tmp;
            }
        }

        /// <summary>
        /// Приём в виде байтового массива
        /// </summary>
        public LinkData RecieveByte()
        {
            var rcv = _port.Receive();
            if (rcv != null && rcv.Data.Length != 0)
            {
                _timeOutStart = DateTime.Now;
                _rxData.Add(rcv);
            }
            if (_onRcvEvent == null && (DateTime.Now - _timeOutStart).TotalMilliseconds < timeOut)
                return null;
            return _rxData.Get();
        }

        /// <summary>
        /// Изменить значение сигнала DTR
        /// </summary>
        /// <param name="state">Значение сигнала</param>
        public void SetDTR(bool state)
        {
            var signal = _port.GetSignals();
            signal.DTR = state;
            _port.SetSignals(signal);
        }

        /// <summary>
        /// Изменить значение сигнала DSR
        /// </summary>
        /// <param name="state">Значение сигнала</param>
        public void SetDSR(bool state)
        {
            var signal = _port.GetSignals();
            signal.DSR = state;
            _port.SetSignals(signal);
        }

        /// <summary>
        /// Изменить значение сигнала RTS
        /// </summary>
        /// <param name="state">Значение сигнала</param>
        public void SetRTS(bool state)
        {
            var signal = _port.GetSignals();
            signal.RTS = state;
            _port.SetSignals(signal);
        }

        /// <summary>
        /// Управление состоянием порта
        /// </summary>
        /// <value><c>true</c> порт открыт; закрыт, <c>false</c>.</value>
        public bool PortState
        {
            get
            {
                return _port.GetPortState();
            }
            set
            {
                _port.PortState(value);
            }
        }

        /// <summary>
        /// Освобождение реурсов подсистемы Link
        /// </summary>
        /// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="LinkSystem.Link"/>. The
        /// <see cref="Dispose"/> method leaves the <see cref="LinkSystem.Link"/> in an unusable state. After calling
        /// <see cref="Dispose"/>, you must release all references to the <see cref="LinkSystem.Link"/> so the garbage
        /// collector can reclaim the memory that the <see cref="LinkSystem.Link"/> was occupying.</remarks>
        public void Dispose()
        {
            _port.Dispose();
        }

        public override int GetHashCode()
        {
            return _port.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("[Link: Port={0}]", _port.Name());
        }
    }
}

