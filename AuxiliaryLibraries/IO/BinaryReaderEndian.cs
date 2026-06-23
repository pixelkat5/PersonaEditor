using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace AuxiliaryLibraries.IO
{
    public class BinaryReaderEndian : BinaryReader
    {
        public BinaryReaderEndian(Stream stream) : base(stream) { }

        public BinaryReaderEndian(Stream stream, Encoding encoding, bool leaveOpen) : base(stream, encoding, leaveOpen) { }

        public override short ReadInt16()
        {
            Span<byte> data = stackalloc byte[2];
            BaseStream.ReadExactly(data);
            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReadInt16BigEndian(data) : BinaryPrimitives.ReadInt16LittleEndian(data);
        }

        public override ushort ReadUInt16()
        {
            Span<byte> data = stackalloc byte[2];
            BaseStream.ReadExactly(data);
            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReadUInt16BigEndian(data) : BinaryPrimitives.ReadUInt16LittleEndian(data);
        }

        public override int ReadInt32()
        {
            Span<byte> data = stackalloc byte[4];
            BaseStream.ReadExactly(data);
            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReadInt32BigEndian(data) : BinaryPrimitives.ReadInt32LittleEndian(data);
        }

        public override uint ReadUInt32()
        {
            Span<byte> data = stackalloc byte[4];
            BaseStream.ReadExactly(data);
            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReadUInt32BigEndian(data) : BinaryPrimitives.ReadUInt32LittleEndian(data);
        }

        public override long ReadInt64()
        {
            Span<byte> data = stackalloc byte[8];
            BaseStream.ReadExactly(data);
            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReadInt64BigEndian(data) : BinaryPrimitives.ReadInt64LittleEndian(data);
        }

        public override ulong ReadUInt64()
        {
            Span<byte> data = stackalloc byte[8];
            BaseStream.ReadExactly(data);
            return BitConverter.IsLittleEndian ? BinaryPrimitives.ReadUInt64BigEndian(data) : BinaryPrimitives.ReadUInt64LittleEndian(data);
        }
    }
}
