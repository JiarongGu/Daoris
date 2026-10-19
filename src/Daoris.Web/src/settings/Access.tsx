/**
 * Where a pause's value was set (AUTONOMY1a, D184 point 1): for the repository, its workspace, this machine, or nowhere, which is
 * today's behaviour (`AccessRules`' sources).
 */
export type AccessSource = 'repository' | 'workspace' | 'machine' | 'default';

/** One pause as it resolves (`LINES`' `access`): its name, its value and where that was set; a newer driver may name others. */
export type ResolvedPause = { pause: string; value: string; source: AccessSource | (string & {}) };

/**
 * One repository's access setup as the driver resolves it (`LINES`' `access`, AUTONOMY1a): whether every pause is open, and each
 * pause in the order every door says them. Never null for a repository the driver judged; absent on a shell older than it.
 */
export type RepositoryAccess = { repository: string; workspace: string; full: boolean; pauses: ResolvedPause[] };

/** One level as it is set (`STATE`'s `access`, `workspaceAccess`, `machineAccess`): full access, or the pauses it names. */
export type AccessLevel = { full: boolean; pauses: { pause: string; value: string }[] };

/**
 * One change to a level (`SET_ACCESS`'s `access`): `full`, the pauses it sets (a null value taking one back to the level above),
 * or null to clear the level.
 */
export type AccessEdit = { access: 'full' | Record<string, string | null> | null };

/** The edit for a repository, a workspace or this machine, as `SET_ACCESS` takes it: exactly one of the three. */
export type AccessChange = ({ repository: string } | { workspace: string } | { machine: true }) & AccessEdit;
