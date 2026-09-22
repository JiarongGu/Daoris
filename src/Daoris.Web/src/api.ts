// The service's surface. Same origin in production; the dev server proxies /api, so no code path
// differs between the two. Reads everywhere; the writes are exactly the narrow service-state set —
// quests and refresh (D36, D38). Doctrine has no write here, by design (D31): where a rule should
// change, the UI proposes the command to run in the repository that owns it.

// `workspace` rides every cross-repository shape (D48): it is the unit of sharing, so a view that
// shows material from more than one must be able to say which is which. Optional on the way in —
// a host older than workspaces simply does not send it, and nothing here should crash over that.
export type Status = { semantic: boolean; tier: string; note?: string };
/**
 * Which point in a repository's history a deployment's copy of its knowledge came from (D48 §6).
 *
 * Absent on a deployment that reads its own checkouts — it has no feed, and what it shows is the
 * machine's own state. Present on a shared one, where the index is a *claim about a commit*: naming
 * the commit is what makes staleness something a person can see rather than must assume.
 */
export type Provenance = {
  commit: string; shortCommit: string; committedAt: string; branch: string; origin?: string;
};
export type Repository = {
  name: string; total: number; local: number; canonical: number; workspace?: string;
  fed?: Provenance;
};
export type Hit = {
  id: string; repository: string; kind: string; title: string;
  path: string; excerpt?: string; score: number; workspace?: string;
};
export type Entry = {
  id: string; repository: string; kind: string; provenance: string;
  title: string; path: string; body: string; workspace?: string;
};
export type ConvergenceEntry = {
  id: string; repository: string; kind: string; title: string; path: string;
};
export type Convergence = {
  method: 'Identical' | 'Restatement' | 'Convergent';
  similarity: number;
  repositories: string[];
  entries: ConvergenceEntry[];
  suggestion: string;
};
export type Quest = {
  id: string; from: string; to: string; title: string; body: string;
  status: 'Open' | 'Taken' | 'Done' | 'Declined';
  note?: string; filed: string; updated: string; workspace?: string;
};
export type QuestAction = { quest: Quest; message: string };
export type Registration = {
  repository: string; adopted: boolean; registered: boolean; summary?: string;
  owns: string[]; accepts: string[]; packs: string[]; entries: number; workspace?: string;
  /**
   * The checkout on THIS machine — answered only to a loopback caller of a local host (D46/D47 §4),
   * so it is absent in a browser over a remote and absent for a teammate's mirrored registration. Its
   * presence is exactly the question "is there a working tree here to manage".
   */
  root?: string;
  joined?: boolean;
  sharesKnowledge?: boolean;
};
export type SessionState =
  | 'queued' | 'starting' | 'working' | 'awaiting-person'
  | 'completed' | 'declined' | 'stood-down' | 'failed' | 'stopped';
/**
 * A session's RECORD (D46) — the process lives on the driving machine, never here.
 *
 * `kind` is how it was entered (D49 §3): the driver planned a `driven` one from a quest, a person
 * opened a `chat`. Everything else about them is the same, which is the point — and it is why
 * `quest` is optional: a conversation may serve none.
 */
export type Session = {
  id: string; quest?: string | null; repository: string; adapter: string; state: SessionState;
  kind?: 'driven' | 'chat';
  note?: string; evidence?: string; created: string; updated: string; workspace?: string;
  /** The harness version observed at spawn (D49 §4) — which tool produced this, answerable later. */
  harnessVersion?: string | null;
  /**
   * Which named credential profile it ran as. Machine-local and answered only to the machine that
   * ran it, exactly like the transcript — so a browser over a keyed remote sees null here, and that
   * is the guarantee working rather than a field the service forgot to fill.
   */
  profile?: string | null;
  /**
   * The working tree it holds (D51) — the unit of exclusion, and a filesystem path outright, so it
   * is guarded exactly as the profile and the transcript are. Null is the repository's registered
   * root: a session that asked for no tree of its own, or a record from a machine that rightly sent
   * no path. Surfaces show its last segment, which is the branch the tree was cut for.
   */
  tree?: string | null;
};

/**
 * What one re-scan changed — and, when the semantic half failed, the service's own sentence.
 *
 * `absent` names registered repositories whose checkout is no longer where the registry says it is
 * (D48 §3). Named rather than skipped: a repository that quietly stops contributing looks exactly like
 * one with nothing to say, and the count still looks healthy.
 */
export type RefreshReport = {
  entries: number; repositories: number; withheld: number; semanticError?: string; absent?: string[];
};

/** What a retire actually did — and its sentence, which is mostly about what it did NOT do. */
export type Retired = { repository: string; retired: boolean; message: string };

/**
 * A query string from the parameters that are actually set — null and undefined are omitted, so a
 * door asked with no workspace is asked for every circle it holds (D48 §4: the door never invents a
 * default, and neither does this).
 */
import { PAGE_SHOWN } from './results';

function qs(params: Record<string, string | number | boolean | null | undefined>): string {
  const pairs = Object.entries(params)
    .filter(([, value]) => value !== null && value !== undefined)
    .map(([key, value]) => `${key}=${encodeURIComponent(String(value))}`);
  return pairs.length ? `?${pairs.join('&')}` : '';
}

async function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(path, { signal });
  if (!response.ok) {
    // The service reports its own errors as { error }; anything else means the host itself failed,
    // and the status line is the only thing that will say anything useful.
    const body = await response.json().catch(() => null);
    throw new Error(body?.error ?? `${response.status} ${response.statusText}`);
  }
  return response.json() as Promise<T>;
}

async function post<T>(path: string, body: unknown, method: 'POST' | 'DELETE' = 'POST'): Promise<T> {
  const response = await fetch(path, {
    method,
    ...(body === undefined
      ? {}
      : { headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) }),
  });
  if (!response.ok) {
    // The service's refusal sentence IS the contract — a publish to a non-adopter, a decline with no
    // reason, a respond racing a take that already won. It reaches the person verbatim (D38).
    const parsed = await response.json().catch(() => null);
    throw new Error(parsed?.error ?? `${response.status} ${response.statusText}`);
  }
  return response.json() as Promise<T>;
}

export const api = {
  status: (signal?: AbortSignal) => get<Status>('/api/status', signal),
  // Every cross-repository read below takes the scope (WSP5; D48 §4): null asks for every circle the
  // deployment holds, and the switcher says so; a name asks for that one, and nothing else is mixed in.
  repositories: (workspace: string | null, signal?: AbortSignal) =>
    get<Repository[]>(`/api/repositories${qs({ workspace })}`, signal),
  entry: (id: string, signal?: AbortSignal) =>
    get<Entry>(`/api/entry?id=${encodeURIComponent(id)}`, signal),
  search: (q: string, localOnly: boolean, workspace: string | null, signal?: AbortSignal) =>
    // One MORE than the view shows, so the view can say whether there are more without guessing
    // (`results.ts`). The extra row is never rendered.
    get<Hit[]>(`/api/search${qs({ q, localOnly, limit: PAGE_SHOWN + 1, workspace })}`, signal),
  convergence: (minimumSimilarity: number, workspace: string | null, signal?: AbortSignal) =>
    // One more than the view shows, for the same reason search asks for one more (`results.ts`).
    get<Convergence[]>(`/api/convergence${qs({ minimumSimilarity, limit: PAGE_SHOWN + 1, workspace })}`, signal),
  quests: (repository: string | null, includeClosed: boolean, workspace: string | null, signal?: AbortSignal) =>
    get<Quest[]>(`/api/quests${qs({ includeClosed, repository, workspace })}`, signal),
  registry: (workspace: string | null, signal?: AbortSignal) =>
    get<Registration[]>(`/api/registry${qs({ workspace })}`, signal),
  sessions: (repository: string | null, includeClosed: boolean, workspace: string | null, signal?: AbortSignal) =>
    get<Session[]>(`/api/sessions${qs({ includeClosed, repository, workspace })}`, signal),
  // The registration lifecycle (D48 §3/§7). Registration state only: no file is written, no doctrine
  // is touched, and adding a repository still needs the shell — a page may not name a machine path.
  registerRepository: (body: {
    repository: string; root?: string; workspace?: string;
    domain?: { summary?: string; owns: string[]; accepts: string[] };
    packs?: string[]; join?: boolean; shareKnowledge?: boolean;
  }) => post<{ repository: string; workspace: string }>('/api/registry', body),
  wireRepository: (repository: string, workspace: string) =>
    post<{ repository: string; workspace: string }>(
      `/api/registry/${encodeURIComponent(repository)}/workspace`, { workspace }),
  retireRepository: (repository: string) =>
    post<Retired>(`/api/registry/${encodeURIComponent(repository)}`, undefined, 'DELETE'),
  publishQuest: (quest: { from: string; to: string; title: string; body: string }) =>
    post<QuestAction>('/api/quests', quest),
  respondQuest: (id: string, action: 'take' | 'done' | 'decline', reason: string | null) =>
    post<QuestAction>(`/api/quests/${encodeURIComponent(id)}/respond`, { action, reason }),
  // Through the same helper as every write, so the service's refusal — a shared deployment is fed,
  // not scanned — reaches the person as the sentence, never as a bare status code.
  refresh: () => post<RefreshReport>('/api/refresh', {}),
};
