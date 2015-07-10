using System.Windows;
using System.Windows.Input;

namespace NetChat2Client.Windows
{
    public partial class ChangeAliasDialog
    {
        #region Dependency Properties

        public static readonly DependencyProperty AliasErrorVisibilityProperty = DependencyProperty.Register("AliasErrorVisibility", typeof(Visibility), typeof(ChangeAliasDialog), null);

        public Visibility AliasErrorVisibility
        {
            get { return (Visibility)this.GetValue(AliasErrorVisibilityProperty); }
            set { this.SetValue(AliasErrorVisibilityProperty, value); }
        }

        #endregion Dependency Properties

        public ChangeAliasDialog(string currentAlias)
        {
            InitializeComponent();
            this.AliasErrorVisibility = Visibility.Collapsed;
            this.Alias = currentAlias;
            this.AliasEntryBox.Text = currentAlias;
            this.AliasEntryBox.Focus();
            this.AliasEntryBox.SelectAll();
        }

        public string Alias { get; private set; }

        private void AcceptAlias_OnClick(object sender, RoutedEventArgs e)
        {
            this.TryConfirmAlias();
        }

        private void AliasEntryBox_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.Enter)
            {
                this.TryConfirmAlias();
            }
        }

        private void CancelAlias_OnClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void TryConfirmAlias()
        {
            string newAlias = this.AliasEntryBox.Text.Trim();
            if (newAlias.Length <= 0 || newAlias.Length > 15)
            {
                this.AliasErrorVisibility = Visibility.Visible;
                return;
            }

            this.Alias = newAlias;
            this.DialogResult = true;
        }
    }
}