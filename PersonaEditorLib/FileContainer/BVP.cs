using AuxiliaryLibraries.Extensions;
using AuxiliaryLibraries.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PersonaEditorLib.FileContainer
{
    public class BVP : IGameData
    {
        List<int> FlagList = new List<int>();

        public BVP(string path)
        {
            Name = Path.GetFileName(path);
            Open(File.ReadAllBytes(path));
        }

        public BVP(string name, byte[] data)
        {
            Name = name;
            Open(data);
        }

        private void Open(byte[] data)
        {
            using (BinaryReader reader = IOTools.OpenReadFile(new MemoryStream(data), IsLittleEndian))
            {
                List<int[]> Entry = new List<int[]>();
                int tableEnd = 0;

                while (reader.BaseStream.Position + 12 <= reader.BaseStream.Length)
                {
                    int[] entry = reader.ReadInt32Array(3);
                    Entry.Add(entry);

                    if (entry[1] == 0)
                        break;

                    if (tableEnd == 0)
                        tableEnd = entry[1];

                    if (reader.BaseStream.Position >= tableEnd)
                        break;
                }

                for (int i = 0; i < Entry.Count && Entry[i][1] != 0; i++)
                {
                    FlagList.Add(Entry[i][0]);
                    reader.BaseStream.Position = Entry[i][1];
                    string name = Path.GetFileNameWithoutExtension(Name) + "(" + i.ToString().PadLeft(3, '0') + ").BMD";
                    SubFiles.Add(GameFormatHelper.OpenFile(name, reader.ReadBytes(Entry[i][2]), FormatEnum.BMD));
                }
            }
        }

        public int Count
        {
            get { return SubFiles.Count; }
        }

        public bool IsLittleEndian { get; set; } = true;

        public string Name { get; private set; } = "";

        #region IGameFile

        public FormatEnum Type => FormatEnum.BVP;

        public List<GameFile> SubFiles { get; } = new List<GameFile>();

        public int GetSize()
        {
            int size = (SubFiles.Count + 1) * 12;
            foreach (var subFile in SubFiles)
            {
                size += subFile.GameData.GetSize();
                size += IOTools.Alignment(size, 16);
            }
            return size;
        }

        public byte[] GetData()
        {
            using (MemoryStream MS = new MemoryStream())
            using (BinaryWriter writer = IOTools.OpenWriteFile(MS, IsLittleEndian))
            {
                writer.BaseStream.Position = (SubFiles.Count + 1) * 12;

                List<int[]> Entry = new List<int[]>();

                for (int i = 0; i < SubFiles.Count; i++)
                {
                    byte[] data = SubFiles[i].GameData.GetData();
                    Entry.Add(new int[] { FlagList[i], (int)writer.BaseStream.Position, data.Length });

                    writer.Write(data);
                    writer.Write(new byte[IOTools.Alignment(writer.BaseStream.Position, 16)]);
                }

                writer.BaseStream.Position = 0;

                foreach (var a in Entry)
                    writer.WriteInt32Array(a);

                return MS.ToArray();
            }
        }

        #endregion
    }
}
