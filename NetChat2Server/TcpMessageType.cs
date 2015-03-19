using System;

namespace NetChat2Server
{
    [Flags]
    public enum TcpMessageType
    {
        SystemMessage = 1, //not for general chatting
        SilentData = 2,
        ClientStarted = 4,
        ClientJoined = 8,
        ClientLeft = 16,
        ClientDropped = 32,
        NameChanged = 64, //is a SystemMessage
        Message = 128,
        UserList = 256,
    }
}