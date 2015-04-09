using System.Configuration;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NetChat2Client
{
    public partial class ChooseServerWindow
    {
        #region Dependency Properties

        public static readonly DependencyProperty ConnectionErrorStringProperty = DependencyProperty.Register("ConnectionErrorString", typeof(string), typeof(ChooseServerWindow), null);

        public string ConnectionErrorString
        {
            get { return (string)this.GetValue(ConnectionErrorStringProperty); }
            set { this.SetValue(ConnectionErrorStringProperty, value); }
        }

        #endregion Dependency Properties

        //its not perfect, 999.888.777.666 would still be considered valid. but should be good enough for now
        private const string IpAddressRegex = @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$";

        public ChooseServerWindow()
        {
            InitializeComponent();
            this.PortNumberBox.Text = ConfigurationManager.AppSettings["ServerPort"];
            this.AliasBox.Text = SystemHelper.GetCurrentUserName();
            this.HostBox.Focus();
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

            var win = new MainWindow(host, portNum, alias);
            win.Show();
            this.Close();
        }

        private void EnterChat_Click(object sender, RoutedEventArgs e)
        {
            //this.ConnectionErrorString = "sodfinsdf sf sf sf sdf sdfs dfaesfr ";
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