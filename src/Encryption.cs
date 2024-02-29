using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public class AesEncryption
{
    const byte ivLength = 16;

    byte[] key = new byte[16];

    public AesEncryption()
    {
        string path = "encryption_key.txt";

        if (!File.Exists(path))
        {
            File.Create(path).Dispose();

            using (TextWriter writer = new StreamWriter(path))
            {
                writer.WriteLine("0123456789abcdef"); // default encryption key
                writer.Close();
            }

        }
        else if (File.Exists(path))
        {
            using (TextReader reader = new StreamReader(path))
            {
                string keyString = reader.ReadLine();
                Console.WriteLine(keyString);
                key = Encoding.ASCII.GetBytes(keyString);
                reader.Close();
            }
        }
    }

    public byte[] Encrypt(string message)
    {
        try
        {
            byte[] unencryptedBytes = Encoding.ASCII.GetBytes(message);

            byte[] randomIV = new byte[16]; // creates a byte array of 16 length for IV

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomIV); // fills the IV array with random bytes
            }

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
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
    public string Decrypt(byte[] encryptedMessageWithIV)
    {
        try
        {
            byte[] extractedIV = new byte[ivLength]; // creates a byte array for the received IV
            Array.Copy(encryptedMessageWithIV, extractedIV, ivLength); // copies it in it

            byte[] encryptedMessage = new byte[encryptedMessageWithIV.Length - ivLength]; // creates a byte array for the received message
            Array.Copy(encryptedMessageWithIV, ivLength, encryptedMessage, 0, encryptedMessage.Length); // copes it in it

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
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