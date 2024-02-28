using System.IO;
using System.Security.Cryptography;
using System.Text;

public class AesEncryption
{
    const byte ivLength = 16;

    byte[] key = Encoding.UTF8.GetBytes("0123456789abcdef");
    public byte[] Encrypt(string message)
    {
        try
        {
            byte[] unencryptedBytes = Encoding.UTF8.GetBytes(message);

            byte[] iv;
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())// Generate a random IV (Initialization Vector)
            {
                iv = new byte[16]; // Generate random 16 bytes for IV
                rng.GetBytes(iv);
            }

            //Console.Write("IV generated: ");
            //foreach (byte b in iv)
            //{
            //    Console.Write(b);
            //}
            //Console.WriteLine();

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(unencryptedBytes, 0, unencryptedBytes.Length);
                        csEncrypt.FlushFinalBlock();
                        byte[] encryptedMessage = msEncrypt.ToArray();

                        byte[] encryptedBytesWithIV = new byte[encryptedMessage.Length + iv.Length]; // Create a new byte array to store the combined data (original data + IV)
                        Array.Copy(encryptedMessage, encryptedBytesWithIV, encryptedMessage.Length); // Copy the original data to the combined data array
                        Array.Copy(iv, 0, encryptedBytesWithIV, encryptedMessage.Length, iv.Length);  // Copy the IV to the combined data array, starting from the end of the original data

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
            // Extract the IV from the combinedData
            byte[] iv = new byte[ivLength];
            Array.Copy(encryptedMessageWithIV, encryptedMessageWithIV.Length - ivLength, iv, 0, ivLength);

            // Remove the IV from the combinedData to get the original data
            byte[] encryptedMessage = new byte[encryptedMessageWithIV.Length - ivLength];
            Array.Copy(encryptedMessageWithIV, encryptedMessage, encryptedMessage.Length);

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

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