using System.Text;
using System.Text.Json;
using Gaem_server.Classes;
using Gaem_server.ClassesShared;
using Gaem_server.src.ClassesShared;
using log4net;
using ChatMessageContext = Gaem_server.ClassesShared.ChatMessageContext;
using InitialDataContext = Gaem_server.ClassesShared.InitialDataContext;

namespace Gaem_server.Static;

public static class PacketProcessor
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(PacketProcessor));
    public static byte[] MakePacketForSending(int type, object obj, byte[] aesKey)
    {
        byte[] jsonBytes = [];
        switch (type)
        {
            case 1:
                jsonBytes = JsonSerializer.SerializeToUtf8Bytes(obj, InitialDataContext.Default.InitialData);
                break;
            case 20:
                jsonBytes = JsonSerializer.SerializeToUtf8Bytes(obj, PlayerDataContext.Default.PlayerData);
                break;
            case 21:
                jsonBytes = JsonSerializer.SerializeToUtf8Bytes(obj, PlayerDataArrayContext.Default.PlayerDataArray);
                break;
            case 30:
                jsonBytes = JsonSerializer.SerializeToUtf8Bytes(obj, ChatMessageContext.Default.ChatMessage);
                break;
        }
        Console.WriteLine($"Serialized json: {Encoding.UTF8.GetString(jsonBytes)}");
        // ByteProcessor.PrintByteArrayAsHex(jsonBytes);

        jsonBytes = AppendPacketType(type, jsonBytes);
        // Console.WriteLine($"Added packet type: {Encoding.UTF8.GetString(jsonBytes)}");
        //  ByteProcessor.PrintByteArrayAsHex(jsonBytes);

        if (EncryptionAes.encryptionEnabled) jsonBytes = EncryptionAes.Encrypt(jsonBytes, aesKey);
        // Console.WriteLine($"Encrypted: {Encoding.UTF8.GetString(jsonBytes)}");
        //  ByteProcessor.PrintByteArrayAsHex(jsonBytes);

        jsonBytes = AppendLengthToBeginning(jsonBytes);
        // Console.WriteLine($"Added length: {Encoding.UTF8.GetString(jsonBytes)}");
        //  ByteProcessor.PrintByteArrayAsHex(jsonBytes);

        return jsonBytes;
    }

    private static byte[] AppendPacketType(int packetType, byte[] jsonBytes)
    {
        // stores int in a 1 byte length array
        byte[] arrayThatHoldsPacketType = new byte[1];
        arrayThatHoldsPacketType[0] = (byte)packetType;

        // creates an array that will hold both the length and the encrypted message
        byte[] jsonWithType = new byte[1 + jsonBytes.Length];

        // copies the 1 byte packet type value array to the beginning
        Array.Copy(arrayThatHoldsPacketType, jsonWithType, 1);

        // copies the message to after the second byte
        Array.ConstrainedCopy(jsonBytes, 0, jsonWithType, 1, jsonBytes.Length);

        return jsonWithType;
    }

    private static byte[] AppendLengthToBeginning(byte[] encryptedMessageBytes)
    {
        // stores int in a 2 byte length array
        byte[] arrayThatHoldsLength = new byte[2];
        arrayThatHoldsLength[0] = (byte)(encryptedMessageBytes.Length >> 8);
        arrayThatHoldsLength[1] = (byte)encryptedMessageBytes.Length;

        // creates an array that will hold both the length and the encrypted message
        byte[] mergedArray = new byte[encryptedMessageBytes.Length + 2];

        // copies the 2 byte length holder array to the beginning
        Array.Copy(arrayThatHoldsLength, mergedArray, 2);

        // copies the encrypted message to after the second byte
        Array.ConstrainedCopy(encryptedMessageBytes, 0, mergedArray, 2, encryptedMessageBytes.Length);

        return mergedArray;
    }

    public static List<Packet> ProcessReceivedBytes(byte[] receivedBytes, Player packetOwner)
    {
        // the list that will hold the separated encrypted packets
        List<Packet> packets = new List<Packet>();

        int currentIndex = 0;
        int foundPackets = 0;
        while (currentIndex < receivedBytes.Length)
        {
            Packet packet = new Packet();
            packet.owner = packetOwner;

            // creates a 2 byte length array that stores the value read from the first 2 bytes of the given array
            byte[] arrayThatHoldsLength = new byte[2];
            Array.ConstrainedCopy(receivedBytes, currentIndex, arrayThatHoldsLength, 0, 2);

            // reads the int from the 2 byte length holder array
            int length = 0;
            length |= (arrayThatHoldsLength[0] & 0xFF) << 8;
            length |= (arrayThatHoldsLength[1] & 0xFF);
            logger.Debug($"Received packet length: {length}");

            // separate the packet part from the length
            byte[] packetBytes = new byte[length];
            Array.ConstrainedCopy(receivedBytes, currentIndex + 2, packetBytes, 0, length);
            logger.Debug("Separated packet from length:");
            ByteProcessor.PrintByteArrayAsHex(packetBytes);

            // decrypt if encrypted
            if (EncryptionAes.encryptionEnabled)  // if encryption is enabled
            {
                packetBytes = EncryptionAes.Decrypt(packetBytes, packetOwner.aesKey);
            }
            logger.Debug("Decrypted packet:");
            ByteProcessor.PrintByteArrayAsHex(packetBytes);

            // read the first byte to get packet type
            packet.type = packetBytes[0] & 0xFF;
            int packetLength = packetBytes.Length - 1;
            logger.Debug($"Packet type is: {packet.type}");

            // read the rest of the byte array
            byte[] jsonBytes = new byte[packetLength];
            Array.ConstrainedCopy(packetBytes, 1, jsonBytes, 0, packetLength);

            // decode into json string
            packet.json = Encoding.UTF8.GetString(jsonBytes);
            logger.Debug($"Json: {packet.json}");

            packets.Add(packet);

            logger.Debug($"Separated packet, start index: {currentIndex}, length: {packetLength}");
            currentIndex += length + 2;
            foundPackets++;
        }
        if (foundPackets > 1)
        {
            logger.Debug($"Multiple packets were received as one, packets: {foundPackets}");
        }
        return packets;
    }
}
