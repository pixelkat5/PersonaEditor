using AuxiliaryLibraries.WPF;
using AuxiliaryLibraries.WPF.Wrapper;
using PersonaEditor.Classes;
using PersonaEditorLib.Other;
using PersonaEditorLib.Sprite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PersonaEditor.ViewModels.Editors
{
    class HIPFontGlyphVM : BindingObject
    {
        private readonly ABCGlyph glyph;
        private readonly BitmapSource atlas;
        private readonly List<int> codePoints;
        private BitmapSource image;

        public HIPFontGlyphVM(IEnumerable<int> codePoints, ABCGlyph glyph, BitmapSource atlas)
        {
            this.codePoints = codePoints.OrderBy(x => x).ToList();
            this.glyph = glyph;
            this.atlas = atlas;
            RefreshMappings();
        }

        public string Characters { get; private set; }
        public string CodePoints { get; private set; }
        public int Index => glyph.Index;
        public BitmapSource Image => image ??= Crop(atlas, glyph);
        public int Left { get => glyph.Left; set { glyph.Left = value; Notify(nameof(Left)); } }
        public int Width { get => glyph.Width; set { glyph.Width = value; Notify(nameof(Width)); } }
        public int Advance { get => glyph.Advance; set { glyph.Advance = value; Notify(nameof(Advance)); } }
        public int Top { get => glyph.Top; set { glyph.Top = value; Notify(nameof(Top)); } }
        public int X { get => glyph.X; set { glyph.X = value; Notify(nameof(X)); InvalidateImage(); } }
        public int Y { get => glyph.Y; set { glyph.Y = value; Notify(nameof(Y)); InvalidateImage(); } }
        public int Right { get => glyph.Right; set { glyph.Right = value; Notify(nameof(Right)); InvalidateImage(); } }
        public int Bottom { get => glyph.Bottom; set { glyph.Bottom = value; Notify(nameof(Bottom)); InvalidateImage(); } }
        public ABCGlyph Glyph => glyph;

        public bool HasCodePoint(int codePoint) => codePoints.Contains(codePoint);

        public void AddMapping(int codePoint, bool clearExisting)
        {
            if (clearExisting)
                codePoints.Clear();

            if (!codePoints.Contains(codePoint))
                codePoints.Add(codePoint);

            codePoints.Sort();
            RefreshMappings();
        }

        public void RemoveMapping(int codePoint)
        {
            if (codePoints.Remove(codePoint))
                RefreshMappings();
        }

        private void InvalidateImage()
        {
            image = null;
            Notify(nameof(Image));
        }

        private void RefreshMappings()
        {
            Characters = string.Concat(codePoints.Select(GetDisplayCharacter));
            CodePoints = string.Join(", ", codePoints.Select(x => $"U+{x:X4}"));
            Notify(nameof(Characters));
            Notify(nameof(CodePoints));
        }

        private static string GetDisplayCharacter(int codePoint)
        {
            if (codePoint < 0x20 || codePoint == 0x7F)
                return $"U+{codePoint:X4}";

            string value = char.ConvertFromUtf32(codePoint);
            return string.IsNullOrWhiteSpace(value) ? " " : value;
        }


        private static BitmapSource Crop(BitmapSource atlas, ABCGlyph glyph)
        {
            int x = Math.Clamp(glyph.X, 0, atlas.PixelWidth - 1);
            int y = Math.Clamp(glyph.Y, 0, atlas.PixelHeight - 1);
            int width = Math.Clamp(glyph.Right - x, 1, atlas.PixelWidth - x);
            int height = Math.Clamp(glyph.Bottom - y, 1, atlas.PixelHeight - y);
            var cropped = new CroppedBitmap(atlas, new Int32Rect(x, y, width, height));
            cropped.Freeze();
            return cropped;
        }
    }

    class HIPFontEditorVM : BindingObject, IEditor
    {
        private readonly HIP hip;
        private readonly List<MappingEdit> mappingEdits = new List<MappingEdit>();
        private bool edited;

        public HIPFontEditorVM(HIP hip)
        {
            this.hip = hip ?? throw new ArgumentNullException(nameof(hip));
            BitmapSource atlas = hip.GetBitmap().GetBitmapSource();
            foreach (var group in hip.Font.GetMappedGlyphs().GroupBy(x => x.Glyph.Index))
            {
                Glyphs.Add(new HIPFontGlyphVM(group.Select(x => (int)x.Character), group.First().Glyph, atlas));
            }
        }

        public ObservableCollection<HIPFontGlyphVM> Glyphs { get; } = new ObservableCollection<HIPFontGlyphVM>();

        public void MarkEdited() => edited = true;

        public void ApplyMapping(HIPFontGlyphVM glyph, string target, bool clearSelectedMappings)
        {
            if (glyph == null)
            {
                MessageBox.Show("Select a glyph first.", "ABC mapping", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int codePoint = ParseCodePoint(target);
                if (codePoint > hip.Font.MaxCodePoint)
                    throw new ArgumentOutOfRangeException(nameof(target), $"U+{codePoint:X4} is outside this ABC lookup table.");

                foreach (var item in Glyphs)
                    if (item != glyph)
                        item.RemoveMapping(codePoint);

                glyph.AddMapping(codePoint, clearSelectedMappings);
                mappingEdits.Add(new MappingEdit(codePoint, glyph.Index, clearSelectedMappings));
                edited = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ABC mapping", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool Close()
        {
            if (!edited)
                return true;

            var result = MessageBox.Show("Save ABC font changes?", "Saving", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel)
                return false;
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    foreach (var edit in mappingEdits)
                        hip.Font.SetGlyphMapping(edit.CodePoint, edit.GlyphIndex, edit.ClearExistingMappings);

                    foreach (var glyph in Glyphs)
                        hip.Font.SetGlyph(glyph.Glyph);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Invalid glyph", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            return true;
        }

        private static int ParseCodePoint(string value)
        {
            value = value?.Trim() ?? "";
            if (value.Length == 0)
                throw new FormatException("Enter a character or codepoint.");

            if (value.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
                return int.Parse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.Parse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            if (value.Length == 1 || char.IsSurrogatePair(value, 0))
                return char.ConvertToUtf32(value, 0);

            return int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private readonly struct MappingEdit
        {
            public MappingEdit(int codePoint, int glyphIndex, bool clearExistingMappings)
            {
                CodePoint = codePoint;
                GlyphIndex = glyphIndex;
                ClearExistingMappings = clearExistingMappings;
            }

            public int CodePoint { get; }
            public int GlyphIndex { get; }
            public bool ClearExistingMappings { get; }
        }

    }
}
