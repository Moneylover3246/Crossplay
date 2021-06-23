using System.IO;

namespace Crossplay
{
    public static class BinaryReaderExt
    {
        public static byte[] ReadToEnd(this BinaryReader reader)
        {
            return reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
        }
    }
}
