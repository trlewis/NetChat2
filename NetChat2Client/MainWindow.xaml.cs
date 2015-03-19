using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using NetChat2Server;

namespace NetChat2Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Dependency Properties

        public static readonly DependencyProperty ChatClientProperty = DependencyProperty.Register("ChatClient", typeof(ChatClient), typeof(MainWindow), null);
        public static readonly DependencyProperty ConnectionLabelTextProperty = DependencyProperty.Register("ConnectionLabelText", typeof(string), typeof(MainWindow), null);

        public ChatClient ChatClient
        {
            get { return (ChatClient)this.GetValue(ChatClientProperty); }
            set { this.SetValue(ChatClientProperty, value); }
        }

        public string ConnectionLabelText
        {
            get { return (string)this.GetValue(ConnectionLabelTextProperty); }
            set { this.SetValue(ConnectionLabelTextProperty, value); }
        }

        #endregion Dependency Properties

        public MainWindow()
        {
            InitializeComponent();
            this.Load();
        }

        private void ChatClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //check this in xaml first
            if (e.PropertyName == "IncomingMessages")
            {
                Dispatcher.Invoke(() =>
                {
                    while (this.ChatClient.IncomingMessages.Count > 0)
                    {
                        TcpMessage msgOut = null;
                        if (this.ChatClient.IncomingMessages.TryDequeue(out msgOut))
                        {
                            this.MessageReceived(msgOut);
                        }
                    }
                });
            }
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
            var hostName = ConfigurationManager.AppSettings["ServerIpAddress"];
            var port = int.Parse(ConfigurationManager.AppSettings["ServerPort"]);

            this.ChatClient = new ChatClient(hostName, port);

            this.ChatClient.PropertyChanged += ChatClient_PropertyChanged;

            this.NickNameBox.Text = this.ChatClient.NickName;
            this.NickNameBox.LostFocus += this.NickNameBox_LostFocus;
            this.RichActivityBox.Document.Blocks.Clear();
            this.RichActivityBox.TextChanged += (obj, textEventArgs) => this.RichActivityBox.ScrollToEnd();

            this.ChatClient.Start();
            this.ConnectionLabelText = "Client Socket Program - Server Connected";
            this.Closed += this.MainWindow_Closed;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            this.ChatClient.ShutDown();
        }

        private void MessageReceived(TcpMessage tcpm)
        {
            this.RichActivityBox.Dispatcher.InvokeAsync(() =>
            {
                //paragraph needs to be created in here otherwise when an exception gets thrown when it gets added to the
                //rich text box
                var par = new Paragraph { Margin = new Thickness(0) };
                var timeStamp = tcpm.SentTime.ToString("HH:mm:ss");

                if (tcpm.MessageType.HasFlag(TcpMessageType.ErrorMessage))
                {
                    var msg = string.Format(">> [{0}] {1}", timeStamp, tcpm.Contents[0]);
                    var msgRun = new Run(msg) { Foreground = Brushes.Red, FontWeight = FontWeights.Bold };
                    par.Inlines.Add(msgRun);
                }

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
            var newName = ((TextBox)sender).Text.Trim();
            if (this.ChatClient.ChangeNickName(newName))
            {
                this.NickNameBox.Text = newName;
            }
        }

        private void SendMessage_OnClick(object sender, RoutedEventArgs e)
        {
            this.SendUserMessage();
        }

        private void SendUserMessage()
        {
            if (this.ChatClient == null)
            {
                return;
            }
            var boxString = this.EntryBox.Text.Trim();
            if (boxString.Length <= 0)
            {
                return;
            }

            var tcpm = new TcpMessage
                       {
                           SentTime = DateTime.Now,
                           MessageType = TcpMessageType.Message,
                           Contents = new List<string> { this.ChatClient.NickName, boxString }
                       };

            this.ChatClient.SendMessage(tcpm);

            this.EntryBox.Clear();
            this.EntryBox.Focus();
        }
    }
}