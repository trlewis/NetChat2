﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
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
        private readonly SimpleAes _encryption = new SimpleAes("zA4Hj7Gn40cN48$&^Cs4h&JE3ydf#%@[", "fv3GHkP(*jbnr4J}");
        private readonly Mutex _streamMutex = new Mutex();
        private List<UserListItem> _clientList = new List<UserListItem>();
        private Color _nameColor;
        private NetworkStream _serverStream;

        public ChatClient(TcpClient socket, string alias)
        {
            this.Alias = alias;
            this._clientSocket = socket;
            this.IncomingMessages = new ConcurrentQueue<TcpMessage>();
            this.NameColor = Colors.Black;
            this.IsTyping = false;
        }

        public string Alias { get; private set; }

        public IList<UserListItem> ClientList
        {
            get
            {
                if (!this._clientListMutex.WaitOne(100))
                {
                    return null;
                }

                var returnList = new List<UserListItem>(this._clientList);
                this._clientListMutex.ReleaseMutex();
                return returnList;
            }
        }

        /// <summary>
        /// This is what whatever uses this class will use to do whatever they need to do when a message comes in
        /// </summary>
        public ConcurrentQueue<TcpMessage> IncomingMessages { get; private set; }

        private bool IsRunning { get; set; }

        public bool IsTyping { get; set; }

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
                if (value == this._nameColor)
                {
                    return;
                }

                this._nameColor = value;
                this.NotifyPropertyChanged(() => this.NameBrush);
            }
        }

        public void ChangeAlias(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias) || alias == this.Alias)
            {
                return;
            }

            string oldName = this.Alias;
            this.Alias = alias;
            this.SendMessage(TcpMessageType.SystemMessage | TcpMessageType.AliasChanged,
                new List<string> { oldName, this.Alias });

            this.NotifyPropertyChanged(() => this.Alias);
        }

        private void SendMessage(TcpMessageType type, IList<string> contents, bool async = true)
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
            if (!this.IsRunning)
            {
                return;
            }

            msg.Color = this.NameColor;

            Action send = () =>
            {
                string serialized = JsonConvert.SerializeObject(msg, Formatting.Indented);
                byte[] messageBytes = this._encryption.Encrypt(serialized);
                byte[] lengthBytes = BitConverter.GetBytes(messageBytes.Length);
                var sendBytes = new byte[messageBytes.Length + lengthBytes.Length];
                Array.Copy(lengthBytes, sendBytes, 4);
                Array.Copy(messageBytes, 0, sendBytes, lengthBytes.Length, messageBytes.Length);

                if (!this._streamMutex.WaitOne(100))
                {
                    return;
                }

                this.MessageReceived(msg);

                try
                {
                    this._serverStream.Write(sendBytes, 0, sendBytes.Length);
                    this._serverStream.Flush();
                }
                catch (ObjectDisposedException)
                {
                }
                catch (IOException)
                {
                    var lostMessage = new TcpMessage
                                      {
                                          SentTime = DateTime.Now,
                                          Contents = new List<string> { "Server connection lost" },
                                          MessageType = TcpMessageType.ErrorMessage
                                      };
                    this.MessageReceived(lostMessage);
                    this.IsRunning = false;
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
            if (this.IsRunning)
            {
                this.SendMessage(TcpMessageType.SystemMessage | TcpMessageType.ClientLeft, new List<string> { this.Alias }, false);
                this.IsRunning = false;
            }
        }

        public void Start()
        {
            this._serverStream = this._clientSocket.GetStream();
            this.IsRunning = true;

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
            while (this.IsRunning)
            {
                Thread.Sleep(2);

                if (!this._streamMutex.WaitOne(100))
                {
                    continue;
                }

                var messageString = string.Empty;

                if (this._serverStream.CanRead && this._serverStream.DataAvailable)
                {
                    var lengthBytes = new byte[4];
                    this._serverStream.Read(lengthBytes, 0, 4);
                    int messageLength = BitConverter.ToInt32(lengthBytes, 0);
                    var message = new byte[messageLength];

                    int index = 0;
                    while (index < messageLength)
                    {
                        int bytesRead = this._serverStream.Read(message, index, messageLength - index);
                        index += bytesRead;
                    }

                    messageString = this._encryption.Decrypt(message);

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
            if (msg.MessageType.HasFlag(TcpMessageType.AliasChanged))
            {
                UserListItem toChange = this._clientList.SingleOrDefault(c => c.Alias == msg.Contents[0]);
                if (toChange != null)
                {
                    toChange.Alias = msg.Contents[1];
                }

                this.NotifyPropertyChanged(() => this.ClientList);
            }

            //change color of user in list
            if (msg.MessageType.HasFlag(TcpMessageType.Message) || msg.MessageType.HasFlag(TcpMessageType.UserTyping))
            {
                UserListItem client = this._clientList.FirstOrDefault(c => c.Alias == msg.Contents[0]);
                if (client != null)
                {
                    client.Color = msg.Color;
                }
            }

            if (msg.MessageType.HasFlag(TcpMessageType.ClientJoined))
            {
                this._clientList.Add(new UserListItem(msg.Contents[0]));
                this.NotifyPropertyChanged(() => this.ClientList);
            }

            if (msg.MessageType.HasFlag(TcpMessageType.ClientLeft) || msg.MessageType.HasFlag(TcpMessageType.ClientDropped))
            {
                UserListItem toRemove = this._clientList.FirstOrDefault(c => c.Alias == msg.Contents[0]);
                if (toRemove != null)
                {
                    this._clientList.Remove(toRemove);
                }

                this.NotifyPropertyChanged(() => this.ClientList);
            }

            if (!msg.MessageType.HasFlag(TcpMessageType.SilentData))
            {
                this.IncomingMessages.Enqueue(msg);

                this.NotifyPropertyChanged(() => this.IncomingMessages);
            }

            if (msg.MessageType.HasFlag(TcpMessageType.UserList))
            {
                IList<string> validNames = msg.Contents.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                var list = new List<UserListItem>(validNames.Select(n => new UserListItem(n)));
                if (list.All(u => u.Alias != this.Alias))
                {
                    var me = new UserListItem(this.Alias) { Color = this.NameColor };
                    list.Add(me);
                }
                this._clientList = list;
                this.NotifyPropertyChanged(() => this.ClientList);
            }

            if (msg.MessageType.HasFlag(TcpMessageType.UserTyping))
            {
                UserListItem user = this._clientList.FirstOrDefault(c => c.Alias == msg.Contents[0]);
                if (user != null)
                {
                    user.IsTyping = msg.IsTyping == true;
                }
            }
        }
    }
}