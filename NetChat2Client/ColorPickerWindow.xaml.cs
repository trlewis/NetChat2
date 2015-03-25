using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NetChat2Client
{
    /// <summary>
    /// Interaction logic for ColorPickerWindow.xaml
    /// </summary>
    public partial class ColorPickerWindow : Window
    {
        #region Dependency Properties

        
        public static readonly DependencyProperty RedProperty = DependencyProperty.Register("Red", typeof(int), typeof(ColorPickerWindow));

        public int Red
        {
            get { return (int)GetValue(RedProperty); }
            set { SetValue(RedProperty, value); }
        }

        
        public static readonly DependencyProperty GreenProperty = DependencyProperty.Register("Green", typeof(int), typeof(ColorPickerWindow));

        public int Green
        {
            get { return (int)GetValue(GreenProperty); }
            set { SetValue(GreenProperty, value); }
        }

        
        public static readonly DependencyProperty BlueProperty = DependencyProperty.Register("Blue", typeof(int), typeof(ColorPickerWindow));

        public int Blue
        {
            get { return (int)GetValue(BlueProperty); }
            set { SetValue(BlueProperty, value); }
        }
	


        #endregion Dependency Properties


        public ColorPickerWindow()
        {
            InitializeComponent();
        }
    }
}
