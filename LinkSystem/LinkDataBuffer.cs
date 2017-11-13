using System;
using System.Collections.Generic;
using System.Linq;

namespace LinkSystem
{
    public class LinkDataBuffer
    {
        internal class IdentifiedBuffer
        {
            public LinkBuffer Data { get; set; }
            public object Identifier { get; set; }
        }

        private List<IdentifiedBuffer> _data = new List<IdentifiedBuffer>();

        /// <summary>
        /// Добавить данных в буфер
        /// </summary>
        /// <param name="data">Данные для добавления</param>
        public void Add(LinkData data)
        {
            var buffer = _data.FirstOrDefault(x => x.Identifier == data.Identifier);
            if (buffer == null)
            {
                buffer = new IdentifiedBuffer() { Identifier = data.Identifier, Data = new LinkBuffer() };
                _data.Add(buffer);
            }

            buffer.Data.Add(data.Data);
        }

        /// <summary>
        /// Получить первые попавшиеся даные
        /// </summary>
        /// <param name="len">Длина порции данных</param>
        public LinkData Get(int len = -1)
        {
            var buffer = _data.FirstOrDefault();
            if (buffer == null) return null;
            _data.Remove(buffer);

            return new LinkData(buffer.Data.Get(len), buffer.Identifier);
        }

        /// <summary>
        /// Очистить буфер для идентификатора
        /// </summary>
        /// <param name="identifier">Идентияикатор</param>
        public void Clear(object identifier)
        { 
            var buffer = _data.FirstOrDefault(x => x.Identifier == identifier);
            if (buffer == null) return;
            buffer.Data.Clear();
        }

        /// <summary>
        /// Очистить все данные
        /// </summary>
        public void Clear()
        {
            _data.Clear();
        }
    }
}
