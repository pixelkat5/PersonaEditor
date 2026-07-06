using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace PersonaEditor.Views.Tools
{
    public partial class ColorPickerTool : Window, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Notify(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion INotifyPropertyChanged implementation

        private Color _Color = Colors.Transparent;
        public Color Color
        {
            get { return _Color; }
            set
            {
                if (value != _Color)
                {
                    _Color = value;
                    Notify("Color");
                }
            }
        }
        
        public ColorPickerTool(Color color = new Color())
        {
            InitializeComponent();

            // If the incoming color is fully transparent (e.g. the "no background
            // set yet" default), start the picker fully opaque instead. Otherwise
            // dragging the RGB canvas can never produce a visible color, since
            // CanvasRGBUC_SelectColorChanged preserves whatever alpha we start with.
            if (color.A == 0)
                color.A = 255;

            Color = color;
            CanvasRGBUC.SelectColorChanged += CanvasRGBUC_SelectColorChanged;
        }

        private void CanvasRGBUC_SelectColorChanged(Color color)
        {
            Color temp = new Color()
            {
                A = Color.A,
                R = color.R,
                G = color.G,
                B = color.B
            };
            Color = temp;
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}