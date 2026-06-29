using AuxiliaryLibraries.Media;
using AuxiliaryLibraries.Media.Formats.DDS;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace PersonaEditorLib.Sprite
{
    public class GNF : IGameData, IImage
    {
        private const int Magic = 0x20464E47; // "GNF "
        private const int HeaderSizeOffset = 4;
        private const int TextureCountOffset = 9;
        private const int StreamSizeOffset = 12;
        private const int Word1Offset = 20;
        private const int Word2Offset = 24;
        private const int Word4Offset = 32;
        private const int MetadataOffset = 44;

        private const int SurfaceFormatMask = 0x03f00000;
        private const int SurfaceFormatShift = 20;
        private const int WidthMask = 0x00003fff;
        private const int HeightMask = 0x0fffc000;
        private const int HeightShift = 14;
        private const int PitchMask = 0x07ffe000;
        private const int PitchShift = 13;

        private readonly byte[] header;
        private byte[] textureData;
        private Bitmap bitmap;

        public GNF(byte[] data)
        {
            if (data == null || data.Length < 0x30)
                throw new ArgumentException("GNF data is too small.", nameof(data));
            if (!IsGnf(data))
                throw new InvalidDataException("GNF: wrong magic.");

            int contentSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(HeaderSizeOffset, 4));
            int dataOffset = checked(8 + contentSize);
            if (contentSize < 0x28 || dataOffset > data.Length)
                throw new InvalidDataException("GNF: invalid header size.");
            if (data[TextureCountOffset] != 1)
                throw new NotSupportedException("GNF: only single-texture GNF files are supported.");

            int streamSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(StreamSizeOffset, 4));
            if (streamSize <= 0 || streamSize > data.Length)
                streamSize = data.Length;

            int dataSize = Math.Max(0, streamSize - dataOffset);
            header = data.AsSpan(0, dataOffset).ToArray();
            textureData = data.AsSpan(dataOffset, dataSize).ToArray();
        }

        public static bool IsGnf(byte[] data)
            => data != null && data.Length >= 4 && BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0, 4)) == Magic;

        public FormatEnum Type => FormatEnum.GNF;
        public List<GameFile> SubFiles { get; } = new List<GameFile>();
        public int Width => (int)(Word2 & WidthMask) + 1;
        public int Height => (int)((Word2 & HeightMask) >> HeightShift) + 1;
        public int GetSize() => header.Length + textureData.Length;

        private uint Word1
        {
            get => BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(Word1Offset, 4));
        }

        private uint Word2
        {
            get => BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(Word2Offset, 4));
            set => BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(Word2Offset, 4), value);
        }

        private uint Word4
        {
            get => BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(Word4Offset, 4));
            set => BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(Word4Offset, 4), value);
        }

        private SurfaceFormat Format => (SurfaceFormat)((Word1 & SurfaceFormatMask) >> SurfaceFormatShift);

        public byte[] GetData()
        {
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(StreamSizeOffset, 4), checked(header.Length + textureData.Length));
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(MetadataOffset, 4), textureData.Length);

            byte[] data = new byte[header.Length + textureData.Length];
            header.CopyTo(data, 0);
            textureData.CopyTo(data, header.Length);
            return data;
        }

        public Bitmap GetBitmap()
        {
            if (bitmap != null)
                return bitmap;

            byte[] linear = Ps4Swizzle(textureData, Width, Height, GetBlockSize(Format), true);
            byte[] bgra;

            switch (Format)
            {
                case SurfaceFormat.BC1:
                    DDSDecompressor.DDSDecompress(Width, Height, linear, DDSFourCC.DXT1, out bgra);
                    break;
                case SurfaceFormat.BC2:
                    DDSDecompressor.DDSDecompress(Width, Height, linear, DDSFourCC.DXT3, out bgra);
                    break;
                case SurfaceFormat.BC3:
                    DDSDecompressor.DDSDecompress(Width, Height, linear, DDSFourCC.DXT5, out bgra);
                    break;
                case SurfaceFormat.BC7:
                    bgra = Bc7.Decode(linear, Width, Height);
                    break;
                default:
                    throw new NotSupportedException($"GNF: unsupported surface format {Format}.");
            }

            bitmap = new Bitmap(Width, Height, PixelFormats.Bgra32, bgra, null);
            return bitmap;
        }

        public void SetBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException(nameof(bitmap));
            if (!TryGetDdsFormat(Format, out var fourCC))
                throw new NotSupportedException($"GNF: PNG replacement is not supported for {Format}.");

            if (!DDSCompressor.DDSCompress(bitmap, fourCC, out byte[] compressed))
                throw new NotSupportedException($"GNF: failed to encode {Format} replacement.");

            textureData = Ps4Swizzle(compressed, bitmap.Width, bitmap.Height, GetBlockSize(Format), false);
            SetDimensions(bitmap.Width, bitmap.Height);
            this.bitmap = null;
        }

        private void SetDimensions(int width, int height)
        {
            if (width <= 0 || width > 0x4000 || height <= 0 || height > 0x4000)
                throw new ArgumentOutOfRangeException(nameof(width), "GNF dimensions are outside the supported range.");

            uint word2 = Word2;
            word2 &= ~(uint)(WidthMask | HeightMask);
            word2 |= (uint)((width - 1) & WidthMask);
            word2 |= (uint)(((height - 1) << HeightShift) & HeightMask);
            Word2 = word2;

            uint word4 = Word4;
            word4 &= ~(uint)PitchMask;
            word4 |= (uint)(((width - 1) << PitchShift) & PitchMask);
            Word4 = word4;
        }

        private static bool TryGetDdsFormat(SurfaceFormat format, out DDSFourCC fourCC)
        {
            switch (format)
            {
                case SurfaceFormat.BC1:
                    fourCC = DDSFourCC.DXT1;
                    return true;
                case SurfaceFormat.BC2:
                    fourCC = DDSFourCC.DXT3;
                    return true;
                case SurfaceFormat.BC3:
                    fourCC = DDSFourCC.DXT5;
                    return true;
                default:
                    fourCC = DDSFourCC.NONE;
                    return false;
            }
        }

        private static int GetBlockSize(SurfaceFormat format)
        {
            return format switch
            {
                SurfaceFormat.BC1 => 8,
                SurfaceFormat.BC2 => 16,
                SurfaceFormat.BC3 => 16,
                SurfaceFormat.BC7 => 16,
                _ => throw new NotSupportedException($"GNF: unsupported surface format {format}.")
            };
        }

        private static byte[] Ps4Swizzle(byte[] data, int width, int height, int blockSize, bool unswizzle)
        {
            int widthBlocks = Math.Max(1, (width + 3) / 4);
            int heightBlocks = Math.Max(1, (height + 3) / 4);
            int widthTiles = (widthBlocks + 7) / 8;
            int heightTiles = (heightBlocks + 7) / 8;
            int linearSize = checked(widthBlocks * heightBlocks * blockSize);
            int tiledSize = checked(widthTiles * heightTiles * 64 * blockSize);
            byte[] output = new byte[unswizzle ? linearSize : tiledSize];
            int dataIndex = 0;

            for (int tileY = 0; tileY < heightTiles; tileY++)
            {
                for (int tileX = 0; tileX < widthTiles; tileX++)
                {
                    for (int t = 0; t < 64; t++)
                    {
                        int morton = Morton(t, 8, 8);
                        int y = tileY * 8 + morton / 8;
                        int x = tileX * 8 + morton % 8;

                        if (x < widthBlocks && y < heightBlocks)
                        {
                            int linearIndex = (y * widthBlocks + x) * blockSize;
                            if (linearIndex + blockSize <= output.Length && dataIndex + blockSize <= data.Length)
                            {
                                if (unswizzle)
                                    Buffer.BlockCopy(data, dataIndex, output, linearIndex, blockSize);
                                else
                                    Buffer.BlockCopy(data, linearIndex, output, dataIndex, blockSize);
                            }
                        }

                        dataIndex += blockSize;
                    }
                }
            }

            return output;
        }

        private static int Morton(int value, int sx, int sy)
        {
            int xMask = 1;
            int yMask = 1;
            int x = 0;
            int y = 0;

            while (sx > 1 || sy > 1)
            {
                if (sx > 1)
                {
                    x += xMask * (value & 1);
                    value >>= 1;
                    xMask <<= 1;
                    sx >>= 1;
                }
                if (sy > 1)
                {
                    y += yMask * (value & 1);
                    value >>= 1;
                    yMask <<= 1;
                    sy >>= 1;
                }
            }

            return y * 8 + x;
        }

        private enum SurfaceFormat
        {
            BC1 = 0x23,
            BC2 = 0x24,
            BC3 = 0x25,
            BC7 = 0x29
        }

        private static class Bc7
        {
            private static readonly string[] Partition2 =
            {
                "0011001100110011", "0001000100010001", "0111011101110111", "0001001100110111",
                "0000000100010011", "0011011101111111", "0001001101111111", "0000000100110111",
                "0000000000010011", "0011011111111111", "0000000101111111", "0000000000010111",
                "0001011111111111", "0000000011111111", "0000111111111111", "0000000000001111",
                "0000100011101111", "0111000100000000", "0000000010001110", "0111001100010000",
                "0011000100000000", "0000100011001110", "0000000010001100", "0111001100110001",
                "0011000100010000", "0000100010001100", "0110011001100110", "0011011001101100",
                "0001011111101000", "0000111111110000", "0111000110001110", "0011100110011100",
                "0101010101010101", "0000111100001111", "0101101001011010", "0011001111001100",
                "0011110000111100", "0101010110101010", "0110100101101001", "0101101010100101",
                "0111001111001110", "0001001111001000", "0011001001001100", "0011101111011100",
                "0110100110010110", "0011110011000011", "0110011010011001", "0000011001100000",
                "0100111001000000", "0010011100100000", "0000001001110010", "0000010011100100",
                "0110110010010011", "0011011011001001", "0110001110011100", "0011100111000110",
                "0110110011001001", "0110001100111001", "0111111010000001", "0001100011100111",
                "0000111100110011", "0011001111110000", "0010001011101110", "0100010001110111"
            };

            private static readonly string[] Partition3 =
            {
                "0011001102212222", "0001001122112221", "0000200122112211", "0222002200110111",
                "0000000011221122", "0011001100220022", "0022002211111111", "0011001122112211",
                "0000000011112222", "0000111111112222", "0000111122222222", "0012001200120012",
                "0112011201120112", "0122012201220122", "0011011211221222", "0011200122002220",
                "0001001101121122", "0111001120012200", "0000112211221122", "0022002200221111",
                "0111011102220222", "0001000122212221", "0000001101220122", "0000110022102210",
                "0122012200110000", "0012001211222222", "0110122112210110", "0000011012211221",
                "0022110211020022", "0110011020022222", "0011012201220011", "0000200022112221",
                "0000000211221222", "0222002200120011", "0011001200220222", "0120012001200120",
                "0000111122220000", "0120120120120120", "0120201212010120", "0011220011220011",
                "0011112222000011", "0101010122222222", "0000000021212121", "0022112200221122",
                "0022001100220011", "0220122102201221", "0101222222220101", "0000212121212121",
                "0101010101012222", "0222011102220111", "0002111200021112", "0000211221122112",
                "0222011101110222", "0002111211120002", "0110011001102222", "0000000021122112",
                "0110011022222222", "0022001100110022", "0022112211220022", "0000000000002112",
                "0002000100020001", "0222122202221222", "0101222222222222", "0111201122012220"
            };

            private static readonly int[] Anchor2 =
            {
                15,15,15,15,15,15,15,15,15,15,15,15,15,15,15,15,
                15,2,8,2,2,8,8,15,2,8,2,2,8,8,2,2,
                15,15,6,8,2,8,15,15,2,8,2,2,2,15,15,6,
                6,2,6,8,15,15,2,2,15,15,15,15,15,2,2,15
            };

            private static readonly int[] Anchor3A =
            {
                3,3,15,15,8,3,15,15,8,8,6,6,6,5,3,3,
                3,3,8,15,3,3,6,10,5,8,8,6,8,5,15,15,
                8,15,3,5,6,10,8,15,15,3,15,5,15,15,15,15,
                3,15,5,5,5,8,5,10,5,10,8,13,15,12,3,3
            };

            private static readonly int[] Anchor3B =
            {
                15,8,8,3,15,15,3,8,15,15,15,15,15,15,15,8,
                15,8,15,3,15,8,15,8,3,15,6,10,15,15,10,8,
                15,3,15,10,10,8,9,10,6,15,8,15,3,6,6,8,
                15,3,15,15,15,15,15,15,15,15,15,15,3,15,15,8
            };

            private static readonly byte[] Weights2 = { 0, 21, 43, 64 };
            private static readonly byte[] Weights3 = { 0, 9, 18, 27, 37, 46, 55, 64 };
            private static readonly byte[] Weights4 = { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 };

            public static byte[] Decode(byte[] data, int width, int height)
            {
                int blocksX = Math.Max(1, (width + 3) / 4);
                int blocksY = Math.Max(1, (height + 3) / 4);
                byte[] output = new byte[checked(width * height * 4)];
                Span<byte> blockPixels = stackalloc byte[16 * 4];

                for (int by = 0; by < blocksY; by++)
                {
                    for (int bx = 0; bx < blocksX; bx++)
                    {
                        int blockOffset = (by * blocksX + bx) * 16;
                        if (blockOffset + 16 > data.Length)
                            break;

                        DecodeBlock(data.AsSpan(blockOffset, 16), blockPixels);
                        for (int py = 0; py < 4; py++)
                        {
                            int y = by * 4 + py;
                            if (y >= height)
                                continue;

                            for (int px = 0; px < 4; px++)
                            {
                                int x = bx * 4 + px;
                                if (x >= width)
                                    continue;

                                int src = (py * 4 + px) * 4;
                                int dst = (y * width + x) * 4;
                                output[dst + 0] = blockPixels[src + 2];
                                output[dst + 1] = blockPixels[src + 1];
                                output[dst + 2] = blockPixels[src + 0];
                                output[dst + 3] = blockPixels[src + 3];
                            }
                        }
                    }
                }

                return output;
            }

            private static void DecodeBlock(ReadOnlySpan<byte> block, Span<byte> rgba)
            {
                int mode = GetMode(block);
                if (mode >= 8)
                {
                    FillError(rgba);
                    return;
                }

                int subsets = GetSubsetCount(mode);
                int partition = GetPartition(block, mode);
                int rotation = mode == 4 ? ReadBits(block, 5, 2) : mode == 5 ? ReadBits(block, 6, 2) : 0;
                int indexMode = mode == 4 ? ReadBits(block, 7, 1) : 0;
                int endpointCount = subsets * 2;
                Span<Color> endpoints = stackalloc Color[6];
                ReadEndpoints(block, mode, endpointCount, endpoints);

                int colorBits = GetColorIndexBits(mode, indexMode);
                int alphaBits = GetAlphaIndexBits(mode, indexMode);
                for (int i = 0; i < 16; i++)
                {
                    int subset = GetSubset(mode, subsets, partition, i);
                    Color c0 = endpoints[subset * 2];
                    Color c1 = endpoints[subset * 2 + 1];
                    int colorIndex = ReadIndex(block, mode, subsets, partition, colorBits, i, false);
                    int alphaIndex = alphaBits == 0 ? 0 : ReadIndex(block, mode, subsets, partition, alphaBits, i, true);

                    byte r = Interpolate(c0.R, c1.R, colorIndex, colorBits);
                    byte g = Interpolate(c0.G, c1.G, colorIndex, colorBits);
                    byte b = Interpolate(c0.B, c1.B, colorIndex, colorBits);
                    byte a = alphaBits == 0 ? (byte)c0.A : Interpolate(c0.A, c1.A, alphaIndex, alphaBits);

                    Rotate(ref r, ref g, ref b, ref a, rotation);
                    int o = i * 4;
                    rgba[o + 0] = r;
                    rgba[o + 1] = g;
                    rgba[o + 2] = b;
                    rgba[o + 3] = a;
                }
            }

            private static void ReadEndpoints(ReadOnlySpan<byte> block, int mode, int endpointCount, Span<Color> endpoints)
            {
                int bit = mode switch
                {
                    0 => 5,
                    1 => 8,
                    2 => 9,
                    3 => 10,
                    4 => 8,
                    5 => 8,
                    6 => 7,
                    7 => 14,
                    _ => 0
                };
                int colorRawBits = GetColorRawBits(mode);
                int alphaRawBits = GetAlphaRawBits(mode);
                bool hasAlpha = HasAlpha(mode);

                for (int i = 0; i < endpointCount; i++)
                {
                    endpoints[i].R = ReadBits(block, bit, colorRawBits);
                    bit += colorRawBits;
                }
                for (int i = 0; i < endpointCount; i++)
                {
                    endpoints[i].G = ReadBits(block, bit, colorRawBits);
                    bit += colorRawBits;
                }
                for (int i = 0; i < endpointCount; i++)
                {
                    endpoints[i].B = ReadBits(block, bit, colorRawBits);
                    bit += colorRawBits;
                }
                if (hasAlpha)
                {
                    for (int i = 0; i < endpointCount; i++)
                    {
                        endpoints[i].A = ReadBits(block, bit, alphaRawBits);
                        bit += alphaRawBits;
                    }
                }

                Span<int> pBits = stackalloc int[6];
                int pBitCount = GetPBitCount(mode);
                for (int i = 0; i < pBitCount; i++)
                {
                    pBits[i] = ReadBits(block, bit, 1);
                    bit++;
                }

                int colorPrecision = GetColorPrecision(mode);
                int alphaPrecision = GetAlphaPrecision(mode);
                for (int i = 0; i < endpointCount; i++)
                {
                    int p = mode == 1 ? pBits[i / 2] : pBits[i];
                    int r = endpoints[i].R;
                    int g = endpoints[i].G;
                    int b = endpoints[i].B;
                    int a = hasAlpha ? endpoints[i].A : 0;

                    if (pBitCount != 0)
                    {
                        r = (r << 1) | p;
                        g = (g << 1) | p;
                        b = (b << 1) | p;
                        if (hasAlpha)
                            a = (a << 1) | p;
                    }

                    endpoints[i].R = Expand(r, colorPrecision);
                    endpoints[i].G = Expand(g, colorPrecision);
                    endpoints[i].B = Expand(b, colorPrecision);
                    endpoints[i].A = hasAlpha ? Expand(a, alphaPrecision) : (byte)255;
                }
            }

            private static int GetMode(ReadOnlySpan<byte> block)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (ReadBits(block, i, 1) != 0)
                        return i;
                }
                return 8;
            }

            private static int GetPartition(ReadOnlySpan<byte> block, int mode)
            {
                return mode switch
                {
                    0 => ReadBits(block, 1, 4),
                    1 => ReadBits(block, 2, 6),
                    2 => ReadBits(block, 3, 6),
                    3 => ReadBits(block, 4, 6),
                    7 => ReadBits(block, 8, 6),
                    _ => 0
                };
            }

            private static int ReadIndex(ReadOnlySpan<byte> block, int mode, int subsets, int partition, int bitCount, int index, bool alpha)
            {
                int offset = GetIndexOffset(subsets, partition, bitCount, index);
                int count = GetIndexBitCount(subsets, partition, bitCount, index);
                return ReadBits(block, GetIndexBegin(mode, bitCount, alpha) + offset, count);
            }

            private static int GetIndexOffset(int subsets, int partition, int bitCount, int index)
            {
                if (index == 0)
                    return 0;
                if (subsets == 1)
                    return bitCount * index - 1;
                if (subsets == 2)
                    return index <= Anchor2[partition] ? bitCount * index - 1 : bitCount * index - 2;

                int a = Anchor3A[partition];
                int b = Anchor3B[partition];
                if (index <= a && index <= b)
                    return bitCount * index - 1;
                if (index > a && index > b)
                    return bitCount * index - 3;
                return bitCount * index - 2;
            }

            private static int GetIndexBitCount(int subsets, int partition, int bitCount, int index)
            {
                if (index == 0)
                    return bitCount - 1;
                if (subsets == 2 && index == Anchor2[partition])
                    return bitCount - 1;
                if (subsets == 3 && (index == Anchor3A[partition] || index == Anchor3B[partition]))
                    return bitCount - 1;
                return bitCount;
            }

            private static int GetIndexBegin(int mode, int bitCount, bool alpha)
            {
                return mode switch
                {
                    0 => 83,
                    1 => 82,
                    2 => 99,
                    3 => 98,
                    4 => bitCount == 2 ? 50 : 81,
                    5 => alpha ? 97 : 66,
                    6 => 65,
                    7 => 98,
                    _ => 0
                };
            }

            private static int GetSubset(int mode, int subsets, int partition, int index)
            {
                if (subsets == 1)
                    return 0;
                if (subsets == 2)
                    return Partition2[partition][index] - '0';
                return Partition3[partition][index] - '0';
            }

            private static int ReadBits(ReadOnlySpan<byte> data, int bitOffset, int bitCount)
            {
                int value = 0;
                for (int i = 0; i < bitCount; i++)
                {
                    int bit = bitOffset + i;
                    if ((data[bit >> 3] & (1 << (bit & 7))) != 0)
                        value |= 1 << i;
                }
                return value;
            }

            private static byte Expand(int value, int bits)
            {
                if (bits <= 0)
                    return 0;
                if (bits >= 8)
                    return (byte)value;

                value <<= 8 - bits;
                return (byte)(value | (value >> bits));
            }

            private static byte Interpolate(int a, int b, int index, int precision)
            {
                if (precision == 0)
                    return (byte)a;
                byte[] weights = precision == 2 ? Weights2 : precision == 3 ? Weights3 : Weights4;
                return (byte)(((64 - weights[index]) * a + weights[index] * b + 32) >> 6);
            }

            private static void Rotate(ref byte r, ref byte g, ref byte b, ref byte a, int rotation)
            {
                byte t;
                switch (rotation)
                {
                    case 1:
                        t = a;
                        a = r;
                        r = t;
                        break;
                    case 2:
                        t = a;
                        a = g;
                        g = t;
                        break;
                    case 3:
                        t = a;
                        a = b;
                        b = t;
                        break;
                }
            }

            private static void FillError(Span<byte> rgba)
            {
                for (int i = 0; i < 16; i++)
                {
                    int o = i * 4;
                    rgba[o + 0] = 255;
                    rgba[o + 1] = 0;
                    rgba[o + 2] = 255;
                    rgba[o + 3] = 255;
                }
            }

            private static int GetSubsetCount(int mode) => mode switch { 0 => 3, 1 => 2, 2 => 3, 3 => 2, 7 => 2, _ => 1 };
            private static bool HasAlpha(int mode) => mode >= 4 && mode <= 7;
            private static int GetPBitCount(int mode) => mode switch { 0 => 6, 1 => 2, 3 => 4, 6 => 2, 7 => 4, _ => 0 };
            private static int GetColorRawBits(int mode) => mode switch { 0 => 4, 1 => 6, 2 => 5, 3 => 7, 4 => 5, 5 => 7, 6 => 7, 7 => 5, _ => 0 };
            private static int GetAlphaRawBits(int mode) => mode switch { 4 => 6, 5 => 8, 6 => 7, 7 => 5, _ => 0 };
            private static int GetColorPrecision(int mode) => mode switch { 0 => 5, 1 => 7, 2 => 5, 3 => 8, 4 => 5, 5 => 7, 6 => 8, 7 => 6, _ => 0 };
            private static int GetAlphaPrecision(int mode) => mode switch { 4 => 6, 5 => 8, 6 => 8, 7 => 6, _ => 0 };
            private static int GetColorIndexBits(int mode, int indexMode) => mode switch { 0 => 3, 1 => 3, 2 => 2, 3 => 2, 4 => indexMode == 0 ? 2 : 3, 5 => 2, 6 => 4, 7 => 2, _ => 0 };
            private static int GetAlphaIndexBits(int mode, int indexMode) => mode switch { 4 => indexMode == 0 ? 3 : 2, 5 => 2, 6 => 4, 7 => 2, _ => 0 };

            private struct Color
            {
                public int R;
                public int G;
                public int B;
                public int A;
            }
        }
    }
}
