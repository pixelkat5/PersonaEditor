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
        public List<SPRKey> KeyList { get; } = new List<SPRKey>();
        public bool HasSpriteInfo => KeyList.Count != 0;
        public string GtxSidecarPath { get; private set; }

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

        public void LoadGtxSidecar(string path)
        {
            if (File.Exists(path))
            {
                GtxSidecarPath = path;
                LoadGfuv(File.ReadAllBytes(path));
            }
        }

        public byte[] GetGtxData() => GetGtxData(File.ReadAllBytes(GtxSidecarPath));

        public byte[] GetGtxData(byte[] data)
        {
            byte[] returned = new byte[data.Length];
            Buffer.BlockCopy(data, 0, returned, 0, data.Length);

            int keyIndex = 0;
            for (int offset = 0; offset <= returned.Length - 0x34; offset++)
            {
                if (returned[offset] != 0x47 || returned[offset + 1] != 0x46 || returned[offset + 2] != 0x55 || returned[offset + 3] != 0x56)
                    continue;

                using (var br = new BinaryReader(new MemoryStream(returned, offset, returned.Length - offset)))
                {
                    br.ReadInt32();
                    int size = br.ReadInt32();
                    if (size < 0x34 || offset + size > returned.Length)
                        continue;

                    br.ReadBytes(4);
                    int count = br.ReadInt32();
                    if (count < 0 || 0x34 + count * 8 > size)
                        continue;

                    string textureName = ReadFixedString(br, 0x24);
                    if (GetTextureIndex(textureName) < 0)
                        continue;

                    if (keyIndex + count > KeyList.Count)
                        throw new InvalidDataException("TPC: GFUV sprite count does not match loaded keys.");

                    using (var bw = new BinaryWriter(new MemoryStream(returned)))
                    {
                        bw.BaseStream.Position = offset + 0x34;
                        for (int i = 0; i < count; i++)
                        {
                            var key = KeyList[keyIndex++];
                            bw.Write(Convert.ToInt16(key.X1));
                            bw.Write(Convert.ToInt16(key.Y1));
                            bw.Write(Convert.ToInt16(key.X2 - key.X1));
                            bw.Write(Convert.ToInt16(key.Y2 - key.Y1));
                        }
                    }
                }

                offset += 3;
            }

            return returned;
        }

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

        private void LoadGfuv(byte[] data)
        {
            int keyIndex = KeyList.Count;
            for (int offset = 0; offset <= data.Length - 0x34; offset++)
            {
                if (data[offset] != 0x47 || data[offset + 1] != 0x46 || data[offset + 2] != 0x55 || data[offset + 3] != 0x56)
                    continue;

                using (var br = new BinaryReader(new MemoryStream(data, offset, data.Length - offset)))
                {
                    br.ReadInt32();
                    int size = br.ReadInt32();
                    if (size < 0x34 || offset + size > data.Length)
                        continue;

                    br.ReadBytes(4);
                    int count = br.ReadInt32();
                    if (count < 0 || 0x34 + count * 8 > size)
                        continue;

                    string textureName = ReadFixedString(br, 0x24);
                    int textureIndex = GetTextureIndex(textureName);
                    if (textureIndex < 0)
                        continue;

                    for (int i = 0; i < count; i++)
                    {
                        int x = br.ReadInt16();
                        int y = br.ReadInt16();
                        int width = br.ReadInt16();
                        int height = br.ReadInt16();
                        KeyList.Add(new SPRKey(keyIndex++, textureIndex, x, y, width, height));
                    }
                }

                offset += 3;
            }
        }

        private int GetTextureIndex(string textureName)
        {
            for (int i = 0; i < SubFiles.Count; i++)
                if (string.Equals(Path.GetFileNameWithoutExtension(SubFiles[i].Name), textureName, StringComparison.OrdinalIgnoreCase))
                    return i;

            return -1;
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
