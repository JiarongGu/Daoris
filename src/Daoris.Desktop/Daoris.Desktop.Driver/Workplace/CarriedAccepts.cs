using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// An Accept the person pressed on a session whose tree was not clean or whose landing conflicted, kept on the session until it lands
/// (LANDBACK1 part 1, D87's note): <i>accepted; lands when its tree is clean</i>. Daoris carried the session on in its own words, and the
/// yes is the person's press, so the landing that follows needs no second one.
/// </summary>
/// <param name="Session">The session the Accept was pressed on.</param>
/// <param name="Tree">Its tree, where the landing is judged.</param>
/// <param name="At">When the person pressed.</param>
/// <param name="Carries">How many times Daoris carried the session on for this Accept, at most <see cref="LandCarry.Bound"/>.</param>
/// <param name="Awaiting">
/// The last carry-on's turn has not ended: the landing waits for it. Set when the words were kept, cleared at the session's next
/// conclusion (<see cref="AutoLander.Concluded"/>), so the landing never judges while the carry-on runs, and a turn that changed nothing
/// is still judged once it ends.
/// </param>
public sealed record CarriedAccept(string Session, string Tree, DateTimeOffset At, int Carries, bool Awaiting);

/// <summary>
/// The Accepts kept on sessions (<see cref="CarriedAccept"/>): <c>&lt;home&gt;/sessions/carried-accepts.json</c>, machine-local like the
/// due list it feeds (<see cref="AutoLandings"/>), since which sessions this machine lands is a fact about this machine.
/// </summary>
/// <remarks>
/// <para><b>One yes per session.</b> A new press replaces the kept one with a fresh count, and the stop, the decline, the discard of the
/// session and a landing's end take it back: a yes outlasts none of them (D87's LANDBACK1 note).</para>
///
/// <para><b>A file that does not read is no yes</b>, never a failure: the session then waits for the person's press, as it did before.</para>
/// </remarks>
public sealed class CarriedAccepts(string home)
{
    public const string FileName = "carried-accepts.json";

    private static readonly object Gate = new();

    private static readonly RecordList<CarriedAccept> List = new(Gate, "accepts", Read, ToJson);

    public string FilePath => DaorisHome.Entry(home, HomeEntry.Sessions, FileName);

    /// <summary>The record's lock across processes: the loop's look and a terminal's press edit this file one at a time.</summary>
    public string LockPath => RecordLock.PathOf(home, FileName);

    /// <summary>Every yes kept, in the order they were pressed.</summary>
    public IReadOnlyList<CarriedAccept> All() => List.Read(FilePath);

    /// <summary>A session's kept yes, or null.</summary>
    public CarriedAccept? Of(string session) => All().FirstOrDefault(each => Same(each, session));

    /// <summary>
    /// The person's press, kept: it replaces any yes the session held, with no carry-on counted yet. Kept only once the carry-on's words
    /// were, so a session Daoris could not tell stays as the Accept refused it.
    /// </summary>
    public void Keep(string session, string tree, DateTimeOffset at) => Edit(all =>
        [.. all.Where(each => !Same(each, session)), new CarriedAccept(session, tree, at, Carries: 0, Awaiting: false)]);

    /// <summary>A carry-on's words were kept: counted, and the landing waits for its turn to end.</summary>
    public void CarriedOn(string session) => Edit(all =>
        [.. all.Select(each => Same(each, session) ? each with { Carries = each.Carries + 1, Awaiting = true } : each)]);

    /// <summary>The carry-on's turn ended: the next look judges the tree.</summary>
    public void TurnEnded(string session) => Edit(all =>
        [.. all.Select(each => Same(each, session) && each.Awaiting ? each with { Awaiting = false } : each)]);

    /// <summary>Take the yes back, where one is kept: a stop, a decline, a discard or its landing's end. Nothing is written where none is.</summary>
    public void Take(string session)
    {
        if (Of(session) is null) return;
        Edit(all => [.. all.Where(each => !Same(each, session))]);
    }

    /// <summary>The yes of sessions whose records are gone (HIST1c), dropped: a deleted session's tree is never landed.</summary>
    public IReadOnlyCollection<string> Forget(IReadOnlyCollection<string> sessions)
    {
        lock (Gate)
        {
            using var held = RecordLock.Take(LockPath);
            var all = All();
            var gone = all.Where(each => sessions.Any(session => Same(each, session))).ToList();
            if (gone.Count == 0) return [];
            AtomicFile.WriteText(FilePath, ToJson([.. all.Where(each => !gone.Contains(each))]));
            return [.. gone.Select(each => each.Session)];
        }
    }

    private static bool Same(CarriedAccept each, string session) => string.Equals(each.Session, session, StringComparison.OrdinalIgnoreCase);

    private void Edit(Func<IReadOnlyList<CarriedAccept>, IReadOnlyList<CarriedAccept>> change) => List.Edit(FilePath, change, LockPath);

    private static string ToJson(IReadOnlyList<CarriedAccept> accepts) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartArray("accepts");
        foreach (var each in accepts)
        {
            writer.WriteStartObject();
            writer.WriteString("session", each.Session);
            writer.WriteString("tree", each.Tree);
            writer.WriteString("at", Moments.Write(each.At.ToUniversalTime()));
            writer.WriteNumber("carries", each.Carries);
            if (each.Awaiting) writer.WriteBoolean("awaiting", true);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    private static CarriedAccept? Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (Text(element, "session") is not { } session || Text(element, "tree") is not { } tree || Moments.Read(Text(element, "at")) is not { } at) return null;
        return new CarriedAccept(session, tree, at, Int(element, "carries") ?? 0, Flag(element, "awaiting"));
    }
}
