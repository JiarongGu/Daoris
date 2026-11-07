using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Daoris.Driver;

/// <summary>
/// The rule every pipe Daoris serves on this machine is made with: the instance's pipe and the
/// events relay's alike.
/// </summary>
public static class UserPipe
{
    /// <summary>
    /// This user's alone, and nobody's from another machine. Owned by the user's SID, not left to the token's default owner, which for an
    /// elevated process is the Administrators group: a process of the same user that is not elevated refused a pipe owned so, so the pipe
    /// is this user's at either elevation.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static PipeSecurity Security()
    {
        var security = new PipeSecurity();
        using (var identity = WindowsIdentity.GetCurrent())
        {
            security.SetOwner(identity.User!);
            security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        }

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        return security;
    }
}
