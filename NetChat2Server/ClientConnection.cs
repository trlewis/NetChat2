﻿using System;
using System.Configuration;
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
        private long _lagLimit;
        private DateTime _lastHeartbeat;

        public ClientConnection(ChatServer server)
        {
            this._server = server;
            this.IsConnected = true;
            this._clientStreamMutex = new Mutex();
            this._lagLimit = int.Parse(ConfigurationManager.AppSettings["ClientTimeout"]);
        }

        public string Alias { get; set; }

        public string ClientNum { get; private set; }

        public bool IsConnected { get; set; }

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

            this._lastHeartbeat = DateTime.Now;
        }

        private void ChatThread(int bufferSize)
        {
            this._clientNetworkStream = this._clientSocket.GetStream();

            while (this.IsConnected)
            {
                Thread.Sleep(2);

                var lagTime = DateTime.Now - this._lastHeartbeat;
                if (lagTime.TotalMilliseconds >= this._lagLimit)
                {
                    this._server.ConnectionDropped(this, true);
                    break;
                }

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
                catch (JsonReaderException e)
                {
                    Console.WriteLine(">> Error parsing JSON from client {0}", this.Alias ?? this.ClientNum);
                }
                catch (Exception e)
                {
                    Console.WriteLine(">> Error in client #{0}: {1}", this.ClientNum, e);
                }
            }
        }

        private void HandleIncomingMessage(TcpMessage msg)
        {
            if (!msg.MessageType.HasFlag(TcpMessageType.Heartbeat))
            {
                this._server.IncomingMessage(this, msg);
            }

            if (msg.MessageType.HasFlag(TcpMessageType.AliasChanged))
            {
                this.Alias = msg.Contents[1];
            }

            if (msg.MessageType.HasFlag(TcpMessageType.ClientJoined))
            {
                this.Alias = msg.Contents[0];
            }

            if (msg.MessageType.HasFlag(TcpMessageType.Heartbeat))
            {
                this._lastHeartbeat = DateTime.Now;
            }
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