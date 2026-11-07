using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// One toolchain's row: its signature files, the manifest beside them, the file besides the manifest that names its workspace
/// members where there is one, and the rules of the folders beside each.
/// </summary>
internal sealed record SignatureRow(
    string Toolchain, IReadOnlyList<string> Signature, string? Manifest, bool Workspaces, IReadOnlyList<string> ReadsNothing, IReadOnlyList<FileRule> Rules,
    string? WorkspaceFile = null);

/// <summary>A signature row reached by a tracked file: the file, its folder, and one of the row's rules made repository-relative.</summary>
internal sealed record SignatureHit(SignatureRow Row, string File, string Folder, FileRule Rule);

/// <summary>
/// The toolchains' signatures, data the desktop ships (<c>Defaults/file-signatures.json</c>) read by one engine, so no code names a
/// stack's folders: each row's signature files, the folders beside them in the rule file's format, the local signatures and the
/// tracked files the analysis searches.
/// </summary>
internal sealed class FileSignatures
{
    private static readonly Lazy<FileSignatures> Carried = new(() =>
    {
        using var stream = typeof(FileSignatures).Assembly.GetManifestResourceStream("Daoris.Driver.Defaults/file-signatures.json")
            ?? throw new InvalidOperationException("the shipped file signatures are not in the assembly");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    });

    private FileSignatures(IReadOnlyList<SignatureRow> rows, IReadOnlyList<string> local, IReadOnlyList<string> scripts)
    {
        Rows = rows;
        Local = local;
        Scripts = scripts;
    }

    internal static FileSignatures Shipped => Carried.Value;

    internal IReadOnlyList<SignatureRow> Rows { get; }

    internal IReadOnlyList<string> Local { get; }

    internal IReadOnlyList<string> Scripts { get; }

    /// <summary>The harnesses' and editors' settings files the floor never copies.</summary>
    internal IReadOnlyList<string> Settings { get; private init; } = [];

    /// <exception cref="FormatException">A row or a rule of it does not read: the data is the desktop's own, so it is a defect.</exception>
    internal static FileSignatures Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        var problems = new List<string>();
        var rows = new List<SignatureRow>();
        foreach (var row in root.GetProperty("rows").EnumerateArray())
        {
            var name = Json.Text(row, "toolchain") ?? "";
            rows.Add(new SignatureRow(
                name, Json.Strings(row, "signature"), Json.Text(row, "manifest"), Json.Flag(row, "workspaces"), Json.Strings(row, "readsNothing"),
                FileRuleFile.Rules(row, $"{name}.", problems), Json.Text(row, "workspaceFile")));
        }

        if (problems.Count > 0) throw new FormatException($"the file signatures do not read: {string.Join("; ", problems)}");
        return new FileSignatures(rows, Json.Strings(root, "local"), Json.Strings(root, "scripts")) { Settings = Json.Strings(root, "settings") };
    }

    /// <summary>Every row a tracked file's name reaches, each of its rules made relative to that file's folder.</summary>
    internal IReadOnlyList<SignatureHit> Over(IEnumerable<string> tracked)
    {
        var hits = new List<SignatureHit>();
        foreach (var file in tracked)
        {
            var slash = file.LastIndexOf('/');
            var (folder, name) = (file[..(slash + 1)], file[(slash + 1)..]);
            foreach (var row in Rows.Where(row => row.Signature.Any(glob => UnionJoins.Glob(glob).IsMatch(name))))
            {
                hits.AddRange(row.Rules.Select(rule => new SignatureHit(row, file, folder, Beside(rule, folder))));
            }
        }

        return hits;
    }

    /// <summary>The local signature <paramref name="path"/> matches, or null.</summary>
    internal string? LocalOf(string path)
    {
        var name = path.TrimEnd('/');
        name = name[(name.LastIndexOf('/') + 1)..];
        return Local.FirstOrDefault(pattern => pattern.TrimEnd('/').Contains('/') || pattern.EndsWith('/')
            ? RoomPaths.Declared(pattern).IsMatch(path)
            : UnionJoins.Glob(pattern).IsMatch(name));
    }

    private static FileRule Beside(FileRule rule, string folder) => folder.Length == 0 ? rule : rule with
    {
        Paths = [.. rule.Paths.Select(path => folder + path)],
        Key = rule.Key is { } key ? [.. key.Select(path => folder + path)] : null,
    };
}
