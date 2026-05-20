using System.Collections.ObjectModel;
using AuxiliaryLibraries.WPF;
using PersonaEditorLib.SpriteContainer;
using PersonaEditor.Classes;

namespace PersonaEditor.ViewModels.Editors
{
    class SPREditorVM : BindingObject, IEditor
    {
        public ObservableCollection<SPRTextureVM> TextureList { get; } = new ObservableCollection<SPRTextureVM>();

        public SPREditorVM(SPR spr)
        {
            if (spr == null)
                throw new System.ArgumentNullException(nameof(spr));

            for (int i = 0; i < spr.SubFiles.Count; i++)
                TextureList.Add(new SPRTextureVM(spr.SubFiles[i], spr.KeyList.List, i));
        }

        public SPREditorVM(SPR3 spr3)
        {
            if (spr3 == null)
                throw new System.ArgumentNullException(nameof(spr3));

            for (int i = 0; i < spr3.SubFiles.Count; i++)
                TextureList.Add(new SPRTextureVM(spr3.SubFiles[i], spr3.KeyList, i));
        }

        public SPREditorVM(SPR6 spr6)
        {
            if (spr6 == null)
                throw new System.ArgumentNullException(nameof(spr6));

            for (int i = 0; i < spr6.SubFiles.Count; i++)
                TextureList.Add(new SPRTextureVM(spr6.SubFiles[i], spr6.KeyList, i));
        }

        public bool Close()
        {
            return true;
        }
    }
}