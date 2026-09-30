using System.Runtime.InteropServices;

namespace Daoris.Desktop;

/// <summary>
/// Writes an install's <see cref="TaskbarIdentity"/> into each window's property store (TASKBAR1, D108),
/// so the taskbar groups the window under the person's pinned Daoris and a pin made from the running
/// window starts the launcher at the install's root rather than the application in <c>app/</c>.
/// </summary>
/// <remarks>
/// <para><b>Per window, not per process.</b> The relaunch properties are window properties, read only
/// beside a window-level id, so the id is written there too. <c>SetCurrentProcessExplicitAppUserModelID</c>
/// would add nothing for these windows and would also relabel everything else the process shows, the
/// tray icon's hidden window and its notifications among them: a change TASKBAR1 did not ask for,
/// which no test here could see.</para>
///
/// <para><b>From each handle, back at its end.</b> A property belongs to one window handle, and a
/// recreated handle carries nothing of the old one, so the identity is written as each handle is
/// created. Windows requires a window's properties to be removed before it is closed, or what they
/// hold is not given back, so each is set to empty as the handle is destroyed.</para>
///
/// <para><b>The id last.</b> Windows asks that <c>PreventPinning</c> be set before a window's id, which
/// reads as the taskbar taking a window's other properties when its id arrives. Its documentation says
/// nothing of the order for the relaunch properties, so they are set first on that reading: the order
/// costs nothing if it does not matter.</para>
///
/// <para>A window that cannot be labelled still opens: the cost is a second button, as before.</para>
/// </remarks>
internal static class TaskbarWindow
{
    /// <summary>This process's identity, or null in a workspace build, which wears none.</summary>
    private static readonly TaskbarIdentity? Identity = TaskbarIdentity.For(AppContext.BaseDirectory);

    /// <summary>
    /// Have <paramref name="form"/> wear the install's identity for as long as it has a handle. Nothing
    /// in a workspace build.
    /// </summary>
    public static void Wear(Form form)
    {
        if (Identity is not { } identity) return;

        form.HandleCreated += (_, _) => Write(form.Handle, Properties(identity));
        form.HandleDestroyed += (_, _) =>
        {
            if (form.IsHandleCreated) Write(form.Handle, Cleared);
        };
        if (form.IsHandleCreated) Write(form.Handle, Properties(identity));
    }

    /// <summary>The format every <c>System.AppUserModel</c> key shares (<c>propkey.h</c>).</summary>
    private static readonly Guid AppUserModel = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");

    private const uint RelaunchCommand = 2;       // PKEY_AppUserModel_RelaunchCommand
    private const uint RelaunchIconResource = 3;  // PKEY_AppUserModel_RelaunchIconResource
    private const uint RelaunchDisplayName = 4;   // PKEY_AppUserModel_RelaunchDisplayNameResource
    private const uint Id = 5;                    // PKEY_AppUserModel_ID

    private static (uint Key, string? Value)[] Properties(TaskbarIdentity identity) =>
    [
        (RelaunchCommand, identity.RelaunchCommand),
        (RelaunchDisplayName, identity.RelaunchDisplayName),
        (RelaunchIconResource, identity.RelaunchIcon),
        (Id, identity.AppId),
    ];

    private static readonly (uint Key, string? Value)[] Cleared =
        [(Id, null), (RelaunchCommand, null), (RelaunchDisplayName, null), (RelaunchIconResource, null)];

    /// <summary>Each value as a string, or empty (<c>VT_EMPTY</c>) to remove it. Stored at once: no commit.</summary>
    private static void Write(IntPtr window, (uint Key, string? Value)[] properties)
    {
        if (window == IntPtr.Zero) return;

        IPropertyStore? store = null;
        try
        {
            var iid = typeof(IPropertyStore).GUID;
            if (SHGetPropertyStoreForWindow(window, ref iid, out store) < 0 || store is null) return;

            foreach (var (key, value) in properties)
            {
                var name = new PropertyKey { FormatId = AppUserModel, PropertyId = key };
                var variant = new PropVariant();
                try
                {
                    if (value is not null)
                    {
                        variant.Type = VtLpwstr;
                        variant.Pointer = Marshal.StringToCoTaskMemUni(value);
                    }
                    _ = store.SetValue(ref name, ref variant);
                }
                finally
                {
                    // The store copies what it is given, so the string is ours to free.
                    if (variant.Pointer != IntPtr.Zero) Marshal.FreeCoTaskMem(variant.Pointer);
                }
            }
        }
        catch (Exception error) when (error is COMException or InvalidCastException or EntryPointNotFoundException)
        {
            // Not worth a window: an unlabelled one is grouped by its executable, as before D108.
        }
        finally
        {
            if (store is not null) Marshal.ReleaseComObject(store);
        }
    }

    private const ushort VtLpwstr = 31;

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    /// <summary>A <c>PROPVARIANT</c> carrying a string or nothing: its type, three reserved words, and the union.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort Type;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public IntPtr Pointer;
        public IntPtr Rest;
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(
        IntPtr window, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? store);
}
