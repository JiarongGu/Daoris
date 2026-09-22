import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Quest, Session } from './api';
import { usePublishQuest, useQuests, useRegistry, useRespondQuest, useSessions } from './queries';
import { useConsidered, useDriver, useStopSession } from './shell';
import { ago, sentence, sessionTool, sittingDays } from './format';
import { sittingBecause } from './signals';
import {
  Button, Card, CheckField, Drawer, EmptyState, Icon, type Notify, PageHeader, Pill, QUEST_TONE,
  SectionTitle, SelectField, SESSION_ACTIVE, SESSION_TONE, SkeletonRows, useErrorNotify,
} from './ui';
import { cn } from './lib/cn';

/** Radix Select cannot carry an empty value, so "everyone" travels as a sentinel. */
const EVERYONE = '*';

type Draft = { from: string; to: string; title: string; body: string };
const EMPTY_DRAFT: Draft = { from: '', to: '', title: '', body: '' };

/**
 * The task half of the platform (D38, D40): the list is for reading — a card is a summary and a
 * door — and the acting happens in the detail drawer, where there is room to act deliberately.
 * Sitting time is the management signal, so a week of silence wears a mark.
 *
 * Publish and respond go through the same endpoints and the same `QuestExchange` judgement as every
 * other door, refusals surfaced verbatim — the service's sentence is the contract, so it is never
 * translated or rephrased here.
 */
export function QuestsView({ notify, onAttend, opening, onOpened }: {
  notify: Notify;
  /**
   * A draft handed in by a door — SURF6b's "send it back as a quest" arrives with the repository the
   * work came from already named. The composer opens on it; the person writes the rest, because the
   * ask and its reason are the part that has to travel (`repository-owns-its-work`).
   */
  opening?: { from?: string; to?: string } | null;
  /** Consumed — so re-rendering, or closing and reopening the view, does not reopen the composer. */
  onOpened?: () => void;
  /**
   * The door into Work (design §3): this view keeps the record summary and hands the session over
   * rather than growing a second console. Absent where Work is — a browser has no frame to open.
   */
  onAttend?: (session: string) => void;
}) {
  const { t } = useTranslation();
  const [repository, setRepository] = useState(EVERYONE);
  const [includeClosed, setIncludeClosed] = useState(false);
  const [detail, setDetail] = useState<Quest | null>(null);
  const [composing, setComposing] = useState(false);
  const [declining, setDeclining] = useState(false);
  const [reason, setReason] = useState('');
  const [draft, setDraft] = useState<Draft>(EMPTY_DRAFT);

  // A door asked for the composer, pre-filled. Consumed on arrival: this is an event, not a state,
  // and leaving it set would reopen the drawer every time anything else here re-rendered.
  if (opening) {
    setDraft({ ...EMPTY_DRAFT, from: opening.from ?? '', to: opening.to ?? '' });
    setComposing(true);
    onOpened?.();
  }

  const quests = useQuests(repository === EVERYONE ? null : repository, includeClosed);
  const registry = useRegistry();
  const sessions = useSessions(null, true);
  const driver = useDriver();
  const considered = useConsidered().data ?? [];
  const stop = useStopSession();
  const publish = usePublishQuest();
  const respond = useRespondQuest();
  // Every query this view renders from, the arc's new ones included — a session surface or driver
  // bridge that fails silently is indistinguishable from a family with no driver attached.
  useErrorNotify(quests.error ?? registry.error ?? sessions.error ?? driver.error, notify);

  // The freshest attempt per quest: a retry is its own record, and the drawer shows where things
  // stand now, not the history (the service keeps that).
  const sessionFor = new Map<string, Session>();
  for (const session of sessions.data ?? []) {
    // A chat may serve no quest at all (D49 §3) — it belongs to its repository, and it is shown in
    // Projects rather than here. Only a session that names a quest can mark one.
    if (!session.quest) continue;
    const held = sessionFor.get(session.quest);
    if (!held || session.updated >= held.updated) sessionFor.set(session.quest, session);
  }

  // Only an adopter can be addressed — offering anything else would invite an ask the service
  // refuses. The service still holds the judgement; this only keeps the form from lying.
  const adopters = (registry.data ?? []).filter((r) => r.adopted).map((r) => r.repository);
  const adopterOptions = adopters.map((name) => ({ value: name, label: name }));
  const target = (registry.data ?? []).find((r) => r.repository === draft.to);
  const busy = publish.isPending || respond.isPending;

  const onPublish = () => publish.mutate(draft, {
    onSuccess: (result) => {
      notify(result.message);
      setDraft(EMPTY_DRAFT);
      setComposing(false);
    },
    onError: (e) => notify(sentence(e), 'error'),
  });

  const onRespond = (quest: Quest, action: 'take' | 'done' | 'decline', why: string | null = null) =>
    respond.mutate({ id: quest.id, action, reason: why }, {
      onSuccess: (result) => {
        notify(result.message);
        setDetail(null);
        setDeclining(false);
        setReason('');
      },
      onError: (e) => notify(sentence(e), 'error'),
    });

  const openDetail = (quest: Quest) => {
    setDetail(quest);
    setDeclining(false);
    setReason('');
  };

  const open = (quests.data ?? []).filter((q) => q.status === 'Open');
  const taken = (quests.data ?? []).filter((q) => q.status === 'Taken');
  const closed = (quests.data ?? []).filter((q) => q.status === 'Done' || q.status === 'Declined');
  const groups: { title: string; items: Quest[] }[] = [
    { title: t('quests.groups.open', { count: open.length }), items: open },
    { title: t('quests.groups.progress', { count: taken.length }), items: taken },
    ...(includeClosed ? [{ title: t('quests.groups.closed', { count: closed.length }), items: closed }] : []),
  ];

  const card = (quest: Quest) => {
    const sat = sittingDays(quest.filed);
    const tone = QUEST_TONE[quest.status];
    const session = sessionFor.get(quest.id);
    return (
      <Card
        key={quest.id}
        className={cn(
          'mb-3.5 cursor-pointer transition-colors duration-(--speed) hover:border-line-strong hover:border-l-accent',
          (quest.status === 'Done' || quest.status === 'Declined') && 'opacity-75',
        )}
      >
        <div
          role="button" tabIndex={0}
          onClick={() => openDetail(quest)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); openDetail(quest); }
          }}
        >
          {/* Status FIRST, beside the title it describes — the order D41 §5 already specifies for
              Overview's rows (`pill · title · route · how long`). Pushed to the far edge it sat a
              thousand pixels from the thing it was about, and the eye had to cross the whole card to
              connect them. The secondary marks stay right: they are exceptions, not identity. */}
          <header className="flex items-baseline gap-2">
            <Pill tone={tone} title={t(`statusHint.${quest.status}`)}>{t(`status.${quest.status}`)}</Pill>
            <span className="min-w-0 flex-1 truncate text-body font-semibold">{quest.title}</span>
            <span className="flex shrink-0 items-baseline gap-1.5">
              {/* A week of silence is the signal this view exists to surface. */}
              {quest.status === 'Open' && sat >= 7 && (
                <Pill tone="declined">{t('quests.card.sat', { days: sat })}</Pill>
              )}
              {/* A live driven session marks its quest; finished ones live in the drawer's record. */}
              {session && SESSION_ACTIVE.has(session.state) && (
                <Pill tone={SESSION_TONE[session.state]} title={t('quests.session.hint')}>
                  {t(`sessionState.${session.state}`)}
                </Pill>
              )}
            </span>
          </header>
          <p className="mt-1 text-body text-accent">
            {quest.from} → {quest.to}
            <span className="font-mono text-meta text-ink-faint">
              {' '}· {t('quests.card.filed', { ago: ago(quest.filed) })}
              {quest.updated !== quest.filed && <> · {t('quests.card.moved', { ago: ago(quest.updated) })}</>}
            </span>
          </p>
          <p className="mt-1.5 line-clamp-2 text-body text-ink-soft">{quest.body}</p>
        </div>
      </Card>
    );
  };

  return (
    <section>
      <PageHeader
        title={t('quests.title')}
        description={t('quests.description')}
        action={
          <Button variant="primary" onClick={() => setComposing(true)}>
            <Icon name="plus" size={14} />{t('quests.new')}
          </Button>
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-4">
        <label className="flex items-center gap-2.5 text-body text-ink-soft">
          {t('quests.addressedTo')}
          <SelectField
            value={repository}
            onChange={setRepository}
            ariaLabel={t('quests.addressedTo')}
            options={[{ value: EVERYONE, label: t('quests.everyone') }, ...adopterOptions]}
          />
        </label>
        <CheckField checked={includeClosed} onChange={setIncludeClosed} label={t('quests.includeClosed')} />
      </div>

      {quests.isPending && <SkeletonRows rows={4} />}

      {quests.data?.length === 0 && (
        <EmptyState
          icon="inbox"
          headline={repository === EVERYONE
            ? t('quests.empty.headlineAll')
            : t('quests.empty.headlineFor', { repository })}
          body={t('quests.empty.body')}
          action={
            <Button onClick={() => setComposing(true)}>
              <Icon name="plus" size={14} />{t('overview.outstanding.ask')}
            </Button>
          }
        />
      )}

      <div className={cn(busy && quests.data && 'opacity-60 transition-opacity duration-(--speed)')}>
        {groups.map((group) =>
          group.items.length > 0 && (
            <div key={group.title}>
              <SectionTitle>{group.title}</SectionTitle>
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
              <Pill tone={QUEST_TONE[detail.status]}>
                {t(`status.${detail.status}`)}
              </Pill>
              <span className="font-mono text-meta text-ink-faint">#{detail.id}</span>
            </>
          }
          footer={
            (detail.status === 'Open' || detail.status === 'Taken') && (
              <div className="flex w-full flex-wrap items-center gap-2">
                {detail.status === 'Open' && (
                  <Button disabled={busy} onClick={() => onRespond(detail, 'take')}>
                    <Icon name="check" size={14} />{t('quests.detail.take')}
                  </Button>
                )}
                <Button variant="primary" disabled={busy} onClick={() => onRespond(detail, 'done')}>
                  {t('quests.detail.done')}
                </Button>
                {declining ? (
                  <>
                    <input
                      autoFocus
                      placeholder={t('quests.detail.declinePlaceholder')}
                      value={reason}
                      onChange={(e) => setReason(e.target.value)}
                      className="min-h-[1.9rem] flex-1 basis-56 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
                    />
                    {/* Declining without a reason is refused by the service; the form does not offer
                        the mistake. */}
                    <Button
                      variant="danger" disabled={busy || !reason.trim()}
                      onClick={() => onRespond(detail, 'decline', reason)}
                    >
                      {t('quests.detail.declineConfirm')}
                    </Button>
                  </>
                ) : (
                  <Button variant="ghost" disabled={busy} onClick={() => setDeclining(true)}>
                    {t('quests.detail.decline')}
                  </Button>
                )}
              </div>
            )
          }
        >
          <dl className="mb-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-body">
            <dt className="text-ink-faint">{t('quests.detail.from')}</dt><dd className="m-0">{detail.from}</dd>
            <dt className="text-ink-faint">{t('quests.detail.to')}</dt><dd className="m-0">{detail.to}</dd>
            <dt className="text-ink-faint">{t('quests.detail.filed')}</dt>
            <dd className="m-0">{new Date(detail.filed).toLocaleString()} · {ago(detail.filed)}</dd>
            {detail.updated !== detail.filed && (
              <>
                <dt className="text-ink-faint">{t('quests.detail.moved')}</dt>
                <dd className="m-0">{new Date(detail.updated).toLocaleString()} · {ago(detail.updated)}</dd>
              </>
            )}
            <dt className="text-ink-faint">{t('quests.detail.state')}</dt>
            <dd className="m-0">{t(`statusHint.${detail.status}`)}</dd>
            {(() => {
              // Why this machine's driver is not starting it, in its own words (D46 §3) — the
              // whole sentence here, where there is room; the Overview row carries it truncated.
              const sitting = sittingBecause(considered, detail.id);
              return sitting && (
                <>
                  <dt className="text-ink-faint">{t('quests.detail.sitting')}</dt>
                  <dd className="m-0">{sitting.reason}</dd>
                </>
              );
            })()}
          </dl>
          <p className="m-0 whitespace-pre-wrap text-body leading-relaxed">{detail.body}</p>
          {detail.note && (
            <p className="mt-4 rounded-control bg-accent-soft px-3 py-2.5 text-body italic">{detail.note}</p>
          )}
          {(() => {
            const session = sessionFor.get(detail.id);
            if (!session) return null;
            return (
              /* The driven session's RECORD (D46 §4) — read-only here: the process, and the person's
                 controls over it, live where a driver is attached, which is the desktop. The note and
                 evidence are the driver's observations and render verbatim, like every system sentence. */
              <div className="mt-5">
                <SectionTitle>{t('quests.session.title')}</SectionTitle>
                <div className="flex flex-wrap items-center gap-2">
                  <Pill tone={SESSION_TONE[session.state]}>{t(`sessionState.${session.state}`)}</Pill>
                  <span className="font-mono text-meta text-ink-faint">
                    {session.id} · {sessionTool(session)}
                    {' · '}{t('quests.session.moved', { ago: ago(session.updated) })}
                  </span>
                  {/* Stop reaches a PROCESS, so it renders only where one is actually running — the
                      shell's driver — never in a browser that could only wish (D46 §6). */}
                  {driver.data?.running.includes(session.id) && (
                    <Button
                      variant="danger"
                      disabled={stop.isPending}
                      onClick={() => stop.mutate(session.id, {
                        onSuccess: () => notify(t('quests.session.stopped', { id: session.id })),
                        onError: (e) => notify(sentence(e), 'error'),
                      })}
                    >
                      {t('quests.session.stop')}
                    </Button>
                  )}
                </div>
                {session.note && (
                  <p className="mt-2 mb-0 text-body text-ink-soft">{session.note}</p>
                )}
                {session.evidence && (
                  <pre className="mt-2 mb-0 overflow-x-auto whitespace-pre-wrap rounded-control border border-line-strong bg-raised px-3 py-2.5 font-mono text-small">
                    {session.evidence}
                  </pre>
                )}
                {/* One home for the stream (design §3, D55): the console was here, and a session's
                    console is now the Work frame's output panel. This keeps the record summary and
                    becomes a DOOR — which is only offered where Work exists at all. */}
                {onAttend && driver.data && (
                  <Button className="mt-2.5" onClick={() => onAttend(session.id)}>{t('work.open')}</Button>
                )}
                <p className="mt-2 mb-0 text-small text-ink-faint">{t('quests.session.hint')}</p>
              </div>
            );
          })()}
        </Drawer>
      )}

      {composing && (
        <Drawer
          title={t('quests.compose.title')}
          onClose={() => setComposing(false)}
          meta={<span className="font-mono text-meta text-ink-faint">{t('quests.compose.meta')}</span>}
          footer={
            <div className="flex flex-wrap items-center gap-2">
              <Button
                variant="primary"
                disabled={busy || !draft.from || !draft.to || !draft.title.trim() || !draft.body.trim()}
                onClick={onPublish}
              >
                {t('quests.compose.publish')}
              </Button>
              <Button variant="ghost" disabled={busy} onClick={() => setComposing(false)}>
                {t('common.cancel')}
              </Button>
            </div>
          }
        >
          <form className="grid gap-3" onSubmit={(e) => { e.preventDefault(); onPublish(); }}>
            <p className="m-0 text-body text-ink-soft">{t('quests.compose.hint')}</p>
            <div className="grid grid-cols-2 gap-2.5 max-md:grid-cols-1">
              <label className="grid gap-1 text-small text-ink-soft">
                {t('quests.compose.from')}
                <SelectField
                  value={draft.from} required
                  onChange={(from) => setDraft({ ...draft, from })}
                  placeholder={t('quests.compose.fromPlaceholder')}
                  ariaLabel={t('quests.compose.from')}
                  options={adopterOptions}
                />
              </label>
              <label className="grid gap-1 text-small text-ink-soft">
                {t('quests.compose.to')}
                <SelectField
                  value={draft.to} required
                  onChange={(to) => setDraft({ ...draft, to })}
                  placeholder={t('quests.compose.toPlaceholder')}
                  ariaLabel={t('quests.compose.to')}
                  options={adopterOptions}
                />
              </label>
            </div>
            <label className="grid gap-1 text-small text-ink-soft">
              {t('quests.compose.titleLabel')}
              <input
                required value={draft.title}
                onChange={(e) => setDraft({ ...draft, title: e.target.value })}
                className="min-h-[1.9rem] rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
              />
            </label>
            {target && !target.registered && (
              /* The same caution the service gives an agent, before the person relies on it. */
              <p className="m-0 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
                {t('quests.compose.caution', { repository: target.repository })}
              </p>
            )}
            <label className="grid gap-1 text-small text-ink-soft">
              {t('quests.compose.bodyLabel')}
              <textarea
                required value={draft.body}
                onChange={(e) => setDraft({ ...draft, body: e.target.value })}
                className="min-h-28 resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
              />
            </label>
          </form>
        </Drawer>
      )}
    </section>
  );
}
