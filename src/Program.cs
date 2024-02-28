
using System.Security.Cryptography;
using System.Text;

class Program
{
    private static void Main()
    {
        byte maxPlayers = 10;
        int port = 1942;
        Server server = new Server(maxPlayers, port);

        //byte[] key = new byte[32]; // 256 bits = 32 bytes
        //using (var rng = new RNGCryptoServiceProvider())
        //{
        //    rng.GetBytes(key);
        //}

        //AesEncryption aes = new AesEncryption();

        //string message = "wtf";
        //byte[] key = Encoding.UTF8.GetBytes("0123456789abcdef"); // 16-byte key for AES-128

        //byte[] encryptedBytes = aes.Encrypt(message);
        //if (encryptedBytes == null) return;

        //string roundtrip = aes.Decrypt(encryptedBytes);
        //if (roundtrip == null) return;
        //Console.WriteLine($"Decrypted message: {roundtrip}");

    }
}
