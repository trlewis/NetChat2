using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
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

        private readonly string _host;
        private readonly int _port;
        private DispatcherTimer _timer;

        public MainWindow(string host, int port, string alias)
        {
            InitializeComponent();
            this._host = host;
            this._port = port;
            this.Load(alias);
        }

        //private void AliasBoxLostFocus(object sender, RoutedEventArgs e)
        //{
        //    //var newName = ((TextBox)sender).Text.Trim();
        //    //if (this.ChatClient.ChangeAlias(newName))
        //    //{
        //    //    this.AliasBox.Text = newName;
        //    //}
        //}

        private void BlinkWindow()
        {
            var blink = new Thread(() =>
            {
                Func<bool> isactive = () => this.IsActive;
                for (var i = 0; i < 5; i++)
                {
                    Dispatcher.Invoke(() => this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused);
                    Thread.Sleep(500);
                    Dispatcher.Invoke(() => this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None);
                    Thread.Sleep(500);
                    if (Dispatcher.Invoke(isactive))
                    {
                        break;
                    }
                }
                if (!Dispatcher.Invoke(isactive))
                {
                    Dispatcher.Invoke(() => this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.Paused);
                }
            });
            blink.Start();
        }

        private void ChatClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
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

        private void Load(string alias)
        {
            this.TaskbarItemInfo = new TaskbarItemInfo { ProgressValue = 1 };
            this.Activated += MainWindow_Activated;

            this.ChatClient = new ChatClient(this._host, this._port, alias);

            this.ChatClient.PropertyChanged += ChatClient_PropertyChanged;

            //this.AliasBox.Text = this.ChatClient.Alias;
            //this.AliasBox.LostFocus += this.AliasBoxLostFocus;
            this.RichActivityBox.Document.Blocks.Clear();
            this.RichActivityBox.TextChanged += (obj, textEventArgs) => this.RichActivityBox.ScrollToEnd();

            this.ChatClient.Start();
            this.ConnectionLabelText = "Client Socket Program - Server Connected";
            this.Closed += this.MainWindow_Closed;

            this._timer = new DispatcherTimer();
            this._timer.Tick += this.HeartbeatTimer_Tick;
            this._timer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            this._timer.Start();
        }

        private void HeartbeatTimer_Tick(object sender, EventArgs e)
        {
            this.ChatClient.SendHeartbeat();
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            if (this.TaskbarItemInfo == null)
            {
                return;
            }

            this.TaskbarItemInfo.ProgressState = TaskbarItemProgressState.None;
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

                    if (tcpm.MessageType.HasFlag(TcpMessageType.AliasChanged))
                        msg += string.Format("[{0}] {1} is now {2}", timeStamp, tcpm.Contents[0], tcpm.Contents[1]);

                    var msgRun = new Run(msg) { Foreground = Brushes.Blue, FontWeight = FontWeights.Bold };
                    par.Inlines.Add(msgRun);
                }

                if (tcpm.MessageType.HasFlag(TcpMessageType.Message))
                {
                    var name = tcpm.Contents[0];
                    var text = tcpm.Contents[1];
                    var toMe = text.Contains(string.Format("@{0}", this.ChatClient.Alias));

                    var timeRun = new Run(string.Format("[{0}]", timeStamp)) { FontWeight = FontWeights.Bold};
                    var nameRun = new Run(string.Format(" {1}: ", timeStamp, name)) { FontWeight = FontWeights.Bold };
                    var textRun = new Run(text);

                    if(toMe)
                    {
                        timeRun.Foreground = new SolidColorBrush(Colors.DarkRed);
                        textRun.Foreground = new SolidColorBrush(Colors.DarkRed);
                    }

                    par.Inlines.Add(timeRun);
                    par.Inlines.Add(nameRun);
                    par.Inlines.Add(textRun);

                    if (toMe && !this.IsActive)
                    {
                        this.BlinkWindow();
                    }
                }

                if (par.Inlines.Count <= 0)
                {
                    return;
                }
                this.RichActivityBox.Document.Blocks.Add(par);
            });
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
                           Contents = new List<string> { this.ChatClient.Alias, boxString }
                       };

            this.ChatClient.SendMessage(tcpm);

            this.EntryBox.Clear();
            this.EntryBox.Focus();
        }

        private void Alias_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //var red = this.ChatClient.NameColor.R;
            //var green = this.ChatClient.NameColor.G;
            //var blue = this.ChatClient.NameColor.B;
            //System.Drawing.Color inColor = System.Drawing.Color.FromArgb(red, green, blue);
            //var dialog = new System.Windows.Forms.ColorDialog { AllowFullOpen = false, Color = inColor, AnyColor = true };

            //if(dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            //{
            //    var newColor = System.Windows.Media.Color.FromArgb(255, (byte)dialog.Color.R, (byte)dialog.Color.G, (byte)dialog.Color.B);
            //    this.ChatClient.NameColor = newColor;
            //}
        }
    }
}