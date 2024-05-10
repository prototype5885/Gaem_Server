using System.Security.Cryptography;
using log4net;

namespace Gaem_server.Static;

public static class ByteProcessor
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(ByteProcessor));

    public static void PrintByteArrayAsHex(byte[] byteArray)
    {
        string bytes = "";
        foreach (byte b in byteArray)
        {
            bytes = bytes + ($"{b:X2} ");
        }
        logger.Debug(bytes);
    }

    public static List<byte[]> PartitionByteArray(byte[] array, int partitionSize)
    {
        int numOfPartitions = (int)Math.Ceiling((double)array.Length / partitionSize);
        logger.Debug($"Number of partitions: {numOfPartitions}");

        List<byte[]> partitions = new List<byte[]>();
        for (int i = 0; i < numOfPartitions; i++)
        {
            int start = i * partitionSize;
            int end = Math.Min(start + partitionSize, array.Length);

            int partitionLength = end - start;
            byte[] partition = new byte[partitionLength];
            Array.Copy(array, start, partition, 0, partitionLength);

            partitions.Add(partition);
        }
        return partitions;
    }

    public static byte[] ReconstructByteArray(byte[][] partitions)
    {
        byte[] buffer = new byte[1024];
        int currentIndex = 0;
        foreach (byte[] partition in partitions)
        {
            Array.ConstrainedCopy(partition, 0, buffer, currentIndex, partition.Length);
            currentIndex += partition.Length;
        }
        byte[] reconstructedArray = new byte[currentIndex];
        Array.Copy(buffer, reconstructedArray, currentIndex);

        return reconstructedArray;
    }

    public static byte[] GenerateRandomByteArray(int length)
    {
        byte[] randomKey = new byte[length];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomKey);
        }
        return randomKey;
    }
}

