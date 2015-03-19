using System;
using System.ComponentModel;
using System.Linq.Expressions;

namespace NetChat2Client
{
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string propertyName)
        {
            var changeHandler = this.PropertyChanged;
            if (changeHandler != null)
            {
                var args = new PropertyChangedEventArgs(propertyName);
                changeHandler(this, args);
            }
        }

        protected void NotifyPropertyChanged(Expression<Func<object>> propertyExpression)
        {
            if (propertyExpression == null)
            {
                throw new ArgumentNullException("propertyExpression");
            }

            var ux = propertyExpression.Body as UnaryExpression;
            var mex = ux == null ? (MemberExpression)propertyExpression.Body : (MemberExpression)ux.Operand;

            this.NotifyPropertyChanged(mex.Member.Name);
        }
    }
}