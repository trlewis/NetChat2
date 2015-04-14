using System.Windows.Media;

namespace NetChat2Client
{
    public class UserListItem : ObservableObject
    {
        private string _alias;

        private Color _color;

        private bool _isTyping;

        public UserListItem(string alias)
        {
            this.Alias = alias;
            this.Color = Colors.Black;
        }

        public string Alias
        {
            get { return this._alias; }
            set
            {
                this._alias = value;
                this.NotifyPropertyChanged(() => this.DisplayAlias);
            }
        }

        public SolidColorBrush AliasBrush
        {
            get { return new SolidColorBrush(this.Color); }
        }

        public Color Color
        {
            get { return this._color; }
            set
            {
                if (value == this._color)
                {
                    return;
                }
                this._color = value;
                this.NotifyPropertyChanged(() => this.Color);
                this.NotifyPropertyChanged(() => this.AliasBrush);
            }
        }

        public string DisplayAlias
        {
            get { return this.IsTyping ? string.Format("{0}...", this.Alias) : this.Alias; }
        }

        public bool IsTyping
        {
            get { return this._isTyping; }
            set
            {
                if (value == this._isTyping)
                {
                    return;
                }

                this._isTyping = value;
                this.NotifyPropertyChanged(() => this.IsTyping);
                this.NotifyPropertyChanged(() => this.DisplayAlias);
            }
        }
    }
}