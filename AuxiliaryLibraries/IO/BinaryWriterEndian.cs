using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace AuxiliaryLibraries.IO
{
    public class BinaryWriterEndian : BinaryWriter
    {
        public BinaryWriterEndian(Stream stream) : base(stream) { }

        public BinaryWriterEndian(Stream stream, Encoding encoding, bool leaveOpen) : base(stream, encoding, leaveOpen) { }

        public override void Write(short value)
        {
            Span<byte> data = stackalloc byte[2];
            if (BitConverter.IsLittleEndian)
                BinaryPrimitives.WriteInt16BigEndian(data, value);
            else
                BinaryPrimitives.WriteInt16LittleEndian(data, value);
            base.Write(data);
        }

        public override void Write(ushort value)
        {
            Span<byte> data = stackalloc byte[2];
            if (BitConverter.IsLittleEndian)
                BinaryPrimitives.WriteUInt16BigEndian(data, value);
            else
                BinaryPrimitives.WriteUInt16LittleEndian(data, value);
            base.Write(data);
        }

        public override void Write(int value)
        {
            Span<byte> data = stackalloc byte[4];
            if (BitConverter.IsLittleEndian)
                BinaryPrimitives.WriteInt32BigEndian(data, value);
            else
                BinaryPrimitives.WriteInt32LittleEndian(data, value);
            base.Write(data);
        }

        public override void Write(uint value)
        {
            Span<byte> data = stackalloc byte[4];
            if (BitConverter.IsLittleEndian)
                BinaryPrimitives.WriteUInt32BigEndian(data, value);
            else
                BinaryPrimitives.WriteUInt32LittleEndian(data, value);
            base.Write(data);
        }

        public override void Write(long value)
        {
            Span<byte> data = stackalloc byte[8];
            if (BitConverter.IsLittleEndian)
                BinaryPrimitives.WriteInt64BigEndian(data, value);
            else
                BinaryPrimitives.WriteInt64LittleEndian(data, value);
            base.Write(data);
        }

        public override void Write(ulong value)
        {
            Span<byte> data = stackalloc byte[8];
            if (BitConverter.IsLittleEndian)
                BinaryPrimitives.WriteUInt64BigEndian(data, value);
            else
                BinaryPrimitives.WriteUInt64LittleEndian(data, value);
            base.Write(data);
        }
    }
}
