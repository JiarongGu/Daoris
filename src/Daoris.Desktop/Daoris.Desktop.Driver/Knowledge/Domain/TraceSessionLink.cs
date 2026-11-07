using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>One session a chain reaches: its record's facts, then this machine's, then what stood when it started.</summary>
public sealed record TraceSessionLink(string Id)
{
    /// <summary>Its store: <see cref="TraceStores.Sessions"/>.</summary>
    public string Source => TraceStores.Sessions;

    /// <summary><c>driven</c> or <c>chat</c>, as the record says.</summary>
    public string Kind { get; init; } = "driven";

    public TraceState State { get; init; } = new("");

    public DateTimeOffset? Opened { get; init; }

    public DateTimeOffset? Moved { get; init; }

    public TraceAgent Agent { get; init; } = new();

    /// <summary>A teammate's record: it ran on another machine, which keeps its account, tree, events and rules.</summary>
    public bool Teammate { get; init; }

    /// <summary>Its tree's folder name, never its path; null where the record names none.</summary>
    public string? Tree { get; init; }

    /// <summary>Its tree's path, the terminal's: a machine path never goes to a page (D47 §4).</summary>
    [JsonIgnore]
    public string? TreePath { get; init; }

    /// <summary>The commit its tree grew from; null where the record names none.</summary>
    public string? BaseCommit { get; init; }

    public string? Quest { get; init; }

    /// <summary>The ask an intake answers (D65 §1b).</summary>
    public string? Ask { get; init; }

    /// <summary>The record before it on its quest, or that it is the first; null with no quest or no moment it opened.</summary>
    public TraceBefore? Before { get; init; }

    /// <summary>It took the quest through its own connector (STANDDOWN2).</summary>
    public bool Took { get; init; }

    /// <summary>Its record's note, as written.</summary>
    public string? Note { get; init; }

    /// <summary>The person's answer to its park, as written.</summary>
    public string? Answer { get; init; }

    /// <summary>The commits the driver read off its tree at its end; null where its record keeps none.</summary>
    public TraceEvidence? Evidence { get; init; }

    /// <summary>This machine's record of it; null for a teammate's.</summary>
    public TraceEvents? Events { get; init; }

    /// <summary>The rules it was handed; null for a teammate's.</summary>
    public TraceRules? Rules { get; init; }

    /// <summary>Where its work landed; null for a teammate's.</summary>
    public TraceLanding? Landing { get; init; }

    /// <summary>What stood when it started; null unless it is a driven session on a quest.</summary>
    public TraceStood? Stood { get; init; }
}

/// <summary>A record's state, with what its flags add to it.</summary>
/// <param name="State">The record's state, or empty where it says none.</param>
public sealed record TraceState(string State)
{
    /// <summary>A failure an account's limit made (TOOL4c).</summary>
    public bool Limit { get; init; }

    /// <summary>A stop that was not the person's: the driver closed, or the sweep found it orphaned (D104).</summary>
    public bool Interrupted { get; init; }

    /// <summary>A park the person answered: it goes on at the driver's next look (STANDDOWN2).</summary>
    public bool Answered { get; init; }
}

/// <summary>What ran a session, as its record names it; each null where unsaid.</summary>
public sealed record TraceAgent
{
    public string? Door { get; init; }

    /// <summary>The tool's version the record opened on (D49 §4).</summary>
    public string? DoorVersion { get; init; }

    /// <summary>The account it ran as; null on this machine's record is the tool's own sign-in, and unsaid on a teammate's.</summary>
    public string? Account { get; init; }

    /// <summary>A teammate's record: its account stays on the machine that ran it.</summary>
    public bool Teammate { get; init; }
}

/// <summary>The record before a session on its quest, by when each opened.</summary>
public sealed record TraceBefore(bool First)
{
    public string? Session { get; init; }

    public string? State { get; init; }

    /// <summary>Whether it ended (its state is one a record ends in); otherwise it is still in that state.</summary>
    public bool Ended { get; init; }

    /// <summary>When it last moved.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>One of <see cref="TraceTrees"/>.</summary>
    public string? Tree { get; init; }
}

/// <summary>A session's evidence (D46 §4): the driver's heading, its lines, and the commits they name.</summary>
public sealed record TraceEvidence(string Said, IReadOnlyList<string> Lines, IReadOnlyList<TraceCommit> Commits);

public sealed record TraceCommit(string Sha, string Line);

/// <summary>A session a landing names and the service holds no record of: what the landing alone says.</summary>
public sealed record TraceUnrecordedLink(string Session)
{
    public IReadOnlyList<TraceBranch> Branches { get; init; } = [];
}
