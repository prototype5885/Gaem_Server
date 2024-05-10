using System;
using System.Security.Cryptography;
using log4net;

namespace Gaem_server.Static;

public class EncryptionAes
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(EncryptionAes));
    
    private const byte keyLength = 16;
    public static bool encryptionEnabled = true;
    
    public static byte[] GenerateRandomKey(int length)
    {
        byte[] generatedKey = new byte[length];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(generatedKey); // fills the IV array with random bytes
        }
        return generatedKey;
    }

    public static byte[] Encrypt(byte[] messageBytes, byte[] encryptionKey)
    {
        try
        {
            // creates random IV byte array
            byte[] randomIv = GenerateRandomKey(16); // fills the IV array with random bytes

            using (Aes aes = Aes.Create())
            {
                aes.Key = encryptionKey;
                aes.IV = randomIv;

                using (MemoryStream msEncrypt = new())
                {
                    using (CryptoStream csEncrypt = new(msEncrypt, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        // encrypt the message
                        csEncrypt.Write(messageBytes, 0, messageBytes.Length);
                        csEncrypt.FlushFinalBlock();
                        byte[] encryptedMessage = msEncrypt.ToArray();

                        // combines the IV byte array and the encrypted message byte array, the IV array is first, message is second
                        byte[] encryptedBytesWithIV = new byte[encryptedMessage.Length +  keyLength]; // creates a byte array to store both the message and the iv
                        Array.Copy(randomIv, encryptedBytesWithIV, keyLength); // copies the IV to the beginning of the array
                        Array.Copy(encryptedMessage, 0, encryptedBytesWithIV, keyLength, encryptedMessage.Length); // copies the message after the IV
                        
                        // return the encrypted message as byte array
                        return encryptedBytesWithIV;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e.ToString());
            return null;
        }
    }

    public static byte[] Decrypt(byte[] encryptedMessageWithIv, byte[] encryptionKey)
    {
        try
        {
            // reads the first 16 bytes as IV
            byte[] extractedIv = new byte[keyLength]; // creates a byte array for the received IV
            Array.Copy(encryptedMessageWithIv, extractedIv, keyLength); // copies it in it

            // reads the rest of the byte array as message
            byte[] encryptedMessage = new byte[encryptedMessageWithIv.Length - keyLength]; // creates a byte array for the received message
            Array.Copy(encryptedMessageWithIv, keyLength, encryptedMessage, 0, encryptedMessage.Length); // copes it in it

            // decrypt using IV and key
            using (Aes aes = Aes.Create())
            {
                aes.Key = encryptionKey;
                aes.IV = extractedIv;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream msDecrypt = new(encryptedMessage))
                {
                    using (CryptoStream csDecrypt = new(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (MemoryStream msPlainText = new MemoryStream())
                        {
                            csDecrypt.CopyTo(msPlainText);
                            return msPlainText.ToArray();
                        }
                        // using (StreamReader srDecrypt = new(csDecrypt))
                        // {
                        //     return srDecrypt.ReadToEnd();
                        // }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e.ToString());
            return null;
        }
    }
}