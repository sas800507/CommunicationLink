using System;

namespace LinkSystem
{
    public class LinkBuffer
    {
        int _size;
        readonly byte[] _buffer;
        int _currentLen;

        /// <summary>
        /// Определяет поведение буфера <see cref="LinkSystem.LinkBuffer"/> при переполнении.
        /// </summary>
        /// <value><c>true</c> из начала вытесняется кусок; теряется входной массив, <c>false</c>.</value>
        public bool PushOnOverflow { get; set; }
        /// <summary>
        /// Указывает количество байт в буфере
        /// </summary>
        /// <value>Количество байт в буфере</value>
        public int Length { get { return _currentLen; } }
        /// <summary>
        /// Указывает размер буфера, указанный при инициализации
        /// </summary>
        /// <value>Размер буфера</value>
        public int Size { get { return _size; } }

        void BufferPush(int pushCnt)
        {
            var len = (_currentLen - pushCnt) < 0 ? pushCnt : (_currentLen - pushCnt);
            if (len < 0) return;
            var tmp = new byte[len];
            Buffer.BlockCopy(_buffer, pushCnt, tmp, 0, tmp.Length);
            Buffer.BlockCopy(tmp, 0, _buffer, 0, tmp.Length);
            _currentLen = tmp.Length;
        }

        public LinkBuffer(int size = 1024)
        {
            _currentLen = 0;
            _size = size;
            _buffer = new byte[_size];
            PushOnOverflow = false;
        }

        /// <summary>
        /// Добавить массив к буферу
        /// </summary>
        /// <param name="array">Массив для добавления</param>
        /// <param name="len">Длина добавляемая с начала массива. Если -1, то добавлется весь массив</param>
        public void Add(byte[] array, int len = -1)
        {
            if (array == null)
                return;
            if (len == -1) len = array.Length;

            if (len > (_size - _currentLen))
            {
                if (PushOnOverflow)
                {
                    BufferPush(len);
                    Buffer.BlockCopy(array, 0, _buffer, _currentLen, len);
                    _currentLen += len;
                }
            }
            else
            {
                Buffer.BlockCopy(array, 0, _buffer, _currentLen, len);
                _currentLen = _currentLen + len;
            }
        }

        /// <summary>
        /// Вычитать из массива указанное количество байт
        /// </summary>
        /// <param name="size">Размер читаемого куска, если = -1, то возращается всё что есть в буфере</param>
        public byte[] Get(int size = -1)
        {
            if (size == -1) size = Length;
            var result = new byte[size];
            Buffer.BlockCopy(_buffer, 0, result, 0, size);
            BufferPush(size);
            return result;
        }

        /// <summary>
        /// Очистка буфера
        /// </summary>
        public void Clear()
        {
            _currentLen = 0;
        }
    }
}

