using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The person's said words as a record carries them, in the shape the service answers them: a stand-in for a park they answered.</summary>
internal static class PersonWords
{
    /// <summary>One person's word of <paramref name="text"/>, under the id the service gives a record's old answer column.</summary>
    public static IReadOnlyList<SaidWordView> Of(string text) => [new SaidWordView("answer", text, DateTimeOffset.MinValue, [], false)];
}
