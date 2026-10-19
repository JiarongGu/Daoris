using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// What sessions on this machine consumed, as every window holds it (PUSH1e, D182 points 2-4): <c>USAGE</c>'s answer, read from the
/// usage file once per wake for every window and pushed when it moves, so a window's mount and focus read <c>USAGE</c> again in no
/// window that holds it.
/// </summary>
/// <remarks>
/// <para><b>One builder, two doors.</b> The route answers <see cref="Answer"/> and the slice judges <see cref="Judge"/>, each record
/// written by <see cref="Session"/> and <see cref="Account"/> over one read of the file, so the pull and the push cannot drift.</para>
///
/// <para><b>Keyed, two partitions</b> (<see cref="StateSlice.Keyed"/>): <see cref="Sessions"/>, each measured session under its id,
/// and <see cref="Accounts"/>, each account's share under its door and account (<see cref="AccountKey"/>). A session's end pushes that
/// session and its account's share alone; the oldest let go past the file's bound is named among the keys gone. A place in the list
/// is no field: the page orders the sessions newest first by <c>when</c>, a tie by id, and the accounts by door then account, as the
/// route orders them.</para>
///
/// <para><b>Its wakes.</b> A session's end, which is when a driven session, an intake or a conversation records what it used, and the
/// service coming up; a terminal's driver sharing the home records into the same file, read at the period.</para>
///
/// <para><b>Desktop-only.</b> A record names the account it ran as, which only this machine's windows read (TOOL3, D47 §4).</para>
/// </remarks>
public static class UsageStateSlice
{
    /// <summary>The slice's name, in the page's words.</summary>
    public const string Name = "usage";

    /// <summary>The partition of measured sessions, keyed by session.</summary>
    public const string Sessions = "sessions";

    /// <summary>The partition of each account's share, keyed by <see cref="AccountKey"/>.</summary>
    public const string Accounts = "accounts";

    /// <summary>What a session's record carries, as the route writes it and the page's <c>UsedSession</c> reads it.</summary>
    public static readonly IReadOnlyList<string> SessionFields = ["session", "repository", "harness", "profile", "used", "size", "when"];

    /// <summary>What an account's share carries, as the route writes it and the page's <c>UsedAccount</c> reads it.</summary>
    public static readonly IReadOnlyList<string> AccountFields = ["harness", "profile", "sessions", "used"];

    /// <summary>What wakes it on the kit's bus: a session's end and the service coming up.</summary>
    public static readonly IReadOnlyList<SliceWake> WakesOn = [new(DriverLoop.EndedType), new("DRIVER_READY")];

    /// <summary>The slice the shell's loop owns, reading <paramref name="loop"/>'s usage file; judged at once, with no service.</summary>
    public static StateSlice Of(DriverLoop loop, IEventBus bus) =>
        new(Name, bus, _ => Task.FromResult<IReadOnlyList<SliceJudged>?>(Judge(loop.Usage.Sessions)))
        {
            Keyed = true,
            WakesOn = WakesOn,
            Failed = error => loop.Log?.Warn("state.slice", ("slice", Name), ("message", error.Message)),
        };

    /// <summary>One judgement over the sessions read: each under its id, each account's share under its key.</summary>
    public static IReadOnlyList<SliceJudged> Judge(IReadOnlyList<UsageEntry> sessions)
    {
        var measured = new JsonObject();
        foreach (var entry in sessions) measured[entry.Session] = Session(entry);
        var shares = new JsonObject();
        foreach (var account in SessionUsage.ByAccount(sessions)) shares[AccountKey(account)] = Account(account);
        return [new SliceJudged(Accounts, shares), new SliceJudged(Sessions, measured)];
    }

    /// <summary><c>USAGE</c>'s answer over one read of the file: the sessions newest first, and each account's share.</summary>
    public static object Answer(SessionUsage usage)
    {
        var sessions = usage.Sessions;
        return new
        {
            // Newest first: a person looking at this is asking about recent work.
            Sessions = sessions.OrderByDescending(entry => entry.When).Select(Session).ToArray(),
            Accounts = SessionUsage.ByAccount(sessions).Select(Account).ToArray(),
        };
    }

    /// <summary>One measured session, as the route and the slice write it: the own home's <c>profile</c> written null, as the route always wrote it.</summary>
    public static JsonNode Session(UsageEntry entry) =>
        Written(new { entry.Session, entry.Repository, entry.Harness, entry.Profile, entry.Used, entry.Size, entry.When });

    /// <summary>One account's share, as the route and the slice write it.</summary>
    public static JsonNode Account(AccountUsage account) => Written(new { account.Harness, account.Profile, account.Sessions, account.Used });

    /// <summary>
    /// An account's key in its partition: the door and the account, <c>claude-code/work</c>, or the door alone for its own home, so a
    /// share of the own home and one of an account never meet.
    /// </summary>
    public static string AccountKey(AccountUsage account) => account.Profile is null ? account.Harness : $"{account.Harness}/{account.Profile}";

    /// <summary>The wire's options with every null written: the page reads the own home's account as <c>profile: null</c>.</summary>
    private static readonly JsonSerializerOptions KeepNulls = new(IpcJson.Options)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    private static JsonNode Written(object record) => JsonSerializer.SerializeToNode(record, KeepNulls)!;
}
