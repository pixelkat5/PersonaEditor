using AuxiliaryLibraries.Media;
using AuxiliaryLibraries.Tools;
using PersonaEditorLib.Sprite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace PersonaEditorLib.SpriteContainer
{
    public class SPR3 : IGameData
    {
        private SPR3Header header;
        private readonly List<SPR3EntryHeader> entries = new List<SPR3EntryHeader>();
        private readonly List<SPRKey> keys = new List<SPRKey>();
        private readonly CTPK ctpk;

        public SPR3(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = IOTools.OpenReadFile(ms, true))
            {
                header = IOTools.FromBytes<SPR3Header>(reader.ReadBytes(Marshal.SizeOf(typeof(SPR3Header))));
                reader.ReadUInt32(); // reserved
                var dataOffset = reader.ReadUInt32();

                for (int i = 0; i < header.EntryCount; i++)
                    entries.Add(IOTools.FromBytes<SPR3EntryHeader>(reader.ReadBytes(Marshal.SizeOf(typeof(SPR3EntryHeader)))));

                for (int i = 0; i < header.EntryCount; i++)
                    keys.Add(new SPRKey(reader.ReadBytes(0x80), i));

                reader.BaseStream.Position = dataOffset;
                ctpk = new CTPK(reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position)));
            }

            for (int i = 0; i < ctpk.TextureCount; i++)
            {
                var name = ctpk.GetTextureName(i);
                if (string.IsNullOrWhiteSpace(name))
                    name = $"texture_{i:D3}";
                if (!name.EndsWith(".ctpktex", StringComparison.OrdinalIgnoreCase))
                    name += ".ctpktex";
                SubFiles.Add(new GameFile(name, new CTPKTextureProxy(ctpk, i)));
            }
        }

        public List<SPRKey> KeyList => keys;

        public FormatEnum Type => FormatEnum.SPR3;

        public List<GameFile> SubFiles { get; } = new List<GameFile>();

        public int GetSize() => GetData().Length;

        public byte[] GetData()
        {
            using (var ms = new MemoryStream())
            using (var writer = IOTools.OpenWriteFile(ms, true))
            {
                header.EntryCount = (ushort)keys.Count;
                writer.Write(IOTools.GetBytes(header));
                writer.Write(0);

                int dataOffset = 0x28 + entries.Count * Marshal.SizeOf(typeof(SPR3EntryHeader)) + keys.Count * 0x80;
                writer.Write(dataOffset);

                foreach (var entry in entries)
                    writer.Write(IOTools.GetBytes(entry));

                foreach (var key in keys)
                {
                    using (var keyStream = new MemoryStream())
                    using (var keyWriter = IOTools.OpenWriteFile(keyStream, true))
                    {
                        key.Get(keyWriter);
                        var keyBytes = keyStream.ToArray();
                        if (keyBytes.Length != 0x80)
                            throw new Exception("SPR3: invalid key size");
                        writer.Write(keyBytes);
                    }
                }

                writer.Write(ctpk.GetData());
                return ms.ToArray();
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SPR3Header
        {
            public uint Const1;
            public uint Const2;
            public uint Magic;
            public uint HeaderSize;
            public uint Unknown1;
            public ushort Unknown2;
            public ushort EntryCount;
            public uint DataValueOffset;
            public uint EntryOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SPR3EntryHeader
        {
            public uint Zero1;
            public uint EntryOffset;
        }

        private class CTPKTextureProxy : IGameData, IImage
        {
            private readonly CTPK owner;
            private readonly int index;

            public CTPKTextureProxy(CTPK owner, int index)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
                this.index = index;
            }

            public FormatEnum Type => FormatEnum.CTPK;

            public List<GameFile> SubFiles { get; } = new List<GameFile>();

            public int GetSize() => owner.GetSize();

            public byte[] GetData() => owner.GetData();

            public Bitmap GetBitmap() => owner.GetBitmap(index);

            public void SetBitmap(Bitmap bitmap) => owner.SetBitmap(index, bitmap);
        }
    }
}
