using Microsoft.Win32;
using System.IO;
using System.Windows.Input;
using System.ComponentModel;
using AuxiliaryLibraries.WPF;
using PersonaEditor.ViewModels;
using PersonaEditor.View.Settings;
using PersonaEditor.ViewModels.Settings;

namespace PersonaEditor.Views
{
    class MainWindowVM : BindingObject
    {
        Views.Tools.Visualizer visualizer;
        Views.Tools.SetChar setchar;
        private bool batchRunning;

        public MultiFileEditVM MultiFile { get; } = new MultiFileEditVM();

        private object _MainControlDC = null;
        public object MainControlDC
        {
            get { return _MainControlDC; }
            set
            {
                _MainControlDC = value;
                Notify("MainControlDC");
            }
        }

        #region Events

        public ICommand WindowClosing { get; }
        private void Window_Closing(object arg)
        {
            if (MultiFile.CloseFile())
            {
                if (visualizer != null)
                    visualizer.Close();
                if (setchar != null)
                    setchar.Close();

                ApplicationSettings.AppSetting.Default.Save();
                ApplicationSettings.BackgroundDefault.Default.Save();
                ApplicationSettings.SPREditor.Default.Save();
                ApplicationSettings.WindowSetting.Default.Save();
            }
            else
                (arg as CancelEventArgs).Cancel = true;
        }

        public ICommand clickOpenFile { get; }
        private void OpenFile()
        {
            OpenFileDialog OFD = new OpenFileDialog();
            if (OFD.ShowDialog() == true)
                MultiFile.OpenFile(OFD.FileName);
        }

        public ICommand clickSaveAsFile { get; }
        private void SaveAsFile()
        {
            if (MultiFile.OpenFileName != "")
            {
                SaveFileDialog SFD = new SaveFileDialog();
                SFD.OverwritePrompt = true;

                string dirpath = Path.GetDirectoryName(MultiFile.OpenFileName);
                string filename = Path.GetFileName(MultiFile.OpenFileName);
                if (Directory.Exists(dirpath))
                    SFD.InitialDirectory = dirpath;
                SFD.FileName = filename;

                string ext = Path.GetExtension(MultiFile.OpenFileName).Remove(0, 1);
                SFD.Filter = ext.ToUpper() + "|*." + ext;

                if (SFD.ShowDialog() == true)
                    MultiFile.SaveFile(SFD.FileName);
            }

        }

        public ICommand clickBatchExportImage { get; }
        private void BatchExportImage()
        {
            string source = SelectFolder("Select source folder");
            if (source == null)
                return;

            string output = SelectFolder("Select output folder (Cancel to export beside source files)");
            RunBatch("Batch Image Export", () => PersonaEditor.Classes.BatchProcessor.ExportImages(source, output));
        }

        public ICommand clickBatchImportImage { get; }
        private void BatchImportImage()
        {
            string source = SelectFolder("Select original folder");
            if (source == null)
                return;

            string input = SelectFolder("Select modified image folder (Cancel to use original folder)");
            RunBatch("Batch Image Import", () => PersonaEditor.Classes.BatchProcessor.ImportImages(source, input ?? source), true);
        }

        public ICommand clickBatchExportText { get; }
        private void BatchExportText()
        {
            string source = SelectFolder("Select source folder");
            if (source == null)
                return;

            string output = SelectTextSavePath("Select output TXT file (Cancel to export beside source files)");
            RunBatch("Batch Text Export", () => PersonaEditor.Classes.BatchProcessor.ExportText(source, output));
        }

        public ICommand clickBatchImportText { get; }
        private void BatchImportText()
        {
            string source = SelectFolder("Select original folder");
            if (source == null)
                return;

            string input = SelectTextOpenPath("Select translated TXT file (Cancel to use TXT files beside source files)");
            RunBatch("Batch Text Import", () => PersonaEditor.Classes.BatchProcessor.ImportText(source, input), true);
        }

        public ICommand clickVisualizerOpen { get; }
        private void ToolVisualizerOpen()
        {
            if (visualizer != null)
                if (visualizer.IsLoaded)
                {
                    visualizer.Activate();
                    return;
                }

            visualizer = new Views.Tools.Visualizer() { DataContext = new ViewModels.Tools.VisualizerVM() };
            visualizer.Show();
        }

        public ICommand clickSetCharOpen { get; }
        private void ToolSetCharOpen()
        {
            if (setchar != null)
                if (setchar.IsLoaded)
                {
                    setchar.Activate();
                    return;
                }

            setchar = new Views.Tools.SetChar() { DataContext = new ViewModels.Tools.SetCharVM() };
            setchar.Show();
        }

        public ICommand clickSettingOpen { get; }
        private void SettingOpen()
        {
            ApplicationSettings.AppSetting.Default.Save();
            ApplicationSettings.BackgroundDefault.Default.Save();
            ApplicationSettings.SPREditor.Default.Save();
            ApplicationSettings.WindowSetting.Default.Save();
            SetSettings setSettings = new SetSettings() { DataContext = new SetSettingsVM() };
            setSettings.ShowDialog();
            Static.BackManager.EmptyUpdate();
        }

        public ICommand clickAboutOpen { get; }
        private void AboutOpen()
        {
            (new About()).ShowDialog();
        }

        private string SelectFolder(string title)
        {
            var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = title
            };

            return dialog.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok ? dialog.FileName : null;
        }

        private string SelectTextSavePath(string title)
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = "Output.txt",
                OverwritePrompt = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private string SelectTextOpenPath(string title)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                CheckFileExists = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private async void RunBatch(string title, System.Func<PersonaEditor.Classes.BatchResult> action, bool import = false)
        {
            if (batchRunning)
                return;

            try
            {
                batchRunning = true;
                Mouse.OverrideCursor = Cursors.Wait;
                var result = await System.Threading.Tasks.Task.Run(action);
                string count = import ? $"Imported: {result.Imported}" : $"Exported: {result.Exported}";
                string saved = import ? "\nModified source files were overwritten." : "";
                System.Windows.MessageBox.Show($"{count}\nFailed: {result.Failed}{saved}", title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                batchRunning = false;
                Mouse.OverrideCursor = null;
            }
        }

        #endregion Events

        public ICommand clickTest { get; }
        private void TestClick()
        {          
        }

        public void OpenFile(string path)
        {
            if (File.Exists(path))
                MultiFile.OpenFile(path);
        }

        public MainWindowVM()
        {
            WindowClosing = new RelayCommand(Window_Closing);
            clickOpenFile = new RelayCommand(OpenFile);
            clickSaveAsFile = new RelayCommand(SaveAsFile);
            clickBatchExportImage = new RelayCommand(BatchExportImage);
            clickBatchImportImage = new RelayCommand(BatchImportImage);
            clickBatchExportText = new RelayCommand(BatchExportText);
            clickBatchImportText = new RelayCommand(BatchImportText);
            clickSettingOpen = new RelayCommand(SettingOpen);
            clickVisualizerOpen = new RelayCommand(ToolVisualizerOpen);
            clickSetCharOpen = new RelayCommand(ToolSetCharOpen);

            clickAboutOpen = new RelayCommand(AboutOpen);

            clickTest = new RelayCommand(TestClick);
        }
    }
}
