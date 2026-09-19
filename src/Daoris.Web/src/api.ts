// The service's surface. Same origin in production; the dev server proxies /api, so no code path
// differs between the two. Reads everywhere; the writes are exactly the narrow service-state set —
// quests and refresh (D36, D38). Doctrine has no write here, by design (D31): where a rule should
// change, the UI proposes the command to run in the repository that owns it.

export type Status = { semantic: boolean; tier: string; note?: string };
export type Repository = { name: string; total: number; local: number; canonical: number };
export type Hit = {
  id: string; repository: string; kind: string; title: string;
  path: string; excerpt?: string; score: number;
};
export type Entry = {
  id: string; repository: string; kind: string; provenance: string;
  title: string; path: string; body: string;
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
  note?: string; filed: string; updated: string;
};
export type QuestAction = { quest: Quest; message: string };
export type Registration = {
  repository: string; adopted: boolean; registered: boolean; summary?: string;
  owns: string[]; accepts: string[]; packs: string[]; entries: number;
  /** Machine-local (D46): present only when the service answers a caller on its own machine. */
  root?: string;
};
export type SessionState =
  | 'queued' | 'starting' | 'working' | 'awaiting-person'
  | 'completed' | 'declined' | 'stood-down' | 'failed' | 'stopped';
/** A driver-started session's RECORD (D46) — the process lives on the driving machine, never here. */
export type Session = {
  id: string; quest: string; repository: string; adapter: string; state: SessionState;
  note?: string; evidence?: string; transcript?: string; created: string; updated: string;
};

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

async function post<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(path, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    // The service's refusal sentence IS the contract — a publish to a non-adopter, a decline with no
    // reason, a keyed deployment refusing a browser write. It reaches the person verbatim (D38).
    const parsed = await response.json().catch(() => null);
    throw new Error(parsed?.error ?? `${response.status} ${response.statusText}`);
  }
  return response.json() as Promise<T>;
}

export const api = {
  status: (signal?: AbortSignal) => get<Status>('/api/status', signal),
  repositories: (signal?: AbortSignal) => get<Repository[]>('/api/repositories', signal),
  entry: (id: string, signal?: AbortSignal) =>
    get<Entry>(`/api/entry?id=${encodeURIComponent(id)}`, signal),
  search: (q: string, localOnly: boolean, signal?: AbortSignal) =>
    get<Hit[]>(`/api/search?q=${encodeURIComponent(q)}&localOnly=${localOnly}&limit=40`, signal),
  convergence: (minimumSimilarity: number, signal?: AbortSignal) =>
    get<Convergence[]>(`/api/convergence?minimumSimilarity=${minimumSimilarity}&limit=40`, signal),
  quests: (repository: string | null, includeClosed: boolean, signal?: AbortSignal) =>
    get<Quest[]>(
      `/api/quests?includeClosed=${includeClosed}`
      + (repository ? `&repository=${encodeURIComponent(repository)}` : ''),
      signal,
    ),
  registry: (signal?: AbortSignal) => get<Registration[]>('/api/registry', signal),
  sessions: (repository: string | null, includeClosed: boolean, signal?: AbortSignal) =>
    get<Session[]>(
      `/api/sessions?includeClosed=${includeClosed}`
      + (repository ? `&repository=${encodeURIComponent(repository)}` : ''),
      signal,
    ),
  publishQuest: (quest: { from: string; to: string; title: string; body: string }) =>
    post<QuestAction>('/api/quests', quest),
  respondQuest: (id: string, action: 'take' | 'done' | 'decline', reason: string | null) =>
    post<QuestAction>(`/api/quests/${encodeURIComponent(id)}/respond`, { action, reason }),
  refresh: async () => {
    const response = await fetch('/api/refresh', { method: 'POST' });
    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
    return response.json();
  },
};
