using System;
using System.Text;
using Logger;
using System.Linq;
using System.IO.Ports;

namespace LinkSystem
{
    public enum LinkType
    {
        None,
        COMPort,
        TCPServer,
        TCPClient
    }


    /// <summary>
    /// Класс для реализации единого доступа к портам
    /// </summary>
    public class Link
    {
        public Log Log
        { 
            set
            {
                switch (_type)
                {
                    case LinkType.COMPort:
                        _com.Log = value;
                        break;
                    case LinkType.TCPServer:
                        _tcpserver.Log = value;
                        break;
                    case LinkType.TCPClient:
                        _tcpclient.Log = value;
                        break;
                }
            } 
        }

        LinkType _type = LinkType.None;
        COMPort _com;
        TCPServer _tcpserver;
        TCPClient _tcpclient;

        int _txLen;
        byte[] _tx = new byte[1024*1024];
        //int _rxLen;
        //byte[] _rx = new byte[1024*1024];
        LinkBuffer _rx = new LinkBuffer();
        double timeOut = 100;
        DateTime _timeOutStart;

        public Link()
        {
            _type = LinkType.TCPServer;
            _tcpserver = new TCPServer(5555);
        }

        static string getParamValue(string param)
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
            var port = getParamValue(args.FirstOrDefault(x => x.ToLower().Contains("port")));
            var speed = getParamValue(args.FirstOrDefault(x => x.ToLower().Contains("speed")));
            Parity parity;
            int dataBits;
            StopBits stopBits;
            var format = getParamValue(args.FirstOrDefault(x => x.ToLower().Contains("format")));
            formatToConfig(format, out parity, out dataBits, out stopBits);

            _type = LinkType.COMPort;
            var setSpeed = StrToInt(speed, 9600);
            SetTimeOut(setSpeed);
            _com = new COMPort(port, setSpeed, parity, dataBits, stopBits);
        }

        void initTCPServer(string[] args)
        {
            var port = getParamValue(args.FirstOrDefault(x => x.ToLower().Contains("port")));

            _type = LinkType.TCPServer;
            _tcpserver = new TCPServer(StrToInt(port, 5555));
        }

        void initTCPClient(string[] args)
        {
            var ip = getParamValue(args.FirstOrDefault(x => x.ToLower().Contains("ip")));
            var port = getParamValue(args.FirstOrDefault(x => x.ToLower().Contains("port")));

            _type = LinkType.TCPClient;
            _tcpclient = new TCPClient(ip, StrToInt(port, 5555));
        }

        void initLink(string[] args)
        {
            string type = args.FirstOrDefault(x => x.ToLower().Contains("type"));
            if (!string.IsNullOrEmpty(type))
            {
                switch (getParamValue(type))
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

        /// <summary>
        /// Конструктор на основе параметров программы
        /// </summary>
        /// <param name="args">Аргументы программы</param>
        public Link(string[] args)
        {
            initLink(args);
        }

        /// <summary>
        /// Конструктор с инициализацией COM-порта
        /// </summary>
        /// <param name="comPort">Объект описывающий СОМ-порт</param>
        public Link(COMPort comPort)
        {
            _type = LinkType.COMPort;
            _com = comPort;
        }

        /// <summary>
        /// Конструктор с инициализацией TCPServer
        /// </summary>
        /// <param name="tcpServer">Объект описыващий TCPServer</param>
        public Link(TCPServer tcpServer)
        {
            _type = LinkType.TCPServer;
            _tcpserver = tcpServer;
        }

        /// <summary>
        /// Конструктор с инициализацией TCPClient
        /// </summary>
        /// <param name="tcpClient">Объект описыващий TCPClient</param>
        public Link(TCPClient tcpClient)
        {
            _type = LinkType.TCPClient;
            _tcpclient = tcpClient;
        }

        /// <summary>
        /// Процедура отправки массива байт
        /// </summary>
        /// <param name="arr">Передаваемый массив байт</param>
        public void Send(byte[] arr)
        {
            Buffer.BlockCopy(arr, 0, _tx, 0, arr.Length);
            _txLen = arr.Length;
        }

        /// <summary>
        /// Процедура отправки строкового
        /// </summary>
        /// <param name="str">Передаваемая строка</param>
        public void Send(string str)
        {
            var arr = Encoding.ASCII.GetBytes(str);
            Buffer.BlockCopy(arr, 0, _tx, 0, arr.Length);
            _txLen = str.Length;
        }

        /// <summary>
        /// Процедура обслуживания порта
        /// Задачи: вызывать реализации методов приёма/передачи данных
        /// </summary>
        public void Process()
        {
            byte[] rcv = null;
            switch (_type)
            {
                case LinkType.COMPort:
                    if (_txLen > 0)
                    {
                        _com.Send(_tx, 0, _txLen);
                        _txLen = 0;
                    }
                    rcv = _com.Receive();
                    break;
                case LinkType.TCPServer:
                    if (_txLen > 0)
                    {
                        _tcpserver.Send(_tx, 0, _txLen);
                        _txLen = 0;
                    }
                    rcv = _tcpserver.Receive();
                    break;
                case LinkType.TCPClient:
                    if (_txLen > 0)
                    {
                        _tcpclient.Send(_tx, 0, _txLen);
                        _txLen = 0;
                    }
                    rcv = _tcpclient.Receive();
                    break;
            }
            if (rcv != null && rcv.Length != 0)
            {
                _rx.Add(rcv);
                _timeOutStart = DateTime.Now;
                //Buffer.BlockCopy(rcv, 0, _rx, 0, rcv.Length);
                //_rxLen = rcv.Length;
            }
        }

        /// <summary>
        /// Приём в виде строки
        /// </summary>
        public string Recieve()
        {
            if (_rx.Length == 0)
                return string.Empty;
            if ((DateTime.Now - _timeOutStart).TotalMilliseconds < timeOut)
                return string.Empty;
            var len = _rx.Length;
            var result = Encoding.ASCII.GetString(_rx.Get(), 0, len);

            //_rxLen = 0;
            return result;
        }

        /// <summary>
        /// Приём в виде байтового массива
        /// </summary>
        public byte[] RecieveByte()
        {
            //var result = new byte[_rx.Length];
            //Buffer.BlockCopy(_rx, 0, result, 0, _rxLen);
            //_rxLen = 0;
            return _rx.Get();
        }

        /// <summary>
        /// Изменить значение сигнала DTR
        /// </summary>
        /// <param name="state">Значение сигнала</param>
        public void SetDTR(bool state)
        {
            if (_type == LinkType.COMPort)
            {
                var signal = _com.GetSignals();
                signal.DTR = state;
                _com.SetSignals(signal);
            }
        }

        /// <summary>
        /// Изменить значение сигнала DSR
        /// </summary>
        /// <param name="state">Значение сигнала</param>
        public void SetDSR(bool state)
        {
            if (_type == LinkType.COMPort)
            {
                var signal = _com.GetSignals();
                signal.DSR = state;
                _com.SetSignals(signal);
            }
        }

        /// <summary>
        /// Изменить значение сигнала RTS
        /// </summary>
        /// <param name="state">Значение сигнала</param>
        public void SetRTS(bool state)
        {
            if (_type == LinkType.COMPort)
            {
                var signal = _com.GetSignals();
                signal.RTS = state;
                _com.SetSignals(signal);
            }
        }

        /// <summary>
        /// Управление состоянием порта
        /// </summary>
        /// <param name="state">true - Открыт, false - Закрыт</param>
        public bool PortState
        {
            get
            {
                switch (_type)
                {
                    case LinkType.COMPort:
                        return _com.GetPortState();
                    case LinkType.TCPServer:
                        return _tcpserver.GetPortState();
                    case LinkType.TCPClient:
                        return _tcpclient.GetPortState();
                    default:
                        return false;
                }
            }
            set
            { 
                switch (_type)
                {
                    case LinkType.COMPort:
                        _com.PortState(value);
                        break;
                    case LinkType.TCPServer:
                        _tcpserver.PortState(value);
                        break;
                    case LinkType.TCPClient:
                        _tcpclient.PortState(value);
                        break;
                }
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
            switch (_type)
            {
                case LinkType.COMPort:
                    _com.Dispose();
                    break;
                case LinkType.TCPServer:
                    _tcpserver.Dispose();
                    break;
                case LinkType.TCPClient:
                    _tcpclient.Dispose();
                    break;
            }
        }
    }
}

