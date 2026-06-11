using AuxiliaryLibraries.WPF;
using PersonaEditor.Classes;
using PersonaEditorLib.Text;
using System.Collections.ObjectModel;

namespace PersonaEditor.ViewModels.Editors
{
    class ATFEditorVM : BindingObject, IEditor
    {
        public ObservableCollection<ATFEntryVM> Entries { get; } = new ObservableCollection<ATFEntryVM>();

        public ATFEditorVM(ATF atf)
        {
            if (atf == null)
                throw new System.ArgumentNullException(nameof(atf));

            for (int i = 0; i < atf.Entries.Count; i++)
                Entries.Add(new ATFEntryVM(i, atf.Entries[i]));
        }

        public bool Close() => true;
    }

    class ATFEntryVM : BindingObject
    {
        private readonly ATF.ATFEntry entry;
        private string newText;

        public int Index { get; }
        public string OldText => entry.OldText;

        public string NewText
        {
            get { return newText; }
            set
            {
                if (newText != value)
                {
                    newText = value;
                    entry.NewText = value;
                    Notify("NewText");
                }
            }
        }

        public ATFEntryVM(int index, ATF.ATFEntry entry)
        {
            this.entry = entry;
            Index = index;
            newText = entry.NewText;
        }
    }
}
