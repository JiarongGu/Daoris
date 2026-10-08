namespace Daoris.Knowledge;

/// <summary>
/// A store a newer Daoris wrote, which this build will not open (KSCHEMA1): its schema version is past the one this
/// build knows.
/// </summary>
/// <remarks>
/// <para><b>An older build never drops a newer store.</b> Every host on a machine opens the one file, and a connector an
/// update left behind is an older build. It read any version but its own as a mismatch to rebuild, so it dropped the
/// newer index and stamped its own version, and the newer host then failed every knowledge route. Only a version
/// older than this build's is rebuilt; a newer one is refused, and nothing in the file is written.</para>
///
/// <para><b>A refusal, not a defect</b>: its sentence is the whole of what a person can act on, so it prints as that
/// sentence wherever it is printed, a report of it left unhandled included. The stack would only say where the store
/// was opened, which the sentence already does.</para>
/// </remarks>
public sealed class NewerStoreException : Exception
{
    public NewerStoreException(string store, int found, int known)
        : base(Sentence(store, found, known))
    {
        Store = store;
        Found = found;
        Known = known;
    }

    /// <summary>The file, as the connection names it.</summary>
    public string Store { get; }

    /// <summary>The schema version the file carries.</summary>
    public int Found { get; }

    /// <summary>The schema version this build writes.</summary>
    public int Known { get; }

    /// <summary>The sentence alone: what a host left unhandled prints, and what the machine log keeps as its stack.</summary>
    public override string ToString() => Message;

    private static string Sentence(string store, int found, int known) =>
        $"'{store}' belongs to a newer Daoris: its schema is version {found}, and this build's is {known}. "
        + "This build left it as it was, since rebuilding it would drop what the newer one keeps. "
        + $"Update the Daoris in '{Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)}' to the one that wrote it.";
}
