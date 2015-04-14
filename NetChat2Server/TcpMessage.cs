using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace NetChat2Server
{
    public class TcpMessage
    {
        public Color Color { get; set; }

        public IList<string> Contents { get; set; }

        /// <summary>
        /// meant to be used in conjunction with the UserTyping TcpMessageType
        /// </summary>
        public bool? IsTyping { get; set; }

        public TcpMessageType MessageType { get; set; }

        public DateTime SentTime { get; set; }
    }
}