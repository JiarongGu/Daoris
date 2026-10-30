using System.Collections.Frozen;

namespace Daoris.Driver;

/// <summary>The kinds of news the person may be told of (§4.4's notifications row), one row each in <see cref="NotifyRule"/>.</summary>
public enum NewsKind
{
    /// <summary>A session waits on the person.</summary>
    Parked,

    /// <summary>A session ended without the person asking.</summary>
    Ended,

    /// <summary>A quest parked on its failed sessions.</summary>
    QuestParked,

    /// <summary>Starts wait for an account.</summary>
    Waiting,

    /// <summary>A conversation answered the person.</summary>
    Answered,

    /// <summary>A notice another process wrote, arriving in the windows.</summary>
    Arrival,

    /// <summary>An urgent message to everyone on an ask's channel.</summary>
    Message,

    /// <summary>A tool call held for the person.</summary>
    HeldCall,

    /// <summary>A Confirm waiting: a wait on the person, not news.</summary>
    Confirm,

    /// <summary>A line of the look's, listed and counted.</summary>
    Look,
}

/// <summary>
/// The notify rule in one table (FOUNDATION1 m-v, the foundation design §4.4): the switch gates news alike, a Confirm waiting is a wait on the
/// person and stays ungated (D196), and a look's news is never said aloud (NOTIFY2h). Recording is not telling: a notice is kept whatever the
/// switch says, and a piece of news is claimed in the said-set whatever it says, so turning the switch on announces nothing that happened while it
/// was off.
/// </summary>
public static class NotifyRule
{
    /// <summary>How each kind answers the switch.</summary>
    public enum Gate
    {
        /// <summary>Told while the switch is on and not while it is off.</summary>
        Switch,

        /// <summary>Told whatever the switch says.</summary>
        Ungated,

        /// <summary>Never said aloud.</summary>
        Never,
    }

    /// <summary>Each kind and its gate.</summary>
    public static readonly IReadOnlyDictionary<NewsKind, Gate> Table = new Dictionary<NewsKind, Gate>
    {
        [NewsKind.Parked] = Gate.Switch,
        [NewsKind.Ended] = Gate.Switch,
        [NewsKind.QuestParked] = Gate.Switch,
        [NewsKind.Waiting] = Gate.Switch,
        [NewsKind.Answered] = Gate.Switch,
        [NewsKind.Arrival] = Gate.Switch,
        [NewsKind.Message] = Gate.Switch,
        [NewsKind.HeldCall] = Gate.Switch,
        [NewsKind.Confirm] = Gate.Ungated,
        [NewsKind.Look] = Gate.Never,
    }.ToFrozenDictionary();

    /// <summary>The kind of news an attention event is.</summary>
    public static NewsKind KindOf(AttentionKind kind) => kind switch
    {
        AttentionKind.Parked => NewsKind.Parked,
        AttentionKind.Ended => NewsKind.Ended,
        AttentionKind.QuestParked => NewsKind.QuestParked,
        AttentionKind.Waiting => NewsKind.Waiting,
        AttentionKind.Answered => NewsKind.Answered,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "an attention kind with no row in the notify rule"),
    };

    /// <summary>Whether news of <paramref name="kind"/> is told, the switch being as given.</summary>
    public static bool Tells(NewsKind kind, bool switchOn) => Table[kind] switch
    {
        Gate.Switch => switchOn,
        Gate.Ungated => true,
        _ => false,
    };

    /// <summary>Whether an attention event is told, the switch being as given.</summary>
    public static bool Tells(AttentionEvent item, bool switchOn) => Tells(KindOf(item.Kind), switchOn);

    /// <summary>
    /// The machine's notify switch, read from its choices at <paramref name="configPath"/> now, for news told outside any look. Choices that do not
    /// read err toward saying it, as the shell's notifier does.
    /// </summary>
    public static bool SwitchOn(string configPath)
    {
        try
        {
            return DriverConfig.Load(configPath).Notify;
        }
        catch (DriverConfigUnreadableException)
        {
            return true;
        }
    }
}
