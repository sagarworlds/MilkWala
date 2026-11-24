using Xunit;

namespace MilkWala.Tests;

/// <summary>
/// Tests that change the app-wide current language share one collection that runs on its own, so they cannot
/// interfere with each other or with tests that read the language.
/// </summary>
[CollectionDefinition("Language", DisableParallelization = true)]
public class LanguageCollection
{
}
