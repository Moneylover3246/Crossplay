using System.Text;
using System.IO;

public class PacketFactory
{
    private MemoryStream memoryStream;
    public BinaryWriter writer;
    public PacketFactory(bool writeOffset = true)
    {
        memoryStream = new MemoryStream();
        writer = new BinaryWriter(memoryStream);
        if (writeOffset)
        {
            writer.BaseStream.Position = 3L;
        }
    }

    public PacketFactory SetType(short type)
    {
        long currentPosition = writer.BaseStream.Position;
        writer.BaseStream.Position = 2L;
        writer.Write(type);
        writer.BaseStream.Position = currentPosition;
        return this;
    }

    public PacketFactory PackBool(bool flag)
    {
        writer.Write(flag);
        return this;
    }

    public PacketFactory PackByte(byte num)
    {
        writer.Write(num);
        return this;
    }
    public PacketFactory PackSByte(sbyte num)
    {
        writer.Write(num);
        return this;
    }

    public PacketFactory PackInt16(short num)
    {
        writer.Write(num);
        return this;
    }

    public PacketFactory PackUInt16(ushort num)
    {
        writer.Write(num);
        return this;
    }

    public PacketFactory PackInt32(int num)
    {
        writer.Write(num);
        return this;
    }

    public PacketFactory PackUInt32(uint num)
    {
        writer.Write(num);
        return this;
    }

    public PacketFactory PackInt64(long num)
    {
        writer.Write(num);
        return this;
    }

    public PacketFactory PackUInt64(ulong num)
    {
        writer.Write(num);
        return this;
    }

    public PacketFactory PackSingle(float num)
    {
        writer.Write(num);
        return this;
    }

    public PacketFactory PackString(string str)
    {
        writer.Write(str);
        return this;
    }

    public PacketFactory PackBuffer(byte[] buffer)
    {
        writer.Write(buffer);
        return this;
    }

    private void UpdateLength()
    {
        long currentPosition = writer.BaseStream.Position;
        writer.BaseStream.Position = 0L;
        writer.Write((short)currentPosition);
        writer.BaseStream.Position = currentPosition;
    }

    public byte[] GetByteData()
    {
        UpdateLength();
        return memoryStream.ToArray();
    }

    public byte[] ToArray()
    {
        return memoryStream.ToArray();
    }
}