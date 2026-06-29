using AuxiliaryLibraries.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Buffers.Binary;
using PersonaEditorLib.Other;
using DrawingColor = System.Drawing.Color;

namespace PersonaEditorLib.Sprite
{
    public class HIP : IGameData, IImage
    {
        private const byte Format8bppIndexed = 0x01;
        private const byte Format16bppGrayScale = 0x04;
        private const byte Format32bppArgb = 0x10;
        private const byte Format16bppRgb565 = 0x40;

        private const byte EncodingRawRepeat = 0x01;
        private const byte EncodingKey = 0x02;
        private const byte EncodingRaw = 0x08;
        private const byte EncodingRawSignRepeat = 0x10;
        private const byte EncodingRawCanvas = 0x20;

        private const byte ExtraRenderableLayers = 0x20;
        private static readonly byte[] MagicNumber = new byte[] { 0x48, 0x49, 0x50, 0x00 };

        private Bitmap bitmap;
        private byte format;
        private byte encoding;
        private byte layeredImages;
        private byte extraParams;
        private uint version;
        private int canvasWidth;
        private int canvasHeight;
        private int imageWidth;
        private int imageHeight;
        private int offsetX;
        private int offsetY;
        private byte[] layerExtra = Array.Empty<byte>();
        private DrawingColor[] palette = Array.Empty<DrawingColor>();
        private bool bigEndian;
        private bool dirty;
        private byte[] originalData;

        public HIP(byte[] data)
        {
            Read(data);
        }

        public FormatEnum Type => FormatEnum.HIP;
        public List<GameFile> SubFiles { get; } = new List<GameFile>();
        public ABCFont Font { get; private set; }
        public bool HasFont => Font != null;
        public int GetSize() => !dirty && originalData != null ? originalData.Length : GetData().Length;
        public Bitmap GetBitmap() => bitmap;

        public void LoadAbcSidecar(string path)
        {
            if (!File.Exists(path))
                return;

            try
            {
                LoadAbcData(File.ReadAllBytes(path), path);
            }
            catch
            {
                Font = null;
            }
        }

        public void LoadAbcData(byte[] data, string sidecarPath = null)
        {
            Font = new ABCFont(data, bitmap.Width, bitmap.Height)
            {
                SidecarPath = sidecarPath
            };
        }

        public void SetBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
                return;

            dirty = true;
            this.bitmap = bitmap.PixelFormat == PixelFormats.Bgra32 ? bitmap : bitmap.ConvertTo(PixelFormats.Bgra32, null);
            imageWidth = bitmap.Width;
            imageHeight = bitmap.Height;

            if (encoding == EncodingRawCanvas || !HasRenderableLayer)
            {
                canvasWidth = imageWidth;
                canvasHeight = imageHeight;
                offsetX = 0;
                offsetY = 0;
            }
            else
            {
                canvasWidth = Math.Max(canvasWidth, offsetX + imageWidth);
                canvasHeight = Math.Max(canvasHeight, offsetY + imageHeight);
            }
        }

        public byte[] GetData()
        {
            if (!dirty && originalData != null)
                return originalData.ToArray();

            byte writeEncoding = encoding == EncodingRawRepeat ? EncodingRawRepeat : EncodingRaw;
            byte[] nativePixels = GetNativePixels(bitmap);

            using MemoryStream ms = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(ms);
            writer.Write(MagicNumber);
            WriteUInt32(writer, version == 0 ? 0x125u : version);
            writer.Write(0);
            WriteUInt32(writer, (uint)(format == Format8bppIndexed ? palette.Length : 0));
            WriteInt32(writer, canvasWidth);
            WriteInt32(writer, canvasHeight);
            writer.Write(format);
            writer.Write(writeEncoding);
            writer.Write(layeredImages);
            writer.Write(extraParams);

            uint layerHeaderSize = HasRenderableLayer ? (uint)(0x10 + layerExtra.Length) : 0;
            WriteUInt32(writer, layerHeaderSize);
            if (HasRenderableLayer)
            {
                WriteInt32(writer, imageWidth);
                WriteInt32(writer, imageHeight);
                WriteInt32(writer, offsetX);
                WriteInt32(writer, offsetY);
                writer.Write(layerExtra);
            }

            if (format == Format8bppIndexed)
            {
                foreach (var color in palette)
                {
                    writer.Write(color.B);
                    writer.Write(color.G);
                    writer.Write(color.R);
                    writer.Write(color.A);
                }
            }

            if (writeEncoding == EncodingRawRepeat)
                WriteRawRepeat(writer, nativePixels, BytesPerPixel);
            else
                writer.Write(nativePixels);

            long end = ms.Position;
            ms.Position = 8;
            WriteUInt32(writer, (uint)end);
            return ms.ToArray();
        }

        private void Read(byte[] data)
        {
            if (data.Length < 0x20 || !data.Take(4).SequenceEqual(MagicNumber))
                throw new Exception("HIP: wrong magic number");

            originalData = data.ToArray();
            bigEndian = data[4] == 0;
            version = ReadUInt32(data, 4);
            uint fileLength = ReadUInt32(data, 8);
            uint colorRange = ReadUInt32(data, 12);
            canvasWidth = ReadInt32(data, 16);
            canvasHeight = ReadInt32(data, 20);
            format = data[24];
            encoding = data[25];
            layeredImages = data[26];
            extraParams = data[27];
            uint layerHeaderSize = ReadUInt32(data, 28);

            int position = 0x20;
            if (HasRenderableLayer && layerHeaderSize >= 0x10)
            {
                imageWidth = ReadInt32(data, position);
                imageHeight = ReadInt32(data, position + 4);
                offsetX = ReadInt32(data, position + 8);
                offsetY = ReadInt32(data, position + 12);
                position += 0x10;
                layerExtra = data.Skip(position).Take((int)layerHeaderSize - 0x10).ToArray();
                position += layerExtra.Length;
            }
            else
            {
                imageWidth = canvasWidth;
                imageHeight = canvasHeight;
                offsetX = 0;
                offsetY = 0;
                position += (int)layerHeaderSize;
            }

            palette = ReadPalette(data, ref position, colorRange);

            Stream pixelStream = OpenPixelStream(data, position);
            byte[] nativePixels = DecodePixels(pixelStream, fileLength, position);
            bitmap = CreateBitmap(nativePixels);
        }

        private DrawingColor[] ReadPalette(byte[] data, ref int position, uint colorRange)
        {
            if (format != Format8bppIndexed)
                return Array.Empty<DrawingColor>();

            if (colorRange == 0)
            {
                var missing = new DrawingColor[256];
                missing[0] = DrawingColor.Transparent;
                for (int i = 1; i < missing.Length; i++)
                    missing[i] = DrawingColor.Black;
                return missing;
            }

            int count = (int)Math.Min(colorRange, 256);
            DrawingColor[] colors = new DrawingColor[count];
            for (int i = 0; i < colorRange; i++)
            {
                byte b = data[position++];
                byte g = data[position++];
                byte r = data[position++];
                byte a = data[position++];
                if (i < count)
                    colors[i] = DrawingColor.FromArgb(a, r, g, b);
            }

            return colors;
        }

        private Stream OpenPixelStream(byte[] data, int position)
        {
            if (position + 4 <= data.Length && BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position, 4)) == 0x73676573)
                return DecompressSegs(new MemoryStream(data, position, data.Length - position), true);

            if (position + 20 <= data.Length && BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position + 16, 4)) == 0x73676573)
                return DecompressSegs(new MemoryStream(data, position + 16, data.Length - position - 16), true);

            return new MemoryStream(data, position, data.Length - position);
        }

        private byte[] DecodePixels(Stream stream, uint fileLength, int dataStart)
        {
            int width = UsesCanvasPixels ? canvasWidth : imageWidth;
            int height = UsesCanvasPixels ? canvasHeight : imageHeight;
            int expected = checked(width * height * BytesPerPixel);

            return encoding switch
            {
                EncodingKey => DecodeKey(stream, expected),
                EncodingRawSignRepeat => DecodeRawSignRepeat(stream, expected),
                _ => DecodeRaw(stream, expected)
            };
        }

        private byte[] DecodeRaw(Stream stream, int expected)
        {
            byte[] pixels = new byte[expected];
            int index = 0;
            bool repeat = encoding == EncodingRawRepeat;
            Span<byte> color = stackalloc byte[4];

            while (index < pixels.Length && stream.Position < stream.Length)
            {
                if (!TryReadExact(stream, color.Slice(0, BytesPerPixel)))
                    break;
                int repeatCount = repeat && stream.Position < stream.Length ? stream.ReadByte() : 1;
                for (int i = 0; i < repeatCount && index < pixels.Length; i++)
                {
                    color.Slice(0, Math.Min(BytesPerPixel, pixels.Length - index)).CopyTo(pixels.AsSpan(index));
                    index += BytesPerPixel;
                }
            }

            return pixels;
        }

        private byte[] DecodeKey(Stream stream, int expected)
        {
            byte[] pixels = new byte[expected];
            int key = stream.ReadByte();
            stream.ReadByte();
            int pos = 0;
            Span<byte> bytes = stackalloc byte[4];

            while (pos < pixels.Length && stream.Position < stream.Length)
            {
                if (!TryReadExact(stream, bytes.Slice(0, BytesPerPixel)))
                    break;

                if (bytes[0] == key && bytes.Length >= 3)
                {
                    if (bytes[0] != bytes[1])
                    {
                        int back = bytes[1] == 0xFF ? key : bytes[1];
                        back++;
                        int repeat = bytes[2];
                        for (int i = 0; i < repeat && pos < pixels.Length; i++)
                        {
                            int src = pos - back * BytesPerPixel;
                            if (src < 0)
                                break;
                            Buffer.BlockCopy(pixels, src, pixels, pos, BytesPerPixel);
                            pos += BytesPerPixel;
                        }
                        if (stream.Position > 0)
                            stream.Position--;
                    }
                    else
                    {
                        stream.Position = Math.Max(0, stream.Position - 3);
                        if (!TryReadExact(stream, bytes.Slice(0, BytesPerPixel)))
                            break;
                        CopyEndianAware(bytes.Slice(0, BytesPerPixel), pixels, ref pos);
                    }
                }
                else
                {
                    CopyEndianAware(bytes.Slice(0, BytesPerPixel), pixels, ref pos);
                }
            }

            return pixels;
        }

        private byte[] DecodeRawSignRepeat(Stream stream, int expected)
        {
            byte[] pixels = new byte[expected];
            int pos = 0;
            Span<byte> valBytes = stackalloc byte[4];
            Span<byte> color = stackalloc byte[4];

            while (pos < pixels.Length && stream.Position + 4 <= stream.Length)
            {
                if (!TryReadExact(stream, valBytes))
                    break;
                int value = bigEndian ? BinaryPrimitives.ReadInt32BigEndian(valBytes) : BinaryPrimitives.ReadInt32LittleEndian(valBytes);
                if (value < 0)
                {
                    value &= 0x7FFFFFFF;
                    for (int i = 0; i < value && pos < pixels.Length; i++)
                    {
                        if (!TryReadExact(stream, color.Slice(0, BytesPerPixel)))
                            break;
                        CopyEndianAware(color.Slice(0, BytesPerPixel), pixels, ref pos);
                    }
                }
                else
                {
                    if (!TryReadExact(stream, color.Slice(0, BytesPerPixel)))
                        break;
                    for (int i = 0; i < value && pos < pixels.Length; i++)
                        CopyEndianAware(color.Slice(0, BytesPerPixel), pixels, ref pos);
                }
            }

            return pixels;
        }

        private Bitmap CreateBitmap(byte[] nativePixels)
        {
            int width = UsesCanvasPixels ? canvasWidth : imageWidth;
            int height = UsesCanvasPixels ? canvasHeight : imageHeight;
            byte[] bgra = NativeToBgra(nativePixels, width, height);

            if (!UsesCanvasPixels && HasRenderableLayer)
                return new Bitmap(width, height, PixelFormats.Bgra32, bgra, null);

            return new Bitmap(width, height, PixelFormats.Bgra32, bgra, null);
        }

        private byte[] NativeToBgra(byte[] nativePixels, int width, int height)
        {
            byte[] bgra = new byte[width * height * 4];

            for (int i = 0, o = 0; o < bgra.Length; i += BytesPerPixel, o += 4)
            {
                switch (format)
                {
                    case Format8bppIndexed:
                        int index = nativePixels[i];
                        DrawingColor color = index < palette.Length ? palette[index] : DrawingColor.Transparent;
                        bgra[o] = color.B;
                        bgra[o + 1] = color.G;
                        bgra[o + 2] = color.R;
                        bgra[o + 3] = color.A;
                        break;
                    case Format16bppGrayScale:
                        ushort gray16 = ReadPixelUInt16(nativePixels, i);
                        byte gray = (byte)(gray16 >> 8);
                        bgra[o] = gray;
                        bgra[o + 1] = gray;
                        bgra[o + 2] = gray;
                        bgra[o + 3] = 255;
                        break;
                    case Format16bppRgb565:
                        ushort rgb = ReadPixelUInt16(nativePixels, i);
                        bgra[o] = Expand5((rgb >> 0) & 0x1F);
                        bgra[o + 1] = Expand6((rgb >> 5) & 0x3F);
                        bgra[o + 2] = Expand5((rgb >> 11) & 0x1F);
                        bgra[o + 3] = 255;
                        break;
                    default:
                        bgra[o] = nativePixels[i];
                        bgra[o + 1] = nativePixels[i + 1];
                        bgra[o + 2] = nativePixels[i + 2];
                        bgra[o + 3] = nativePixels[i + 3];
                        break;
                }
            }

            return bgra;
        }

        private byte[] GetNativePixels(Bitmap source)
        {
            byte[] bgra = (source.PixelFormat == PixelFormats.Bgra32 ? source : source.ConvertTo(PixelFormats.Bgra32, null)).CopyData();
            byte[] output = new byte[source.Width * source.Height * BytesPerPixel];

            for (int i = 0, o = 0; i < bgra.Length; i += 4, o += BytesPerPixel)
            {
                switch (format)
                {
                    case Format8bppIndexed:
                        output[o] = FindNearestPaletteIndex(bgra[i + 2], bgra[i + 1], bgra[i], bgra[i + 3]);
                        break;
                    case Format16bppGrayScale:
                        byte gray = (byte)((bgra[i + 2] * 299 + bgra[i + 1] * 587 + bgra[i] * 114) / 1000);
                        WritePixelUInt16(output, o, (ushort)((gray << 8) | gray));
                        break;
                    case Format16bppRgb565:
                        ushort rgb = (ushort)(((bgra[i + 2] >> 3) << 11) | ((bgra[i + 1] >> 2) << 5) | (bgra[i] >> 3));
                        WritePixelUInt16(output, o, rgb);
                        break;
                    default:
                        output[o] = bgra[i];
                        output[o + 1] = bgra[i + 1];
                        output[o + 2] = bgra[i + 2];
                        output[o + 3] = bgra[i + 3];
                        break;
                }
            }

            return output;
        }

        private byte FindNearestPaletteIndex(byte r, byte g, byte b, byte a)
        {
            if (palette.Length == 0)
                return 0;

            int bestIndex = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                int dr = palette[i].R - r;
                int dg = palette[i].G - g;
                int db = palette[i].B - b;
                int da = palette[i].A - a;
                int distance = dr * dr + dg * dg + db * db + da * da;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return (byte)bestIndex;
        }

        private Stream DecompressSegs(Stream stream, bool useBigEndian)
        {
            using BinaryReader reader = new BinaryReader(stream);
            long begin = stream.Position;
            reader.ReadBytes(4);
            short flags = ReadInt16(reader.ReadBytes(2), useBigEndian);
            short chunkCount = ReadInt16(reader.ReadBytes(2), useBigEndian);
            uint fullSize = ReadUInt32(reader.ReadBytes(4), useBigEndian);
            reader.ReadBytes(4);
            long pos = begin + chunkCount * 8;
            int workaround = 0;
            MemoryStream output = new MemoryStream(new byte[fullSize]);

            for (int i = 0; i < chunkCount; i++)
            {
                ushort zSize = ReadUInt16(reader.ReadBytes(2), useBigEndian);
                ushort sizeRaw = ReadUInt16(reader.ReadBytes(2), useBigEndian);
                uint offset = ReadUInt32(reader.ReadBytes(4), useBigEndian) - 1;
                if (i == 0 && offset == 0)
                    workaround = 1;
                if (workaround != 0)
                    offset += (uint)pos;

                int size = sizeRaw == 0 ? 0x10000 : sizeRaw;
                long save = stream.Position;
                stream.Position = begin + offset;
                byte[] chunk = reader.ReadBytes(zSize);
                if (size == zSize)
                    output.Write(chunk, 0, chunk.Length);
                else
                {
                    using DeflateStream deflate = new DeflateStream(new MemoryStream(chunk), CompressionMode.Decompress);
                    deflate.CopyTo(output);
                }
                stream.Position = save;
            }

            output.Position = 0;
            return output;
        }

        private void CopyEndianAware(ReadOnlySpan<byte> bytes, byte[] pixels, ref int pos)
        {
            int count = Math.Min(bytes.Length, pixels.Length - pos);
            if (bigEndian && bytes.Length > 1)
            {
                for (int i = 0; i < count; i++)
                    pixels[pos + i] = bytes[bytes.Length - 1 - i];
            }
            else
            {
                bytes.Slice(0, count).CopyTo(pixels.AsSpan(pos));
            }
            pos += bytes.Length;
        }

        private void WriteRawRepeat(BinaryWriter writer, byte[] pixels, int bytesPerPixel)
        {
            for (int i = 0; i < pixels.Length; i += bytesPerPixel)
            {
                int repeat = 1;
                while (repeat < 255 && i + (repeat + 1) * bytesPerPixel <= pixels.Length &&
                       pixels.AsSpan(i, bytesPerPixel).SequenceEqual(pixels.AsSpan(i + repeat * bytesPerPixel, bytesPerPixel)))
                {
                    repeat++;
                }

                writer.Write(pixels, i, bytesPerPixel);
                writer.Write((byte)repeat);
                i += (repeat - 1) * bytesPerPixel;
            }
        }

        private bool HasRenderableLayer => (extraParams & ExtraRenderableLayers) != 0 && layeredImages != 0;
        private bool UsesCanvasPixels => extraParams == 0 || encoding == EncodingRawCanvas;

        private int BytesPerPixel => format switch
        {
            Format8bppIndexed => 1,
            Format16bppGrayScale => 2,
            Format16bppRgb565 => 2,
            _ => 4
        };

        private ushort ReadPixelUInt16(byte[] data, int offset)
            => bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2)) : BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));

        private void WritePixelUInt16(byte[] data, int offset, ushort value)
        {
            if (bigEndian)
                BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset, 2), value);
            else
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), value);
        }

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

        private static bool TryReadExact(Stream stream, Span<byte> buffer)
        {
            while (!buffer.IsEmpty)
            {
                int read = stream.Read(buffer);
                if (read == 0)
                    return false;
                buffer = buffer.Slice(read);
            }
            return true;
        }

        private static byte Expand5(int value) => (byte)((value << 3) | (value >> 2));
        private static byte Expand6(int value) => (byte)((value << 2) | (value >> 4));
        private static short ReadInt16(byte[] data, bool bigEndian) => bigEndian ? BinaryPrimitives.ReadInt16BigEndian(data) : BinaryPrimitives.ReadInt16LittleEndian(data);
        private static ushort ReadUInt16(byte[] data, bool bigEndian) => bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(data) : BinaryPrimitives.ReadUInt16LittleEndian(data);
        private static uint ReadUInt32(byte[] data, bool bigEndian) => bigEndian ? BinaryPrimitives.ReadUInt32BigEndian(data) : BinaryPrimitives.ReadUInt32LittleEndian(data);
    }
}
