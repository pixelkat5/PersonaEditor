using AuxiliaryLibraries.Media;
using AuxiliaryLibraries.Tools;
using PersonaEditorLib.Other;
using PersonaEditorLib.Sprite;
using System;
using System.Collections.Generic;
using System.IO;
using Bitmap = AuxiliaryLibraries.Media.Bitmap;

namespace PersonaEditorLib.SpriteContainer
{
    public class G1T : IGameData
    {
        private const uint Magic = 0x47315447; // GT1G
        private const uint DdsMagic = 0x20534444; // DDS

        private const uint DdsHeaderFlagsTexture = 0x00001007;
        private const uint DdsHeaderFlagsMipmap = 0x00020000;
        private const uint DdsHeaderFlagsPitch = 0x00000008;
        private const uint DdsHeaderFlagsLinearSize = 0x00080000;
        private const uint DdsSurfaceFlagsTexture = 0x00001000;
        private const uint DdsSurfaceFlagsMipmap = 0x00400008;

        private const uint DdsPfAlphaPixels = 0x00000001;
        private const uint DdsPfFourCC = 0x00000004;
        private const uint DdsPfRgb = 0x00000040;
        private const uint DdsPfRgba = DdsPfRgb | DdsPfAlphaPixels;

        public uint Version { get; private set; }
        public uint Platform { get; private set; }
        public uint Unknown2 { get; private set; }

        private readonly List<G1TextureEntry> textures = new List<G1TextureEntry>();

        public G1T(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            data = UnwrapIfNeeded(data);

            using (var ms = new MemoryStream(data))
            using (var reader = IOTools.OpenReadFile(ms, true))
            {
                Read(reader);
            }

            BuildSubFiles();
        }

        public FormatEnum Type => FormatEnum.G1T;

        public List<GameFile> SubFiles { get; } = new List<GameFile>();

        public int GetSize()
        {
            int size = 0x1C + textures.Count * 8;
            foreach (var entry in textures)
                size += 4 + (entry.LocalExtraData?.Length ?? 0) + (entry.Payload?.Length ?? 0);
            return size;
        }

        public byte[] GetData()
        {
            using (var ms = new MemoryStream())
            using (var writer = IOTools.OpenWriteFile(ms, true))
            {
                writer.Write(Magic);
                writer.Write(Version);
                long fileSizePos = writer.BaseStream.Position;
                writer.Write(0u);
                long offsetTableAddressPos = writer.BaseStream.Position;
                writer.Write(0u);
                writer.Write((uint)textures.Count);
                writer.Write(Platform);
                writer.Write(Unknown2);

                for (int i = 0; i < textures.Count; i++)
                    writer.Write(textures[i].NormalMapFlags);

                uint offsetTableAddress = (uint)writer.BaseStream.Position;
                var offsetTable = new uint[textures.Count];
                long offsetTablePos = writer.BaseStream.Position;
                for (int i = 0; i < textures.Count; i++)
                    writer.Write(0u);

                for (int i = 0; i < textures.Count; i++)
                {
                    var entry = textures[i];
                    offsetTable[i] = (uint)(writer.BaseStream.Position - offsetTableAddress);

                    byte mipAndZ = (byte)(((entry.MipMapCount & 0x0F) << 4) | (entry.ZMipMapCount & 0x0F));
                    writer.Write(mipAndZ);
                    writer.Write(entry.TypeCode);
                    writer.Write((ushort)(((entry.Dy & 0x0F) << 4) | (entry.Dx & 0x0F)));
                    writer.Write(entry.Flags);

                    if (entry.LocalExtraData != null && entry.LocalExtraData.Length > 0)
                        writer.Write(entry.LocalExtraData);

                    writer.Write(entry.Payload ?? new byte[0]);
                }

                long fileSize = writer.BaseStream.Length;
                writer.BaseStream.Position = offsetTablePos;
                for (int i = 0; i < offsetTable.Length; i++)
                    writer.Write(offsetTable[i]);

                writer.BaseStream.Position = fileSizePos;
                writer.Write((uint)fileSize);
                writer.BaseStream.Position = offsetTableAddressPos;
                writer.Write(offsetTableAddress);
                writer.BaseStream.Position = fileSize;
                return ms.ToArray();
            }
        }

        private void Read(BinaryReader reader)
        {
            if (reader.ReadUInt32() != Magic)
                throw new InvalidDataException("G1T: wrong magic.");

            Version = reader.ReadUInt32();
            uint fileSize = reader.ReadUInt32();
            uint offsetTableAddress = reader.ReadUInt32();
            uint textureCount = reader.ReadUInt32();
            Platform = reader.ReadUInt32();
            Unknown2 = reader.ReadUInt32();

            uint[] normalFlags = new uint[textureCount];
            for (int i = 0; i < textureCount; i++)
                normalFlags[i] = reader.ReadUInt32();

            reader.BaseStream.Position = offsetTableAddress;
            uint[] offsets = new uint[textureCount];
            for (int i = 0; i < textureCount; i++)
                offsets[i] = reader.ReadUInt32();

            textures.Clear();
            for (int i = 0; i < textureCount; i++)
            {
                long texPos = offsetTableAddress + offsets[i];
                long texEnd = (i + 1 < textureCount)
                    ? offsetTableAddress + offsets[i + 1]
                    : Math.Min(fileSize, (uint)reader.BaseStream.Length);
                if (texEnd < texPos)
                    texEnd = texPos;

                reader.BaseStream.Position = texPos;
                byte mipAndZ = reader.ReadByte();
                byte type = reader.ReadByte();
                ushort dim = reader.ReadUInt16();
                uint flags = reader.ReadUInt32();

                byte mip = (byte)((mipAndZ >> 4) & 0x0F);
                byte zMip = (byte)(mipAndZ & 0x0F);
                int dx = dim & 0x0F;
                int dy = (dim >> 4) & 0x0F;

                byte[] localExtra = null;
                if ((flags >> 24) == 0x10)
                {
                    uint extraSize = reader.ReadUInt32();
                    if (extraSize >= 4 && extraSize <= (uint)(texEnd - reader.BaseStream.Position + 4))
                    {
                        localExtra = new byte[extraSize];
                        Buffer.BlockCopy(BitConverter.GetBytes(extraSize), 0, localExtra, 0, 4);
                        int rem = (int)extraSize - 4;
                        if (rem > 0)
                        {
                            var tail = reader.ReadBytes(rem);
                            Buffer.BlockCopy(tail, 0, localExtra, 4, tail.Length);
                        }
                    }
                }

                long payloadPos = reader.BaseStream.Position;
                int payloadLen = (int)Math.Max(0, texEnd - payloadPos);
                byte[] payload = reader.ReadBytes(payloadLen);

                textures.Add(new G1TextureEntry
                {
                    NormalMapFlags = normalFlags[i],
                    MipMapCount = mip == 0 ? (byte)1 : mip,
                    ZMipMapCount = zMip,
                    TypeCode = type,
                    Dx = dx,
                    Dy = dy,
                    Flags = flags,
                    LocalExtraData = localExtra,
                    Payload = payload
                });
            }
        }

        private static byte[] UnwrapIfNeeded(byte[] data)
        {
            if (data.Length < 4)
                return data;

            // Some Koei containers prepend an "IDRK0000" wrapper before GT1G.
            uint idrk = BitConverter.ToUInt32(data, 0);
            if (idrk != 0x4B524449) // "IDRK"
                return data;

            int maxScan = Math.Min(data.Length - 4, 0x200);
            for (int i = 0; i <= maxScan; i++)
            {
                if (BitConverter.ToUInt32(data, i) == Magic)
                {
                    var unwrapped = new byte[data.Length - i];
                    Buffer.BlockCopy(data, i, unwrapped, 0, unwrapped.Length);
                    return unwrapped;
                }
            }

            return data;
        }

        private void BuildSubFiles()
        {
            SubFiles.Clear();
            for (int i = 0; i < textures.Count; i++)
            {
                if (CanConvertToDds(textures[i]))
                    SubFiles.Add(new GameFile($"{i:D3}.dds", new G1TextureProxy(this, i)));
                else
                    SubFiles.Add(new GameFile($"{i:D3}.bin", new DAT(textures[i].Payload ?? new byte[0])));
            }
        }

        private byte[] BuildDdsBytes(G1TextureEntry entry)
        {
            if (!TryGetDdsLayout(entry.TypeCode, out bool compressed, out uint fourCC, out bool bgra))
                return null;

            int width = 1 << entry.Dx;
            int height = 1 << entry.Dy;
            int mipCount = Math.Max(1, (int)entry.MipMapCount);
            byte[] payload = entry.Payload ?? new byte[0];

            using (var ms = new MemoryStream())
            using (var writer = IOTools.OpenWriteFile(ms, true))
            {
                writer.Write(DdsMagic);
                writer.Write(124u); // size

                uint flags = DdsHeaderFlagsTexture | (compressed ? DdsHeaderFlagsLinearSize : DdsHeaderFlagsPitch);
                if (mipCount > 1)
                    flags |= DdsHeaderFlagsMipmap;
                writer.Write(flags);
                writer.Write((uint)height);
                writer.Write((uint)width);

                uint topLevelSize;
                if (compressed)
                {
                    int blockSize = fourCC == 0x31545844 ? 8 : 16; // DXT1 = 8, DXT3/5 = 16
                    topLevelSize = (uint)(Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * blockSize);
                }
                else
                {
                    topLevelSize = (uint)(width * 4);
                }
                writer.Write(topLevelSize);
                writer.Write(0u); // depth
                writer.Write((uint)(mipCount > 1 ? mipCount : 0));

                for (int i = 0; i < 11; i++)
                    writer.Write(0u);

                writer.Write(32u); // pixel format size
                if (compressed)
                {
                    writer.Write(DdsPfFourCC);
                    writer.Write(fourCC);
                    writer.Write(0u);
                    writer.Write(0u);
                    writer.Write(0u);
                    writer.Write(0u);
                    writer.Write(0u);
                }
                else
                {
                    writer.Write(DdsPfRgba);
                    writer.Write(0u); // fourCC
                    writer.Write(32u);
                    if (bgra)
                    {
                        writer.Write(0x00ff0000u);
                        writer.Write(0x0000ff00u);
                        writer.Write(0x000000ffu);
                    }
                    else
                    {
                        writer.Write(0x000000ffu);
                        writer.Write(0x0000ff00u);
                        writer.Write(0x00ff0000u);
                    }
                    writer.Write(0xff000000u);
                }

                uint caps = DdsSurfaceFlagsTexture;
                if (mipCount > 1)
                    caps |= DdsSurfaceFlagsMipmap;
                writer.Write(caps);
                writer.Write(0u);
                writer.Write(0u);
                writer.Write(0u);
                writer.Write(0u);

                writer.Write(payload);
                return ms.ToArray();
            }
        }

        private void ApplyDdsBytesToTexture(int index, byte[] ddsBytes)
        {
            if (ddsBytes == null || ddsBytes.Length < 128)
                throw new InvalidDataException("G1T: invalid DDS data.");

            using (var ms = new MemoryStream(ddsBytes))
            using (var reader = IOTools.OpenReadFile(ms, true))
            {
                if (reader.ReadUInt32() != DdsMagic)
                    throw new InvalidDataException("G1T: replacement texture is not DDS.");

                reader.ReadUInt32(); // header size
                reader.ReadUInt32(); // flags
                uint height = reader.ReadUInt32();
                uint width = reader.ReadUInt32();
                reader.ReadUInt32(); // pitch/linear size
                reader.ReadUInt32(); // depth
                uint mipCount = reader.ReadUInt32();
                if (mipCount == 0)
                    mipCount = 1;

                reader.BaseStream.Position += 11 * 4;
                uint pfSize = reader.ReadUInt32();
                uint pfFlags = reader.ReadUInt32();
                uint fourCC = reader.ReadUInt32();
                uint rgbBitCount = reader.ReadUInt32();
                uint rMask = reader.ReadUInt32();
                uint gMask = reader.ReadUInt32();
                uint bMask = reader.ReadUInt32();
                uint aMask = reader.ReadUInt32();
                reader.BaseStream.Position += 5 * 4;

                if (fourCC == 0x30315844) // DX10
                    throw new NotSupportedException("G1T: DX10 DDS replacement is not supported.");
                if (!IsPowerOfTwo((int)width) || !IsPowerOfTwo((int)height))
                    throw new NotSupportedException("G1T: replacement DDS must use power-of-two dimensions.");

                byte newType;
                if ((pfFlags & DdsPfFourCC) != 0)
                {
                    if (fourCC == 0x31545844) // DXT1
                        newType = 0x59;
                    else if (fourCC == 0x33545844) // DXT3
                        newType = 0x5A;
                    else if (fourCC == 0x35545844) // DXT5
                        newType = 0x5B;
                    else
                        throw new NotSupportedException("G1T: unsupported DDS FOURCC for replacement.");
                }
                else
                {
                    if (rgbBitCount != 32 || aMask != 0xff000000)
                        throw new NotSupportedException("G1T: only 32-bit RGBA DDS replacement is supported.");
                    if (rMask == 0x00ff0000 && gMask == 0x0000ff00 && bMask == 0x000000ff)
                        newType = 0x01;
                    else if (rMask == 0x000000ff && gMask == 0x0000ff00 && bMask == 0x00ff0000)
                        newType = 0x02;
                    else
                        throw new NotSupportedException("G1T: unsupported DDS channel mask layout.");
                }

                var entry = textures[index];
                entry.TypeCode = newType;
                entry.MipMapCount = (byte)Math.Min(0x0F, (int)mipCount);
                entry.Dx = IntLog2((int)width);
                entry.Dy = IntLog2((int)height);
                entry.Payload = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
            }
        }

        private bool CanConvertToDds(G1TextureEntry entry)
        {
            return TryGetDdsLayout(entry.TypeCode, out _, out _, out _);
        }

        private static bool TryGetDdsLayout(byte typeCode, out bool compressed, out uint fourCC, out bool bgra)
        {
            compressed = false;
            fourCC = 0;
            bgra = true;

            switch (typeCode)
            {
                case 0x01:
                    compressed = false;
                    bgra = true;
                    return true;
                case 0x02:
                    compressed = false;
                    bgra = false;
                    return true;
                case 0x06:
                case 0x10:
                case 0x59:
                case 0x60:
                    compressed = true;
                    fourCC = 0x31545844; // DXT1
                    return true;
                case 0x07:
                case 0x11:
                case 0x5A:
                case 0x61:
                    compressed = true;
                    fourCC = 0x33545844; // DXT3
                    return true;
                case 0x08:
                case 0x12:
                case 0x5B:
                case 0x62:
                    compressed = true;
                    fourCC = 0x35545844; // DXT5
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;

        private static int IntLog2(int value)
        {
            int result = 0;
            while (value > 1)
            {
                value >>= 1;
                result++;
            }
            return result;
        }

        private class G1TextureProxy : IGameData, IImage
        {
            private readonly G1T owner;
            private readonly int index;

            public G1TextureProxy(G1T owner, int index)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
                this.index = index;
            }

            public FormatEnum Type => FormatEnum.DDS;

            public List<GameFile> SubFiles { get; } = new List<GameFile>();

            public int GetSize()
            {
                var data = owner.BuildDdsBytes(owner.textures[index]);
                return data?.Length ?? 0;
            }

            public byte[] GetData()
            {
                var data = owner.BuildDdsBytes(owner.textures[index]);
                if (data == null)
                    throw new NotSupportedException("G1T: unsupported texture type for DDS conversion.");
                return data;
            }

            public Bitmap GetBitmap()
            {
                var ddsBytes = GetData();
                return new DDS(ddsBytes).GetBitmap();
            }

            public void SetBitmap(Bitmap bitmap)
            {
                if (bitmap == null)
                    throw new ArgumentNullException(nameof(bitmap));

                var dds = new DDS(GetData());
                dds.SetBitmap(bitmap);
                owner.ApplyDdsBytesToTexture(index, dds.GetData());
            }
        }

        private class G1TextureEntry
        {
            public uint NormalMapFlags;
            public byte MipMapCount;
            public byte ZMipMapCount;
            public byte TypeCode;
            public int Dx;
            public int Dy;
            public uint Flags;
            public byte[] LocalExtraData;
            public byte[] Payload;
        }
    }
}
