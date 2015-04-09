using System;
using System.Configuration;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace NetChat2Client
{
    public partial class ChooseServerWindow
    {
        #region Dependency Properties

        public static readonly DependencyProperty ConnectingStringProperty = DependencyProperty.Register("ConnectingString", typeof(string), typeof(ChooseServerWindow), null);
        public static readonly DependencyProperty ConnectingStringVisibilityProperty = DependencyProperty.Register("ConnectingStringVisibility", typeof(Visibility), typeof(ChooseServerWindow), null);
        public static readonly DependencyProperty ConnectionErrorStringProperty = DependencyProperty.Register("ConnectionErrorString", typeof(string), typeof(ChooseServerWindow), null);

        public string ConnectingString
        {
            get { return (string)this.GetValue(ConnectingStringProperty); }
            set { this.SetValue(ConnectingStringProperty, value); }
        }

        public Visibility ConnectingStringVisibility
        {
            get { return (Visibility)this.GetValue(ConnectingStringVisibilityProperty); }
            set { this.SetValue(ConnectingStringVisibilityProperty, value); }
        }

        public string ConnectionErrorString
        {
            get { return (string)this.GetValue(ConnectionErrorStringProperty); }
            set { this.SetValue(ConnectionErrorStringProperty, value); }
        }

        #endregion Dependency Properties

        //its not perfect, 999.888.777.666 would still be considered valid. but should be good enough for now
        private const string IpAddressRegex = @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$";

        private int _connectingDots;

        public ChooseServerWindow()
        {
            InitializeComponent();
            this.ConnectingStringVisibility = Visibility.Collapsed;
            this.PortNumberBox.Text = ConfigurationManager.AppSettings["ServerPort"];
            this.AliasBox.Text = SystemHelper.GetCurrentUserName();
            this.HostBox.Focus();

            this.ConnectingString = "Connecting";
            var timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 0, 1);
            timer.Tick += DotsTimer_Tick;
            timer.Start();
        }

        private void CreateChatWindow()
        {
            this.ConnectionErrorString = string.Empty;

            //check host
            var host = this.HostBox.Text.Trim();
            if (!Regex.IsMatch(host, IpAddressRegex))
            {
                this.ConnectionErrorString = "Invalid IP format";
                return;
            }

            //check port number
            var portText = this.PortNumberBox.Text.Trim();
            int portNum;
            if (!int.TryParse(portText, out portNum))
            {
                this.ConnectionErrorString = "Port number invalid";
                return;
            }

            if (portNum < 1 || portNum > 65535)
            {
                this.ConnectionErrorString = "Port must be in range [1-65535]";
                return;
            }

            //check alias
            var alias = this.AliasBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(alias))
            {
                this.ConnectionErrorString = "Alias required";
                return;
            }

            //doing this in a thread so the UI stays responsive. can't use Dispatcher.InvokeAsync because
            //that would still freeze up the UI while trying to connect
            Action<string, int, string> connectAction = (hostIp, port, aliasStr) =>
            {
                var client = new TcpClient();
                try
                {
                    client.Connect(host, portNum);
                    var chatClient = new ChatClient(client, alias);
                    Dispatcher.Invoke(() =>
                    {
                        var win = new MainWindow(chatClient);
                        win.Show();
                        this.Close();
                    });
                }
                catch (Exception)
                {
                    this.Dispatcher.Invoke(() => this.ConnectionErrorString = "Could not connect");
                }
                finally
                {
                    this.Dispatcher.Invoke(() => this.EnterChatButton.IsEnabled = true);
                    this.Dispatcher.Invoke(() => this.ConnectingStringVisibility = Visibility.Collapsed);
                }
            };

            this.EnterChatButton.IsEnabled = false;
            this.ConnectingStringVisibility = Visibility.Visible;
            var connectThread = new Thread(() => connectAction(host, portNum, alias));
            connectThread.Start();
        }

        private void DotsTimer_Tick(object sender, EventArgs e)
        {
            this._connectingDots++;
            if (this._connectingDots > 3)
            {
                this._connectingDots = 0;
            }

            this.ConnectingString = "Connecting" + (new String('.', this._connectingDots));
        }

        private void EnterChat_Click(object sender, RoutedEventArgs e)
        {
            this.CreateChatWindow();
        }

        private void TextBox_OnGotFocus(object sender, RoutedEventArgs e)
        {
            var textbox = (TextBox)sender;
            textbox.SelectAll();
        }

        private void TextBox_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                this.CreateChatWindow();
            }
        }
    }
}