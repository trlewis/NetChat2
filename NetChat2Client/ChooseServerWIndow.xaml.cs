using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Configuration;

namespace NetChat2Client
{
    public partial class ChooseServerWindow : Window
    {
        #region Dependency Properties

        
        public static readonly DependencyProperty HostProperty = DependencyProperty.Register("Host", typeof(string), typeof(ChooseServerWindow));

        public string Host
        {
            get { return (string)GetValue(HostProperty); }
            set { SetValue(HostProperty, value); }
        }

        
        public static readonly DependencyProperty PortNumberProperty = DependencyProperty.Register("PortNumber", typeof(string), typeof(ChooseServerWindow));

        public string PortNumber
        {
            get { return (string)GetValue(PortNumberProperty); }
            set { SetValue(PortNumberProperty, value); }
        }

        
        public static readonly DependencyProperty AliasProperty = DependencyProperty.Register("Alias", typeof(string), typeof(ChooseServerWindow));

        public string Alias
        {
            get { return (string)GetValue(AliasProperty); }
            set { SetValue(AliasProperty, value); }
        }				

        #endregion Dependency Properties

        public ChooseServerWindow()
        {
            InitializeComponent();
            this.PortNumber = ConfigurationManager.AppSettings["ServerPort"];
            this.Alias = SystemHelper.GetCurrentUserName();
        }

        private void EnterChat_Click(object sender, RoutedEventArgs e)
        {
            //TODO: make sure all fields have a value. verify ip is in "*.*.*.*" format. verify port number/range. 
            var portNum = int.Parse(this.PortNumber);
            var win = new MainWindow(this.Host, portNum, this.Alias);
            win.Show();
            this.Close();
        }
    }
}
