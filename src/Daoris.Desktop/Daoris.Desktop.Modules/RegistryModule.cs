using System.Text.Json;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The machine-path half of managing repositories (D48 §7): the shell picks a folder and says what it
/// found; the page does the registering through the ordinary loopback door.
/// </summary>
/// <remarks>
/// <para><b>Only the path work lives here.</b> Registering, re-wiring and retiring are plain HTTP calls
/// the page already knows how to make — routing them through IPC would be a second door onto the same
/// judgement, which is exactly what <see cref="Daoris.Knowledge.QuestExchange"/> exists to prevent one
/// layer up. What a browser genuinely cannot do is name a directory on this machine, so that is the
/// whole of this module.</para>
///
/// <para><b>It inspects and never writes.</b> Adoption is the repository's own agent's job — `init`,
/// `sync`, the collision review — and a shell that wrote a manifest would be doing that work from
/// outside, badly. This answers three facts about a folder and leaves every decision to the person.</para>
/// </remarks>
public sealed class RegistryModule(IEventBus events, Func<string?> pickFolder) : ModuleBase(events: events)
{
    public override string ModuleName => "DAORIS.REGISTRY";

    protected override Task<object?> RouteMessageAsync(
        IpcRequest request, IModuleContext context, CancellationToken cancellationToken)
    {
        switch (request.Type)
        {
            // Null is the person cancelling the dialog, which is an answer and not an error.
            case "PICK_FOLDER":
                return Task.FromResult<object?>(pickFolder() is { } folder ? Inspect(folder) : null);

            // The person editing their own tracked file through a form instead of a text editor
            // (D48 §7). The manifest is inert data (D26), not doctrine — and doctrine stays unwritable
            // from every surface (D31). The diff lands uncommitted, for that repository's own review.
            case "WRITE_DECLARATION":
            {
                var path = PayloadHelper.GetRequiredValue<string>(request.Payload, "path");
                WriteDeclaration(path, request.Payload);
                return Task.FromResult<object?>(Inspect(path));
            }

            default:
                throw UnknownType(request);
        }
    }

    /// <summary>
    /// What is true about a folder, before anyone decides anything: does it exist, has it adopted, is
    /// it a git checkout, and what has it declared about itself.
    /// </summary>
    /// <remarks>
    /// The git question matters because a repository with no history is one where a driven session's
    /// work cannot be reviewed as a diff (D46) — worth saying out loud at the moment of adding, not
    /// discovering at the moment of driving.
    /// </remarks>
    private static object Inspect(string path)
    {
        var manifest = Path.Combine(path, "daoris.json");
        var exists = Directory.Exists(path);
        var declaration = exists && File.Exists(manifest) ? ReadDeclaration(manifest) : null;

        return new
        {
            Path = path,
            Name = exists ? new DirectoryInfo(path.TrimEnd(Path.DirectorySeparatorChar, '/')).Name : "",
            Exists = exists,
            Adopted = declaration is not null,
            Git = exists && Directory.Exists(Path.Combine(path, ".git")),
            Summary = declaration?.Summary,
            Owns = declaration?.Owns ?? [],
            Accepts = declaration?.Accepts ?? [],
            Packs = declaration?.Packs ?? [],
            Join = declaration?.Join ?? false,
            ShareKnowledge = declaration?.ShareKnowledge ?? false,
        };
    }

    /// <summary>
    /// Merge a declaration into the repository's own `daoris.json`, keeping everything else exactly
    /// as it was.
    /// </summary>
    /// <remarks>
    /// <para><b>Merged, never rewritten.</b> The manifest carries `source`, `packs`, `target`,
    /// `coreBudgetBytes` and anything a later version adds; a form that serialized only the fields it
    /// knows about would silently delete the rest, and the person would find out at the next `sync`.
    /// So the document is re-emitted property by property and only the two blocks this form owns are
    /// replaced.</para>
    ///
    /// <para>Written the way every Daoris write happens: beside, then renamed — BOM-less UTF-8 with LF
    /// endings, because this file is tracked and a line-ending flip reads as a whole-file diff.</para>
    /// </remarks>
    private static void WriteDeclaration(string path, JsonElement? payload)
    {
        var manifest = Path.Combine(path, "daoris.json");
        if (!File.Exists(manifest))
        {
            // Adoption is the repository's own agent's job (`daoris init`, then the collision review).
            // A shell that wrote a first manifest would be doing that work from outside, badly.
            throw Refusals.Because(
                Refusals.RepositoryNotAdopted,
                $"`{Path.GetFileName(path)}` has not adopted Daoris. Its own agent runs `daoris init` "
                + "and the adoption review; this form edits a declaration that already exists.",
                ("repository", Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, '/'))));
        }

        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.NameEquals("domain") || property.NameEquals("remote")) continue;
                property.WriteTo(writer);
            }

            // `?? default` would be a JsonElement of kind Undefined, and every reader below throws on
            // one — a defensive line that defends nothing and fails obscurely. Unreachable today (the
            // `path` lookup above would have failed first on a null payload), which is exactly why it
            // was worth removing rather than trusting: nothing would have caught it changing.
            var sent = payload is { ValueKind: JsonValueKind.Object } given ? given : EmptyObject;
            writer.WriteStartObject("domain");
            writer.WriteString("summary", String(sent, "summary") ?? "");
            WriteStrings(writer, "owns", Strings(sent, "owns"));
            WriteStrings(writer, "accepts", Strings(sent, "accepts"));
            writer.WriteEndObject();

            var join = Bool(sent, "join");
            writer.WriteStartObject("remote");
            writer.WriteBoolean("join", join);
            // Knowledge without join is a manifest the CLI refuses; a form must not be able to write one.
            writer.WriteBoolean("knowledge", join && Bool(sent, "shareKnowledge"));
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        var beside = manifest + ".tmp";
        File.WriteAllText(beside, System.Text.Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n");
        File.Move(beside, manifest, overwrite: true);
    }

    /// <summary>An object with no properties — what every reader below treats as "nothing was sent".</summary>
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    private static void WriteStrings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    private sealed record Declaration(
        string? Summary, IReadOnlyList<string> Owns, IReadOnlyList<string> Accepts,
        IReadOnlyList<string> Packs, bool Join, bool ShareKnowledge);

    private static Declaration? ReadDeclaration(string manifest)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));
            var root = document.RootElement;
            var domain = root.TryGetProperty("domain", out var d) && d.ValueKind == JsonValueKind.Object
                ? d
                : (JsonElement?)null;
            var remote = root.TryGetProperty("remote", out var m) && m.ValueKind == JsonValueKind.Object
                ? m
                : (JsonElement?)null;
            var join = remote is not null && Bool(remote.Value, "join");

            return new Declaration(
                domain is null ? null : String(domain.Value, "summary"),
                domain is null ? [] : Strings(domain.Value, "owns"),
                domain is null ? [] : Strings(domain.Value, "accepts"),
                Strings(root, "packs"),
                join,
                // Knowledge feeds only from a joined repository (D47 §4), narrowed wherever a manifest
                // is read — this one was written by hand as often as by the CLI.
                join && remote is not null && Bool(remote.Value, "knowledge"));
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            // A manifest that will not parse is adopted-but-unreadable: the repository's own tooling
            // will say why, and the person still needs to see that something is there. Unreadable for
            // any other reason lands here too — a file open in another process, or one this account
            // may not read — because the question being asked is "is there a repository here", and a
            // locked file is not an answer of "no", nor a reason to take the folder picker down.
            return new Declaration(null, [], [], [], false, false);
        }
    }

    private static bool Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() is { Length: > 0 } text ? text : null
            : null;

    private static IReadOnlyList<string> Strings(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return [];

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text) items.Add(text);
        }

        return items;
    }
}
