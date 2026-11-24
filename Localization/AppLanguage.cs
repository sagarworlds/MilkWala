using System.Globalization;

namespace MilkWala.Localization
{
    /// <summary>The language the app is currently shown in, shared by every screen.</summary>
    public static class AppLanguage
    {
        public const string DefaultCode = "en";

        // Culture used for dates and numbers in each language (month and day names follow it).
        private static readonly Dictionary<string, string> Cultures = new()
        {
            ["en"] = "en-IN",
            ["hi"] = "hi-IN",
            ["mr"] = "mr-IN"
        };

        public static string Code { get; private set; } = DefaultCode;

        public static bool IsSupported(string? code) =>
            code != null && Array.IndexOf(Translations.SupportedLanguages, code) >= 0;

        /// <summary>Switches the current language.</summary>
        /// <exception cref="ArgumentException">The language is not one of <see cref="Translations.SupportedLanguages"/>.</exception>
        public static void Set(string code)
        {
            if (!IsSupported(code))
                throw new ArgumentException($"Unsupported language '{code}'.", nameof(code));
            Code = code;
        }

        /// <summary>
        /// Makes dates and numbers follow the current language (for example Marathi month names). If the device has no
        /// data for that culture the current culture is kept; the text itself is translated either way.
        /// </summary>
        public static void ApplyCulture()
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(Cultures[Code]);
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
            catch (CultureNotFoundException)
            {
                // Keep the device culture.
            }
        }
    }
}
