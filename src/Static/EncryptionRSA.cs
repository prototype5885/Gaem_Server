using System.Security.Cryptography;
using log4net;

namespace Gaem_server.Static;

public static class EncryptionRsa
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(EncryptionRsa));

    private static RSAParameters publicKey;
    private static RSAParameters privateKey;

    public static void Initialize()
    {
        using RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(4096);
        publicKey = rsa.ExportParameters(false);
        privateKey = rsa.ExportParameters(true);
        logger.Debug("Generated RSA keypair");
    }

    public static byte[] Encrypt(byte[] messageBytes, RSAParameters publicKey)
    {
        using RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        rsa.ImportParameters(publicKey);
        return rsa.Encrypt(messageBytes, false);
    }

    public static byte[] Decrypt(byte[] encryptedBytes)
    {
        using RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        rsa.ImportParameters(privateKey);
        return rsa.Decrypt(encryptedBytes, false);
    }

    public static byte[] PublicKeyToByteArray()
    {
        using RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        rsa.ImportParameters(publicKey);
        byte[] publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
        logger.Debug($@"Public key length: {publicKeyBytes.Length}");
        return publicKeyBytes;
    }

    private static RSAParameters ByteArrayToPublicKey(byte[] serverPublicRsaKeyBytes)
    {
        using RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        rsa.ImportSubjectPublicKeyInfo(serverPublicRsaKeyBytes, out _);
        RSAParameters publickey = rsa.ExportParameters(false);
        return publickey;
    }
}

