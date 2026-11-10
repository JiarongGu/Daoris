namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The place section a conversation working in its folder in place is told with its first message, ahead of the person's words
/// (<c>FolderPrompt</c>, <c>ChatTold.HoldPlace</c>), as a stand-in agent sets it apart from the words it answers.
/// </summary>
internal static class StubPlace
{
    /// <summary>
    /// The script lines a stand-in starts with: <c>placeApart(text)</c> answers <c>{ place, said }</c>, the kind of place told
    /// (<c>folder</c>, <c>checkout</c>) or null, and the text without its section. A section is its heading and one paragraph, as a first
    /// session in a fresh folder is told it. <c>wordsOf(text)</c> is the text without that section and the standing block after the words.
    /// </summary>
    public const string Js = """
        const placeApart = (text) => {
          const placed = /(?:^|\n\n)(## Your place\n\n[^\n]*)\n\n/.exec(text);
          if (!placed) return { place: null, said: text };
          const rest = text.slice(placed.index + placed[0].length);
          return {
            place: /not a git repository/.test(placed[1]) ? 'folder' : 'checkout',
            said: placed.index > 0 ? text.slice(0, placed.index) + '\n\n' + rest : rest,
          };
        };
        const wordsOf = (text) => placeApart(text.replace(/(\n\n| \| )?Daoris's standing block for this session: [\s\S]*$/, '')).said;

        """;
}
