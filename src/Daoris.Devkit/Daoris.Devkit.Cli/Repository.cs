namespace Daoris.Devkit.Cli;

/// <summary>Finding the repository, and the text the CLI writes into a new one.</summary>
internal static class Repository
{
    /// <summary>
    /// Development, like everything else here. Stamped by the release workflow, never by hand — the
    /// version gate this binary runs exists because a sibling did exactly that.
    /// </summary>
    public const string DevkitVersion = "0.0.1";

    /// <summary>
    /// Walk up for the `.git` directory.
    /// </summary>
    /// <remarks>
    /// By `.git` rather than by the declaration file: the devkit is usually invoked from a hook or from
    /// a subdirectory, and requiring the caller to be at the root would make every hook fragile in a
    /// way that is tedious to debug. A worktree has `.git` as a FILE, so both are accepted.
    /// </remarks>
    public static string FindRoot(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            var git = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git)) return directory.FullName;
        }

        throw new DevkitException($"'{start}' is not inside a git repository");
    }

    /// <summary>
    /// What `init` writes: every universal gate configured for the common case, and one commented
    /// example of a declared gate. Nothing invented about the repository it lands in.
    /// </summary>
    public const string StarterDeclaration = """
        {
          "devkit": "0.0.1",

          "gates": [],

          "sensitive": {
            "patternsFile": "local/sensitive-patterns.txt"
          },

          "version": {},

          "docs": {}
        }
        """;

    /// <summary>
    /// The two hooks worth installing, and only those two.
    /// </summary>
    /// <remarks>
    /// Both run the sensitive scan and nothing else. A pre-commit hook that ran the whole gate set
    /// would be a hook people turn off within a week, and then the scan that actually protects the
    /// repository is gone along with it — so the expensive gates stay in `verify`, where waiting for
    /// them is the point.
    ///
    /// The commit-message hook exists because messages are history too, and are the easiest place to
    /// forget: the change itself gets reviewed and the message rarely does.
    /// </remarks>
    public static readonly (string Name, string Body)[] Hooks =
    [
        ("pre-commit", """
            #!/bin/sh
            # Sensitive-content guard, on the staged changes. Installed by `daoris-devkit install-hooks`.
            # Bypass deliberately with: git commit --no-verify
            daoris-devkit scan

            """),
        ("commit-msg", """
            #!/bin/sh
            # The commit MESSAGE is history too, and is the half nobody reviews.
            daoris-devkit scan --message "$1"

            """),
    ];

    /// <summary>The whole surface, in the shape a person reads when they are stuck.</summary>
    public const string Usage = """
        usage: daoris-devkit <command> [flags]

          verify                  every universal gate, then this repository's declared ones (default)
          scan [--tree]           the sensitive-content scan alone; staged changes unless --tree
          scan --message <file>   scan a commit message — for the commit-msg hook
          scan --history          AUDIT all history: every object, every path it ever had
          init                    write a starter daoris.gates.json
          install-hooks           write .githooks/ and point git at it
          version                 print the devkit version

        flags:
          --allow-builtins-only   run the sensitive scan without the private pattern list

        exit: 0 clean · 1 a gate failed · 2 the devkit could not run
        """;
}
