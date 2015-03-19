using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace NetChat2Server
{
    public class ClientConnection
    {
        private readonly Mutex _clientStreamMutex;
        private readonly ChatServer _server;
        private NetworkStream _clientNetworkStream;
        private TcpClient _clientSocket;

        public ClientConnection(ChatServer server)
        {
            this._server = server;
            this.IsConnected = true;
            this._clientStreamMutex = new Mutex();
        }

        public string ClientNum { get; private set; }

        public bool IsConnected { get; set; }

        public string NickName { get; set; }

        public void SendMessage(TcpMessage msg)
        {
            var thread = new Thread(() => this.SendMessageThread(msg));
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

            while (true)
            {
                Thread.Sleep(2);
                if (!this._clientSocket.Connected)
                {
                    break;
                }

                try
                {
                    if (!this._clientStreamMutex.WaitOne(250))
                    {
                        continue;
                    }

                    var dataFromClient = string.Empty;
                    if (this._clientNetworkStream.CanRead && this._clientNetworkStream.DataAvailable)
                    {
                        var readBuffer = new byte[bufferSize];
                        do
                        {
                            var bytesRead = this._clientNetworkStream.Read(readBuffer, 0,
                                this._clientSocket.ReceiveBufferSize);
                            dataFromClient += Encoding.UTF8.GetString(readBuffer, 0, bytesRead);
                        } while (this._clientNetworkStream.DataAvailable);
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
                catch (Exception e)
                {
                    Console.WriteLine(">> Error in client #{0}: {1}", this.ClientNum, e);
                    this._server.ConnectionDropped(this, true);
                }
            }
        }

        private void HandleIncomingMessage(TcpMessage msg)
        {
            if (msg.MessageType.HasFlag(TcpMessageType.NameChanged))
            {
                this.NickName = msg.Contents[1];
            }
            if (msg.MessageType.HasFlag(TcpMessageType.ClientJoined))
            {
                this.NickName = msg.Contents[0];
            }

            this._server.IncomingMessage(this, msg);
        }

        private void SendMessageThread(TcpMessage msg)
        {
            if (!this._clientStreamMutex.WaitOne(250))
            {
                return;
            }
            var serialized = JsonConvert.SerializeObject(msg, Formatting.Indented);
            var sendBytes = Encoding.UTF8.GetBytes(serialized);

            try
            {
                if (!this._clientSocket.Connected)
                {
                    return;
                }

                this._clientNetworkStream.Write(sendBytes, 0, sendBytes.Length);
                this._clientNetworkStream.Flush();
            }
            catch (IOException)
            {
                this._server.ConnectionDropped(this, true);
            }
            finally
            {
                this._clientStreamMutex.ReleaseMutex();
            }
        }
    }
}