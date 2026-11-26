using System.Text.RegularExpressions;
using MilkWala.Localization;
using MilkWala.Models;
using Xunit;

namespace MilkWala.Tests;

public class TranslationsTests
{
    private static readonly string[] Languages = Translations.SupportedLanguages;

    private static string[] Placeholders(string text) =>
        Regex.Matches(text, @"\{(\d+)(?::[^}]*)?\}").Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x).ToArray();

    [Fact]
    public void The_three_languages_are_offered_in_order()
    {
        Assert.Equal(new[] { "en", "hi", "mr" }, Languages);
    }

    [Fact]
    public void Every_language_has_every_key()
    {
        var missing = Languages
            .SelectMany(lang => Translations.Keys.Where(key => !Translations.Has(lang, key)).Select(key => $"{lang}: {key}"))
            .ToList();

        Assert.True(missing.Count == 0, "Missing translations:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void No_text_is_empty()
    {
        var empty = Languages
            .SelectMany(lang => Translations.Keys.Where(key => string.IsNullOrWhiteSpace(Translations.Get(lang, key))).Select(key => $"{lang}: {key}"))
            .ToList();

        Assert.True(empty.Count == 0, "Empty texts:\n" + string.Join("\n", empty));
    }

    [Fact]
    public void Parameters_are_the_same_in_every_language()
    {
        var problems = new List<string>();
        foreach (var key in Translations.Keys)
        {
            var english = Placeholders(Translations.Get("en", key));
            foreach (var lang in Languages.Where(l => l != "en"))
            {
                var other = Placeholders(Translations.Get(lang, key));
                if (!english.SequenceEqual(other))
                    problems.Add($"{key}: en uses {{{string.Join(",", english)}}} but {lang} uses {{{string.Join(",", other)}}}");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private sealed class Anything : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) => "x";
    }

    [Fact]
    public void Every_format_text_can_be_formatted()
    {
        var failures = new List<string>();
        foreach (var lang in Languages)
        foreach (var key in Translations.Keys.Where(k => k.StartsWith("Fmt") || k.StartsWith("Sync")))
        {
            try
            {
                // Arguments that accept any format specifier, so only a malformed placeholder (unbalanced brace, bad index) fails.
                _ = string.Format(Translations.Get(lang, key), new Anything(), new Anything(), new Anything(), new Anything());
            }
            catch (FormatException ex)
            {
                failures.Add($"{lang}: {key}: {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void Two_choice_texts_have_exactly_one_separator_in_every_language()
    {
        foreach (var lang in Languages)
        foreach (var key in Translations.Keys.Where(k => k.StartsWith("Bool")))
            Assert.Equal(2, Translations.Get(lang, key).Split('|').Length);
    }

    [Fact]
    public void Hindi_and_Marathi_texts_are_actually_translated()
    {
        // Every text in these languages contains Devanagari; a pasted English line would not.
        var untranslated = new List<string>();
        foreach (var lang in new[] { "hi", "mr" })
        foreach (var key in Translations.Keys)
        {
            var text = Translations.Get(lang, key);
            if (!text.Any(c => c >= '\u0900' && c <= '\u097F'))
                untranslated.Add($"{lang}: {key} = {text}");
        }

        Assert.True(untranslated.Count == 0, "Not translated:\n" + string.Join("\n", untranslated));
    }

    [Fact]
    public void Every_value_of_every_displayed_enum_has_a_label_in_every_language()
    {
        var enumTypes = new[] { typeof(MilkType), typeof(DeliveryStatus), typeof(ComplaintStatus), typeof(ComplaintCategory), typeof(ExpenseCategory), typeof(UserRole) };
        var missing = new List<string>();
        foreach (var type in enumTypes)
        foreach (Enum value in Enum.GetValues(type))
        foreach (var lang in Languages)
        {
            var key = EnumText.KeyFor(value);
            if (!Translations.Has(lang, key))
                missing.Add($"{lang}: {key}");
        }

        Assert.True(missing.Count == 0, string.Join("\n", missing));
    }

    [Fact]
    public void An_unknown_key_falls_back_to_the_key_and_a_missing_language_to_english()
    {
        Assert.Equal("NoSuchKey", Translations.Get("hi", "NoSuchKey"));
        Assert.Equal(Translations.Get("en", "Cancel"), Translations.Get("fr", "Cancel"));
    }

    [Fact]
    public void The_curated_translations_that_already_existed_are_kept()
    {
        Assert.Equal("वॉलेट बैलेंस", Translations.Get("hi", "WalletBalance"));
        Assert.Equal("एकूण ग्राहक", Translations.Get("mr", "TotalCustomers"));
    }
}
