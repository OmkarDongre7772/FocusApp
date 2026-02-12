using System.Security.Cryptography;
using System.Text;

namespace FocusTracker.Core
{
    public static class AuthCryptoService
    {
        public static string Encrypt(string plain)
        {
            var bytes = Encoding.UTF8.GetBytes(plain);

            var encrypted = ProtectedData.Protect(
                bytes,
                null,
                DataProtectionScope.LocalMachine);

            return Convert.ToBase64String(encrypted);
        }

        public static string Decrypt(string encryptedBase64)
        {
            var encrypted = Convert.FromBase64String(encryptedBase64);

            var decrypted = ProtectedData.Unprotect(
                encrypted,
                null,
                DataProtectionScope.LocalMachine);

            return Encoding.UTF8.GetString(decrypted);
        }
    }
}
