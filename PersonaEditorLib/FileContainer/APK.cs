using PersonaEditorLib.Other;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PersonaEditorLib.FileContainer
{
    public class APK : IGameData
    {
        private const int Version = 0x00020000;
        private const int HeaderSize = 0x10;
        private const int EntrySize = 0xB0;
        private const int NameSize = 0x80;
        private const int Alignment = 0x10;
        private const int DefaultBlockSize = 0x10000;
        private const int Zzz1Version = 1;
        private static readonly byte[] Zzz1Magic = Encoding.ASCII.GetBytes("ZZZ1");

        private readonly List<Entry> entries = new List<Entry>();
        private readonly int headerUnknown;

        public APK(byte[] data)
        {
            if (data == null || data.Length < HeaderSize)
                throw new ArgumentException("APK data is too small.", nameof(data));

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream, Encoding.ASCII))
            {
                int version = reader.ReadInt32();
                int count = reader.ReadInt32();
                int tableOffset = reader.ReadInt32();
                headerUnknown = reader.ReadInt32();

                if (version != Version)
                    throw new InvalidDataException($"APK: unsupported version 0x{version:X8}.");
                if (count < 0 || tableOffset < HeaderSize || tableOffset + count * EntrySize > data.Length)
                    throw new InvalidDataException("APK: invalid entry table.");

                for (int i = 0; i < count; i++)
                {
                    stream.Position = tableOffset + i * EntrySize;
                    string name = ReadName(reader.ReadBytes(NameSize));
                    ulong storedSize = reader.ReadUInt64();
                    ulong unknown1 = reader.ReadUInt64();
                    ulong unknown2 = reader.ReadUInt64();
                    ulong offset = reader.ReadUInt64();
                    ulong unknown4 = reader.ReadUInt64();
                    ulong unknown5 = reader.ReadUInt64();

                    if ((unknown1 | unknown2 | unknown4 | unknown5) != 0)
                        throw new InvalidDataException("APK: unsupported entry flags.");
                    if (offset > (ulong)data.Length || storedSize > (ulong)data.Length - offset)
                        throw new InvalidDataException("APK: entry extends past end of file.");

                    byte[] storedData = new byte[storedSize];
                    Buffer.BlockCopy(data, checked((int)offset), storedData, 0, checked((int)storedSize));

                    bool compressed = IsZzz1(storedData);
                    int blockSize = DefaultBlockSize;
                    byte[] fileData = storedData;
                    if (compressed)
                        fileData = DecompressZzz1(storedData, name, out blockSize);

                    var entry = new Entry(compressed, blockSize);
                    entries.Add(entry);

                    GameFile subFile = GameFormatHelper.OpenFile(name, fileData);
                    if (subFile == null)
                        subFile = GameFormatHelper.OpenFile(name, fileData, FormatEnum.DAT);
                    subFile.Tag = entry;
                    SubFiles.Add(subFile);
                }
            }
        }

        public FormatEnum Type => FormatEnum.APK;

        public List<GameFile> SubFiles { get; } = new List<GameFile>();

        public int GetSize() => GetData().Length;

        public byte[] GetData()
        {
            var storedEntries = new List<StoredEntry>(SubFiles.Count);
            foreach (var subFile in SubFiles)
            {
                var entry = subFile.Tag as Entry ?? GetEntryForSubFile(storedEntries.Count);
                byte[] data = subFile.GameData.GetData();
                byte[] storedData = entry.Compressed ? CompressZzz1(data, entry.BlockSize) : AddPadding(data, Alignment);
                storedEntries.Add(new StoredEntry(subFile.Name, storedData));
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                writer.Write(Version);
                writer.Write(storedEntries.Count);
                writer.Write(HeaderSize);
                writer.Write(headerUnknown);

                long dataOffset = Align(HeaderSize + storedEntries.Count * EntrySize, Alignment);
                long nextOffset = dataOffset;
                foreach (var stored in storedEntries)
                {
                    WriteName(writer, stored.Name);
                    writer.Write((ulong)stored.Data.Length);
                    writer.Write((ulong)0);
                    writer.Write((ulong)0);
                    writer.Write((ulong)nextOffset);
                    writer.Write((ulong)0);
                    writer.Write((ulong)0);
                    nextOffset += stored.Data.Length;
                }

                while (stream.Position < dataOffset)
                    writer.Write((byte)0);

                foreach (var stored in storedEntries)
                    writer.Write(stored.Data);

                return stream.ToArray();
            }
        }

        private Entry GetEntryForSubFile(int index)
            => index < entries.Count ? entries[index] : new Entry(true, DefaultBlockSize);

        private static byte[] DecompressZzz1(byte[] data, string name, out int blockSize)
        {
            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream, Encoding.ASCII))
            {
                if (!IsZzz1(data))
                    throw new InvalidDataException("ZZZ1: wrong magic.");

                stream.Position = 4;
                int version = reader.ReadInt32();
                blockSize = reader.ReadInt32();
                int unpackedSize = reader.ReadInt32();
                int blockCount = reader.ReadInt32();
                int packedSize = reader.ReadInt32();
                int unknown1 = reader.ReadInt32();
                int unknown2 = reader.ReadInt32();

                if (version != Zzz1Version)
                    throw new InvalidDataException($"ZZZ1: unsupported version {version} in {name}.");
                if (blockSize <= 0 || blockCount != (unpackedSize + blockSize - 1) / blockSize)
                    throw new InvalidDataException($"ZZZ1: invalid block table in {name}.");
                if ((unknown1 | unknown2) != 0)
                    throw new InvalidDataException($"ZZZ1: unsupported flags in {name}.");

                int payloadOffset = 0x20 + blockCount * 0x10;
                if (payloadOffset < 0 || packedSize < 0 || payloadOffset + packedSize > data.Length)
                    throw new InvalidDataException($"ZZZ1: payload extends past end of {name}.");

                var output = new MemoryStream(unpackedSize);
                for (int i = 0; i < blockCount; i++)
                {
                    stream.Position = 0x20 + i * 0x10;
                    int compressedSize = reader.ReadInt32();
                    int compressedOffset = reader.ReadInt32();
                    int blockUnknown1 = reader.ReadInt32();
                    int blockUnknown2 = reader.ReadInt32();

                    if ((blockUnknown1 | blockUnknown2) != 0)
                        throw new InvalidDataException($"ZZZ1: unsupported block flags in {name}.");

                    int start = payloadOffset + compressedOffset;
                    if (compressedSize < 0 || compressedOffset < 0 || start + compressedSize > payloadOffset + packedSize)
                        throw new InvalidDataException($"ZZZ1: block extends past payload in {name}.");

                    long before = output.Length;
                    using (var input = new MemoryStream(data, start, compressedSize, false))
                    using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
                        zlib.CopyTo(output);

                    int expected = Math.Min(blockSize, unpackedSize - (int)before);
                    if (output.Length - before != expected)
                        throw new InvalidDataException($"ZZZ1: block {i} unpacked to {output.Length - before} bytes, expected {expected} in {name}.");
                }

                if (output.Length != unpackedSize)
                    throw new InvalidDataException($"ZZZ1: unpacked size mismatch in {name}.");

                return output.ToArray();
            }
        }

        private static byte[] CompressZzz1(byte[] data, int blockSize)
        {
            if (blockSize <= 0)
                blockSize = DefaultBlockSize;

            int blockCount = (data.Length + blockSize - 1) / blockSize;
            var blocks = new List<byte[]>(blockCount);
            int packedSize = 0;

            for (int offset = 0; offset < data.Length; offset += blockSize)
            {
                int count = Math.Min(blockSize, data.Length - offset);
                using (var output = new MemoryStream())
                {
                    using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, true))
                        zlib.Write(data, offset, count);
                    byte[] compressed = output.ToArray();
                    blocks.Add(compressed);
                    packedSize += compressed.Length;
                }
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                writer.Write(Zzz1Magic);
                writer.Write(Zzz1Version);
                writer.Write(blockSize);
                writer.Write(data.Length);
                writer.Write(blockCount);
                writer.Write(packedSize);
                writer.Write(0);
                writer.Write(0);

                int compressedOffset = 0;
                foreach (byte[] block in blocks)
                {
                    writer.Write(block.Length);
                    writer.Write(compressedOffset);
                    writer.Write(0);
                    writer.Write(0);
                    compressedOffset += block.Length;
                }

                foreach (byte[] block in blocks)
                    writer.Write(block);

                WritePadding(writer, Alignment);
                return stream.ToArray();
            }
        }

        private static bool IsZzz1(byte[] data)
            => data.Length >= 4
                && data[0] == Zzz1Magic[0]
                && data[1] == Zzz1Magic[1]
                && data[2] == Zzz1Magic[2]
                && data[3] == Zzz1Magic[3];

        private static string ReadName(byte[] data)
        {
            int length = Array.IndexOf(data, (byte)0);
            if (length < 0)
                length = data.Length;
            string name = Encoding.ASCII.GetString(data, 0, length);
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidDataException("APK: empty entry name.");
            return name;
        }

        private static void WriteName(BinaryWriter writer, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidDataException("APK: empty entry name.");

            foreach (char c in name)
                if (c > 0x7F)
                    throw new InvalidDataException($"APK: non-ASCII entry name {name}.");

            byte[] bytes = Encoding.ASCII.GetBytes(name);
            if (bytes.Length >= NameSize)
                throw new InvalidDataException($"APK: entry name is too long: {name}.");

            writer.Write(bytes);
            writer.Write((byte)0);
            for (int i = bytes.Length + 1; i < NameSize; i++)
                writer.Write((byte)0xFE);
        }

        private static long Align(long value, int alignment)
        {
            long remainder = value % alignment;
            return remainder == 0 ? value : value + alignment - remainder;
        }

        private static byte[] AddPadding(byte[] data, int alignment)
        {
            int size = checked((int)Align(data.Length, alignment));
            byte[] output = new byte[size];
            Buffer.BlockCopy(data, 0, output, 0, data.Length);
            return output;
        }

        private static void WritePadding(BinaryWriter writer, int alignment)
        {
            while (writer.BaseStream.Position % alignment != 0)
                writer.Write((byte)0);
        }

        private class Entry
        {
            public Entry(bool compressed, int blockSize)
            {
                Compressed = compressed;
                BlockSize = blockSize;
            }

            public bool Compressed { get; }
            public int BlockSize { get; }
        }

        private class StoredEntry
        {
            public StoredEntry(string name, byte[] data)
            {
                Name = name;
                Data = data;
            }

            public string Name { get; }
            public byte[] Data { get; }
        }
    }
}
