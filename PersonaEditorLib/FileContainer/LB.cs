using PersonaEditorLib.Other;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PersonaEditorLib.FileContainer
{
    public class LB : IGameData
    {
        private readonly List<Entry> entries = new List<Entry>();

        public LB(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream, Encoding.ASCII))
            {
                while (stream.Position < stream.Length)
                {
                    byte type = reader.ReadByte();
                    if (type == 0xFF)
                        break;

                    bool compressed = reader.ReadByte() != 0;
                    short userId = reader.ReadInt16();
                    int blockLength = reader.ReadInt32();
                    string extension = Encoding.ASCII.GetString(reader.ReadBytes(4)).TrimEnd('\0', ' ');
                    int decompressedLength = reader.ReadInt32();

                    int payloadLength = blockLength - 16;
                    if (payloadLength < 0 || stream.Position + payloadLength > stream.Length)
                        throw new InvalidDataException("LB: invalid entry length.");

                    byte[] payload = reader.ReadBytes(payloadLength);
                    byte[] fileData = compressed ? Decompress(payload, decompressedLength) : payload;

                    var entry = new Entry(type, userId, extension, fileData);
                    entries.Add(entry);

                    string name = $"{entries.Count - 1:D2}_{userId:D2}.{extension.ToLowerInvariant()}";
                    var subFile = GameFormatHelper.OpenFile(name, fileData);
                    if (subFile == null)
                        subFile = GameFormatHelper.OpenFile(name, fileData, FormatEnum.DAT);
                    subFile.Tag = entry;
                    SubFiles.Add(subFile);

                    stream.Position = Align(stream.Position, 64);
                }
            }
        }

        public FormatEnum Type => FormatEnum.LB;

        public List<GameFile> SubFiles { get; } = new List<GameFile>();

        public int GetSize() => GetData().Length;

        public byte[] GetData()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                for (int i = 0; i < SubFiles.Count; i++)
                {
                    var entry = SubFiles[i].Tag as Entry ?? entries[i];
                    byte[] data = SubFiles[i].GameData.GetData();
                    string extension = entry.Extension;

                    writer.Write(entry.Type);
                    writer.Write((byte)0);
                    writer.Write(entry.UserId);
                    writer.Write(data.Length + 16);
                    WriteFixedAscii(writer, extension, 4);
                    writer.Write(data.Length);
                    writer.Write(data);
                    WritePadding(writer, 64);
                }

                writer.Write((byte)0xFF);
                writer.Write((byte)0);
                writer.Write((short)0);
                writer.Write(16);
                WriteFixedAscii(writer, "END0", 4);
                writer.Write(0);
                WritePadding(writer, 64);

                return stream.ToArray();
            }
        }

        private static byte[] Decompress(byte[] data, int decompressedSize)
        {
            var output = new List<byte>(decompressedSize);
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                while (output.Count < decompressedSize)
                {
                    byte op = reader.ReadByte();
                    int count = op & 0x1F;
                    if (count == 0)
                        count = reader.ReadUInt16();

                    switch ((op >> 4) & 0xE)
                    {
                        case 0x00:
                            for (int i = 0; i < count; i++)
                                output.Add(reader.ReadByte());
                            break;
                        case 0x02:
                            for (int i = 0; i < count; i++)
                                output.Add(0);
                            break;
                        case 0x04:
                            byte repeated = reader.ReadByte();
                            for (int i = 0; i < count; i++)
                                output.Add(repeated);
                            break;
                        case 0x06:
                            CopyFromOffset(output, reader.ReadByte(), count);
                            break;
                        case 0x08:
                            CopyFromOffset(output, reader.ReadUInt16(), count);
                            break;
                        case 0x0A:
                            for (int i = 0; i < count; i++)
                            {
                                output.Add(reader.ReadByte());
                                output.Add(0);
                            }
                            break;
                        default:
                            throw new InvalidDataException("LB: invalid compression opcode.");
                    }
                }
            }

            return output.ToArray();
        }

        private static void CopyFromOffset(List<byte> output, int offset, int count)
        {
            for (int i = 0; i < count; i++)
                output.Add(output[output.Count - offset]);
        }

        private static long Align(long value, int alignment)
        {
            long remainder = value % alignment;
            return remainder == 0 ? value : value + alignment - remainder;
        }

        private static void WritePadding(BinaryWriter writer, int alignment)
        {
            while (writer.BaseStream.Position % alignment != 0)
                writer.Write((byte)0);
        }

        private static void WriteFixedAscii(BinaryWriter writer, string value, int length)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value ?? "");
            for (int i = 0; i < length; i++)
                writer.Write(i < bytes.Length ? bytes[i] : (byte)0);
        }

        private class Entry
        {
            public Entry(byte type, short userId, string extension, byte[] data)
            {
                Type = type;
                UserId = userId;
                Extension = extension;
                Data = data;
            }

            public byte Type { get; }
            public short UserId { get; }
            public string Extension { get; }
            public byte[] Data { get; }
        }
    }
}
