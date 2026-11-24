using System.Reflection;
using MilkWala.Localization;
using MilkWala.Models;
using Xunit;

namespace MilkWala.Tests;

[Collection("Language")]
public class StringsAndLanguageTests : IDisposable
{
    public StringsAndLanguageTests() => AppLanguage.Set("en");

    public void Dispose() => AppLanguage.Set("en");

    private static PropertyInfo[] StringProperties() =>
        typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Static);

    [Fact]
    public void Every_Strings_property_has_a_key_and_every_text_key_has_a_property()
    {
        var propertyNames = StringProperties().Select(p => p.Name).ToHashSet();
        var textKeys = Translations.Keys.Where(k => !k.StartsWith("Enum_")).ToHashSet();

        Assert.Empty(propertyNames.Except(textKeys));
        Assert.Empty(textKeys.Except(propertyNames));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("hi")]
    [InlineData("mr")]
    public void Strings_return_the_text_of_the_current_language(string language)
    {
        AppLanguage.Set(language);

        foreach (var property in StringProperties())
            Assert.Equal(Translations.Get(language, property.Name), (string)property.GetValue(null)!);
    }

    [Fact]
    public void Switching_language_changes_what_the_screens_read()
    {
        Assert.Equal("Wallet Balance", Strings.WalletBalance);

        AppLanguage.Set("hi");
        Assert.Equal("वॉलेट बैलेंस", Strings.WalletBalance);

        AppLanguage.Set("mr");
        Assert.NotEqual("वॉलेट बैलेंस", Strings.WalletBalance);
        Assert.NotEqual("Wallet Balance", Strings.WalletBalance);
    }

    [Fact]
    public void An_unsupported_language_is_rejected_and_the_current_one_kept()
    {
        AppLanguage.Set("hi");

        Assert.Throws<ArgumentException>(() => AppLanguage.Set("fr"));

        Assert.Equal("hi", AppLanguage.Code);
        Assert.False(AppLanguage.IsSupported("fr"));
        Assert.False(AppLanguage.IsSupported(null));
        Assert.True(AppLanguage.IsSupported("mr"));
    }

    [Fact]
    public void Enum_values_show_in_the_current_language_and_unknown_enums_show_their_name()
    {
        Assert.Equal("Delivered", EnumText.For(DeliveryStatus.Delivered));
        Assert.Equal("Cow Milk", EnumText.For(MilkType.Cow));

        AppLanguage.Set("hi");
        Assert.Equal("गाय का दूध", EnumText.For(MilkType.Cow));
        Assert.Equal("डिलीवर हुआ", EnumText.For(DeliveryStatus.Delivered));

        Assert.Equal(nameof(DayOfWeek.Friday), EnumText.For(DayOfWeek.Friday)); // no label for this enum
    }

    [Fact]
    public void Formats_fill_in_correctly_in_every_language()
    {
        foreach (var language in Translations.SupportedLanguages)
        {
            AppLanguage.Set(language);
            Assert.Contains("93", string.Format(Strings.FmtLitres, 93));
            Assert.Contains("3", string.Format(Strings.SyncOffline, 3));
        }
    }
}
