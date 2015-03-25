using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace NetChat2Server
{
    public class TcpMessage
    {
        public IList<string> Contents { get; set; }

        public TcpMessageType MessageType { get; set; }

        public DateTime SentTime { get; set; }

        public Color Color { get; set; }
    }
}