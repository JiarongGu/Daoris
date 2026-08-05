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
    var context = new GateContext(root, declaration);
    var allowBuiltinsOnly = flags.Contains("--allow-builtins-only");

    Console.WriteLine($"daoris-devkit {Repository.DevkitVersion} — {root}");

    var report = new GateRunner(context, [
        new SensitiveGate(ScanScope.Tree, new CommandLineGit(root), allowBuiltinsOnly),
        new VersionGate(),
        new DocsGate(new CommandLineGitHistory(root)),
        new DoctrineGate(),
    ]).Run(Console.WriteLine);

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
        : flags.Contains("--tree") ? ScanScope.Tree
        : ScanScope.Staged;

    var gate = new SensitiveGate(
        scope,
        new CommandLineGit(root),
        flags.Contains("--allow-builtins-only"),
        messageIndex >= 0 && messageIndex + 1 < arguments.Count ? arguments[messageIndex + 1] : null);

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

int Print(string text, int code = 0)
{
    (code == 0 ? Console.Out : Console.Error).WriteLine(text);
    return code;
}
