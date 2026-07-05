using K4os.Compression.LZ4;
using PersonaEditorLib.Other;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PersonaEditorLib.SpriteContainer
{
    public class MetaphorAPK : IGameData
    {
        private const uint Magic = 0x4B434150;
        private const uint ZzzMagic = 0x305A5A5A;
        private const int ZzzHeaderSize = 0x30;

        private readonly List<Entry> entries = new List<Entry>();

        public MetaphorAPK(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            Read(reader, data);
            BuildSubFiles();
        }

        public FormatEnum Type => FormatEnum.MetaphorAPK;
        public List<GameFile> SubFiles { get; } = new List<GameFile>();

        public int GetSize() => GetData().Length;

        public byte[] GetData()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            Write(writer);
            return ms.ToArray();
        }

        private void Read(BinaryReader reader, byte[] raw)
        {
            if (reader.ReadUInt32() != Magic)
                throw new InvalidDataException("MetaphorAPK: wrong magic.");

            reader.ReadUInt32();
            int count = reader.ReadInt32();
            reader.ReadInt32();

            entries.Clear();
            for (int i = 0; i < count; i++)
            {
                var e = new Entry();
                byte[] nameBytes = reader.ReadBytes(0x100);
                e.Name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                e.CompressedSize = reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                e.Offset = reader.ReadInt32();
                reader.ReadInt32();
                entries.Add(e);
            }

            foreach (var e in entries)
            {
                byte[] block = new byte[e.CompressedSize];
                Buffer.BlockCopy(raw, e.Offset, block, 0, e.CompressedSize);
                e.DecompressedData = DecompressBlock(block);
            }
        }

        private void Write(BinaryWriter writer)
        {
            var compressed = new List<(string name, byte[] block)>();
            foreach (var e in entries)
                compressed.Add((e.Name, CompressBlock(e.DecompressedData)));

            writer.Write(Magic);
            writer.Write(0x10000u);
            writer.Write(compressed.Count);
            writer.Write(0);

            long headerTableStart = writer.BaseStream.Position;
            foreach (var _ in compressed)
                writer.Write(new byte[0x120]);

            var pointers = new int[compressed.Count];
            for (int i = 0; i < compressed.Count; i++)
            {
                pointers[i] = (int)writer.BaseStream.Position;
                writer.Write(compressed[i].block);
            }

            writer.BaseStream.Position = headerTableStart;
            for (int i = 0; i < compressed.Count; i++)
            {
                byte[] nameBytes = new byte[0x100];
                byte[] src = Encoding.ASCII.GetBytes(compressed[i].name);
                Buffer.BlockCopy(src, 0, nameBytes, 0, Math.Min(src.Length, 0xFF));
                writer.Write(nameBytes);
                writer.Write(compressed[i].block.Length);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(pointers[i]);
                writer.Write(0);
            }
        }

        private static byte[] DecompressBlock(byte[] block)
        {
            if (block.Length < ZzzHeaderSize)
                throw new InvalidDataException("MetaphorAPK: block too small.");
            if (BitConverter.ToUInt32(block, 0) != ZzzMagic)
                throw new InvalidDataException("MetaphorAPK: block missing ZZZ0 magic.");

            int decompSize = BitConverter.ToInt32(block, 0x0C);
            int compSize = BitConverter.ToInt32(block, 0x20);

            byte[] src = new byte[compSize];
            Buffer.BlockCopy(block, ZzzHeaderSize, src, 0, compSize);

            byte[] dst = new byte[decompSize];
            int written = LZ4Codec.Decode(src, 0, src.Length, dst, 0, dst.Length);
            if (written != decompSize)
                throw new InvalidDataException("MetaphorAPK: LZ4 decompression size mismatch.");

            return dst;
        }

        private static byte[] CompressBlock(byte[] data)
        {
            byte[] comp = new byte[LZ4Codec.MaximumOutputSize(data.Length)];
            int compLen = LZ4Codec.Encode(data, 0, data.Length, comp, 0, comp.Length, LZ4Level.L09_HC);

            int paddedLen = compLen + ((0x10 - (compLen % 0x10)) % 0x10);
            byte[] block = new byte[ZzzHeaderSize + paddedLen];

            BitConverter.GetBytes(ZzzMagic).CopyTo(block, 0x00);
            BitConverter.GetBytes(0x010001).CopyTo(block, 0x04);
            BitConverter.GetBytes(data.Length).CopyTo(block, 0x0C);
            BitConverter.GetBytes(compLen + ZzzHeaderSize).CopyTo(block, 0x10);
            BitConverter.GetBytes(compLen).CopyTo(block, 0x20);
            BitConverter.GetBytes(ZzzHeaderSize).CopyTo(block, 0x24);
            Buffer.BlockCopy(comp, 0, block, ZzzHeaderSize, compLen);

            return block;
        }

        private void BuildSubFiles()
        {
            SubFiles.Clear();
            for (int i = 0; i < entries.Count; i++)
                SubFiles.Add(new GameFile(entries[i].Name, new EntryProxy(this, i)));
        }

        private class Entry
        {
            public string Name;
            public int CompressedSize;
            public int Offset;
            public byte[] DecompressedData;
        }

        private class EntryProxy : IGameData
        {
            private readonly MetaphorAPK owner;
            private readonly int index;

            public EntryProxy(MetaphorAPK owner, int index)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
                this.index = index;
            }

            public FormatEnum Type => FormatEnum.DDS;
            public List<GameFile> SubFiles { get; } = new List<GameFile>();
            public int GetSize() => owner.entries[index].DecompressedData?.Length ?? 0;
            public byte[] GetData() => owner.entries[index].DecompressedData ?? Array.Empty<byte>();
        }
    }
}
