using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public class QoSCacheFormatter : IQoSCacheFormatter
    {
        private static readonly BinaryFormatter BinaryFormatter = new BinaryFormatter();

        public byte[] SerializeInt32(int number)
        {
            using var mStream = new MemoryStream();
            BinaryFormatter.Serialize(mStream, number);

            return mStream.ToArray();
        }

        public int DeserializeInt32(byte[] byteArray)
        {
            using var mStream = new MemoryStream();
            mStream.Write(byteArray, 0, byteArray.Length);
            mStream.Position = 0;

            return (int)BinaryFormatter.Deserialize(mStream);
        }

        public byte[] SerializeNumber(long number)
        {
            using var mStream = new MemoryStream();
            BinaryFormatter.Serialize(mStream, number);

            return mStream.ToArray();
        }

        public long DeserializeNumber(byte[] byteArray)
        {
            using var mStream = new MemoryStream();
            mStream.Write(byteArray, 0, byteArray.Length);
            mStream.Position = 0;

            return (long)BinaryFormatter.Deserialize(mStream);
        }

        public byte[] SerializeArrayNumber(long[] numberArray)
        {
            using var mStream = new MemoryStream();
            BinaryFormatter.Serialize(mStream, numberArray);

            return mStream.ToArray();
        }

        public long[] DeserializeArrayNumber(byte[] byteArray)
        {
            using var mStream = new MemoryStream();
            mStream.Write(byteArray, 0, byteArray.Length);
            mStream.Position = 0;

            return BinaryFormatter.Deserialize(mStream) as long[];
        }
    }
}
