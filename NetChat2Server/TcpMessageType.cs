using System;

namespace NetChat2Server
{
    [Flags]
    public enum TcpMessageType
    {
        SystemMessage = 1, //not for general chatting
        ErrorMessage = 2,
        SilentData = 4,
        ClientStarted = 8,
        ClientJoined = 16,
        ClientLeft = 32,
        ClientDropped = 64,
        NameChanged = 128, //is a SystemMessage
        Message = 256,
        UserList = 512,
    }
}