using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using NetChat2Server;

namespace NetChat2Client.Windows
{
    public partial class MainWindow
    {
        #region Dependency Properties

        public static readonly DependencyProperty AliasErrorVisibilityProperty = DependencyProperty.Register("AliasErrorVisibility", typeof(Visibility), typeof(MainWindow), null);
        public static readonly DependencyProperty ChatClientProperty = DependencyProperty.Register("ChatClient", typeof(ChatClient), typeof(MainWindow), null);

        public Visibility AliasErrorVisibility
        {
            get { return (Visibility)this.GetValue(AliasErrorVisibilityProperty); }
            set { this.SetValue(AliasErrorVisibilityProperty, value); }
        }

        public ChatClient ChatClient
        {
            get { return (ChatClient)this.GetValue(ChatClientProperty); }
            set { this.SetValue(ChatClientProperty, value); }
        }

        #endregion Dependency Properties

        private const string UrlRegex = @"^https?:\/\/([\w-]+\.)*(\w{2,})(\/[^ ]+)*(\.\w+)?\/?$";

        public MainWindow(ChatClient connection)
        {
            this.InitializeComponent();
            this.ChatClient = connection;
            this.EntryBox.Focus();
            this.Load();
        }

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

        private void ChangeAlias_OnClick(object sender, RoutedEventArgs e)
        {
            var win = new ChangeAliasDialog(this.ChatClient.Alias);
            win.Closed += (sender2, e2) =>
            {
                if (win.DialogResult == true)
                {
                    this.ChatClient.ChangeAlias(win.Alias);
                }
            };
            win.ShowDialog();
        }

        private void ChangeColor_OnClick(object sender, RoutedEventArgs e)
        {
            var win = new ColorPickerDialog(this.ChatClient.NameColor);
            win.Closed += (sender2, e2) =>
            {
                if (win.DialogResult != true)
                {
                    return;
                }
                this.ChatClient.NameColor = win.Color;
            };

            win.ShowDialog();
        }

        private void ChatClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IncomingMessages")
            {
                Dispatcher.Invoke(() =>
                {
                    while (this.ChatClient.IncomingMessages.Count > 0)
                    {
                        TcpMessage msgOut;
                        if (this.ChatClient.IncomingMessages.TryDequeue(out msgOut))
                            this.MessageReceived(msgOut);
                    }
                });
            }
        }

        /// <summary>
        /// Should only be used when MessageType has the Message flag
        /// </summary>
        /// <param name="msg"></param>
        private Paragraph ConstructMessageParagraph(TcpMessage msg)
        {
            var par = new Paragraph { Margin = new Thickness(0) };

            if (!msg.MessageType.HasFlag(TcpMessageType.Message))
            {
                return null;
            }

            var timeStamp = msg.SentTime.ToString("HH:mm:ss");

            var timeRun = new Run(string.Format("[{0}]", timeStamp)) { FontWeight = FontWeights.Bold };
            var nameRun = new Run(string.Format(" {0}: ", msg.Contents[0])) { FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(msg.Color) };

            var text = msg.Contents[1];
            var toMe = text.Contains(string.Format("@{0}", this.ChatClient.Alias));

            if (toMe)
            {
                timeRun.Foreground = new SolidColorBrush(Colors.DarkRed);
            }

            par.Inlines.Add(timeRun);
            par.Inlines.Add(nameRun);

            var urlRegex = new Regex(UrlRegex);
            var words = text.Split(' ');
            var nonLinkString = string.Empty;

            foreach (var wordTemp in words)
            {
                var word = wordTemp;
                if (urlRegex.IsMatch(word))
                {
                    //probably a better idea to have all the words that aren't links in one run, rather than a run for
                    //every word that isn't a link. so many objects that way...
                    if (!string.IsNullOrWhiteSpace(nonLinkString))
                    {
                        var wordRun = new Run(nonLinkString);
                        if (toMe)
                        {
                            wordRun.Foreground = new SolidColorBrush(Colors.DarkRed);
                        }
                        par.Inlines.Add(wordRun);
                        nonLinkString = string.Empty;
                    }

                    var link = new Hyperlink { IsEnabled = true, NavigateUri = new Uri(word) };
                    link.Inlines.Add(word);
                    link.Cursor = Cursors.Hand;

                    link.RequestNavigate += (sender, e) => Process.Start(e.Uri.ToString());

                    par.Inlines.Add(link);
                    par.Inlines.Add(" "); //seperate the link from the following word
                }
                else
                {
                    nonLinkString += word + " ";
                }
            }

            if (!string.IsNullOrWhiteSpace(nonLinkString))
            {
                var lastWordRun = new Run(nonLinkString);
                if (toMe)
                {
                    lastWordRun.Foreground = new SolidColorBrush(Colors.DarkRed);
                }
                par.Inlines.Add(lastWordRun);
            }

            return par;
        }

        private void EntryBox_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                this.SendUserMessage();
                return;
            }

            if (this.ChatClient == null)
            {
                return;
            }

            if (this.EntryBox.Text.Length <= 0 && this.ChatClient.IsTyping)
            {
                var tcpm = new TcpMessage
                {
                    SentTime = DateTime.Now,
                    MessageType = TcpMessageType.SilentData | TcpMessageType.UserTyping,
                    Contents = new List<string> { this.ChatClient.Alias },
                    IsTyping = false
                };
                this.ChatClient.IsTyping = false;
                this.ChatClient.SendMessage(tcpm);
            }

            if (this.EntryBox.Text.Length > 0 && !this.ChatClient.IsTyping)
            {
                var tcpm = new TcpMessage
                {
                    SentTime = DateTime.Now,
                    MessageType = TcpMessageType.SilentData | TcpMessageType.UserTyping,
                    Contents = new List<string> { this.ChatClient.Alias },
                    IsTyping = true
                };
                this.ChatClient.IsTyping = true;
                this.ChatClient.SendMessage(tcpm);
            }
        }

        private void Exit_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Load()
        {
            this.TaskbarItemInfo = new TaskbarItemInfo { ProgressValue = 1 };
            this.Activated += MainWindow_Activated;

            this.ChatClient.PropertyChanged += ChatClient_PropertyChanged;

            this.RichActivityBox.Document.Blocks.Clear();
            this.RichActivityBox.TextChanged += (obj, textEventArgs) => this.RichActivityBox.ScrollToEnd();

            this.ChatClient.Start();
            this.Closed += this.MainWindow_Closed;
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
            this.ShutDownClient();
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
                    {
                        msg += string.Format("[{0}] Client Started", timeStamp);
                    }

                    if (tcpm.MessageType.HasFlag(TcpMessageType.ClientJoined))
                    {
                        msg += string.Format("[{0}] {1} joined", timeStamp, tcpm.Contents[0]);
                    }

                    if (tcpm.MessageType.HasFlag(TcpMessageType.ClientLeft))
                    {
                        msg += string.Format("[{0}] {1} left", timeStamp, tcpm.Contents[0]);
                    }

                    if (tcpm.MessageType.HasFlag(TcpMessageType.ClientDropped))
                    {
                        msg += string.Format("[{0}] {1} dropped", timeStamp, tcpm.Contents[0]);
                    }

                    if (tcpm.MessageType.HasFlag(TcpMessageType.AliasChanged))
                    {
                        msg += string.Format("[{0}] {1} is now {2}", timeStamp, tcpm.Contents[0], tcpm.Contents[1]);
                    }

                    var msgRun = new Run(msg) { Foreground = Brushes.Blue, FontWeight = FontWeights.Bold };
                    par.Inlines.Add(msgRun);
                }

                if (tcpm.MessageType.HasFlag(TcpMessageType.Message))
                {
                    var toMe = tcpm.Contents[1].Contains(string.Format("@{0}", this.ChatClient.Alias));
                    par = this.ConstructMessageParagraph(tcpm);

                    if (toMe && !this.IsActive)
                    {
                        this.BlinkWindow();
                    }
                }

                if (par.Inlines.Count > 0)
                {
                    this.RichActivityBox.Document.Blocks.Add(par);
                }
            });
        }

        private void NewWindow_OnClick(object sender, RoutedEventArgs e)
        {
            var win = new ChooseServerWindow();
            win.Show();
        }

        private void PurgeChatlog_OnClick(object sender, RoutedEventArgs e)
        {
            this.RichActivityBox.Document.Blocks.Clear();
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
                return;

            var tcpm = new TcpMessage
                       {
                           SentTime = DateTime.Now,
                           MessageType = TcpMessageType.Message | TcpMessageType.UserTyping,
                           Contents = new List<string> { this.ChatClient.Alias, boxString },
                           IsTyping = false
                       };

            this.ChatClient.IsTyping = false;
            this.ChatClient.SendMessage(tcpm);

            this.EntryBox.Clear();
            this.EntryBox.Focus();
        }

        private void ShutDownClient()
        {
            this.ChatClient.ShutDown();
        }
    }
}