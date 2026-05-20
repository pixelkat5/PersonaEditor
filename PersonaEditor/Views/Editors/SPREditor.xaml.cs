using PersonaEditor.ViewModels.Editors;
using PersonaEditor.Views.Tools;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace PersonaEditor.Views.Editors
{
    public partial class SPREditor : UserControl
    {
        private object draggedKey;
        private Point dragStartTexturePoint;
        private int dragStartX1;
        private int dragStartX2;
        private int dragStartY1;
        private int dragStartY2;

        public SPREditor()
        {
            InitializeComponent();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
                if (item.Tag is Color color)
                {
                    var isBackgroundColor = item.Header as string == "Texture Background...";
                    ColorPickerTool tool = new ColorPickerTool(isBackgroundColor ? GetOpaqueBackgroundPickerColor(color) : color);
                    if (tool.ShowDialog() == true)
                        item.Tag = isBackgroundColor ? ToOpaque(tool.Color) : tool.Color;
                }
        }

        private void ResetBackground_Click(object sender, RoutedEventArgs e)
        {
            ApplicationSettings.SPREditor.Default.BackgroundColor = Colors.Transparent;
        }

        private void PickBackground_Click(object sender, RoutedEventArgs e)
        {
            ColorPickerTool tool = new ColorPickerTool(GetOpaqueBackgroundPickerColor(ApplicationSettings.SPREditor.Default.BackgroundColor));
            if (tool.ShowDialog() == true)
                ApplicationSettings.SPREditor.Default.BackgroundColor = ToOpaque(tool.Color);
        }

        private static Color GetOpaqueBackgroundPickerColor(Color color)
        {
            return color.A == 0 ? Colors.DimGray : ToOpaque(color);
        }

        private static Color ToOpaque(Color color)
        {
            return Color.FromArgb(255, color.R, color.G, color.B);
        }

        private void ItemsControl_MouseMove(object sender, MouseEventArgs e)
        {
            var sen = sender as FrameworkElement;
            if (sen == null)
                return;

            Rect temp;
            if (sen.DataContext is SPRTextureVM spr)
                temp = spr.Rect;
            else
                return;

            var texturePoint = GetTexturePoint(e.GetPosition(sender as IInputElement), sen, temp);

            XCoo.Text = Math.Round(texturePoint.X).ToString();
            YCoo.Text = Math.Round(texturePoint.Y).ToString();

            if (draggedKey is SPRKeyVM key && e.LeftButton == MouseButtonState.Pressed)
            {
                var deltaX = (int)Math.Round(texturePoint.X - dragStartTexturePoint.X);
                var deltaY = (int)Math.Round(texturePoint.Y - dragStartTexturePoint.Y);

                key.X1 = dragStartX1 + deltaX;
                key.X2 = dragStartX2 + deltaX;
                key.Y1 = dragStartY1 + deltaY;
                key.Y2 = dragStartY2 + deltaY;

                e.Handled = true;
            }
            else if (draggedKey != null)
            {
                EndDrag(sen);
            }
        }

        private void SpriteBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var key = (sender as FrameworkElement)?.DataContext as SPRKeyVM;
            var itemsControl = FindParent<ItemsControl>(sender as DependencyObject);
            var texture = itemsControl?.DataContext as SPRTextureVM;
            if (key == null || itemsControl == null || texture == null)
                return;

            texture.SelectedItem = key;
            draggedKey = key;
            dragStartTexturePoint = GetTexturePoint(e.GetPosition(itemsControl), itemsControl, texture.Rect);
            dragStartX1 = key.X1;
            dragStartX2 = key.X2;
            dragStartY1 = key.Y1;
            dragStartY2 = key.Y2;

            itemsControl.CaptureMouse();
            e.Handled = true;
        }

        private void ItemsControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            EndDrag(sender as FrameworkElement);
        }

        private void ItemsControl_LostMouseCapture(object sender, MouseEventArgs e)
        {
            draggedKey = null;
        }

        private static Point GetTexturePoint(Point controlPoint, FrameworkElement control, Rect textureRect)
        {
            if (control.ActualWidth == 0 || control.ActualHeight == 0)
                return new Point();

            return new Point(
                (controlPoint.X / control.ActualWidth) * textureRect.Width,
                (controlPoint.Y / control.ActualHeight) * textureRect.Height);
        }

        private void EndDrag(FrameworkElement control)
        {
            draggedKey = null;
            control?.ReleaseMouseCapture();
        }

        private static T FindParent<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T parent)
                    return parent;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
