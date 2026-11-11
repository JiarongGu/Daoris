namespace Daoris.Driver;

/// <summary>
/// The interfaces every plugin the kit writes declares and answers: a sample command, <see cref="Command"/>, that keeps a count in
/// its private storage through the kit's <see cref="PluginKitStorage"/>, the storage itself, and a view stub its page reads.
/// </summary>
public static class PluginKitInterfaces
{
    /// <summary>The sample command: `daoris &lt;id&gt; hello`.</summary>
    public const string Command = "hello";

    /// <summary>The sample view, a section of its plugin's page.</summary>
    public const string View = "overview";

    /// <summary>The manifest's <c>interfaces</c>, as its lines below <c>hooks</c>.</summary>
    internal static string Declared(string name) => $$"""

          "interfaces": {
            "commands": [
              { "name": "{{Command}}", "title": "Say hello", "about": "Says hello, and how many times it was asked, which it keeps in its private storage." }
            ],
            "storage": { "for": "How many times hello was asked: what a plugin keeps for itself goes here, credentials included." },
            "views": [
              { "id": "{{View}}", "title": {{System.Text.Json.JsonSerializer.Serialize(name)}}, "where": ["page"] }
            ]
          },
        """.Replace("\r\n", "\n");
}
