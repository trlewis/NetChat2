using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetChat2Server;
using Newtonsoft.Json;

namespace NetChat2Client
{
    public class ChatClient : ObservableObject
    {
        private readonly Mutex _clientListMutex = new Mutex();
        private readonly string _hostName;
        private readonly int _portNumber;
        private readonly Mutex _streamMutex = new Mutex();
        private List<string> _clientList = new List<string>();
        private TcpClient _clientSocket = new TcpClient();
        private NetworkStream _serverStream;
        private bool _stopThreads;

        public ChatClient(string hostName, int portNumber)
        {
            this._hostName = hostName;
            this._portNumber = portNumber;
            this.IncomingMessages = new ConcurrentQueue<TcpMessage>();
            this.NickName = SystemHelper.GetCurrentUserName();
        }

        public IList<string> ClientList
        {
            get
            {
                if (!this._clientListMutex.WaitOne(100))
                {
                    return null;
                }

                var returnList = new List<string>(this._clientList);
                this._clientListMutex.ReleaseMutex();
                return returnList;
            }
        }

        /// <summary>
        /// This is what whatever uses this class will use to do whatever they need to do when a message comes in
        /// </summary>
        public ConcurrentQueue<TcpMessage> IncomingMessages { get; private set; }

        public string NickName { get; private set; }

        public bool ChangeNickName(string nick)
        {
            if (string.IsNullOrWhiteSpace(nick) || nick == this.NickName)
            {
                return false;
            }

            var oldName = this.NickName;
            this.NickName = nick;
            this.SendMessage(TcpMessageType.SystemMessage | TcpMessageType.NameChanged,
                new List<string> { oldName, this.NickName });
            return true;
        }

        public void SendMessage(TcpMessageType type, IList<string> contents, bool async = true)
        {
            var msg = new TcpMessage
                      {
                          SentTime = DateTime.Now,
                          MessageType = type,
                          Contents = contents
                      };
            this.SendMessage(msg, async);
        }

        public void SendMessage(TcpMessage msg, bool async = true)
        {
            Action send = () =>
            {
                var serialized = JsonConvert.SerializeObject(msg, Formatting.Indented);
                var outStream = Encoding.UTF8.GetBytes(serialized);
                if (!this._streamMutex.WaitOne(100))
                {
                    return;
                }

                this.MessageReceived(msg);

                try
                {
                    this._serverStream.Write(outStream, 0, outStream.Length);
                    this._serverStream.Flush();
                }
                catch (ObjectDisposedException)
                {
                }
                finally
                {
                    this._streamMutex.ReleaseMutex();
                }
            };

            if (async)
            {
                var thread = new Thread(() => send());
                thread.Start();
                return;
            }
            send();//send synchronously
        }

        public void ShutDown()
        {
            this.SendMessage(TcpMessageType.SystemMessage | TcpMessageType.ClientLeft, new List<string> { this.NickName }, false);
            this._stopThreads = true;
        }

        public void Start()
        {
            this._clientSocket.Connect(this._hostName, this._portNumber);
            this._serverStream = this._clientSocket.GetStream();

            //start threads
            var listener = new Thread(this.ListenForIncomingMessages);
            listener.Start();

            var msg = new TcpMessage
                      {
                          SentTime = DateTime.Now,
                          MessageType = TcpMessageType.SystemMessage | TcpMessageType.ClientStarted
                      };
            this.MessageReceived(msg);
            var joinMsg = new TcpMessage
                          {
                              SentTime = DateTime.Now,
                              MessageType = TcpMessageType.SystemMessage | TcpMessageType.ClientJoined,
                              Contents = new List<string> { this.NickName }
                          };
            this.SendMessage(joinMsg);
        }

        private void ListenForIncomingMessages()
        {
            while (true)
            {
                Thread.Sleep(2);
                if (this._stopThreads)
                {
                    break;
                }

                if (!this._streamMutex.WaitOne(100))
                {
                    continue;
                }

                var messageString = string.Empty;

                if (this._serverStream.CanRead && this._serverStream.DataAvailable)
                {
                    var readBuffer = new byte[this._clientSocket.ReceiveBufferSize];
                    do
                    {
                        var bytesRead = this._serverStream.Read(readBuffer, 0, this._clientSocket.ReceiveBufferSize);
                        messageString += Encoding.UTF8.GetString(readBuffer, 0, bytesRead);
                    } while (this._serverStream.DataAvailable);
                    this._serverStream.Flush();
                }

                this._streamMutex.ReleaseMutex();

                if (string.IsNullOrWhiteSpace(messageString))
                {
                    continue;
                }

                try
                {
                    var message = JsonConvert.DeserializeObject<TcpMessage>(messageString);
                    if (message != null)
                    {
                        this.MessageReceived(message);
                    }
                }
                catch (JsonReaderException e)
                {
                    var errorMsg = new TcpMessage
                                   {
                                       SentTime = DateTime.Now,
                                       MessageType = TcpMessageType.ErrorMessage,
                                       Contents = new List<string> { e.ToString() }
                                   };
                    this.MessageReceived(errorMsg);
                }
            }
        }

        private void MessageReceived(TcpMessage msg)
        {
            if (!msg.MessageType.HasFlag(TcpMessageType.SilentData))
            {
                this.IncomingMessages.Enqueue(msg);

                this.NotifyPropertyChanged(() => this.IncomingMessages);
            }

            if (msg.MessageType.HasFlag(TcpMessageType.UserList))
            {
                this._clientList = new List<string>(msg.Contents.Where(n => !string.IsNullOrWhiteSpace(n)));
                this.NotifyPropertyChanged(() => this.ClientList);
            }

            if (msg.MessageType.HasFlag(TcpMessageType.ClientLeft) ||
                msg.MessageType.HasFlag(TcpMessageType.ClientDropped))
            {
                if (this._clientList.Contains(msg.Contents[0]))
                {
                    this._clientList.Remove(msg.Contents[0]);
                }
                this.NotifyPropertyChanged(() => this.ClientList);
            }

            if (msg.MessageType.HasFlag(TcpMessageType.ClientJoined))
            {
                this._clientList.Add(msg.Contents[0]);
                this.NotifyPropertyChanged(() => this.ClientList);
            }
            if (msg.MessageType.HasFlag(TcpMessageType.NameChanged))
            {
                if (this._clientList.Contains(msg.Contents[0]))
                {
                    this._clientList.Remove(msg.Contents[0]);
                }
                this._clientList.Add(msg.Contents[1]);
                this.NotifyPropertyChanged(() => this.ClientList);
            }
        }
    }
}