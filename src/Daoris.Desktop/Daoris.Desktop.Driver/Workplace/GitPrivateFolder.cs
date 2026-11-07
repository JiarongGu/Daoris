namespace Daoris.Driver;

/// <summary>
/// A folder of a git secret, shared by the keys' and the tokens' folders: made with its
/// access limited to the person's Windows account (inherited entries removed; SYSTEM and Administrators keep their implicit reach),
/// or to the owner on another system, <b>before</b> a byte is written into it. The base library alone: the doors compile it in.
/// </summary>
public static class GitPrivateFolder
{
    /// <summary>Make <paramref name="folder"/> with its access limited to this account in one call, so it never stands with its parent's.</summary>
    /// <exception cref="DriverException">The folder is there already.</exception>
    public static void Make(string folder, string what, string code)
    {
        if (Directory.Exists(folder)) throw new DriverException($"git: {what} is there already, and a secret is never written over ({code})");
        Directory.CreateDirectory(Path.GetDirectoryName(folder)!);
        if (OperatingSystem.IsWindows())
        {
            var user = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
            var security = new System.Security.AccessControl.DirectorySecurity();
            security.SetOwner(user);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                user, System.Security.AccessControl.FileSystemRights.FullControl,
                System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                System.Security.AccessControl.PropagationFlags.None, System.Security.AccessControl.AccessControlType.Allow));
            FileSystemAclExtensions.Create(new DirectoryInfo(folder), security);
        }
        else
        {
            Directory.CreateDirectory(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>Whether a folder's access is this account's alone, inherited entries removed (the table's check).</summary>
    public static bool IsPrivate(string folder)
    {
        if (!OperatingSystem.IsWindows()) return (File.GetUnixFileMode(folder) & (UnixFileMode.GroupRead | UnixFileMode.OtherRead)) == 0;
        return IsPrivateHere(folder);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool IsPrivateHere(string folder)
    {
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        var security = new DirectoryInfo(folder).GetAccessControl();
        var rules = security.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier))
            .Cast<System.Security.AccessControl.FileSystemAccessRule>().ToList();
        return security.AreAccessRulesProtected && rules.Count > 0 && rules.All(rule => rule.IdentityReference.Equals(user) && !rule.IsInherited);
    }
}
