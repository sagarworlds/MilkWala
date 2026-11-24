namespace MilkWala.Localization
{
    /// <summary>The words shown for enum values such as a delivery status or a milk type.</summary>
    public static class EnumText
    {
        /// <summary>The translation key of a value, e.g. <c>Enum_DeliveryStatus_Delivered</c>.</summary>
        public static string KeyFor(Enum value) => $"Enum_{value.GetType().Name}_{value}";

        /// <summary>The value in the current language; an enum without a translation shows its own name.</summary>
        public static string For(Enum value)
        {
            var key = KeyFor(value);
            return Translations.Has("en", key) ? Translations.Get(AppLanguage.Code, key) : value.ToString();
        }
    }
}
