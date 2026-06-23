using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PersonaEditorLib.FileContainer
{
    public class PAC : IGameData
    {
        private const uint Magic = 0x43415046;
        private const uint FileHeaderEndPadding = 0x10;
        private const uint NoByteAlignment = 0x40000000;
        private const uint GenerateNameID = 0x80000000;
        private const uint GenerateExtendedNameID = 0xA0000000;

        private bool bigEndian;
        private uint parameters;
        private int fileNameLength;
        private readonly List<uint> indices = new List<uint>();

        public PAC(byte[] data)
        {
            Open(data);
        }

        public FormatEnum Type => FormatEnum.PAC;
        public List<GameFile> SubFiles { get; } = new List<GameFile>();
        public int GetSize()
        {
            int nameLength = Math.Max(fileNameLength, GetMaxNameLength(SubFiles.Select(x => Path.GetFileName(x.Name)).ToArray()));
            int entrySize = GetEntrySize(nameLength, HasNameId);
            int headerSize = 0x20 + entrySize * SubFiles.Count;

            if (HasFileHeaderEndPadding)
                headerSize += Alignment(headerSize, 0x80);

            int fileOffset = 0;
            foreach (var subFile in SubFiles)
            {
                if (HasFileHeaderEndPadding)
                    fileOffset += Alignment(fileOffset, 0x80);

                int storedSize = subFile.GameData.GetSize();

                if (!HasNoByteAlignment)
                    storedSize += Alignment(storedSize, 0x10);
                if (HasFileHeaderEndPadding)
                    storedSize += Alignment(storedSize, 0x80);
                fileOffset += storedSize;
            }

            return headerSize + fileOffset;
        }

        public byte[] GetData()
        {
            int nameLength = Math.Max(fileNameLength, GetMaxNameLength(SubFiles.Select(x => Path.GetFileName(x.Name)).ToArray()));
            int entrySize = GetEntrySize(nameLength, HasNameId);
            int headerSize = 0x20 + entrySize * SubFiles.Count;

            if (HasFileHeaderEndPadding)
                headerSize += Alignment(headerSize, 0x80);

            List<byte[]> fileData = SubFiles.Select(x => x.GameData.GetData()).ToList();
            List<int> offsets = new List<int>();
            int fileOffset = 0;

            for (int i = 0; i < fileData.Count; i++)
            {
                if (HasFileHeaderEndPadding)
                    fileOffset += Alignment(fileOffset, 0x80);

                offsets.Add(fileOffset);
                int storedSize = fileData[i].Length;
                if (!HasNoByteAlignment)
                    storedSize += Alignment(storedSize, 0x10);
                if (HasFileHeaderEndPadding)
                    storedSize += Alignment(storedSize, 0x80);
                fileOffset += storedSize;
            }

            using MemoryStream ms = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(ms);
            writer.Write(Encoding.ASCII.GetBytes("FPAC"));
            WriteUInt32(writer, (uint)headerSize);
            writer.Write(0);
            WriteUInt32(writer, (uint)SubFiles.Count);
            WriteUInt32(writer, parameters == 0 ? 1u : parameters);
            WriteInt32(writer, nameLength);
            WriteUInt32(writer, 0);
            WriteUInt32(writer, 0);

            for (int i = 0; i < SubFiles.Count; i++)
            {
                byte[] nameBytes = new byte[nameLength];
                string name = TruncateName(Path.GetFileName(SubFiles[i].Name), nameLength);
                Encoding.ASCII.GetBytes(name, 0, name.Length, nameBytes, 0);
                writer.Write(nameBytes);
                WriteUInt32(writer, i < indices.Count ? indices[i] : (uint)i);
                WriteUInt32(writer, (uint)offsets[i]);
                WriteUInt32(writer, (uint)fileData[i].Length);

                if (HasNameId)
                    WriteUInt32(writer, GetNameId(name));

                writer.Write(new byte[entrySize - nameLength - 12 - (HasNameId ? 4 : 0)]);
            }

            writer.Write(new byte[headerSize - (int)ms.Position]);

            for (int i = 0; i < fileData.Count; i++)
            {
                if (HasFileHeaderEndPadding)
                    writer.Write(new byte[Alignment((int)ms.Position - headerSize, 0x80)]);

                writer.Write(fileData[i]);

                int written = fileData[i].Length;
                if (!HasNoByteAlignment)
                {
                    int pad = Alignment(written, 0x10);
                    writer.Write(new byte[pad]);
                    written += pad;
                }

                if (HasFileHeaderEndPadding)
                    writer.Write(new byte[Alignment(written, 0x80)]);
            }

            long end = ms.Position;
            ms.Position = 8;
            WriteUInt32(writer, (uint)end);
            return ms.ToArray();
        }

        private void Open(byte[] data)
        {
            if (data.Length < 0x20 || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0, 4)) != Magic)
                throw new Exception("PAC: wrong magic number");

            bigEndian = DetectBigEndian(data);
            uint headerSize = ReadUInt32(data, 4);
            uint fileCount = ReadUInt32(data, 12);
            parameters = ReadUInt32(data, 16);
            fileNameLength = ReadInt32(data, 20);

            if (fileNameLength <= 0 || fileCount > 100000)
                throw new Exception("PAC: invalid header");

            int entrySize = GetEntrySize(fileNameLength, HasNameId);
            int position = 0x20;

            for (int i = 0; i < fileCount; i++)
            {
                string name = Encoding.ASCII.GetString(data, position, fileNameLength).TrimEnd('\0');
                uint index = ReadUInt32(data, position + fileNameLength);
                uint offset = ReadUInt32(data, position + fileNameLength + 4);
                uint length = ReadUInt32(data, position + fileNameLength + 8);
                indices.Add(index);

                long filePosition = headerSize + offset;
                if (filePosition < 0 || filePosition + length > data.Length)
                    throw new Exception("PAC: file entry outside archive");

                byte[] subData = new byte[length];
                Buffer.BlockCopy(data, (int)filePosition, subData, 0, (int)length);

                GameFile objectFile = GameFormatHelper.OpenFile(name, subData);
                if (objectFile == null)
                    objectFile = GameFormatHelper.OpenFile(name, subData, FormatEnum.DAT);
                SubFiles.Add(objectFile);

                position += entrySize;
            }
        }

        private bool DetectBigEndian(byte[] data)
        {
            uint headerSizeLittle = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4));
            uint fileCountLittle = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(12, 4));
            uint parametersLittle = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(16, 4));
            int nameLengthLittle = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(20, 4));

            if (fileCountLittle == 0 || nameLengthLittle <= 0)
                return true;

            int entrySize = GetEntrySize(nameLengthLittle, HasNameIdFor(parametersLittle));
            uint expected = (uint)(0x20 + entrySize * fileCountLittle);
            uint expectedPadded = expected + (uint)Alignment((int)expected, 0x80);
            return headerSizeLittle != expected && headerSizeLittle != expectedPadded;
        }

        private int GetMaxNameLength(string[] names)
        {
            int min = HasNameId ? HasExtendedNameId ? 64 : 32 : 1;
            if (names.Length == 0)
                return 0;

            int length = Math.Max(min, names.Max(x => x.Length));
            int remainder = length % 4;
            length += remainder == 0 ? 4 : 4 - remainder;

            if (HasNameId)
                length = min;

            if (HasFileHeaderEndPadding)
                length += Alignment(length, 0x10);

            return length;
        }

        private int GetEntrySize(int nameLength, bool hasNameId)
        {
            int size = nameLength + 12 + (hasNameId ? 4 : 0);
            int remainder = size % 0x10;
            if (remainder == 0)
                return hasNameId ? size : size + 0x10;
            return size + 0x10 - remainder;
        }

        private string TruncateName(string name, int nameLength)
        {
            if (name.Length < nameLength)
                return name;

            string ext = Path.GetExtension(name);
            string baseName = Path.GetFileNameWithoutExtension(name);
            int maxBaseLength = Math.Max(0, nameLength - ext.Length - 1);
            return baseName.Substring(0, Math.Min(baseName.Length, maxBaseLength)) + ext;
        }

        private uint GetNameId(string name)
        {
            uint nameId = 0;
            foreach (char c in name.ToLowerInvariant())
            {
                nameId *= 0x89;
                nameId += c;
            }

            return nameId;
        }

        private bool HasFileHeaderEndPadding => (parameters & FileHeaderEndPadding) != 0;
        private bool HasNoByteAlignment => (parameters & NoByteAlignment) != 0;
        private bool HasNameId => HasNameIdFor(parameters);
        private bool HasExtendedNameId => (parameters & GenerateExtendedNameID) == GenerateExtendedNameID;
        private static bool HasNameIdFor(uint value) => (value & GenerateNameID) != 0 || (value & GenerateExtendedNameID) == GenerateExtendedNameID;

        private uint ReadUInt32(byte[] data, int offset)
            => bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4)) : BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));

        private int ReadInt32(byte[] data, int offset)
            => bigEndian ? BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4)) : BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));

        private void WriteUInt32(BinaryWriter writer, uint value)
        {
            Span<byte> buffer = stackalloc byte[4];
            if (bigEndian)
                BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            else
                BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            writer.Write(buffer);
        }

        private void WriteInt32(BinaryWriter writer, int value)
        {
            Span<byte> buffer = stackalloc byte[4];
            if (bigEndian)
                BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            else
                BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            writer.Write(buffer);
        }

        private static int Alignment(int position, int alignment)
        {
            int remainder = position % alignment;
            return remainder == 0 ? 0 : alignment - remainder;
        }
    }
}
