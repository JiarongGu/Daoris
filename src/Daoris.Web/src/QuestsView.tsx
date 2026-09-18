import { useCallback, useEffect, useState } from 'react';
import { api, type Quest, type Registration } from './api';
import { ago, sittingDays } from './format';
import { Drawer, EmptyState, Icon, PageHeader, SkeletonRows } from './ui';

/** The coarse state, and the sentence a reader needs to interpret it. */
const STATUS: Record<Quest['status'], string> = {
  Open: 'published — nobody has taken it',
  Taken: "a repository's agent has accepted it",
  Done: 'finished',
  Declined: 'turned down — the note is the part the asker can act on',
};

/**
 * The task half of the platform (D38, D40), in the console language (D41): the list is for reading —
 * a card is a summary and a door — and the acting happens in the detail drawer, where there is room
 * to act deliberately. Sitting time is the management signal, so a week of silence wears a mark.
 *
 * Publish and respond go through the same key-gated endpoints and the same `QuestExchange` judgement
 * as every other door, refusals surfaced verbatim. On a keyed remote deployment the browser has no
 * key, so writes are refused and this view is honestly read-only until person-auth exists (SVC2).
 */
export function QuestsView({ notify, onChanged }: {
  notify: (text: string, kind?: 'ok' | 'error') => void;
  onChanged?: () => void;
}) {
  const [quests, setQuests] = useState<Quest[] | null>(null);
  const [registry, setRegistry] = useState<Registration[]>([]);
  const [repository, setRepository] = useState('');
  const [includeClosed, setIncludeClosed] = useState(false);
  const [detail, setDetail] = useState<Quest | null>(null);
  const [composing, setComposing] = useState(false);
  const [declining, setDeclining] = useState(false);
  const [reason, setReason] = useState('');
  const [draft, setDraft] = useState({ from: '', to: '', title: '', body: '' });
  const [busy, setBusy] = useState(false);

  const load = useCallback((signal?: AbortSignal) => {
    api.quests(repository || null, includeClosed, signal)
      .then(setQuests)
      .catch((e: Error) => { if (e.name !== 'AbortError') notify(e.message, 'error'); });
  }, [repository, includeClosed, notify]);

  useEffect(() => {
    const abort = new AbortController();
    load(abort.signal);
    api.registry(abort.signal)
      .then(setRegistry)
      .catch((e: Error) => { if (e.name !== 'AbortError') notify(e.message, 'error'); });
    return () => abort.abort();
  }, [load, notify]);

  // Only an adopter can be addressed — offering anything else would invite an ask the service
  // refuses. The service still holds the judgement; this only keeps the form from lying.
  const adopters = registry.filter((r) => r.adopted).map((r) => r.repository);
  const target = registry.find((r) => r.repository === draft.to);

  const act = useCallback(async (work: () => Promise<{ message: string }>, after?: () => void) => {
    setBusy(true);
    try {
      const result = await work();
      notify(result.message);
      setDeclining(false);
      setReason('');
      after?.();
      load();
      onChanged?.();
    } catch (e) {
      notify((e as Error).message, 'error');
    } finally {
      setBusy(false);
    }
  }, [load, notify, onChanged]);

  const publish = () => act(
    () => api.publishQuest(draft),
    () => { setDraft({ from: '', to: '', title: '', body: '' }); setComposing(false); },
  );

  const respond = (quest: Quest, action: 'take' | 'done' | 'decline', why: string | null = null) =>
    act(() => api.respondQuest(quest.id, action, why), () => setDetail(null));

  const openDetail = (quest: Quest) => {
    setDetail(quest);
    setDeclining(false);
    setReason('');
  };

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
    return (
      <article
        key={quest.id}
        className={`group quest-card ${quest.status.toLowerCase()}`}
        role="button" tabIndex={0}
        onClick={() => openDetail(quest)}
        onKeyDown={(event) => {
          if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); openDetail(quest); }
        }}
      >
        <header>
          <span className="method">{quest.title}</span>
          <span className="quest-meta">
            {/* A week of silence is the signal this view exists to surface. */}
            {quest.status === 'Open' && sat >= 7 && <span className="pill sat">sat {sat}d</span>}
            <span className={`pill ${quest.status.toLowerCase()}`} title={STATUS[quest.status]}>
              {quest.status}
            </span>
          </span>
        </header>
        <p className="route">
          {quest.from} → {quest.to}
          <span className="where-inline">
            {' '}· filed {ago(quest.filed)}{quest.updated !== quest.filed ? ` · moved ${ago(quest.updated)}` : ''}
          </span>
        </p>
        <p className="excerpt clamp">{quest.body}</p>
      </article>
    );
  };

  return (
    <section className="quests">
      <PageHeader
        title="Quests"
        description="What has been asked of whom — held by the service, pulled by each repository's own agent, never written into anyone's tree."
        action={
          <button className="primary" onClick={() => setComposing(true)}>
            <Icon name="plus" />new quest
          </button>
        }
      />

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
          /> include closed
        </label>
      </div>

      {quests === null && <SkeletonRows rows={4} />}

      {quests?.length === 0 && (
        <EmptyState
          icon="inbox"
          headline={repository ? `Nothing asked of ${repository}` : 'No open quests anywhere'}
          body="The family owes itself nothing right now. When a project needs something from a sibling, it is asked for here — never edited across."
          action={
            <button onClick={() => setComposing(true)}><Icon name="plus" />ask for something</button>
          }
        />
      )}

      <div data-busy={busy && quests !== null}>
        {groups.map((group) =>
          group.items.length > 0 && (
            <div key={group.title}>
              <h2 className="section-title">{group.title}</h2>
              {group.items.map(card)}
            </div>
          ))}
      </div>

      {detail && (
        <Drawer
          title={detail.title}
          onClose={() => setDetail(null)}
          meta={
            <>
              <span className={`pill ${detail.status.toLowerCase()}`}>{detail.status}</span>
              <span className="where-inline">#{detail.id}</span>
            </>
          }
          footer={
            (detail.status === 'Open' || detail.status === 'Taken') && (
              <div className="quest-actions">
                {detail.status === 'Open' && (
                  <button disabled={busy} onClick={() => void respond(detail, 'take')}>
                    <Icon name="check" />take
                  </button>
                )}
                <button className="primary" disabled={busy} onClick={() => void respond(detail, 'done')}>
                  done
                </button>
                {declining ? (
                  <>
                    <input
                      autoFocus
                      placeholder="the reason — it is the part the asker can act on"
                      value={reason} onChange={(e) => setReason(e.target.value)}
                    />
                    {/* Declining without a reason is refused by the service; the form does not offer
                        the mistake. */}
                    <button
                      className="danger" disabled={busy || !reason.trim()}
                      onClick={() => void respond(detail, 'decline', reason)}
                    >
                      decline with this reason
                    </button>
                  </>
                ) : (
                  <button className="ghost" disabled={busy} onClick={() => setDeclining(true)}>
                    decline…
                  </button>
                )}
              </div>
            )
          }
        >
          <dl className="detail-grid">
            <dt>from</dt><dd>{detail.from}</dd>
            <dt>to</dt><dd>{detail.to}</dd>
            <dt>filed</dt><dd>{new Date(detail.filed).toLocaleString()} · {ago(detail.filed)}</dd>
            {detail.updated !== detail.filed && (
              <><dt>moved</dt><dd>{new Date(detail.updated).toLocaleString()} · {ago(detail.updated)}</dd></>
            )}
            <dt>state</dt><dd>{STATUS[detail.status]}</dd>
          </dl>
          <p className="detail-body">{detail.body}</p>
          {detail.note && <p className="detail-note"><em>{detail.note}</em></p>}
        </Drawer>
      )}

      {composing && (
        <Drawer
          title="New quest"
          onClose={() => setComposing(false)}
          meta={<span className="where-inline">published to the service; pulled by its owner</span>}
          footer={
            <div className="quest-actions">
              <button
                className="primary" disabled={busy || !draft.from || !draft.to || !draft.title.trim() || !draft.body.trim()}
                onClick={() => void publish()}
              >
                publish quest
              </button>
              <button className="ghost" disabled={busy} onClick={() => setComposing(false)}>never mind</button>
            </div>
          }
        >
          <form className="quest-form" onSubmit={(e) => { e.preventDefault(); void publish(); }}>
            <p className="hint">
              Say what is needed and why, with the evidence — never the change you would make; whoever
              works there may see a better answer.
            </p>
            <div className="row">
              <label className="field">
                from
                <select
                  required value={draft.from}
                  onChange={(e) => setDraft({ ...draft, from: e.target.value })}
                >
                  <option value="" disabled>the repository asking…</option>
                  {adopters.map((name) => <option key={name} value={name}>{name}</option>)}
                </select>
              </label>
              <label className="field">
                to
                <select
                  required value={draft.to}
                  onChange={(e) => setDraft({ ...draft, to: e.target.value })}
                >
                  <option value="" disabled>the repository asked…</option>
                  {adopters.map((name) => <option key={name} value={name}>{name}</option>)}
                </select>
              </label>
            </div>
            <label className="field">
              what is wanted, in one line
              <input
                required value={draft.title}
                onChange={(e) => setDraft({ ...draft, title: e.target.value })}
              />
            </label>
            {target && !target.registered && (
              /* The same caution the service gives an agent, before the person relies on it. */
              <p className="note">
                `{target.repository}` has not declared what it owns or accepts, so this may not be its
                problem. Worth checking before you rely on it.
              </p>
            )}
            <label className="field">
              why, and the evidence
              <textarea
                required value={draft.body}
                onChange={(e) => setDraft({ ...draft, body: e.target.value })}
              />
            </label>
          </form>
        </Drawer>
      )}
    </section>
  );
}
