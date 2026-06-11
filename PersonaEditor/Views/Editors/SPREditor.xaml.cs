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
        private string resizeDirection;
        private const double ResizeGripSize = 6;
        private const double MinZoom = 0.25;
        private const double MaxZoom = 16;
        private double viewerZoom = 1;
        private bool isViewerPanning;
        private Point viewerPanStart;
        private double viewerPanStartX;
        private double viewerPanStartY;

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

        private void SpriteViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
                return;

            var oldZoom = viewerZoom;
            viewerZoom = Math.Max(MinZoom, Math.Min(MaxZoom, viewerZoom + (e.Delta > 0 ? 0.25 : -0.25)));
            if (Math.Abs(oldZoom - viewerZoom) < double.Epsilon)
                return;

            var position = e.GetPosition(SpriteViewer);
            var horizontalRatio = SpriteViewer.ScrollableWidth <= 0 ? 0 : (SpriteViewer.HorizontalOffset + position.X) / oldZoom;
            var verticalRatio = SpriteViewer.ScrollableHeight <= 0 ? 0 : (SpriteViewer.VerticalOffset + position.Y) / oldZoom;

            SpriteViewerScale.ScaleX = viewerZoom;
            SpriteViewerScale.ScaleY = viewerZoom;
            SpriteViewer.UpdateLayout();
            SpriteViewer.ScrollToHorizontalOffset(horizontalRatio * viewerZoom - position.X);
            SpriteViewer.ScrollToVerticalOffset(verticalRatio * viewerZoom - position.Y);

            e.Handled = true;
        }

        private void SpriteViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle)
                return;

            isViewerPanning = true;
            viewerPanStart = e.GetPosition(SpriteViewer);
            viewerPanStartX = SpriteViewer.HorizontalOffset;
            viewerPanStartY = SpriteViewer.VerticalOffset;
            SpriteViewer.Cursor = Cursors.ScrollAll;
            SpriteViewer.CaptureMouse();
            e.Handled = true;
        }

        private void SpriteViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!isViewerPanning)
                return;

            var position = e.GetPosition(SpriteViewer);
            SpriteViewer.ScrollToHorizontalOffset(viewerPanStartX + viewerPanStart.X - position.X);
            SpriteViewer.ScrollToVerticalOffset(viewerPanStartY + viewerPanStart.Y - position.Y);
            e.Handled = true;
        }

        private void SpriteViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                EndViewerPan();
                e.Handled = true;
            }
        }

        private void SpriteViewer_LostMouseCapture(object sender, MouseEventArgs e)
        {
            EndViewerPan();
        }

        private void EndViewerPan()
        {
            if (!isViewerPanning)
                return;

            isViewerPanning = false;
            SpriteViewer.Cursor = null;
            if (SpriteViewer.IsMouseCaptured)
                SpriteViewer.ReleaseMouseCapture();
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

                if (resizeDirection == null)
                {
                    key.X1 = dragStartX1 + deltaX;
                    key.X2 = dragStartX2 + deltaX;
                    key.Y1 = dragStartY1 + deltaY;
                    key.Y2 = dragStartY2 + deltaY;
                }
                else
                {
                    ResizeSprite(key, deltaX, deltaY);
                }

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
            resizeDirection = GetResizeDirection(e.GetPosition(sender as IInputElement), sender as FrameworkElement);
            draggedKey = key;
            dragStartTexturePoint = GetTexturePoint(e.GetPosition(itemsControl), itemsControl, texture.Rect);
            dragStartX1 = key.X1;
            dragStartX2 = key.X2;
            dragStartY1 = key.Y1;
            dragStartY2 = key.Y2;

            itemsControl.CaptureMouse();
            e.Handled = true;
        }

        private void SpriteBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedKey != null)
                return;

            if (sender is FrameworkElement element)
                element.Cursor = GetCursor(GetResizeDirection(e.GetPosition(element), element));
        }

        private void SpriteBox_MouseLeave(object sender, MouseEventArgs e)
        {
            if (draggedKey == null && sender is FrameworkElement element)
                element.Cursor = Cursors.SizeAll;
        }

        private void ItemsControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            EndDrag(sender as FrameworkElement);
        }

        private void ItemsControl_LostMouseCapture(object sender, MouseEventArgs e)
        {
            draggedKey = null;
            resizeDirection = null;
        }

        private void ResizeSprite(SPRKeyVM key, int deltaX, int deltaY)
        {
            var x1 = dragStartX1;
            var x2 = dragStartX2;
            var y1 = dragStartY1;
            var y2 = dragStartY2;

            if (resizeDirection.Contains("W"))
                x1 = Math.Min(dragStartX1 + deltaX, x2 - 1);
            if (resizeDirection.Contains("E"))
                x2 = Math.Max(dragStartX2 + deltaX, x1 + 1);
            if (resizeDirection.Contains("N"))
                y1 = Math.Min(dragStartY1 + deltaY, y2 - 1);
            if (resizeDirection.Contains("S"))
                y2 = Math.Max(dragStartY2 + deltaY, y1 + 1);

            key.X1 = x1;
            key.X2 = x2;
            key.Y1 = y1;
            key.Y2 = y2;
        }

        private static string GetResizeDirection(Point point, FrameworkElement element)
        {
            if (element == null)
                return null;

            var west = point.X <= ResizeGripSize;
            var east = point.X >= element.ActualWidth - ResizeGripSize;
            var north = point.Y <= ResizeGripSize;
            var south = point.Y >= element.ActualHeight - ResizeGripSize;

            if (north && west)
                return "NW";
            if (north && east)
                return "NE";
            if (south && west)
                return "SW";
            if (south && east)
                return "SE";
            if (north)
                return "N";
            if (south)
                return "S";
            if (west)
                return "W";
            if (east)
                return "E";

            return null;
        }

        private static Cursor GetCursor(string direction)
        {
            switch (direction)
            {
                case "N":
                case "S":
                    return Cursors.SizeNS;
                case "E":
                case "W":
                    return Cursors.SizeWE;
                case "NE":
                case "SW":
                    return Cursors.SizeNESW;
                case "NW":
                case "SE":
                    return Cursors.SizeNWSE;
                default:
                    return Cursors.SizeAll;
            }
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
            resizeDirection = null;
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
