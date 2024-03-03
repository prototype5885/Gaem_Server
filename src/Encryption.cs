using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class Encryption
{
    const byte ivLength = 16;
    public static byte[] Encrypt(string message, byte[] encryptionKey)
    {
        try
        {
            byte[] unencryptedBytes = Encoding.ASCII.GetBytes(message); // turns the string into byte array
            byte[] randomIV = new byte[16]; // creates a byte array of 16 length for IV

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomIV); // fills the IV array with random bytes
            }

            using (Aes aes = Aes.Create())
            {
                aes.Key = encryptionKey;
                aes.IV = randomIV;

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(unencryptedBytes, 0, unencryptedBytes.Length);
                        csEncrypt.FlushFinalBlock();
                        byte[] encryptedMessage = msEncrypt.ToArray();

                        byte[] encryptedBytesWithIV = new byte[encryptedMessage.Length + ivLength]; // creates a byte array to store both the message and the iv
                        Array.Copy(randomIV, encryptedBytesWithIV, ivLength); // copies the IV to the beginning of the array
                        Array.Copy(encryptedMessage, 0, encryptedBytesWithIV, ivLength, encryptedMessage.Length); // copies the message after the IV

                        return encryptedBytesWithIV;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return null;
        }
    }
    public static string Decrypt(byte[] encryptedMessageWithIV, byte[] encryptionKey)
    {
        try
        {
            byte[] extractedIV = new byte[ivLength]; // creates a byte array for the received IV
            Array.Copy(encryptedMessageWithIV, extractedIV, ivLength); // copies it in it

            byte[] encryptedMessage = new byte[encryptedMessageWithIV.Length - ivLength]; // creates a byte array for the received message
            Array.Copy(encryptedMessageWithIV, ivLength, encryptedMessage, 0, encryptedMessage.Length); // copes it in it

            using (Aes aes = Aes.Create())
            {
                aes.Key = encryptionKey;
                aes.IV = extractedIV;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream msDecrypt = new MemoryStream(encryptedMessage))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return null;
        }
    }
}