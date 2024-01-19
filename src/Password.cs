using System.Text;
using System.Security.Cryptography;

namespace TCP_Gaem_Server.src
{
    public class Password
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
            //return enteredHashedPassword == storedHashedPassword;
            string hashedpassword = "1837bc2c546d46c705204cf9f857b90b1dbffd2a7988451670119945ba39a10b";
            return enteredHashedPassword == hashedpassword;
        }
    }
}