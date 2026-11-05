namespace MilkWala.Helpers
{
    /// <summary>
    /// Resolves the "TrueValue|FalseValue" converter parameters used in XAML.
    /// XAML <c>StringFormat</c> cannot format a bool with custom sections, so the value converters
    /// use this instead.
    /// </summary>
    public static class BoolSwitch
    {
        private const char Separator = '|';

        /// <summary>
        /// Picks one side of a <c>"whenTrue|whenFalse"</c> parameter.
        /// </summary>
        /// <param name="value">The bound boolean.</param>
        /// <param name="parameter">Text in the form <c>whenTrue|whenFalse</c>.</param>
        /// <param name="fallback">Returned when the parameter is missing or malformed.</param>
        /// <returns>The side matching <paramref name="value"/>, or <paramref name="fallback"/>.</returns>
        public static string Pick(bool value, string? parameter, string fallback)
        {
            if (string.IsNullOrEmpty(parameter))
                return fallback;

            // Split on the first separator only, so the "false" side may itself contain '|'.
            int index = parameter.IndexOf(Separator);
            if (index < 0)
                return fallback;

            return value ? parameter[..index] : parameter[(index + 1)..];
        }
    }
}
