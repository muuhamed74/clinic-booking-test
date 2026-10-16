namespace Clinic_booking_site.Helpers.Hashing
{
    public class TokenHasher
    {
        public static string Hash(string rawToken)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(bytes);
        }
    }
}
