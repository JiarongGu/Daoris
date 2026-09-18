import { useCallback, useEffect, useState } from 'react';
import { api, type Quest, type Registration } from './api';

/** The coarse state, and the sentence a reader needs to interpret it. */
const STATUS: Record<Quest['status'], string> = {
  Open: 'published — nobody has taken it',
  Taken: "a repository's agent has accepted it",
  Done: 'finished',
  Declined: 'turned down — the note is the part the asker can act on',
};

/**
 * The task half of the platform (D38): what has been asked of whom, and is anything sitting — the
 * question D32 built the quest system to answer, and one no single repository's backlog can.
 *
 * Publish and respond go through the same key-gated endpoints and the same `QuestExchange` judgement
 * as every other door, and a refusal is shown verbatim: the service's sentence is the contract, and a
 * UI that rephrased it would be a second behaviour. On a keyed remote deployment the browser has no
 * key, so the writes are refused and this view is honestly read-only until person-auth exists (SVC2).
 */
export function QuestsView({ onError }: { onError: (message: string) => void }) {
  const [quests, setQuests] = useState<Quest[] | null>(null);
  const [registry, setRegistry] = useState<Registration[]>([]);
  const [repository, setRepository] = useState('');
  const [includeClosed, setIncludeClosed] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [declining, setDeclining] = useState<string | null>(null);
  const [reason, setReason] = useState('');
  const [draft, setDraft] = useState({ from: '', to: '', title: '', body: '' });
  const [busy, setBusy] = useState(false);

  const load = useCallback((signal?: AbortSignal) => {
    api.quests(repository || null, includeClosed, signal)
      .then(setQuests)
      .catch((e: Error) => { if (e.name !== 'AbortError') onError(e.message); });
  }, [repository, includeClosed, onError]);

  useEffect(() => {
    const abort = new AbortController();
    load(abort.signal);
    api.registry(abort.signal)
      .then(setRegistry)
      .catch((e: Error) => { if (e.name !== 'AbortError') onError(e.message); });
    return () => abort.abort();
  }, [load, onError]);

  // Only an adopter can be addressed — offering anything else would invite an ask the service
  // refuses. The service still holds the judgement; this only keeps the form from lying.
  const adopters = registry.filter((r) => r.adopted).map((r) => r.repository);
  const target = registry.find((r) => r.repository === draft.to);

  const act = useCallback(async (work: () => Promise<{ message: string }>) => {
    setBusy(true);
    setNotice(null);
    try {
      const result = await work();
      setNotice(result.message);
      setDeclining(null);
      setReason('');
      load();
    } catch (e) {
      onError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }, [load, onError]);

  const publish = () => act(async () => {
    const result = await api.publishQuest(draft);
    setDraft({ from: '', to: '', title: '', body: '' });
    return result;
  });

  return (
    <section className="quests">
      <form
        className="quest-form"
        onSubmit={(e) => { e.preventDefault(); void publish(); }}
      >
        <p className="hint">
          Ask another repository for something. Say what is needed and why, with the evidence — never
          the change you would make; whoever works there may see a better answer. The quest is held by
          the service and pulled by that repository's own agent. Nothing is written into its tree.
        </p>
        <div className="row">
          <select
            required value={draft.from} aria-label="from repository"
            onChange={(e) => setDraft({ ...draft, from: e.target.value })}
          >
            <option value="" disabled>from…</option>
            {adopters.map((name) => <option key={name} value={name}>{name}</option>)}
          </select>
          <select
            required value={draft.to} aria-label="to repository"
            onChange={(e) => setDraft({ ...draft, to: e.target.value })}
          >
            <option value="" disabled>to…</option>
            {adopters.map((name) => <option key={name} value={name}>{name}</option>)}
          </select>
          <input
            required placeholder="one line: what is wanted" value={draft.title}
            onChange={(e) => setDraft({ ...draft, title: e.target.value })} style={{ flex: '1 1 16rem' }}
          />
        </div>
        {target && !target.registered && (
          /* The same caution the service gives an agent, shown before the person relies on it. */
          <p className="note">
            `{target.repository}` has not declared what it owns or accepts, so this may not be its
            problem. Worth checking before you rely on it.
          </p>
        )}
        <textarea
          required placeholder="why, and the evidence" value={draft.body}
          onChange={(e) => setDraft({ ...draft, body: e.target.value })}
        />
        <div className="quest-actions">
          <button type="submit" disabled={busy}>publish quest</button>
        </div>
      </form>

      <div className="controls">
        <label>
          addressed to
          <select value={repository} onChange={(e) => setRepository(e.target.value)}>
            <option value="">everyone</option>
            {adopters.map((name) => <option key={name} value={name}>{name}</option>)}
          </select>
        </label>
        <label className="toggle">
          <input
            type="checkbox" checked={includeClosed}
            onChange={(e) => setIncludeClosed(e.target.checked)}
          /> include finished and declined
        </label>
      </div>

      {notice && <p className="suggestion">{notice}</p>}

      {quests === null && <p className="loading">reading…</p>}
      {quests?.length === 0 && (
        <p className="empty">
          {repository ? `Nothing asked of ${repository}.` : 'No open quests anywhere.'}
        </p>
      )}

      {quests?.map((quest) => (
        <article key={quest.id} className={`group quest ${quest.status.toLowerCase()}`}>
          <header>
            <span className="method">{quest.title}</span>
            <span className={`pill ${quest.status.toLowerCase()}`} title={STATUS[quest.status]}>
              {quest.status}
            </span>
          </header>
          <p className="repos">{quest.from} → {quest.to}</p>
          <p className="excerpt">{quest.body}</p>
          {quest.note && <p className="excerpt"><em>{quest.note}</em></p>}
          <span className="where">#{quest.id} · filed {new Date(quest.filed).toLocaleString()}</span>
          <div className="quest-actions">
            {quest.status === 'Open' && (
              <button disabled={busy} onClick={() => void act(() => api.respondQuest(quest.id, 'take', null))}>
                take
              </button>
            )}
            {(quest.status === 'Open' || quest.status === 'Taken') && (
              <>
                <button disabled={busy} onClick={() => void act(() => api.respondQuest(quest.id, 'done', null))}>
                  done
                </button>
                {declining === quest.id ? (
                  <>
                    <input
                      placeholder="the reason — it is the part the asker can act on"
                      value={reason} onChange={(e) => setReason(e.target.value)} style={{ flex: '1 1 16rem' }}
                    />
                    {/* Declining without a reason is refused by the service; the form simply does not
                        offer the mistake. */}
                    <button
                      disabled={busy || !reason.trim()}
                      onClick={() => void act(() => api.respondQuest(quest.id, 'decline', reason))}
                    >
                      decline with this reason
                    </button>
                  </>
                ) : (
                  <button disabled={busy} onClick={() => { setDeclining(quest.id); setReason(''); }}>
                    decline…
                  </button>
                )}
              </>
            )}
          </div>
        </article>
      ))}
    </section>
  );
}
