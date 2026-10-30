namespace Daoris.Driver;

/// <summary>
/// Census C (FOUNDATION1x, the foundation design §2.2, §2.7; O2-9): a record carrying an <c>actor</c> is held to a role taking its work, by
/// the revision of that role its start kept on its binding. The marks decide the work, since the desks write them at the open and the role
/// says only its staffing; a role taking <c>design</c> is admitted on a record whose marks read <c>build</c>, since design work is read
/// as build from its marks.
/// </summary>
public static class RoleCensus
{
    /// <summary>
    /// Why <paramref name="actor"/> and its binding's role do not take <paramref name="work"/>, or null where the record holds.
    /// </summary>
    /// <param name="work">The work the record's marks read.</param>
    /// <param name="actor">The record's <c>actor</c>, which a start writes as the seeded id of its work; null for a record from before D205.</param>
    /// <param name="bound">The role and revision the binding kept; null for a record whose binding holds none, which is read as its <c>actor</c> at the seed.</param>
    /// <param name="at">The record of a role at a revision (<see cref="RoleCatalog.RecordAt"/>); null where none is kept.</param>
    public static string? Check(string work, string? actor, (string Role, long Revision)? bound, Func<string, long, RoleRecord?> at)
    {
        if (actor is null) return "carries no `actor`";
        if (!ActorChoices.Ids.Contains(actor, StringComparer.Ordinal)) return $"`actor` is `{actor}`, which is not a seeded role every build reads";
        var (role, revision) = bound ?? (actor, 0L);
        if (at(role, revision) is not { } record) return $"its start kept role `{role}` at revision {revision}, which the catalogue does not hold";
        return record.Takes.Contains(work, StringComparer.Ordinal) || (work == RoleWork.Build && record.Takes.Contains(RoleWork.Design, StringComparer.Ordinal))
            ? null
            : $"role `{role}` at revision {revision} does not take `{work}`";
    }
}
