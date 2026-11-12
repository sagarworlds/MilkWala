namespace MilkWala.Helpers
{
    /// <summary>Builds <c>wa.me</c> links that open WhatsApp with a ready-typed message.</summary>
    public static class WhatsAppLink
    {
        private const string IndiaCountryCode = "91";

        /// <summary>
        /// Link that opens a WhatsApp chat with <paramref name="mobile"/> (an Indian number in any common format)
        /// and the message pre-filled.
        /// </summary>
        /// <returns>The link, or null when the number is not a valid 10-digit mobile.</returns>
        public static string? Build(string? mobile, string message)
        {
            var number = MobileNumber.Normalize(mobile);
            if (number.Length == 0)
                return null;

            return $"https://wa.me/{IndiaCountryCode}{number}?text={Uri.EscapeDataString(message ?? string.Empty)}";
        }
    }
}
