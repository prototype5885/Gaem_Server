using System.Text;
using System.Security.Cryptography;
using BCrypt.Net;

public class PasswordHasher
{
    public static string HashPassword(string password)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            // Compute the hash of the password
            byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));

            // Convert the byte array to a hexadecimal string
            return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
        }
    }
    public static bool ValidateCredentials(string enteredPassword, string storedHashedPassword)
    {
        string enteredHashedPassword = HashPassword(enteredPassword);
        return enteredHashedPassword == storedHashedPassword;
    }
    public string EncryptPassword(string hashedPassword)
    {
        return BCrypt.Net.BCrypt.HashPassword(hashedPassword, BCrypt.Net.BCrypt.GenerateSalt());
    }
    public bool VerifyPassword(string hashedPassword, string encryptedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(hashedPassword, encryptedPassword);
    }
}
