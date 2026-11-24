using Microsoft.Maui.Storage;
using MilkWala.Localization;

namespace MilkWala.Services
{
    /// <summary>A language the user can pick.</summary>
    public sealed record LanguageOption(string Code, string NativeName);

    /// <summary>
    /// Holds the chosen language and remembers it between launches. Screens read text through
    /// <see cref="Strings"/>; when the language changes, the app rebuilds its screens (see App) so every
    /// label, title and format picks the new language up.
    /// </summary>
    public class LocalizationService
    {
        private const string PreferenceKey = "AppLanguage";
        private readonly IPreferences _preferences;

        public LocalizationService(IPreferences preferences)
        {
            _preferences = preferences;

            // A saved value that is no longer supported (an older build) falls back to English.
            var saved = preferences.Get(PreferenceKey, AppLanguage.DefaultCode);
            AppLanguage.Set(AppLanguage.IsSupported(saved) ? saved : AppLanguage.DefaultCode);
            AppLanguage.ApplyCulture();
        }

        public static IReadOnlyList<LanguageOption> Languages { get; } = new[]
        {
            new LanguageOption("en", "English"),
            new LanguageOption("hi", "हिन्दी"),
            new LanguageOption("mr", "मराठी")
        };

        public string CurrentLanguage => AppLanguage.Code;

        public event EventHandler? LanguageChanged;

        /// <summary>Changes and remembers the language, then tells the app to rebuild its screens.</summary>
        /// <exception cref="ArgumentException">The language is not supported.</exception>
        public void SetLanguage(string langCode)
        {
            if (!AppLanguage.IsSupported(langCode))
                throw new ArgumentException($"Unsupported language '{langCode}'.", nameof(langCode));
            if (langCode == AppLanguage.Code)
                return;

            AppLanguage.Set(langCode);
            AppLanguage.ApplyCulture();
            _preferences.Set(PreferenceKey, langCode);
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        public string Get(string key) => Translations.Get(AppLanguage.Code, key);
    }
}
