using System.Reflection;
using System.Text.RegularExpressions;
using MilkWala.Localization;
using Xunit;

namespace MilkWala.Tests;

/// <summary>
/// Guards the screens themselves: nothing the user reads may be hardcoded English, and every key a screen uses must exist.
/// Reads the real XAML and view-model files.
/// </summary>
public class XamlLocalizationTests
{
    // Need no translation: tab-bar group titles (never shown) and the language switcher, where each language is named
    // in its own script ("EN", "हिं", "मरा") so a person can always find theirs.
    private static readonly HashSet<string> NeverShown = new() { "Customer View", "Vendor View", "Delivery Staff View", "EN" };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MilkWala.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root (MilkWala.csproj).");
    }

    private static IEnumerable<string> XamlFiles() =>
        Directory.EnumerateFiles(RepoRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}Platforms{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}Resources{Path.DirectorySeparatorChar}"));

    [Fact]
    public void The_screens_were_found()
    {
        Assert.True(XamlFiles().Count() >= 15);
    }

    [Fact]
    public void Every_screen_file_is_well_formed_xml_with_no_duplicate_attributes()
    {
        var broken = new List<string>();
        foreach (var file in XamlFiles())
        {
            try
            {
                System.Xml.Linq.XDocument.Load(file); // throws on duplicate attributes, unclosed tags, bad escapes
            }
            catch (System.Xml.XmlException ex)
            {
                broken.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        Assert.True(broken.Count == 0, string.Join("\n", broken));
    }

    [Fact]
    public void No_screen_text_is_hardcoded_English()
    {
        var literal = new Regex("\\b(Text|Placeholder|Title)=\"([^\"{][^\"]*)\"");
        var offenders = new List<string>();

        foreach (var file in XamlFiles())
            foreach (Match m in literal.Matches(File.ReadAllText(file)))
            {
                var text = System.Net.WebUtility.HtmlDecode(m.Groups[2].Value);
                bool hasEnglishLetters = text.Any(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
                if (hasEnglishLetters && !NeverShown.Contains(text))
                    offenders.Add($"{Path.GetFileName(file)}: {m.Groups[1].Value}=\"{text}\"");
            }

        Assert.True(offenders.Count == 0, "Hardcoded text (use {x:Static loc:Strings.Key}):\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Every_key_the_screens_use_exists()
    {
        var strings = typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Static).Select(p => p.Name).ToHashSet();
        var used = new Regex(@"loc:Strings\.(\w+)");
        var missing = new List<string>();

        foreach (var file in XamlFiles())
            foreach (Match m in used.Matches(File.ReadAllText(file)))
                if (!strings.Contains(m.Groups[1].Value))
                    missing.Add($"{Path.GetFileName(file)}: {m.Groups[1].Value}");

        Assert.True(missing.Count == 0, "Unknown keys:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void A_screen_that_uses_keys_declares_the_namespace()
    {
        var offenders = XamlFiles()
            .Select(f => (File: f, Text: File.ReadAllText(f)))
            .Where(x => x.Text.Contains("loc:Strings.") && !x.Text.Contains("xmlns:loc=\"clr-namespace:MilkWala.Localization\""))
            .Select(x => Path.GetFileName(x.File))
            .ToList();

        Assert.True(offenders.Count == 0, "Missing xmlns:loc in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Enum_values_shown_on_screen_go_through_the_enum_converter()
    {
        var raw = new Regex("Text=\"\\{Binding (Status|MilkType|Category|PreferredMilkType|SelectedRole)(\\}|,\\s*StringFormat)");
        var offenders = new List<string>();

        foreach (var file in XamlFiles())
            foreach (Match m in raw.Matches(File.ReadAllText(file)))
                offenders.Add($"{Path.GetFileName(file)}: {m.Value}");

        // StoreOrder.Status is free text stored by the app, not an enum: it has no converter and is allowed.
        offenders.RemoveAll(o => o.StartsWith("DairyStorePage.xaml"));
        Assert.True(offenders.Count == 0, "Enum shown without the converter:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void View_models_do_not_hardcode_their_titles()
    {
        var offenders = Directory.EnumerateFiles(Path.Combine(RepoRoot(), "ViewModels"), "*.cs")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), "Title = \""))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0, "Hardcoded Title in: " + string.Join(", ", offenders));
    }
}
