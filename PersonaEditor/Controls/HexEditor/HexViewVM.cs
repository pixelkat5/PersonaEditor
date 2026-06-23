using AuxiliaryLibraries.Extensions;
using AuxiliaryLibraries.WPF;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;

namespace PersonaEditor.Controls.HexEditor
{
    class HexViewByteVM : BindingObject
    {
        public string Byte { get; set; } = "..";

        public void SetByte(byte Byte)
        {
            this.Byte = String.Format("{0:X2}", Byte);
            Notify("Byte");
        }

        public HexViewByteVM()
        {

        }

        internal void Reset()
        {
            Byte = "..";
            Notify("Byte");
        }
    }

    class HexViewUIntVM : BindingObject
    {
        public ObservableCollection<HexViewByteVM> Bytes { get; } = new ObservableCollection<HexViewByteVM>();

        public void SetBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length <= 4)
                for (int i = 0; i < bytes.Length; i++)
                    Bytes[i].SetByte(bytes[i]);
        }

        public HexViewUIntVM()
        {
            for (int i = 0; i < 4; i++)
                Bytes.Add(new HexViewByteVM());
        }

        internal void Reset()
        {
            foreach (var a in Bytes)
                a.Reset();
        }
    }

    class HexViewLineVM : BindingObject
    {
        private string offset = "0";
        public string Offset => offset;

        private string asText = "................";
        public string AsText => asText;

        public void SetOffset(long offset)
        {
            this.offset = String.Format("0x{0:X8}", offset);
            Notify("Offset");
        }

        public void SetBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length <= 16)
            {
                for (int i = 0; i < UInts.Count; i++)
                {
                    int offset = i * 4;
                    if (offset < bytes.Length)
                        UInts[i].SetBytes(bytes.Slice(offset, Math.Min(4, bytes.Length - offset)));
                }

                Encode(bytes);
            }
        }

        public ObservableCollection<HexViewUIntVM> UInts { get; } = new ObservableCollection<HexViewUIntVM>();

        public HexViewLineVM()
        {
            for (int i = 0; i < 4; i++)
                UInts.Add(new HexViewUIntVM());
        }

        private void Encode(ReadOnlySpan<byte> bytes)
        {
            char[] chars = new char[16];
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = i < bytes.Length && bytes[i] >= 0x20 && bytes[i] <= 0x7E ? (char)bytes[i] : '.';
            }

            asText = new string(chars);
            Notify("AsText");
        }

        internal void Reset()
        {
            asText = "................";
            Notify("AsText");
            foreach (var a in UInts)
                a.Reset();
        }
    }

    class HexViewVM : BindingObject
    {
        #region Private

        private Stream stream;

        private double ViewHeight = 0;
        private double TableHeight = 0;

        private long startOffset = 0;

        private double sizeColumnWidth = 80;

        #endregion Private

        #region Public

        public ObservableCollection<HexViewLineVM> Lines { get; } = new ObservableCollection<HexViewLineVM>();

        public double SizeColumnWidth => sizeColumnWidth;

        public double ActualHeight
        {
            set
            {
                TableHeight = value;
                CompareHeight();
            }
        }

        public FontFamily FontFamily { get; } = new FontFamily(System.Drawing.FontFamily.GenericMonospace.Name);

        #endregion Public

        private void UpdateLines()
        {
            for (int i = 0; i < Lines.Count; i++)
                SetLine(Lines[i], i);
        }

        private void SetLine(HexViewLineVM line, int index)
        {
            long newoffset = startOffset + index * 16;
            line.SetOffset(newoffset);
            line.Reset();
            if (this.stream is Stream stream && stream.Length > newoffset)
            {
                stream.Position = newoffset;
                long available = stream.Length - stream.Position;
                if (available >= 16)
                {
                    Span<byte> temp = stackalloc byte[16];
                    stream.ReadExactly(temp);
                    line.SetBytes(temp);
                }
                else
                {
                    Span<byte> temp = stackalloc byte[(int)available];
                    stream.ReadExactly(temp);
                    line.SetBytes(temp);
                }
            }
        }

        private void AddLine()
        {
            var line = new HexViewLineVM();
            SetLine(line, Lines.Count);
            Lines.Add(line);
        }

        private void RemoveLine()
        {
            Lines.RemoveAt(Lines.Count - 1);
        }

        private void CompareHeight()
        {
            // double h = Lines.Count == 0 ? 0 : TableHeight / Lines.Count;

            if (TableHeight < ViewHeight)
                AddLine();
            //else
            //{
            //    if (TableHeight - 2 * h > ViewHeight)
            //        RemoveLine();
            //}
        }

        public void SetHeight(double height)
        {
            ViewHeight = height;
            CompareHeight();
        }

        public void SetStartOffset(long offset)
        {
            if (startOffset != offset)
            {
                startOffset = offset;
                UpdateLines();
            }
        }

        public void SetStream(Stream stream)
        {
            this.stream = stream;
            UpdateLines();
        }

        public HexViewVM()
        {
        }
    }
}
