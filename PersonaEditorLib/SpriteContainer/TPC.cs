using PersonaEditorLib.Other;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PersonaEditorLib.SpriteContainer
{
    public sealed class TPC : IGameData
    {
        public List<GameFile> SubFiles { get; } = new List<GameFile>();

        public TPC(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                if (br.BaseStream.Length < 4)
                    throw new Exception("TPC: data length unacceptable");

                int count = br.ReadInt32();
                if (count < 0)
                    throw new Exception("TPC: negative CTPK count");

                for (int i = 0; i < count; i++)
                {
                    string name = ReadFixedString(br, 0x20);
                    if (string.IsNullOrWhiteSpace(name))
                        name = $"{i:D3}.ctpk";
                    else if (Path.GetExtension(name).Length == 0)
                        name = name + ".ctpk";

                    if (br.BaseStream.Position + 4 > br.BaseStream.Length)
                        throw new Exception("TPC: unexpected end of file");
                    int size = br.ReadInt32();
                    if (size < 0 || br.BaseStream.Position + size > br.BaseStream.Length)
                        throw new Exception("TPC: invalid CTPK size");

                    var bytes = br.ReadBytes(size);
                    var objectFile = GameFormatHelper.OpenFile(name, bytes, FormatEnum.CTPK);
                    if (objectFile == null)
                        objectFile = new GameFile(name, new DAT(bytes));
                    SubFiles.Add(objectFile);
                }
            }
        }

        public FormatEnum Type => FormatEnum.TPC;

        public int GetSize()
        {
            int size = 4;
            foreach (var file in SubFiles)
                size += 0x20 + 4 + file.GameData.GetSize();
            return size;
        }

        public byte[] GetData()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(SubFiles.Count);
                foreach (var file in SubFiles)
                {
                    WriteFixedString(bw, file.Name, 0x20);
                    int dataSize = file.GameData.GetSize();
                    bw.Write(dataSize);
                    bw.Write(file.GameData.GetData());
                }
                return ms.ToArray();
            }
        }

        private static string ReadFixedString(BinaryReader br, int length)
        {
            var bytes = br.ReadBytes(length);
            int end = Array.IndexOf(bytes, (byte)0);
            if (end < 0)
                end = bytes.Length;
            return Encoding.ASCII.GetString(bytes, 0, end).TrimEnd('\0', ' ');
        }

        private static void WriteFixedString(BinaryWriter bw, string value, int length)
        {
            var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
            if (bytes.Length > length)
                bw.Write(bytes, 0, length);
            else
            {
                bw.Write(bytes);
                if (bytes.Length < length)
                    bw.Write(new byte[length - bytes.Length]);
            }
        }
    }
}
