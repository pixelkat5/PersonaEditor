using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace PersonaEditorLib.Other
{
    public sealed class ABCFont
    {
        private const int LookupOffset = 0x18;
        private const int MaxCodePointOffset = 0x14;
        private const int GlyphSize = 24;
        private const int RectSize = 32;
        private const int RectXOffset = 0;
        private const int RectRightOffset = 4;
        private const int RectYOffset = 8;
        private const int RectBottomOffset = 12;
        private byte[] data;
        private readonly int lookupCount;
        private readonly int glyphCount;
        private readonly int glyphOffset;
        private readonly int rectOffset;
        private readonly int rectScale;
        private readonly GlyphCoordinateEncoding coordinateEncoding;
        private readonly int codePointBase;

        public ABCFont(byte[] data, int atlasWidth, int atlasHeight)
        {
            if (data == null || data.Length < 0x18)
                throw new ArgumentException("ABC font data is too small.", nameof(data));
            if (atlasWidth <= 0 || atlasHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(atlasWidth));

            this.data = data.AsSpan().ToArray();
            uint maxCodePoint = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(MaxCodePointOffset, 4));
            lookupCount = checked((int)maxCodePoint + 1);
            if (lookupCount <= 0)
                throw new ArgumentException("ABC lookup table length is invalid.", nameof(data));

            int lookupEnd = checked(LookupOffset + lookupCount * 2);
            if (lookupEnd > data.Length)
                throw new ArgumentException("ABC lookup table is incomplete.", nameof(data));

            glyphOffset = checked((lookupEnd + 3) & ~3);
            glyphCount = GetGlyphCount();
            rectOffset = checked(glyphOffset + glyphCount * GlyphSize);
            if (glyphCount > 0 && rectOffset > data.Length - glyphCount * RectSize)
                throw new ArgumentException("ABC glyph or rectangle table is incomplete.", nameof(data));

            AtlasWidth = atlasWidth;
            AtlasHeight = atlasHeight;
            rectScale = DetectRectScale();
            coordinateEncoding = DetectCoordinateEncoding();
            codePointBase = DetectCodePointBase();
            ValidateLookup();
        }

        public int AtlasWidth { get; }
        public int AtlasHeight { get; }
        public int MaxCodePoint => lookupCount - 1 + codePointBase;
        public int GlyphCount => glyphCount;
        public string SidecarPath { get; internal set; }

        public IEnumerable<ABCMappedGlyph> GetMappedGlyphs()
        {
            for (int slot = 0; slot < lookupCount; slot++)
            {
                ushort index = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(LookupOffset + slot * 2, 2));
                if (index != 0)
                    yield return new ABCMappedGlyph((char)(slot + codePointBase), GetGlyph(index));
            }
        }

        public ABCGlyph GetGlyph(int index)
        {
            int offset = GetGlyphOffset(index);
            int rect = GetRectOffset(index);

            int leftOffset = coordinateEncoding == GlyphCoordinateEncoding.NormalFloat ? offset + 16 : offset + 18;
            return new ABCGlyph(
                index,
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(leftOffset, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(leftOffset + 2, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(leftOffset + 4, 2)),
                coordinateEncoding == GlyphCoordinateEncoding.NormalFloat
                    ? BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 22, 2))
                    : 0,
                ReadRectCoord(rect + RectXOffset),
                ReadRectCoord(rect + RectYOffset),
                ReadRectCoord(rect + RectRightOffset),
                ReadRectCoord(rect + RectBottomOffset));
        }

        public void SetGlyph(ABCGlyph glyph)
        {
            if (glyph == null)
                throw new ArgumentNullException(nameof(glyph));
            if (glyph.X < 0 || glyph.Y < 0 || glyph.Right <= glyph.X || glyph.Bottom <= glyph.Y ||
                glyph.Right > AtlasWidth || glyph.Bottom > AtlasHeight)
                throw new ArgumentOutOfRangeException(nameof(glyph), "Glyph rectangle is outside the HIP atlas.");

            int offset = GetGlyphOffset(glyph.Index);
            if (coordinateEncoding == GlyphCoordinateEncoding.NormalFloat)
            {
                WriteSingle(offset, (float)glyph.X / AtlasWidth);
                WriteSingle(offset + 4, (float)glyph.Y / AtlasHeight);
                WriteSingle(offset + 8, (float)glyph.Right / AtlasWidth);
                WriteSingle(offset + 12, (float)glyph.Bottom / AtlasHeight);
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 16, 2), checked((short)glyph.Left));
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 18, 2), checked((short)glyph.Width));
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 20, 2), checked((short)glyph.Advance));
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 22, 2), checked((short)glyph.Top));
            }
            else
            {
                WritePackedFloatHighWord(offset + 4, (float)glyph.X / AtlasWidth);
                WritePackedFloatHighWord(offset + 8, (float)glyph.Y / AtlasHeight);
                WritePackedFloatHighWord(offset + 12, (float)glyph.Right / AtlasWidth);
                WritePackedFloatHighWord(offset + 16, (float)glyph.Bottom / AtlasHeight);
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 18, 2), checked((short)glyph.Left));
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 20, 2), checked((short)glyph.Width));
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 22, 2), checked((short)glyph.Advance));
            }

            int rect = GetRectOffset(glyph.Index);
            WriteRectCoord(rect + RectXOffset, glyph.X);
            WriteRectCoord(rect + RectRightOffset, glyph.Right);
            WriteRectCoord(rect + RectYOffset, glyph.Y);
            WriteRectCoord(rect + RectBottomOffset, glyph.Bottom);
        }

        public int GetGlyphIndex(int codePoint)
        {
            int slot = GetLookupSlot(codePoint);
            return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(LookupOffset + slot * 2, 2));
        }

        public void SetGlyphMapping(int codePoint, int glyphIndex, bool clearExistingMappings)
        {
            int slot = GetLookupSlot(codePoint);
            GetGlyphOffset(glyphIndex);

            if (clearExistingMappings)
                ClearGlyphMappings(glyphIndex);

            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(LookupOffset + slot * 2, 2), checked((ushort)glyphIndex));
        }

        public void ClearGlyphMappings(int glyphIndex)
        {
            GetGlyphOffset(glyphIndex);

            for (int i = 0; i < lookupCount; i++)
            {
                int offset = LookupOffset + i * 2;
                if (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2)) == glyphIndex)
                    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), 0);
            }
        }

        public byte[] GetData() => data.AsSpan().ToArray();

        private int GetGlyphCount()
        {
            int max = 0;
            for (int i = 0; i < lookupCount; i++)
                max = Math.Max(max, BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(LookupOffset + i * 2, 2)));
            return max;
        }

        private int DetectRectScale()
        {
            uint limit = (uint)Math.Max(AtlasWidth, AtlasHeight) * 4u;
            for (int index = 1; index <= glyphCount; index++)
            {
                int rect = GetRectOffset(index);
                uint max = Math.Max(
                    Math.Max(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(rect + RectXOffset, 4)),
                        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(rect + RectRightOffset, 4))),
                    Math.Max(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(rect + RectYOffset, 4)),
                        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(rect + RectBottomOffset, 4))));

                if (max > limit)
                    return 0x10000;
            }

            return 1;
        }

        private GlyphCoordinateEncoding DetectCoordinateEncoding()
        {
            int index = 0;
            for (int i = 0; i < lookupCount && index == 0; i++)
                index = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(LookupOffset + i * 2, 2));

            if (index == 0)
                return GlyphCoordinateEncoding.NormalFloat;

            int offset = GetGlyphOffset(index);
            int rect = GetRectOffset(index);
            double x = (double)ReadRectCoord(rect + RectXOffset) / AtlasWidth;
            double y = (double)ReadRectCoord(rect + RectYOffset) / AtlasHeight;
            double right = (double)ReadRectCoord(rect + RectRightOffset) / AtlasWidth;
            double bottom = (double)ReadRectCoord(rect + RectBottomOffset) / AtlasHeight;

            double normalError = CoordinateError(
                ReadSingle(offset), ReadSingle(offset + 4), ReadSingle(offset + 8), ReadSingle(offset + 12),
                x, y, right, bottom);
            double packedError = CoordinateError(
                ReadPackedFloatHighWord(offset + 4), ReadPackedFloatHighWord(offset + 8),
                ReadPackedFloatHighWord(offset + 12), ReadPackedFloatHighWord(offset + 16),
                x, y, right, bottom);

            return packedError < normalError ? GlyphCoordinateEncoding.PackedHighWordFloat : GlyphCoordinateEncoding.NormalFloat;
        }

        private int DetectCodePointBase()
        {
            if (lookupCount <= 0x20)
                return 0;

            ushort code1F = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(LookupOffset + 0x1E * 2, 2));
            ushort code20 = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(LookupOffset + 0x1F * 2, 2));
            ushort code21 = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(LookupOffset + 0x20 * 2, 2));

            return code1F == 0 && code20 == 1 && code21 == 2 ? 1 : 0;
        }

        private int ReadRectCoord(int offset)
            => checked((int)Math.Round((double)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4)) / rectScale));

        private void WriteRectCoord(int offset, int value)
            => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), checked((uint)((long)value * rectScale)));

        private void ValidateLookup()
        {
            for (int i = 0; i < lookupCount; i++)
            {
                ushort index = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(LookupOffset + i * 2, 2));
                if (index != 0)
                {
                    GetGlyphOffset(index);
                    GetRectOffset(index);
                }
            }
        }

        private int GetGlyphOffset(int index)
        {
            int offset = checked(glyphOffset + (index - 1) * GlyphSize);
            if (index <= 0 || index > glyphCount || offset < glyphOffset || offset > data.Length - GlyphSize)
                throw new ArgumentException($"ABC glyph index {index} is outside the file.");
            return offset;
        }

        private int GetRectOffset(int index)
        {
            int offset = checked(rectOffset + (index - 1) * RectSize);
            if (index <= 0 || index > glyphCount || offset < rectOffset || offset > data.Length - RectSize)
                throw new ArgumentException($"ABC rectangle index {index} is outside the file.");
            return offset;
        }

        private int GetLookupSlot(int codePoint)
        {
            int slot = codePoint - codePointBase;
            if (slot < 0 || slot >= lookupCount)
                throw new ArgumentOutOfRangeException(nameof(codePoint), $"U+{codePoint:X4} is outside this ABC lookup table.");

            return slot;
        }

        private float ReadSingle(int offset) => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4)));
        private void WriteSingle(int offset, float value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), BitConverter.SingleToInt32Bits(value));
        private float ReadPackedFloatHighWord(int offset)
            => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2)) << 16);
        private void WritePackedFloatHighWord(int offset, float value)
        {
            int bits = BitConverter.SingleToInt32Bits(value);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), (ushort)(bits >> 16));
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 2, 2), 0);
        }

        private static double CoordinateError(float u0, float v0, float u1, float v1, double x, double y, double right, double bottom)
            => Math.Abs(u0 - x) + Math.Abs(v0 - y) + Math.Abs(u1 - right) + Math.Abs(v1 - bottom);

        private enum GlyphCoordinateEncoding
        {
            NormalFloat,
            PackedHighWordFloat
        }
    }

    public sealed class ABCMappedGlyph
    {
        public ABCMappedGlyph(char character, ABCGlyph glyph)
        {
            Character = character;
            Glyph = glyph;
        }

        public char Character { get; }
        public ABCGlyph Glyph { get; }
    }

    public sealed class ABCGlyph
    {
        public ABCGlyph(int index, int left, int width, int advance, int top, int x, int y, int right, int bottom)
        {
            Index = index;
            Left = left;
            Width = width;
            Advance = advance;
            Top = top;
            X = x;
            Y = y;
            Right = right;
            Bottom = bottom;
        }

        public int Index { get; }
        public int Left { get; set; }
        public int Width { get; set; }
        public int Advance { get; set; }
        public int Top { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
    }
}
