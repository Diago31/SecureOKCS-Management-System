using Microsoft.AspNetCore.DataProtection;

namespace SecureOKCS.Services
{
    public class CustomerEncryptionService
    {
        private readonly IDataProtector _protector;

        public CustomerEncryptionService(
            IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector(
                "SecureOKCS.CustomerData.V1");
        }

        public string Encrypt(string value)
        {
            return _protector.Protect(value.Trim());
        }

        public string Decrypt(string value)
        {
            try
            {
                return _protector.Unprotect(value);
            }
            catch
            {
                return "Unable to decrypt";
            }
        }

        public string MaskName(string encryptedValue)
        {
            string name = Decrypt(encryptedValue);

            if (name.Length <= 1)
                return "*";

            return name[0] +
                new string('*', name.Length - 1);
        }

        public string MaskEmail(string encryptedValue)
        {
            string email = Decrypt(encryptedValue);
            int at = email.IndexOf('@');

            if (at <= 1)
                return "***@***";

            return email[0] +
                "***" +
                email[at..];
        }

        public string LastFour(string phone)
        {
            phone = phone.Trim();

            return phone.Length <= 4
                ? phone
                : phone[^4..];
        }
    }
}