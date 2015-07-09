using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;

namespace NetChat2Server
{
    public class ClientConnection
    {
        private readonly Mutex _clientStreamMutex;
        private readonly SimpleAes _encryption = new SimpleAes("zA4Hj7Gn40cN48$&^Cs4h&JE3ydf#%@[", "fv3GHkP(*jbnr4J}");
        private readonly ChatServer _server;
        private NetworkStream _clientNetworkStream;
        private TcpClient _clientSocket;

        public ClientConnection(ChatServer server)
        {
            this._server = server;
            this.IsConnected = true;
            this._clientStreamMutex = new Mutex();
        }

        public string Alias { get; private set; }

        public string ClientNum { get; private set; }

        public bool IsConnected { get; set; }

        public void SendMessage(TcpMessage message)
        {
            Action<TcpMessage> sendThread = msg =>
            {
                var serialized = JsonConvert.SerializeObject(msg, Formatting.Indented);
                var sendBytes = this._encryption.Encrypt(serialized);

                if (!this._clientSocket.Connected)
                {
                    return;
                }

                if (!this._clientStreamMutex.WaitOne(250))
                {
                    return;
                }

                try
                {
                    this._clientNetworkStream.Write(sendBytes, 0, sendBytes.Length);
                    this._clientNetworkStream.Flush();
                }
                catch (IOException)
                {
                    this.DropConnection();
                }
                finally
                {
                    this._clientStreamMutex.ReleaseMutex();
                }
            };

            var thread = new Thread(() => sendThread(message));
            thread.Start();
        }

        public void StartClient(TcpClient inSocket, string clientNum)
        {
            this._clientSocket = inSocket;
            this.ClientNum = clientNum;
            var bufferSize = this._clientSocket.ReceiveBufferSize;

            var clientThread = new Thread(() => ChatThread(bufferSize));
            clientThread.Start();
        }

        private void ChatThread(int bufferSize)
        {
            this._clientNetworkStream = this._clientSocket.GetStream();

            while (this.IsConnected)
            {
                Thread.Sleep(2);

                if (!this._clientSocket.Connected)
                {
                    this.DropConnection();
                    break;
                }

                try
                {
                    var dataFromClient = string.Empty;
                    var readBuffer = new byte[bufferSize];

                    if (!this._clientStreamMutex.WaitOne(250))
                    {
                        continue;
                    }

                    if (this._clientNetworkStream.CanRead && this._clientNetworkStream.DataAvailable)
                    {
                        var bytesRead = this._clientNetworkStream.Read(readBuffer, 0,
                            this._clientSocket.ReceiveBufferSize);
                        dataFromClient = this._encryption.Decrypt(readBuffer.Take(bytesRead).ToArray());
                        this._clientNetworkStream.Flush();
                    }

                    this._clientStreamMutex.ReleaseMutex();

                    if (!string.IsNullOrWhiteSpace(dataFromClient))
                    {
                        var msg = JsonConvert.DeserializeObject<TcpMessage>(dataFromClient);
                        if (msg != null)
                        {
                            this.HandleIncomingMessage(msg);
                        }
                    }
                }
                catch (JsonReaderException)
                {
                    Console.WriteLine(">> Error parsing JSON from client {0}", this.Alias ?? this.ClientNum);
                }
                catch (Exception e)
                {
                    Console.WriteLine(">> Error in client #{0}: {1}", this.ClientNum, e);
                }
            }
        }

        private void DropConnection()
        {
            var msg = new TcpMessage
                      {
                          SentTime = DateTime.Now,
                          MessageType = TcpMessageType.ClientDropped | TcpMessageType.SystemMessage,
                          Contents = new List<string> { this.Alias }
                      };

            this._server.HandleIncomingMessage(this, msg);
        }

        private void HandleIncomingMessage(TcpMessage msg)
        {
            this._server.HandleIncomingMessage(this, msg);

            if (msg.MessageType.HasFlag(TcpMessageType.AliasChanged))
            {
                this.Alias = msg.Contents[1];
            }

            if (msg.MessageType.HasFlag(TcpMessageType.ClientJoined))
            {
                this.Alias = msg.Contents[0];
            }
        }
    }
}