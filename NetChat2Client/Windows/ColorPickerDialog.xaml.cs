using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NetChat2Client.Code.Extensions;

namespace NetChat2Client.Windows
{
    public partial class ColorPickerDialog
    {
        #region Dependency Properties

        public static readonly DependencyProperty BlueProperty = DependencyProperty.Register("Blue", typeof(int), typeof(ColorPickerDialog));
        public static readonly DependencyProperty CustomColorBrushProperty = DependencyProperty.Register("CustomColorBrush", typeof(SolidColorBrush), typeof(ColorPickerDialog), null);
        public static readonly DependencyProperty GreenProperty = DependencyProperty.Register("Green", typeof(int), typeof(ColorPickerDialog));
        public static readonly DependencyProperty RedProperty = DependencyProperty.Register("Red", typeof(int), typeof(ColorPickerDialog));

        public int Blue
        {
            get { return (int)GetValue(BlueProperty); }
            set { SetValue(BlueProperty, value); }
        }

        public SolidColorBrush CustomColorBrush
        {
            get { return (SolidColorBrush)this.GetValue(CustomColorBrushProperty); }
            set { this.SetValue(CustomColorBrushProperty, value); }
        }

        public int Green
        {
            get { return (int)GetValue(GreenProperty); }
            set { SetValue(GreenProperty, value); }
        }

        public int Red
        {
            get { return (int)GetValue(RedProperty); }
            set { SetValue(RedProperty, value); }
        }

        #endregion Dependency Properties

        public ColorPickerDialog()
        {
            this.InitializeComponent();
        }

        public ColorPickerDialog(Color inColor)
        {
            this.InitializeComponent();
            this.Red = inColor.R;
            this.Green = inColor.G;
            this.Blue = inColor.B;
        }

        public Color Color { get; private set; }

        private void Accept_OnClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void ColorComponent_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.UpdateColorRectangle();
        }

        private void ColorValueText_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            this.Red = this.Red.Clamp(0, 255);
            this.Green = this.Green.Clamp(0, 255);
            this.Blue = this.Blue.Clamp(0, 255);

            this.UpdateColorRectangle();
        }

        private void UpdateColorRectangle()
        {
            this.Color = new Color
                         {
                             A = 255,
                             R = (byte)this.Red,
                             G = (byte)this.Green,
                             B = (byte)this.Blue
                         };
            this.CustomColorBrush = new SolidColorBrush(this.Color);
        }
    }
}