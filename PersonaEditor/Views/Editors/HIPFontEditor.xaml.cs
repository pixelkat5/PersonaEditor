using PersonaEditor.ViewModels.Editors;
using System.Windows;
using System.Windows.Controls;

namespace PersonaEditor.Views.Editors
{
    public partial class HIPFontEditor : UserControl
    {
        public HIPFontEditor()
        {
            InitializeComponent();
        }

        private void FieldChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is HIPFontEditorVM vm && IsLoaded)
                vm.MarkEdited();
        }

        private void ApplyMapping_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is HIPFontEditorVM vm)
                vm.ApplyMapping(GlyphList.SelectedItem as HIPFontGlyphVM, MappingTarget.Text, ClearSelectedMappings.IsChecked == true);
        }

    }
}
