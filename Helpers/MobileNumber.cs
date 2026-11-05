namespace MilkWala.Helpers
{
    /// <summary>Normalises Indian mobile numbers so "+91 98765-43210" and "9876543210" compare equal.</summary>
    public static class MobileNumber
    {
        private const int SubscriberDigits = 10;

        /// <summary>
        /// Keeps digits only and returns the last 10 (dropping a +91 / 91 / 0 prefix).
        /// Returns an empty string for null, blank or too-short input so callers can treat it as "no number".
        /// </summary>
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Length < SubscriberDigits ? string.Empty : digits[^SubscriberDigits..];
        }
    }
}
