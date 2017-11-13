using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Logger;

namespace LinkSystem
{
    /// <summary>
    /// Внутренний класс для обработки соединения с сервером
    /// </summary>
    internal class TCPServerConnection
    {
        private readonly Thread _thread;
        private bool _terminated;
        private LinkBuffer _rx = new LinkBuffer();
        private LinkBuffer _tx = new LinkBuffer();

        /// <summary>
        /// Время установки соединения
        /// </summary>
        /// <value>Время установки соединения</value>
        public DateTime ConnectedTime { get; }

        /// <summary>
        /// Клиент соединения
        /// </summary>
        /// <value>Клиент</value>
        public TcpClient Client { get; }

        /// <summary>
        /// Признак наличия принятых из соединения данных
        /// </summary>
        /// <value><c>true</c> данные имеются; нет данных, <c>false</c>.</value>
        public bool RcvData { get; private set; }

        /// <summary>
        /// Прервать соединение
        /// </summary>
        public void Terminate()
        {
            _terminated = true;
            _thread.Abort();
        }

        public TCPServerConnection(TcpClient client)
        {
            Client = client;
            ConnectedTime = DateTime.Now;
            _thread = new Thread(Start);
            _thread.Start();
        }

        /// <summary>
        /// Проверка активности соединения
        /// </summary>
        /// <returns><c>true</c>, соединение разорвано <c>false</c> соединение живое.</returns>
        private bool CheckConnection()
        {
            if (Client == null) return false;

            try
            {
                if (!Client.Client.Poll(0, SelectMode.SelectRead)) return false;
                var buff = new byte[1];
                return Client.Client.Receive(buff, SocketFlags.Peek) == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Запустить обработчик клиентского соединения
        /// </summary>
        public void Start()
        {
            var stream = Client.GetStream();

            var message = new byte[_rx.Size];
            while (!_terminated)
            {
                if (CheckConnection()) break;
                if (stream.DataAvailable)
                {
                    var readByte = stream.Read(message, 0, _rx.Size);
                    if (readByte != 0)
                    {
                        RcvData = true;
                        lock (_rx)
                        {
                            _rx.Add(message, readByte);
                        }
                    }
                }
                if (_tx.Length != 0)
                {
                    var length = _tx.Length;
                    stream.Write(_tx.Get(length), 0, length);
                    stream.Flush();
                }
                Thread.Sleep(10);
            }

            _terminated = true;
            Client.Close();
            //_client = null;
        }

        /// <summary>
        /// Проверка активности соединения
        /// </summary>
        /// <returns><c>true</c>, if alive was ised, <c>false</c> otherwise.</returns>
        public bool IsAlive()
        {
            return !_terminated;
            //return _client != null;
        }

        /// <summary>
        /// Вернуть принятые данные
        /// </summary>
        public LinkData Recieve()
        {
            RcvData = false;
            lock (_rx)
            {
                return new LinkData(_rx.Get(), Client);
            }
        }

        /// <summary>
        /// Отправить данные в клиентское соединение
        /// </summary>
        /// <param name="data">Отправляемый массив</param>
        public void Send(byte[] data)
        {
            _tx.Add(data);
        }
    }

    /// <summary>
    /// TCP сервер с многопользовательским подключением
    /// </summary>
    public class TCPServerEx : ILinkPort
    {
        private readonly List<TCPServerConnection> _clients = new List<TCPServerConnection>();
        //private List<Thread> _threads = new List<Thread>();
        private bool _terminated;
        private readonly TcpListener _listener;
        private readonly int _maxConnections;
        private readonly Thread _thread;
        private readonly Thread _threadListener;
        private List<LinkData> _rxData = new List<LinkData>();
        private readonly int _portNum;
        private readonly object _locker = new object();

        public Log Log { get; set; }

        public TCPServerEx(int port, int maxConnections = 10)
        {
            _portNum = port;
            _maxConnections = maxConnections;
            _listener = new TcpListener(IPAddress.Any, port);
            _threadListener = new Thread(ListenerThread);
            _threadListener.Start();
            _thread = new Thread(ConnectionProcessor);
            _thread.Start();
        }

        private void AddToLog(string str)
        {
            if (Log != null)
                Log.AddLine(str);
        }

        /// <summary>
        /// Поток добавления (отлова) соединений
        /// </summary>
        private void ListenerThread()
        {
            _listener.Start();

            while (!_terminated)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    AddConnection(client);
                }
                catch (Exception)
                {
                    // ignored
                }
                Thread.Sleep(50);
            }

            _listener.Stop();
            _threadListener.Abort();
        }

        /// <summary>
        /// Поток обработчик имеющихся подклчюений
        /// Выполняет: удаление лишних, отлов разорванных соединений и сбором данных из подключений
        /// </summary>
        private void ConnectionProcessor()
        {
            while (!_terminated)
            {
                try
                {
                    lock (_locker)
                    {
                        if (_clients.Count >= _maxConnections)
                        {
                            var min = _clients.OrderBy(x => x.ConnectedTime).First();
                            min.Terminate();
                        }
                        foreach (var item in _clients.Where(x => x.IsAlive() == false))
                        {
                            AddToLog("Disconnect: " + item.GetHashCode());
                            if (DisconectEvent != null) DisconectEvent(this, new LinkConnectionEvent(item.Client));
                        }
                        _clients.RemoveAll(x => x.IsAlive() == false);
                        foreach (var item in _clients.Where(x => x.IsAlive() == true))
                        {
                            if (!item.RcvData) continue;
                            lock (_rxData)
                            {
                                _rxData.Add(item.Recieve());
                            }
                        }
                    }

                    if (_rxData.Count > 0)
                            if (RecieveEvent != null) RecieveEvent(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    break;
                }

                Thread.Sleep(10);
            }

            foreach (var item in _clients)
                item.Terminate();

            _thread.Abort();
        }

        /// <summary>
        /// Процедура добавления подключений в общий список
        /// </summary>
        /// <param name="client">TcpClient подключения</param>
        private void AddConnection(TcpClient client)
        {
            try
            {
                var connection = new TCPServerConnection(client);
                lock (_locker)
                {
                    _clients.Add(connection);
                    AddToLog("Connected: " + connection.GetHashCode());
                    if (ConnectEvent != null) ConnectEvent(this, new LinkConnectionEvent(client));
                }
            }
            catch (Exception ex)
            {
                AddToLog(ex.Message);
            }
        }
        #region ILinkPort implementation

        public event EventHandler RecieveEvent;
        public event EventHandler<LinkConnectionEvent> ConnectEvent;
        public event EventHandler<LinkConnectionEvent> DisconectEvent;

        public void Dispose()
        {
            _terminated = true;
            _listener.Stop();
            _threadListener.Abort();
            //_listener.Stop();
        }

        public bool GetPortState()
        {
            return true;
        }

        public void PortState(bool state)
        {
        }

        public LinkData Receive()
        {
            lock (_rxData)
            {
                var data = _rxData.FirstOrDefault();
                if (data == null) return null;

                _rxData.Remove(data);
                return data;
            }
        }

        public void Send(LinkData data)
        {
            if (data == null) return;
            var client = data.Identifier as TcpClient;
            if (client == null) return;
            lock (_locker)
            {
                var connect = _clients.FirstOrDefault(x => x.Client == client);
                if (connect == null) return;
                connect.Send(data.Data);
            }
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

        public string Name()
        {
            return string.Format("TCPServerEx ({0})", _portNum);
        }
        #endregion
    }
}
