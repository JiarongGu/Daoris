namespace Daoris.Driver;

/// <summary>
/// What a session is kept from holding of the person's or Daoris's credentials (the git design §3.1 G5): the token
/// variables it inherited are taken out (<see cref="GitWiring.Untoken"/>), a grant is never handed (<see cref="GitWiring.Hand(System.Diagnostics.ProcessStartInfo, string?)"/>),
/// its <c>gh</c> and <c>az</c> are pointed at folders of Daoris's that hold no sign-in, and the harness's rules deny reading
/// <c>profiles/git/</c>, where Daoris keeps its keys. A session still runs as the person's account, so this is a door, not a wall.
/// </summary>
public static class GitSessionHolds
{
    /// <summary>Where <c>gh</c> keeps its sign-in.</summary>
    public const string GhFolder = "GH_CONFIG_DIR";

    /// <summary>Where <c>az</c> keeps its sign-in.</summary>
    public const string AzFolder = "AZURE_CONFIG_DIR";

    /// <summary>Point a session's <c>gh</c> and <c>az</c> at folders that hold no sign-in, over any the start inherited.</summary>
    public static void Hand(IDictionary<string, string?> environment, string home)
    {
        environment[GhFolder] = Empty(home, "gh");
        environment[AzFolder] = Empty(home, "az");
    }

    /// <summary>The folder Daoris keeps for <paramref name="tool"/>'s configuration, made empty where it was not there.</summary>
    public static string Empty(string home, string tool)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.GitSessions, "empty", tool);
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>The harness's rule that denies reading everything Daoris keeps for git (<c>profiles/git/</c>): keys, host keys and configuration.</summary>
    public static string ProfileDeny(string home) =>
        PermissionRules.ReadRule(Path.GetDirectoryName(DaorisHome.Entry(home, HomeEntry.GitKeys))!);
}
