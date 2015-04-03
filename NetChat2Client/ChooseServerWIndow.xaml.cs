using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NetChat2Client
{
    public partial class ChooseServerWindow
    {
        public ChooseServerWindow()
        {
            InitializeComponent();
            this.PortNumberBox.Text = ConfigurationManager.AppSettings["ServerPort"];
            this.AliasBox.Text = SystemHelper.GetCurrentUserName();
            this.HostBox.Focus();
        }

        private void CreateChatWindow()
        {
            //TODO: make sure all fields have a value. verify ip is in "*.*.*.*" format. verify port number/range.
            var host = this.HostBox.Text;
            var portNum = int.Parse(this.PortNumberBox.Text);
            var alias = this.AliasBox.Text.Trim();
            var win = new MainWindow(host, portNum, alias);
            win.Show();
            this.Close();
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