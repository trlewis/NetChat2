using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using NetChat2Server;
using Newtonsoft.Json;

namespace NetChat2Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Dependency Properties

        public static readonly DependencyProperty ConnectionLabelTextProperty = DependencyProperty.Register("ConnectionLabelText", typeof(string), typeof(MainWindow), null);

        private bool _stopThreads;

        public string ConnectionLabelText
        {
            get { return (string)this.GetValue(ConnectionLabelTextProperty); }
            set { this.SetValue(ConnectionLabelTextProperty, value); }
        }

        #endregion Dependency Properties

        private IList<string> _clientList;

        private TcpClient _clientSocket;
        private string _nickName;
        private NetworkStream _serverStream;
        private Mutex _streamMutex;

        public MainWindow()
        {
            InitializeComponent();
            this.Load();
        }

        private void EntryBox_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                this.SendUserMessage();
            }
        }

        private void Load()
        {
            this._clientList = new List<string>();

            var current = System.Security.Principal.WindowsIdentity.GetCurrent();
            if (current != null)
            {
                var name = current.Name;
                var index = name.LastIndexOf('\\');
                this._nickName = index > 0 ? name.Substring(index + 1) : name;
            }
            else
            {
                this._nickName = "somebody";
            }

            this.NickNameBox.Text = this._nickName;

            this._streamMutex = new Mutex();
            this._stopThreads = false;

            this.RichActivityBox.TextChanged += (obj, textEventArgs) => this.RichActivityBox.ScrollToEnd();

            this._clientSocket = new TcpClient();
            this.Message(new TcpMessage { SentTime = DateTime.Now, MessageType = TcpMessageType.ClientStarted | TcpMessageType.SystemMessage });

            var serverIpAddress = ConfigurationManager.AppSettings["ServerIpAddress"];
            var port = int.Parse(ConfigurationManager.AppSettings["ServerPort"]);

            this._clientSocket.Connect(serverIpAddress, port);
            this.ConnectionLabelText = "Client Socket Program - Server Connected";

            this._serverStream = this._clientSocket.GetStream();
            this.Closed += MainWindow_Closed;

            var receiveThread = new Thread(this.ReceiveMessages);
            receiveThread.Start();

            var joinMessage = new TcpMessage
                              {
                                  MessageType = TcpMessageType.ClientJoined | TcpMessageType.SystemMessage,
                                  SentTime = DateTime.Now,
                                  Contents = new List<string> { this._nickName }
                              };
            this.SendMessage(joinMessage);
            this.NickNameBox.LostFocus += NickNameBox_LostFocus;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (this._streamMutex.WaitOne(500))
            {
                var dropMsg = new TcpMessage
                              {
                                  SentTime = DateTime.Now,
                                  MessageType = TcpMessageType.SystemMessage | TcpMessageType.ClientLeft,
                                  Contents = new List<string> { this._nickName }
                              };
                this.SendMessageThread(dropMsg);

                this._streamMutex.ReleaseMutex();
            }

            this._stopThreads = true;
            this._serverStream.Close();
            this._clientSocket.Close();
        }

        //private void Message(string msg)
        private void Message(TcpMessage tcpm)
        {
            this.RichActivityBox.Dispatcher.InvokeAsync(() =>
            {
                //paragraph needs to be created in here otherwise when an exception gets thrown when it gets added to the
                //rich text box
                var par = new Paragraph { Margin = new Thickness(0) };
                var timeStamp = tcpm.SentTime.ToString("HH:mm:ss");
                if (tcpm.MessageType.HasFlag(TcpMessageType.SystemMessage))
                {
                    var msg = ">> ";

                    if (tcpm.MessageType.HasFlag(TcpMessageType.ClientStarted))
                        msg += string.Format("[{0}] Client Started", timeStamp);

                    if (tcpm.MessageType.HasFlag(TcpMessageType.ClientJoined))
                        msg += string.Format("[{0}] {1} joined", timeStamp, tcpm.Contents[0]);

                    if (tcpm.MessageType.HasFlag(TcpMessageType.ClientLeft))
                        msg += string.Format("[{0}] {1} left", timeStamp, tcpm.Contents[0]);

                    if (tcpm.MessageType.HasFlag(TcpMessageType.ClientDropped))
                        msg += string.Format("[{0}] {1} dropped", timeStamp, tcpm.Contents[0]);

                    if (tcpm.MessageType.HasFlag(TcpMessageType.NameChanged))
                        msg += string.Format("[{0}] {1} is now {2}", timeStamp, tcpm.Contents[0], tcpm.Contents[1]);

                    var msgRun = new Run(msg) { Foreground = Brushes.Blue, FontWeight = FontWeights.Bold };
                    par.Inlines.Add(msgRun);
                }

                if (tcpm.MessageType.HasFlag(TcpMessageType.Message))
                {
                    var name = tcpm.Contents[0];
                    var text = tcpm.Contents[1];
                    var nameRun = new Run(string.Format("[{0}] {1}: ", timeStamp, name)) { FontWeight = FontWeights.Bold };
                    var textRun = new Run(text);
                    par.Inlines.Add(nameRun);
                    par.Inlines.Add(textRun);
                }

                if (par.Inlines.Count <= 0)
                {
                    return;
                }
                this.RichActivityBox.Document.Blocks.Add(par);
            });
        }

        private void NickNameBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var oldName = this._nickName;
            this._nickName = ((TextBox)sender).Text.Trim();

            if (this._nickName.Length <= 0)
            {
                this._nickName = "somebody";
                this.NickNameBox.Text = this._nickName;
            }

            if (oldName == this._nickName)
            {
                return;
            }

            var msg = new TcpMessage
                      {
                          SentTime = DateTime.Now,
                          MessageType = TcpMessageType.NameChanged | TcpMessageType.SystemMessage,
                          Contents = new List<string> { oldName, this._nickName }
                      };
            this.SendMessage(msg);
        }

        private void ReceiveMessages()
        {
            Action redoList = () =>
            {
                var str = string.Empty;
                foreach (var user in this._clientList.Where(u => u != null))
                {
                    var s = user.Trim() + Environment.NewLine;
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        str += s;
                    }
                }

                this.ClientListBox.Dispatcher.Invoke(() => this.ClientListBox.Text = str.Trim());
            };

            while (true)
            {
                Thread.Sleep(5);
                if (this._stopThreads)
                {
                    break;
                }

                if (!this._streamMutex.WaitOne(250))
                {
                    continue;
                }

                var message = string.Empty;

                if (this._serverStream.CanRead && this._serverStream.DataAvailable)
                {
                    var readBuffer = new byte[this._clientSocket.ReceiveBufferSize];
                    do
                    {
                        var bytesRead = this._serverStream.Read(readBuffer, 0, this._clientSocket.ReceiveBufferSize);
                        message += Encoding.UTF8.GetString(readBuffer, 0, bytesRead);
                    } while (this._serverStream.DataAvailable);
                }

                this._serverStream.Flush();

                this._streamMutex.ReleaseMutex();

                if (string.IsNullOrEmpty(message))
                    continue;

                var tcpm = JsonConvert.DeserializeObject<TcpMessage>(message);
                if (tcpm == null)
                {
                    continue;
                }
                if (!tcpm.MessageType.HasFlag(TcpMessageType.SilentData))
                {
                    this.Message(tcpm);
                }

                if (tcpm.MessageType.HasFlag(TcpMessageType.UserList))
                {
                    this._clientList = new List<string>(tcpm.Contents);
                    redoList();
                }

                if (tcpm.MessageType.HasFlag(TcpMessageType.ClientLeft) ||
                    tcpm.MessageType.HasFlag(TcpMessageType.ClientLeft))
                {
                    if (this._clientList.Contains(tcpm.Contents[0]))
                    {
                        this._clientList.Remove(tcpm.Contents[0]);
                    }
                    redoList();
                }
                if (tcpm.MessageType.HasFlag(TcpMessageType.ClientJoined))
                {
                    this._clientList.Add(tcpm.Contents[0]);
                    redoList();
                }
                if (tcpm.MessageType.HasFlag(TcpMessageType.NameChanged))
                {
                    if (this._clientList.Contains(tcpm.Contents[0]))
                    {
                        this._clientList.Remove(tcpm.Contents[0]);
                    }
                    this._clientList.Add(tcpm.Contents[1]);
                    redoList();
                }
            }
        }

        private void SendMessage(TcpMessage tcpm)
        {
            var thread = new Thread(() => this.SendMessageThread(tcpm));
            thread.Start();
            this.Message(tcpm);
        }

        private void SendMessage_OnClick(object sender, RoutedEventArgs e)
        {
            this.SendUserMessage();
        }

        private void SendMessageThread(TcpMessage message)
        {
            var serialized = JsonConvert.SerializeObject(message, Formatting.Indented);
            var outStream = Encoding.UTF8.GetBytes(serialized);
            if (!this._streamMutex.WaitOne(250))
            {
                return;
            }

            try
            {
                this._serverStream.Write(outStream, 0, outStream.Length);
                this._serverStream.Flush();
            }
            catch (ObjectDisposedException)
            {
            }

            this._streamMutex.ReleaseMutex();
        }

        private void SendUserMessage()
        {
            var boxString = this.EntryBox.Text.Trim();
            if (boxString.Length <= 0)
            {
                return;
            }

            var tcpm = new TcpMessage
                       {
                           SentTime = DateTime.Now,
                           MessageType = TcpMessageType.Message,
                           Contents = new List<string> { this._nickName, boxString }
                       };
            this.SendMessage(tcpm);

            this.EntryBox.Clear();
            this.EntryBox.Focus();
        }
    }
}