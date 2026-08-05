// The service's read-only surface. Same origin in production; the dev server proxies /api, so no
// code path differs between the two.

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

export const api = {
  status: (signal?: AbortSignal) => get<Status>('/api/status', signal),
  repositories: (signal?: AbortSignal) => get<Repository[]>('/api/repositories', signal),
  entry: (id: string, signal?: AbortSignal) =>
    get<Entry>(`/api/entry?id=${encodeURIComponent(id)}`, signal),
  search: (q: string, localOnly: boolean, signal?: AbortSignal) =>
    get<Hit[]>(`/api/search?q=${encodeURIComponent(q)}&localOnly=${localOnly}&limit=40`, signal),
  convergence: (minimumSimilarity: number, signal?: AbortSignal) =>
    get<Convergence[]>(`/api/convergence?minimumSimilarity=${minimumSimilarity}&limit=40`, signal),
  refresh: async () => {
    const response = await fetch('/api/refresh', { method: 'POST' });
    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
    return response.json();
  },
};
