import { useCallback, useEffect, useState } from 'react';
import { api, type Quest, type Registration } from './api';
import { ago, sittingDays } from './format';

/** The coarse state, and the sentence a reader needs to interpret it. */
const STATUS: Record<Quest['status'], string> = {
  Open: 'published — nobody has taken it',
  Taken: "a repository's agent has accepted it",
  Done: 'finished',
  Declined: 'turned down — the note is the part the asker can act on',
};

/** A body long enough to clamp gets an expander instead of a scroll of prose. */
const CLAMP_AT = 240;

/**
 * The task half of the platform (D38, reworked under D40): what has been asked of whom, grouped by
 * where it is in its life, with how long each has sat — the management signal — made visible.
 *
 * Publish and respond go through the same key-gated endpoints and the same `QuestExchange` judgement
 * as every other door, and a refusal is shown verbatim: the service's sentence is the contract. On a
 * keyed remote deployment the browser has no key, so the writes are refused and this view is honestly
 * read-only until person-auth exists (SVC2).
 */
export function QuestsView({ onError, onChanged }: {
  onError: (message: string) => void;
  onChanged?: () => void;
}) {
  const [quests, setQuests] = useState<Quest[] | null>(null);
  const [registry, setRegistry] = useState<Registration[]>([]);
  const [repository, setRepository] = useState('');
  const [includeClosed, setIncludeClosed] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [declining, setDeclining] = useState<string | null>(null);
  const [reason, setReason] = useState('');
  const [composing, setComposing] = useState(false);
  const [expanded, setExpanded] = useState<string | null>(null);
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
      onChanged?.();
    } catch (e) {
      onError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }, [load, onError, onChanged]);

  const publish = () => act(async () => {
    const result = await api.publishQuest(draft);
    setDraft({ from: '', to: '', title: '', body: '' });
    setComposing(false);
    return result;
  });

  const open = (quests ?? []).filter((q) => q.status === 'Open');
  const taken = (quests ?? []).filter((q) => q.status === 'Taken');
  const closed = (quests ?? []).filter((q) => q.status === 'Done' || q.status === 'Declined');
  const groups: { title: string; items: Quest[] }[] = [
    { title: `Open — waiting to be taken (${open.length})`, items: open },
    { title: `In progress (${taken.length})`, items: taken },
    ...(includeClosed ? [{ title: `Closed (${closed.length})`, items: closed }] : []),
  ];

  const card = (quest: Quest) => {
    const sat = sittingDays(quest.filed);
    const long = quest.body.length > CLAMP_AT;
    const isExpanded = expanded === quest.id;
    return (
      <article key={quest.id} className={`group quest ${quest.status.toLowerCase()}`}>
        <header>
          <span className="method">{quest.title}</span>
          <span className="quest-meta">
            {/* Sitting time is the management signal; a week of silence deserves to look like one. */}
            {quest.status === 'Open' && sat >= 7 && <span className="pill declined">sat {sat}d</span>}
            <span className={`pill ${quest.status.toLowerCase()}`} title={STATUS[quest.status]}>
              {quest.status}
            </span>
          </span>
        </header>
        <p className="repos">
          {quest.from} → {quest.to}
          <span className="where-inline">
            {' '}· filed {ago(quest.filed)}{quest.updated !== quest.filed ? ` · moved ${ago(quest.updated)}` : ''} · #{quest.id}
          </span>
        </p>
        <p className={`excerpt${long && !isExpanded ? ' clamp' : ''}`}>{quest.body}</p>
        {long && (
          <button className="link subtle" onClick={() => setExpanded(isExpanded ? null : quest.id)}>
            {isExpanded ? 'less' : 'the whole ask'}
          </button>
        )}
        {quest.note && <p className="excerpt"><em>{quest.note}</em></p>}
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
    );
  };

  return (
    <section className="quests">
      <div className="controls">
        <button className="primary" onClick={() => setComposing(!composing)}>
          {composing ? 'never mind' : 'new quest'}
        </button>
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
          /> include closed
        </label>
      </div>

      {composing && (
        <form className="quest-form" onSubmit={(e) => { e.preventDefault(); void publish(); }}>
          <p className="hint">
            Say what is needed and why, with the evidence — never the change you would make; whoever
            works there may see a better answer. The quest is held by the service and pulled by that
            repository's own agent. Nothing is written into its tree.
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
            <button type="submit" className="primary" disabled={busy}>publish quest</button>
          </div>
        </form>
      )}

      {notice && <p className="suggestion">{notice}</p>}

      {quests === null && <p className="loading">reading…</p>}
      {quests?.length === 0 && (
        <p className="empty">
          {repository ? `Nothing asked of ${repository}.` : 'No open quests anywhere — the family owes itself nothing right now.'}
        </p>
      )}

      {groups.map((group) =>
        group.items.length > 0 && (
          <div key={group.title}>
            <h2 className="section-title">{group.title}</h2>
            {group.items.map(card)}
          </div>
        ))}
    </section>
  );
}
