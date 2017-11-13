using System;
using Logger;

namespace LinkSystem
{
    public class PortSignals
    {
        public bool DSR { get; set; }
        public bool DTR { get; set; }
        public bool CTS { get; set; }
        public bool RTS { get; set; }
        public bool CD { get; set; }
        public bool RI { get; set; }
    }

    public class LinkData
    {
        public byte[] Data { get; set; }
        public object Identifier { get; set; }

        public static explicit operator byte[] (LinkData obj)
        {
            return obj.Data;
        }

        public LinkData(byte[] data, object identifier = null)
        {
            Data = data;
            Identifier = identifier;
        }
    }

    /// <summary>
    /// Интерфейс для порта не имеющего сигналов
    /// </summary>
    public interface ILinkPort : IDisposable
    {
        event EventHandler RecieveEvent;
        event EventHandler<LinkConnectionEvent> ConnectEvent;
        event EventHandler<LinkConnectionEvent> DisconectEvent;
        Log Log { get; set; }

        bool GetPortState();
        void PortState(bool state);

        /*
        void Send(byte[] arr, int offset, int count);
        byte[] Receive();
        */
        void Send(LinkData data);
        LinkData Receive();

        PortSignals GetSignals();
        void SetSignals(PortSignals signals);

        string Name();
    }
}

