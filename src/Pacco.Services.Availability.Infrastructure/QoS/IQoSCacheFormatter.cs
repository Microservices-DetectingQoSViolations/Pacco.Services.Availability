namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public interface IQoSCacheFormatter
    {
        byte[] SerializeInt32(int number);
        int DeserializeInt32(byte[] byteArray);

        byte[] SerializeNumber(long number);
        long DeserializeNumber(byte[] byteArray);

        byte[] SerializeArrayNumber(long[] numberArray);
        long[] DeserializeArrayNumber(byte[] byteArray);
    }
}