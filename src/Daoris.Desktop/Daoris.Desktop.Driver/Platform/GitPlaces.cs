namespace Daoris.Driver;

/// <summary>
/// The scheduler's run places and the rules for taking one, read and changed under its lock.
/// </summary>
/// <remarks>
/// <para><b>The bound</b>: at most <c>bound</c> gits run at once in run places.</para>
/// <para><b>A person's place</b>: the background and every git waiting outside Daoris, whoever asked it (a person's push running its
/// hook, a fetch waiting on a grant), hold at most all but one together, so a person's git that waits on nothing outside Daoris always
/// finds one.</para>
/// <para><b>Outside</b>: gits waiting outside Daoris hold at most <c>outside</c> run places, so reads always have the rest; they count
/// toward no repository's bound, so two hook-running acts of one repository leave a place for their hooks' reads of it.</para>
/// <para><b>A repository's background</b>: one repository runs at most <c>repository</c> of the background's reads, so its fan-out
/// never holds every place.</para>
/// <para>A git placed in a place an outside git lends (<see cref="GitScheduler"/>'s re-entry), or one counted apart after it outlived
/// its kill, holds none of these.</para>
/// </remarks>
internal sealed class GitPlaces(int bound, int outside, int repository)
{
    private readonly Dictionary<string, int> _background = new(StringComparer.OrdinalIgnoreCase);
    private int _running;
    private int _personReads;
    private int _outside;

    /// <summary>The run places held.</summary>
    internal int Running => _running;

    /// <summary>Whether <paramref name="ask"/> may take a run place now for <paramref name="whose"/>.</summary>
    internal bool Opens(GitRunAsk ask, StartClass whose) =>
        _running < bound
        && (whose == StartClass.Person && ask.Wait == GitWait.Read || _running - _personReads < Math.Max(1, bound - 1))
        && (ask.Wait == GitWait.Read || _outside < outside)
        && (ask.Wait == GitWait.Outside || whose == StartClass.Person || _background.GetValueOrDefault(ask.Repository) < repository);

    /// <summary>Take a run place for <paramref name="ask"/>.</summary>
    internal void Take(GitRunAsk ask, StartClass whose) => Move(ask, whose, 1);

    /// <summary>Free the run place <paramref name="ask"/> held.</summary>
    internal void Free(GitRunAsk ask, StartClass whose) => Move(ask, whose, -1);

    private void Move(GitRunAsk ask, StartClass whose, int by)
    {
        _running += by;
        if (ask.Wait == GitWait.Outside)
        {
            _outside += by;
        }
        else if (whose == StartClass.Person)
        {
            _personReads += by;
        }
        else
        {
            var now = _background.GetValueOrDefault(ask.Repository) + by;
            if (now <= 0) _background.Remove(ask.Repository);
            else _background[ask.Repository] = now;
        }
    }
}
