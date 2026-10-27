using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one boundary for words that may hold a secret (GITAUTH1a, D198 point 16; the git-auth design §10), its table row by row, and
/// the transport's words each failure class is read from (§5.1).
/// </summary>
/// <remarks>
/// Every secret here is assembled at run time, as <c>SensitiveGateTests</c> assembles its own: the sensitive gate scans this file, and a
/// literal token shape in it would be refused as one.
/// </remarks>
public sealed class GitWordsTests
{
    private static string Classic() => string.Concat("ghp", "_", new string('a', 30), "0123");

    private static string FineGrained() => string.Concat("github", "_pat_", new string('C', 22), "_", new string('d', 40));

    private static string GitLab() => string.Concat("glpat", "-", new string('e', 20));

    private static string KeyBlock() =>
        string.Concat("-----BEGIN ", "OPENSSH PRIVATE KEY-----\nb3BlbnNzaC1rZXktdjEAAAAABG5vbmU\n-----END ", "OPENSSH PRIVATE KEY-----");

    public static TheoryData<string, string, string> Rows() => new()
    {
        { "an address's user and token", "fatal: unable to access 'https://x-access-token:" + Classic() + "@github.com/org/engine.git/'", "fatal: unable to access 'https://github.com/org/engine.git/'" },
        { "an address's user alone", "Cloning into 'engine'... https://someone@dev.azure.com/org/_git/engine", "Cloning into 'engine'... https://dev.azure.com/org/_git/engine" },
        { "an ssh address keeps its user, which is no secret", "git@ssh.dev.azure.com: Permission denied (publickey).", "git@ssh.dev.azure.com: Permission denied (publickey)." },
        { "an ssh:// address loses it as any address does", "ssh://git@127.0.0.1:2222/org/engine.git", "ssh://127.0.0.1:2222/org/engine.git" },
        { "an Authorization header", "> Authorization: Basic dXNlcjpzM2NyZXQ=\n> Host: github.com", "> Authorization: [cut]\n> Host: github.com" },
        { "a Cookie header", "> Cookie: session=abc123; other=1", "> Cookie: [cut]" },
        { "a classic GitHub token", "remote: token " + Classic() + " was refused", "remote: token [cut] was refused" },
        { "a fine-grained GitHub token", "password " + FineGrained(), "password [cut]" },
        { "a GitLab token", "glpat? " + GitLab() + ".", "glpat? [cut]." },
        { "a private key block, whole", "before\n" + KeyBlock() + "\nafter", "before\n[cut]\nafter" },
        { "a private key block cut short", "before\n" + KeyBlock()[..60], "before\n[cut]" },
        { "words with nothing in them", "fatal: Could not read from remote repository.", "fatal: Could not read from remote repository." },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void Each_secret_git_may_say_is_cut(string name, string words, string cut)
    {
        Assert.True(cut == GitWords.Redact(words), $"{name}: {GitWords.Redact(words)}");
    }

    [Fact]
    public void Nothing_stays_nothing()
    {
        Assert.Null(GitWords.Redact(null));
        Assert.Equal("", GitWords.Redact(""));
    }

    [Theory]
    [InlineData("git@ssh.dev.azure.com:v3/org/project/engine", "ssh.dev.azure.com")]
    [InlineData("ssh://git@127.0.0.1:2222/org/engine.git", "127.0.0.1:2222")]
    [InlineData("https://someone@GitHub.com/org/engine.git", "github.com")]
    [InlineData("http://127.0.0.1:8080/org/engine.git", "127.0.0.1:8080")]
    [InlineData("file:///D:/repos/engine", null)]
    [InlineData("D:/repos/engine", null)]
    [InlineData("../engine", null)]
    public void A_host_is_read_from_every_form_of_address(string address, string? host)
    {
        Assert.Equal(host, GitWords.HostOf(address));
    }

    /// <summary>
    /// The transport's words, each class from them (§5.1). The rows marked measured were written by this machine's git and OpenSSH
    /// against local hosts (<c>GitPromptsProcessTests</c>, <c>GitHoldsProcessTests</c>, <c>GitFailuresProcessTests</c>); the rest are
    /// OpenSSH's and git's own sentences as their sources print them, for the hosts this machine has no local server for.
    /// </summary>
    [Theory]
    [InlineData("measured: git, HTTP 401 and no credential", "fatal: could not read Username for 'http://127.0.0.1:50211': terminal prompts disabled", "read", GitFailureClass.Refused)]
    [InlineData("measured: git, HTTP 401 and a credential refused", "fatal: Authentication failed for 'http://127.0.0.1:50211/org/engine.git/'", "read", GitFailureClass.Refused)]
    [InlineData("measured: git, HTTP 403 on a push", "fatal: unable to access 'http://127.0.0.1:50211/org/engine.git/': The requested URL returned error: 403", "write", GitFailureClass.WriteRefused)]
    [InlineData("measured: git, HTTP 403 on a fetch", "fatal: unable to access 'http://127.0.0.1:50211/org/engine.git/': The requested URL returned error: 403", "read", GitFailureClass.Refused)]
    [InlineData("measured: git, a refused connection", "fatal: unable to access 'http://127.0.0.1:50212/org/engine.git/': Failed to connect to 127.0.0.1 port 50212 after 2045 ms: Could not connect to server", "read", GitFailureClass.Unreachable)]
    [InlineData("measured: OpenSSH, a refused connection", "ssh: connect to host 127.0.0.1 port 50213: Connection refused\r\nfatal: Could not read from remote repository.", "read", GitFailureClass.Unreachable)]
    [InlineData("measured: OpenSSH (Git's and Windows'), every key refused", "git@127.0.0.1: Permission denied (publickey).\nfatal: Could not read from remote repository.\n\nPlease make sure you have the correct access rights\nand the repository exists.", "read", GitFailureClass.Refused)]
    [InlineData("OpenSSH, a host offering a password too", "git@ssh.dev.azure.com: Permission denied (password,publickey).\nfatal: Could not read from remote repository.", "read", GitFailureClass.Refused)]
    [InlineData("measured: OpenSSH (Git's and Windows'), a host not yet known, in batch mode", "Host key verification failed.\nfatal: Could not read from remote repository.\n\nPlease make sure you have the correct access rights\nand the repository exists.", "read", GitFailureClass.HostUnknown)]
    [InlineData("OpenSSH, a host not yet known, asked strictly", "No ED25519 host key is known for [127.0.0.1]:2222 and you have requested strict checking.\nHost key verification failed.", "read", GitFailureClass.HostUnknown)]
    [InlineData("measured: OpenSSH (Git's and Windows'), a host's key changed", "@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n@    WARNING: REMOTE HOST IDENTIFICATION HAS CHANGED!     @\n@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\nIT IS POSSIBLE THAT SOMEONE IS DOING SOMETHING NASTY!\nHost key for [127.0.0.1]:47222 has changed and you have requested strict checking.\nHost key verification failed.\nfatal: Could not read from remote repository.", "read", GitFailureClass.HostChanged)]
    [InlineData("OpenSSH, a name not resolved", "ssh: Could not resolve hostname nohost.invalid: No such host is known.", "read", GitFailureClass.Unreachable)]
    [InlineData("git, a repository the host does not have", "fatal: repository 'http://127.0.0.1/org/engine.git/' not found", "read", GitFailureClass.Unclassed)]
    [InlineData("a host's own sentence alone is not read", "remote: Permission to org/engine.git denied to someone.", "write", GitFailureClass.Unclassed)]
    public void A_failure_is_classed_by_the_transports_words(string name, string words, string act, GitFailureClass expected)
    {
        Assert.True(expected == GitFailures.Class(words, act == "write" ? GitAct.Write : GitAct.Read), name);
    }

    [Fact]
    public void Each_class_has_one_code_and_the_codes_read_back()
    {
        foreach (var failure in new[] { GitFailureClass.Refused, GitFailureClass.WriteRefused, GitFailureClass.HostUnknown, GitFailureClass.HostChanged })
        {
            Assert.Equal(failure, GitFailures.OfCode(GitFailures.Code(failure)));
        }

        Assert.Null(GitFailures.OfCode(GitFailures.Code(GitFailureClass.Unreachable)));
        Assert.Null(GitFailures.OfCode("no-identity"));
    }
}
