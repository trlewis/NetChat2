using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Media;
using NetChat2Server;
using Newtonsoft.Json;

namespace NetChat2Client
{
    public class ChatClient : ObservableObject
    {
        private readonly Mutex _clientListMutex = new Mutex();
        private readonly TcpClient _clientSocket;
        private readonly Mutex _streamMutex = new Mutex();
        private List<string> _clientList = new List<string>();
        private Color _nameColor;
        private NetworkStream _serverStream;
        private bool _stopThreads;

        public ChatClient(TcpClient socket, string alias)
        {
            this.Alias = alias;
            this._clientSocket = socket;
            this.IncomingMessages = new ConcurrentQueue<TcpMessage>();
            this.NameColor = Colors.Black;
        }

        public string Alias { get; private set; }

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

        public SolidColorBrush NameBrush
        {
            get
            {
                return new SolidColorBrush(this.NameColor);
            }
        }

        public Color NameColor
        {
            get { return this._nameColor; }
            set
            {
                if (value == null || value == this._nameColor)
                {
                    return;
                }

                this._nameColor = value;
                this.NotifyPropertyChanged(() => this.NameBrush);
            }
        }

        public bool ChangeAlias(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias) || alias == this.Alias)
            {
                return false;
            }

            var oldName = this.Alias;
            this.Alias = alias;
            this.SendMessage(TcpMessageType.SystemMessage | TcpMessageType.AliasChanged,
                new List<string> { oldName, this.Alias });

            this.NotifyPropertyChanged(() => this.Alias);
            return true;
        }

        public void SendHeartbeat()
        {
            var msg = new TcpMessage { MessageType = TcpMessageType.Heartbeat, SentTime = DateTime.Now };
            this.SendMessage(msg);
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
            this.SendMessage(TcpMessageType.SystemMessage | TcpMessageType.ClientLeft, new List<string> { this.Alias }, false);
            this._stopThreads = true;
        }

        public void Start()
        {
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
                              Contents = new List<string> { this.Alias }
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
                catch (JsonReaderException)
                {
                    var errorMsg = new TcpMessage
                                   {
                                       SentTime = DateTime.Now,
                                       MessageType = TcpMessageType.ErrorMessage,
                                       Contents = new List<string> { "JsonReaderException caught" }
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
                var list = new List<string>(msg.Contents.Where(n => !string.IsNullOrWhiteSpace(n))).ToList();
                if (!list.Contains(this.Alias))
                {
                    list.Add(this.Alias);
                }
                this._clientList = list;
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
            if (msg.MessageType.HasFlag(TcpMessageType.AliasChanged))
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