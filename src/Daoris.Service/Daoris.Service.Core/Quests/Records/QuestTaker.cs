namespace Daoris.Knowledge;

/// <summary>
/// Who takes a quest (FABRIC-CLOSE1o): the session the authenticated intake names, null for a door that names none, and how this
/// machine's remotes read at the moment of the take, by circle. What a take needs to judge the bind named for it, inside its write.
/// </summary>
/// <param name="Session">The taking session, from its connector's intake (<c>DAORIS_SESSION_ID</c>); null where no session is named.</param>
/// <param name="Remote">
/// Whether a circle syncs through a remote, by the map sync reads, that has not said every machine of it knows a bound quest
/// (FABRIC-CLOSE1i), asked inside the take's write; null reads none.
/// </param>
public sealed record QuestTaker(string? Session, Func<string, bool>? Remote = null);
