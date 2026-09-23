using System.Text;
using Daoris.Devkit;
using Daoris.Devkit.Cli;

// Windows defaults stdout to the ANSI codepage, which mojibakes every em dash and CJK character. The
// service hit this and so did the family before it; it is cheaper to set than to rediscover.
if (OperatingSystem.IsWindows())
{
    Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}

var arguments = args.ToList();
var command = arguments.Count > 0 && !arguments[0].StartsWith('-') ? arguments[0] : "verify";
var flags = arguments.Where(a => a.StartsWith('-')).ToHashSet(StringComparer.Ordinal);
var root = Repository.FindRoot(Directory.GetCurrentDirectory());

try
{
    return command switch
    {
        "verify" => Verify(),
        "scan" => Scan(),
        "init" => Init(),
        "install-hooks" => InstallHooks(),
        "map" => Map(),
        "version" or "--version" => Print(Repository.DevkitVersion),
        "help" or "--help" or "-h" => Print(Repository.Usage),
        _ => Print($"daoris-devkit: unknown command '{command}'\n\n{Repository.Usage}", code: 2),
    };
}
catch (DevkitException error)
{
    // Exit 2 is a tool error and 1 is a policy failure — a script has to be able to tell "the gates
    // found something" from "the gates could not run".
    Console.Error.WriteLine($"daoris-devkit: {error.Message}");
    return 2;
}

int Verify()
{
    var declaration = GateDeclaration.Read(root);
    VersionPin.Require(declaration.Devkit, Repository.DevkitVersion);

    var context = new GateContext(root, declaration);
    var allowBuiltinsOnly = flags.Contains("--allow-builtins-only");

    Console.WriteLine($"daoris-devkit {Repository.DevkitVersion} — {root}");

    // 🔴 `--universal-only` exists because a repository cannot declare THIS BINARY as one of its own
    // gates: the run would reach that row and start itself. It is also what a repository with
    // per-platform declared gates needs, since those cannot all pass in one process on one machine.
    var report = new GateRunner(context, [
        new SensitiveGate(ScanScope.Tree, new CommandLineGit(root), allowBuiltinsOnly),
        new VersionGate(),
        new DocsGate(new CommandLineGitHistory(root)),
        new LinksGate(new CommandLineGit(root)),
        new DoctrineGate(),
    ]).Run(Console.WriteLine, declared: !flags.Contains("--universal-only"));

    var ran = report.Results.Count(r => !r.Skipped);
    var skipped = report.Results.Count(r => r.Skipped);
    Console.WriteLine(report.Passed
        ? $"\nPASSED — {ran} gate(s) ran{(skipped > 0 ? $", {skipped} skipped" : "")}"
        : "\nFAILED");
    return report.Passed ? 0 : 1;
}

// The pre-commit path: staged changes only, and nothing else. A hook that ran the whole gate set would
// be a hook people disable, and then the scan that actually protects the repository is gone.
int Scan()
{
    var declaration = GateDeclaration.Read(root);
    var context = new GateContext(root, declaration);
    var messageIndex = arguments.IndexOf("--message");
    var scope = messageIndex >= 0 ? ScanScope.Message
        : flags.Contains("--history") ? ScanScope.History
        : flags.Contains("--tree") ? ScanScope.Tree
        : ScanScope.Staged;

    // Said before it starts, not after. A history audit on a large repository takes long enough that
    // silence reads as a hang, and the first instinct is to kill it.
    if (scope == ScanScope.History) Console.WriteLine("scanning all history — every object and every path…");

    var gate = new SensitiveGate(
        scope,
        new CommandLineGit(root),
        flags.Contains("--allow-builtins-only"),
        messageIndex >= 0 && messageIndex + 1 < arguments.Count ? arguments[messageIndex + 1] : null,
        new CommandLineGitObjects(root));

    var result = gate.Run(context);
    Console.WriteLine(result.Passed ? $"sensitive: {result.Detail}" : result.Detail);
    return result.Passed ? 0 : 1;
}

int Init()
{
    var file = Path.Combine(root, GateDeclaration.FileName);
    if (File.Exists(file)) return Print($"{GateDeclaration.FileName} already exists — nothing written.");

    File.WriteAllText(file, Repository.StarterDeclaration, new UTF8Encoding(false));
    return Print($"wrote {GateDeclaration.FileName} — declare this repository's own gates in it.");
}

// The pre-commit scan is the gate that actually protects a repository — it catches a leak before it
// becomes history, which is the only point at which the fix is cheap. Left to be run by hand it is run
// after the commit that needed it.
int InstallHooks()
{
    var hooks = Path.Combine(root, ".githooks");
    Directory.CreateDirectory(hooks);

    foreach (var (name, body) in Repository.Hooks)
    {
        var file = Path.Combine(hooks, name);
        File.WriteAllText(file, body, new UTF8Encoding(false));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    // core.hooksPath rather than copying into .git/hooks: the hooks become tracked files that a clone
    // gets, instead of something every contributor has to be told to install.
    var result = Process.Run("git", ["config", "core.hooksPath", ".githooks"], root);
    if (result.ExitCode != 0) throw new DevkitException($"could not set core.hooksPath: {result.Error.Trim()}");

    return Print($"wrote .githooks/ and set core.hooksPath — commit the directory so a clone gets them.\n"
               + "Bypass a hook deliberately with 'git commit --no-verify'.");
}

// The code map from the project files (MAP3c). `--check` is the gate a repository declares: whether the
// committed map is what its project files say is a fact, and a fact gates (D54). Not a universal gate —
// a map another producer wrote (an agent, a person, another stack's tool) is not this tool's to judge.
int Map()
{
    var produced = CodeMapProducer.Produce(root, new CommandLineGit(root));
    foreach (var note in produced.Notes) Console.WriteLine($"  note  {note}");

    if (flags.Contains("--check"))
    {
        var (fresh, detail) = CodeMapProducer.Check(root, produced);
        Console.WriteLine($"code-map: {detail}");
        return fresh ? 0 : 1;
    }

    if (produced.Problem is not null) return Print($"code-map: {produced.Problem}", code: 1);

    if (CodeMapProducer.Check(root, produced).Fresh)
    {
        return Print($"{produced.File} is already what the project files say — nothing written.");
    }

    CodeMapProducer.Write(root, produced);
    return Print($"wrote {produced.File} — {produced.Modules.Count} module(s), {produced.Dependencies.Count} dependency(ies). "
               + "Commit it with the change that moved it.");
}

int Print(string text, int code = 0)
{
    (code == 0 ? Console.Out : Console.Error).WriteLine(text);
    return code;
}
