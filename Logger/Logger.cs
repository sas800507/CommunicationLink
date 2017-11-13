using System;
using System.IO;
using System.Text;

namespace Logger
{
    public class Log
    {
        readonly string _path = string.Empty;
        readonly bool _append;

        public bool ConsoleOutput { private get; set; }
        public bool AddDateTime { private get; set; }

        public Log(bool console = false)
        {
            ConsoleOutput = console;
        }

        public Log(string path, bool append = false)
        {
            _path = path;
            _append = append;
            if (!_append)
            {
                using (var file = new System.IO.StreamWriter(_path, _append))
                {
                    file.Write(string.Empty);
                }
            }
            _append = true;
        }

        public void AddLine(string str, string suffix = "")
        {
            if (!string.IsNullOrEmpty(suffix)) str = string.Format("{0} : {1}", suffix, str);
            if (AddDateTime) str = string.Format("{0} : {1}", DateTime.Now.ToString("dd.MM.yyyy hh:mm:ss.fff"), str);
            if (ConsoleOutput)
                Console.WriteLine(str);
            if (string.IsNullOrEmpty(_path)) return;
            try
            {
                using (var file = new StreamWriter(_path, _append))
                {
                    file.WriteLine(str);
                }
            }
            catch (Exception)
            {
                return;
            }
        }

        public void AddLine(byte[] data, string suffix = "")
        {
            var hex = new StringBuilder(data.Length * 3);
            foreach (byte b in data)
                hex.AppendFormat("{0:X2}_", b);
            AddLine(hex.ToString(), suffix);
        }
    }
}