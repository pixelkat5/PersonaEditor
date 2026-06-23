using AuxiliaryLibraries.Media;
using AuxiliaryLibraries.Tools;
using PersonaEditorLib.Sprite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Bitmap = AuxiliaryLibraries.Media.Bitmap;
using Color = System.Drawing.Color;

namespace PersonaEditorLib.SpriteContainer
{
    public class SPR6 : IGameData
    {
        private const int Magic = 0x36525053; // "SPR6"
        private static readonly Encoding ShiftJis = Encoding.GetEncoding("shift-jis");

        private readonly List<Spr6TextureEntry> textures = new List<Spr6TextureEntry>();
        private readonly List<Spr6PanelEntry> panels = new List<Spr6PanelEntry>();
        private readonly List<Spr6SpriteEntry> sprites = new List<Spr6SpriteEntry>();
        private readonly List<SPRKey> keys = new List<SPRKey>();
        private int textureIdBase;

        public short Field04 { get; set; }
        public short Field08 { get; set; }
        public short Field0C { get; set; }
        public int Field1C { get; set; }

        public SPR6(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = IOTools.OpenReadFile(ms, true))
            {
                Read(reader);
            }

            BuildEditorData();
        }

        public List<SPRKey> KeyList => keys;

        public FormatEnum Type => FormatEnum.SPR6;

        public List<GameFile> SubFiles { get; } = new List<GameFile>();

        public int GetSize()
        {
            int size = 0x20;
            size += textures.Count * 0x2C;
            size += panels.Count * 0x24;
            size += sprites.Count * 8;
            foreach (var sprite in sprites)
                size += 0x60;
            foreach (var texture in textures)
                size += texture.Data?.Length ?? 0;
            return size;
        }

        public byte[] GetData()
        {
            ApplyKeyChanges();

            using (var ms = new MemoryStream())
            using (var writer = IOTools.OpenWriteFile(ms, true))
            {
                // Header
                writer.Write(Magic);
                writer.Write(Field04);
                writer.Write(Field08);
                var fileSizePos = writer.BaseStream.Position;
                writer.Write(0);
                writer.Write(Field0C);
                writer.Write((short)textures.Count);
                writer.Write((short)sprites.Count);
                writer.Write((short)panels.Count);
                var textureTableOffsetPos = writer.BaseStream.Position;
                writer.Write(0);
                var panelSpriteOffsetPos = writer.BaseStream.Position;
                writer.Write(0);
                writer.Write(Field1C);

                // Texture table
                var textureTableOffset = (int)writer.BaseStream.Position;
                var textureDataOffsetPositions = new List<long>(textures.Count);
                foreach (var texture in textures)
                {
                    WriteFixedString(writer, texture.Description, 20, ShiftJis);
                    writer.Write(texture.Field00);
                    writer.Write(texture.Field04);
                    writer.Write(texture.Field08);
                    writer.Write(texture.Field0A);
                    writer.Write(texture.Data?.Length ?? 0);
                    writer.Write(texture.Field14);
                    textureDataOffsetPositions.Add(writer.BaseStream.Position);
                    writer.Write(0);
                }

                // Panel + sprite pointer block
                var panelSpriteOffset = (int)writer.BaseStream.Position;
                foreach (var panel in panels)
                {
                    WriteFixedString(writer, panel.Description, 20, ShiftJis);
                    writer.Write(panel.Field08);
                    writer.Write(panel.Field0A);
                    writer.Write(panel.Field0C);
                    writer.Write(panel.Field0E);
                    writer.Write(panel.Field10);
                    writer.Write(panel.Field14);
                }

                var spriteOffsetPositions = new List<long>(sprites.Count);
                foreach (var sprite in sprites)
                {
                    writer.Write(0);
                    spriteOffsetPositions.Add(writer.BaseStream.Position);
                    writer.Write(0);
                }

                for (int i = 0; i < sprites.Count; i++)
                {
                    var spriteOffset = (int)writer.BaseStream.Position;
                    var returnPos = writer.BaseStream.Position;
                    writer.BaseStream.Position = spriteOffsetPositions[i];
                    writer.Write(spriteOffset);
                    writer.BaseStream.Position = returnPos;
                    WriteSprite(writer, sprites[i]);
                }

                // Texture raw data
                for (int i = 0; i < textures.Count; i++)
                {
                    var dataOffset = (int)writer.BaseStream.Position;
                    var returnPos = writer.BaseStream.Position;
                    writer.BaseStream.Position = textureDataOffsetPositions[i];
                    writer.Write(dataOffset);
                    writer.BaseStream.Position = returnPos;

                    var data = textures[i].Data ?? new byte[0];
                    writer.Write(data);
                }

                var fileSize = (int)writer.BaseStream.Length;
                writer.BaseStream.Position = fileSizePos;
                writer.Write(fileSize);
                writer.BaseStream.Position = textureTableOffsetPos;
                writer.Write(textureTableOffset);
                writer.BaseStream.Position = panelSpriteOffsetPos;
                writer.Write(panelSpriteOffset);
                writer.BaseStream.Position = writer.BaseStream.Length;

                return ms.ToArray();
            }
        }

        private void Read(BinaryReader reader)
        {
            if (reader.ReadInt32() != Magic)
                throw new InvalidDataException("SPR6: wrong magic.");

            Field04 = reader.ReadInt16();
            Field08 = reader.ReadInt16();
            reader.ReadInt32(); // file size
            Field0C = reader.ReadInt16();
            var textureCount = reader.ReadInt16();
            var spriteCount = reader.ReadInt16();
            var panelCount = reader.ReadInt16();
            var textureTableOffset = reader.ReadInt32();
            var panelSpriteOffset = reader.ReadInt32();
            Field1C = reader.ReadInt32();

            // Texture entries
            reader.BaseStream.Position = textureTableOffset;
            var textureDataOffsets = new List<int>(textureCount);
            for (int i = 0; i < textureCount; i++)
            {
                var texture = new Spr6TextureEntry
                {
                    Description = ReadFixedString(reader, 20, ShiftJis),
                    Field00 = reader.ReadInt32(),
                    Field04 = reader.ReadInt32(),
                    Field08 = reader.ReadInt16(),
                    Field0A = reader.ReadInt16()
                };
                texture.Size = reader.ReadInt32();
                texture.Field14 = reader.ReadInt32();
                textureDataOffsets.Add(reader.ReadInt32());
                textures.Add(texture);
            }

            // Resolve texture data
            for (int i = 0; i < textures.Count; i++)
            {
                var offset = textureDataOffsets[i];
                if (offset <= 0 || textures[i].Size < 0)
                    textures[i].Data = new byte[0];
                else
                {
                    var returnPos = reader.BaseStream.Position;
                    reader.BaseStream.Position = offset;
                    textures[i].Data = reader.ReadBytes(textures[i].Size);
                    reader.BaseStream.Position = returnPos;
                }
            }

            // Panels + sprite pointers
            reader.BaseStream.Position = panelSpriteOffset;
            for (int i = 0; i < panelCount; i++)
                panels.Add(ReadPanel(reader));

            var spriteOffsets = new List<int>(spriteCount);
            for (int i = 0; i < spriteCount; i++)
            {
                reader.ReadInt32(); // expected 0
                spriteOffsets.Add(reader.ReadInt32());
            }

            for (int i = 0; i < spriteOffsets.Count; i++)
            {
                var offset = spriteOffsets[i];
                if (offset <= 0)
                    continue;
                var returnPos = reader.BaseStream.Position;
                reader.BaseStream.Position = offset;
                sprites.Add(ReadSprite(reader));
                reader.BaseStream.Position = returnPos;
            }
        }

        private void BuildEditorData()
        {
            keys.Clear();
            SubFiles.Clear();
            textureIdBase = sprites.Count > 0 && sprites.Min(x => (int)x.TextureId) > 0 ? 1 : 0;

            for (int i = 0; i < sprites.Count; i++)
                keys.Add(CreateKeyFromSprite(sprites[i], i));

            for (int i = 0; i < textures.Count; i++)
            {
                var nameBase = SanitizeName(textures[i].Description, $"texture_{i:D3}");
                SubFiles.Add(new GameFile(nameBase + ".tga", new Spr6TextureProxy(this, i)));
            }
        }

        private void ApplyKeyChanges()
        {
            int count = Math.Min(keys.Count, sprites.Count);
            for (int i = 0; i < count; i++)
            {
                var key = keys[i];
                var sprite = sprites[i];
                sprite.TextureId = (short)(Math.Max(0, key.TextureIndex) + textureIdBase);
                sprite.Field34 = key.X1;
                sprite.Field38 = key.Y1;
                sprite.Width = Math.Max(0, key.X2 - key.X1);
                sprite.Height = Math.Max(0, key.Y2 - key.Y1);
                sprite.Description = key.Comment ?? sprite.Description;
            }
        }

        private SPRKey CreateKeyFromSprite(Spr6SpriteEntry sprite, int index)
        {
            var key = new byte[0x80];
            using (var ms = new MemoryStream(key))
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(0);
                WriteFixedString(writer, sprite.Description, 16, ShiftJis);
                writer.Write(Math.Max(0, (int)sprite.TextureId - textureIdBase));
                writer.Write(0); // rotation flags
                writer.Write(new byte[0x80 - (int)ms.Position]);
            }

            var sprKey = new SPRKey(key, index);
            sprKey.X1 = sprite.Field34;
            sprKey.Y1 = sprite.Field38;
            sprKey.X2 = sprKey.X1 + Math.Max(0, sprite.Width);
            sprKey.Y2 = sprKey.Y1 + Math.Max(0, sprite.Height);
            return sprKey;
        }

        private static Spr6PanelEntry ReadPanel(BinaryReader reader)
        {
            return new Spr6PanelEntry
            {
                Description = ReadFixedString(reader, 20, ShiftJis),
                Field08 = reader.ReadInt16(),
                Field0A = reader.ReadInt16(),
                Field0C = reader.ReadInt16(),
                Field0E = reader.ReadInt16(),
                Field10 = reader.ReadInt32(),
                Field14 = reader.ReadInt32()
            };
        }

        private static Spr6SpriteEntry ReadSprite(BinaryReader reader)
        {
            return new Spr6SpriteEntry
            {
                Field00 = reader.ReadInt16(),
                TextureId = reader.ReadInt16(),
                Description = ReadFixedString(reader, 32, ShiftJis),
                Field30 = reader.ReadInt32(),
                Field34 = reader.ReadInt32(),
                Field38 = reader.ReadInt32(),
                Width = reader.ReadInt32(),
                Height = reader.ReadInt32(),
                Field44 = reader.ReadInt32(),
                Field48 = reader.ReadInt32(),
                Field4C = reader.ReadInt32(),
                Field50 = reader.ReadInt32(),
                Field54 = reader.ReadInt32(),
                Field58 = reader.ReadInt32(),
                Field5C = reader.ReadInt32(),
                Field60 = reader.ReadInt32(),
                Field64 = reader.ReadInt32(),
                Field68 = reader.ReadInt32()
            };
        }

        private static void WriteSprite(BinaryWriter writer, Spr6SpriteEntry sprite)
        {
            writer.Write(sprite.Field00);
            writer.Write(sprite.TextureId);
            WriteFixedString(writer, sprite.Description, 32, ShiftJis);
            writer.Write(sprite.Field30);
            writer.Write(sprite.Field34);
            writer.Write(sprite.Field38);
            writer.Write(sprite.Width);
            writer.Write(sprite.Height);
            writer.Write(sprite.Field44);
            writer.Write(sprite.Field48);
            writer.Write(sprite.Field4C);
            writer.Write(sprite.Field50);
            writer.Write(sprite.Field54);
            writer.Write(sprite.Field58);
            writer.Write(sprite.Field5C);
            writer.Write(sprite.Field60);
            writer.Write(sprite.Field64);
            writer.Write(sprite.Field68);
        }

        private static string ReadFixedString(BinaryReader reader, int length, Encoding encoding)
        {
            var bytes = reader.ReadBytes(length);
            int end = Array.IndexOf(bytes, (byte)0);
            if (end < 0)
                end = bytes.Length;
            return encoding.GetString(bytes, 0, end).TrimEnd('\0', '\r', '\n');
        }

        private static void WriteFixedString(BinaryWriter writer, string value, int length, Encoding encoding)
        {
            var bytes = encoding.GetBytes(value ?? string.Empty);
            if (bytes.Length >= length)
                writer.Write(bytes, 0, length);
            else
            {
                writer.Write(bytes);
                writer.Write(new byte[length - bytes.Length]);
            }
        }

        private static string SanitizeName(string value, string fallback)
        {
            var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(text.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
        }

        private static Bitmap DecodeTexture(byte[] data)
        {
            if (data == null || data.Length < 18)
                throw new InvalidDataException("SPR6 texture: invalid TGA data.");

            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                byte idLength = reader.ReadByte();
                byte colorMapType = reader.ReadByte();
                byte imageType = reader.ReadByte();
                ushort paletteStart = reader.ReadUInt16();
                ushort paletteLength = reader.ReadUInt16();
                byte paletteDepth = reader.ReadByte();
                reader.ReadUInt16(); // x-origin
                reader.ReadUInt16(); // y-origin
                ushort width = reader.ReadUInt16();
                ushort height = reader.ReadUInt16();
                byte bpp = reader.ReadByte();
                byte descriptor = reader.ReadByte();

                if (width == 0 || height == 0)
                    throw new InvalidDataException("SPR6 texture: invalid dimensions.");
                if (imageType != 1 && imageType != 2)
                    throw new InvalidDataException("SPR6 texture: unsupported TGA type.");

                if (idLength > 0)
                    reader.ReadBytes(idLength);

                bool topOrigin = (descriptor & 0x20) != 0;

                if (imageType == 1)
                {
                    if (colorMapType != 1 || bpp != 8 || (paletteDepth != 24 && paletteDepth != 32))
                        throw new InvalidDataException("SPR6 texture: unsupported indexed TGA format.");

                    var palette = new Color[Math.Max(paletteStart + paletteLength, 256)];
                    for (int i = 0; i < paletteLength; i++)
                    {
                        byte b = reader.ReadByte();
                        byte g = reader.ReadByte();
                        byte r = reader.ReadByte();
                        byte a = paletteDepth == 32 ? reader.ReadByte() : (byte)255;
                        palette[paletteStart + i] = Color.FromArgb(a, r, g, b);
                    }

                    int pixelCount = width * height;
                    var src = reader.ReadBytes(pixelCount);
                    if (src.Length != pixelCount)
                        throw new EndOfStreamException("SPR6 texture: unexpected indexed pixel data length.");

                    var indices = new byte[pixelCount];
                    for (int y = 0; y < height; y++)
                    {
                        int srcY = topOrigin ? y : (height - 1 - y);
                        Buffer.BlockCopy(src, srcY * width, indices, y * width, width);
                    }

                    return new Bitmap(width, height, PixelFormats.Indexed8, indices, palette);
                }
                else
                {
                    if (bpp != 24 && bpp != 32)
                        throw new InvalidDataException("SPR6 texture: unsupported true-color TGA format.");

                    int pixelSize = bpp / 8;
                    int pixelCount = width * height;
                    var src = reader.ReadBytes(pixelCount * pixelSize);
                    if (src.Length != pixelCount * pixelSize)
                        throw new EndOfStreamException("SPR6 texture: unexpected pixel data length.");

                    var dst = new byte[pixelCount * 4];
                    for (int y = 0; y < height; y++)
                    {
                        int srcY = topOrigin ? y : (height - 1 - y);
                        for (int x = 0; x < width; x++)
                        {
                            int s = (srcY * width + x) * pixelSize;
                            int d = (y * width + x) * 4;
                            dst[d + 0] = src[s + 0];
                            dst[d + 1] = src[s + 1];
                            dst[d + 2] = src[s + 2];
                            dst[d + 3] = pixelSize == 4 ? src[s + 3] : (byte)255;
                        }
                    }

                    return new Bitmap(width, height, PixelFormats.Bgra32, dst, null);
                }
            }
        }

        private static byte[] EncodeTexture(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));

            var src = bitmap.PixelFormat == PixelFormats.Bgra32 ? bitmap : bitmap.ConvertTo(PixelFormats.Bgra32, null);
            var data = src.CopyData();

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)0);      // ID length
                writer.Write((byte)0);      // color map type
                writer.Write((byte)2);      // uncompressed true-color
                writer.Write((ushort)0);    // color map start
                writer.Write((ushort)0);    // color map length
                writer.Write((byte)0);      // color map depth
                writer.Write((ushort)0);    // x-origin
                writer.Write((ushort)0);    // y-origin
                writer.Write((ushort)src.Width);
                writer.Write((ushort)src.Height);
                writer.Write((byte)32);     // BGRA32
                writer.Write((byte)0x28);   // top-left origin + 8 alpha bits
                writer.Write(data);
                return ms.ToArray();
            }
        }

        private class Spr6TextureProxy : IGameData, IImage
        {
            private readonly SPR6 owner;
            private readonly int index;

            public Spr6TextureProxy(SPR6 owner, int index)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
                this.index = index;
            }

            public FormatEnum Type => FormatEnum.DAT;

            public List<GameFile> SubFiles { get; } = new List<GameFile>();

            public int GetSize() => owner.textures[index].Data?.Length ?? 0;

            public byte[] GetData() => owner.textures[index].Data ?? new byte[0];

            public Bitmap GetBitmap() => DecodeTexture(owner.textures[index].Data);

            public void SetBitmap(Bitmap bitmap)
            {
                owner.textures[index].Data = EncodeTexture(bitmap);
                owner.textures[index].Size = owner.textures[index].Data.Length;
            }
        }

        private class Spr6TextureEntry
        {
            public string Description;
            public int Field00;
            public int Field04;
            public short Field08;
            public short Field0A;
            public int Size;
            public int Field14;
            public byte[] Data;
        }

        private class Spr6PanelEntry
        {
            public string Description;
            public short Field08;
            public short Field0A;
            public short Field0C;
            public short Field0E;
            public int Field10;
            public int Field14;
        }

        private class Spr6SpriteEntry
        {
            public short Field00;
            public short TextureId;
            public string Description;
            public int Field30;
            public int Field34;
            public int Field38;
            public int Width;
            public int Height;
            public int Field44;
            public int Field48;
            public int Field4C;
            public int Field50;
            public int Field54;
            public int Field58;
            public int Field5C;
            public int Field60;
            public int Field64;
            public int Field68;
        }
    }
}
