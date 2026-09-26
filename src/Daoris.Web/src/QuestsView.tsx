import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, canBeAsked, type Quest, type QuestStep, type Session } from './api';
import {
  useDismissConflict, usePublishQuest, useQuests, useRegistry, useRespondQuest, useSessions,
} from './queries';
import { stopNotice, useConsidered, useDriver, useNudge, useRetryQuest, useStopSession, useTrustFolder, useUntrusted } from './shell';
import { TrustAsk } from './work/TrustAsk';
import { ago, sentence, sessionTool, sittingDays, size, stamp } from './format';
import { isImage, linksOf, toUpload } from './attachments';
import { CarriedCount, CarryFields, useCarry } from './compose/carry';
import { AsksSection } from './asks/AsksSection';
import { sittingBecause, sittingSentence } from './signals';
import { buildChain } from './map/chain';
import { ChainStrip } from './map/ChainStrip';
import {
  Button, CheckField, Drawer, EmptyState, failure, Icon, Inline, type Notify, PageHeader, Pill, QUEST_TONE, RecordCard,
  SectionTitle, SelectField, SESSION_ACTIVE, SESSION_TONE, SkeletonRows, useErrorNotify,
} from './ui';
import { cn } from './lib/cn';

/** Radix Select cannot carry an empty value, so "everyone" travels as a sentinel. */
const EVERYONE = '*';

/**
 * An ask being written. `links` is the text as typed — read into addresses at publish — and `files`
 * are the browser's own handles, read whole only when the ask is sent (D65 §2).
 */
type Draft = {
  from: string; to: string; title: string; body: string; links: string; files: File[];
  /** One next step (D65 §4), or none. The service takes a longer chain; the composer offers one. */
  step: QuestStep | null;
};
const EMPTY_DRAFT: Draft = { from: '', to: '', title: '', body: '', links: '', files: [], step: null };

/**
 * The task half of the platform (D38, D40): the list is for reading — a card is a summary and a
 * door — and the acting happens in the detail drawer, where there is room to act deliberately.
 * Sitting time is the management signal, so a week of silence wears a mark.
 *
 * Publish and respond go through the same endpoints and the same `QuestExchange` judgement as every
 * other door, refusals surfaced verbatim — the service's sentence is the contract, so it is never
 * translated or rephrased here.
 */
export function QuestsView({
  notify, onAttend, opening, onOpened, focus, onFocused, asking, onAsked, askFocus, onAskFocused,
}: {
  notify: Notify;
  /**
   * The palette asked for the ask composer (INT4c) — an event like `opening`, consumed by identity and
   * cleared by its holder, so asking twice opens it twice.
   */
  asking?: boolean;
  onAsked?: () => void;
  /**
   * An ask a door asked to see — Overview's band, where an ask waits on a person (INT4d). An event
   * like `focus`: its record opens once the ask is loaded, and the holder is told so it can clear it.
   */
  askFocus?: string | null;
  onAskFocused?: () => void;
  /**
   * A quest a door asked to see — the status bar's conflict list (SYNC6b). An event like `opening`:
   * the drawer opens on it once the quest is loaded, and the holder is told so it can clear it.
   */
  focus?: string | null;
  onFocused?: () => void;
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
  const [reading, setReading] = useState(false);
  // What the draft carries (D65 §2) — the drop, the paste and the chooser are shared with the ask
  // composer (INT4c), and so is absorbing a stray drop while the composer is open.
  const carry = useCarry(
    { links: draft.links, files: draft.files },
    ({ links, files }) => setDraft({ ...draft, links, files }),
    composing);

  // A door asked for the composer, pre-filled. Consumed on arrival: this is an event, not a state,
  // and leaving it set would reopen the drawer every time anything else here re-rendered.
  // 🔴 Consumed by IDENTITY, not by the parent clearing it. A state update during render makes React
  // re-run this component at once, with the SAME props — so "if (opening) set…" saw the draft still
  // there on every pass and looped until React gave up ("Too many re-renders"): the door crashed the
  // view it opened. Found by the first test to hold `opening` the way App does. The parent is told
  // from an effect, because updating another component during this one's render is its own warning.
  const [arrived, setArrived] = useState<typeof opening>(null);
  if (opening && opening !== arrived) {
    setArrived(opening);
    setDraft({ ...EMPTY_DRAFT, from: opening.from ?? '', to: opening.to ?? '' });
    carry.forget();
    setComposing(true);
  }
  useEffect(() => { if (opening) onOpened?.(); }, [opening, onOpened]);

  // The ask composer (INT4c), opened by the header's button or by the palette's event — consumed by
  // identity for `opening`'s reason, and forgotten once the holder clears it.
  const [askComposing, setAskComposing] = useState(false);
  const [askSeen, setAskSeen] = useState(false);
  if (asking && !askSeen) {
    setAskSeen(true);
    setAskComposing(true);
  }
  if (!asking && askSeen) setAskSeen(false);
  useEffect(() => { if (asking) onAsked?.(); }, [asking, onAsked]);

  const quests = useQuests(repository === EVERYONE ? null : repository, includeClosed);
  // Every quest, closed ones included, for the chain a drawer shows (MAP1): the step before this one
  // is usually closed, and very often somebody else's.
  const everything = useQuests(null, true);

  // A quest a door named (SYNC6b), consumed by identity for `opening`'s reason (frontend §4b): the
  // last one seen is remembered, a new one opens the drawer once the quest is loaded, and the holder
  // is told from an effect. Forgotten once the holder clears it, so the same quest asked twice opens twice.
  const [focusSeen, setFocusSeen] = useState<string | null>(null);
  const focused = focus ? everything.data?.find((quest) => quest.id === focus) : undefined;
  if (focus && focused && focus !== focusSeen) {
    setFocusSeen(focus);
    setDetail(focused);
  }
  if (!focus && focusSeen) setFocusSeen(null);
  useEffect(() => { if (focus && focusSeen === focus) onFocused?.(); }, [focus, focusSeen, onFocused]);
  const registry = useRegistry();
  const sessions = useSessions(null, true);
  const driver = useDriver();
  // A quest just published is looked at now, not at the driver's next poll.
  const nudge = useNudge();
  const considered = useConsidered().data ?? [];
  const stop = useStopSession();
  // A start the driver is holding for the agent's trust (D73), and the person's grant of it. The
  // question opens inline, for the quest it was asked about, and only on the press.
  const untrusted = useUntrusted().data ?? [];
  const trust = useTrustFolder();
  const [trustingFor, setTrustingFor] = useState<string | null>(null);
  // A quest parked by its strikes (DRV6), started again — `daoris driver retry`'s screen twin (RETRY1).
  const retry = useRetryQuest();
  const publish = usePublishQuest();
  const respond = useRespondQuest();
  const dismiss = useDismissConflict();
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

  // Only what the host says can be asked is offered (D70: an adopter, or a repository registered with
  // a root) — anything else would invite an ask the service refuses. The service still holds the
  // judgement; this only keeps the form from lying.
  const adopters = (registry.data ?? []).filter(canBeAsked).map((r) => r.repository);
  const adopterOptions = adopters.map((name) => ({ value: name, label: name }));
  // Known to be nobody, not merely not loaded yet: a composer that flashed "nobody" would be a lie.
  const nobody = registry.data !== undefined && adopters.length === 0;
  const target = (registry.data ?? []).find((r) => r.repository === draft.to);
  const busy = publish.isPending || respond.isPending || reading;

  const onPublish = async () => {
    // Read whole only now — a file chosen and then removed was never read at all.
    setReading(true);
    let attachments;
    try {
      attachments = await Promise.all(draft.files.map(toUpload));
    } catch (e) {
      notify(sentence(e), 'error');
      return;
    } finally {
      setReading(false);
    }

    const { from, to, title, body } = draft;
    const then = draft.step ? [draft.step] : [];
    publish.mutate({ from, to, title, body, links: linksOf(draft.links), attachments, then }, {
      onSuccess: (result) => {
        notify(result.message);
        setDraft(EMPTY_DRAFT);
        carry.forget();
        setComposing(false);
        nudge();
      },
      onError: failure(notify),
    });
  };

  const onRespond = (quest: Quest, action: 'take' | 'done' | 'decline', why: string | null = null) =>
    respond.mutate({ id: quest.id, action, reason: why }, {
      onSuccess: (result) => {
        notify(result.message);
        setDetail(null);
        setDeclining(false);
        setReason('');
      },
      onError: failure(notify),
    });

  // A person's dismissal (SYNC6c): the drawer stays open on the quest as it now stands, because the
  // quest itself did not move — only what it was waiting on.
  const onDismiss = (quest: Quest, machine: string, sequence: number) =>
    dismiss.mutate({ id: quest.id, machine, sequence }, {
      onSuccess: (result) => {
        notify(result.message);
        setDetail(result.quest);
      },
      onError: failure(notify),
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

  // The question a taken quest's taker asked another repository and waits on (D79), found among every
  // quest, closed ones too: once it closes the quest resumes, and the wait is no longer the news.
  const questionOf = (quest: Quest) =>
    quest.status === 'Taken' && quest.awaits
      ? { id: quest.awaits, quest: everything.data?.find((candidate) => candidate.id === quest.awaits) }
      : null;
  const answered = (question: Quest | undefined) =>
    question?.status === 'Done' || question?.status === 'Declined';

  const card = (quest: Quest) => {
    const sat = sittingDays(quest.filed);
    const tone = QUEST_TONE[quest.status];
    const session = sessionFor.get(quest.id);
    const question = questionOf(quest);
    return (
      <RecordCard
        key={quest.id}
        closed={quest.status === 'Done' || quest.status === 'Declined'}
        onOpen={() => openDetail(quest)}
      >
        {/* Status FIRST, beside the title it describes — the order D41 §5 already specifies for
            Overview's rows (`pill · title · route · how long`). Pushed to the far edge it sat a
            thousand pixels from the thing it was about, and the eye had to cross the whole card to
            connect them. The secondary marks stay right: they are exceptions, not identity. */}
        <header className="flex items-baseline gap-2">
          <Pill tone={tone} title={t(`statusHint.${quest.status}`)}>{t(`status.${quest.status}`)}</Pill>
          <span className="min-w-0 flex-1 truncate text-body font-semibold">{quest.title}</span>
          <span className="flex shrink-0 items-baseline gap-1.5">
            <CarriedCount links={quest.links?.length ?? 0} files={quest.attachments?.length ?? 0} />
            {/* A week of silence is the signal this view exists to surface. */}
            {quest.status === 'Open' && sat >= 7 && (
              <Pill tone="declined">{t('quests.card.sat', { days: sat })}</Pill>
            )}
            {/* Taken and waiting on another repository's answer (D79) — not stuck, and nothing for
                the person to do. The waiting tone, as `awaiting-person` wears it. */}
            {question && !answered(question.quest) && (
              <Pill tone="open" title={t('quests.card.waitsHint')}>{t('quests.card.waits', { id: question.id })}</Pill>
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
            {/* A step of a chain says which quest's close published it (D65 §4). */}
            {quest.parent && <> · {t('quests.card.follows', { id: quest.parent })}</>}
          </span>
        </p>
        <p className="mt-1.5 line-clamp-2 text-body text-ink-soft">{quest.body}</p>
      </RecordCard>
    );
  };

  return (
    <section>
      <PageHeader
        title={t('quests.title')}
        description={t('quests.description')}
        action={
          // Asking leads (D65): the regular task enters at the circle, and the declarations say where
          // it belongs. A quest to a repository the person already knows is the second door.
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="primary" onClick={() => setAskComposing(true)}>
              <Icon name="plus" size={14} />{t('asks.ask')}
            </Button>
            <Button onClick={() => setComposing(true)}>
              <Icon name="plus" size={14} />{t('quests.new')}
            </Button>
          </div>
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

      {/* Asks first: they are where quests come from, and a proposal waits on a person (INT4a). */}
      <AsksSection
        notify={notify}
        includeClosed={includeClosed}
        composing={askComposing}
        onComposingChange={setAskComposing}
        onOpenQuest={(id) => {
          const quest = everything.data?.find((candidate) => candidate.id === id);
          if (quest) openDetail(quest);
        }}
        focus={askFocus}
        onFocused={onAskFocused}
        onAttend={onAttend}
      />

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
                {/* The one loud control is the quest's next step (UX5 U31): taking it while it is
                    open, closing it once it is taken. Done led an open quest too, with taking it
                    offered as the quiet choice. */}
                {detail.status === 'Open' && (
                  <Button variant="primary" disabled={busy} onClick={() => onRespond(detail, 'take')}>
                    {t('quests.detail.take')}
                  </Button>
                )}
                <Button
                  variant={detail.status === 'Taken' ? 'primary' : 'default'}
                  disabled={busy}
                  onClick={() => onRespond(detail, 'done')}
                >
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
            <dd className="m-0">{stamp(detail.filed)} · {ago(detail.filed)}</dd>
            {detail.updated !== detail.filed && (
              <>
                <dt className="text-ink-faint">{t('quests.detail.moved')}</dt>
                <dd className="m-0">{stamp(detail.updated)} · {ago(detail.updated)}</dd>
              </>
            )}
            <dt className="text-ink-faint">{t('quests.detail.state')}</dt>
            <dd className="m-0">{t(`statusHint.${detail.status}`)}</dd>
            {(() => {
              // What its taker asked and waits on (D79), opened in place — the answer is read there.
              const question = questionOf(detail);
              if (!question) return null;
              const closed = answered(question.quest);
              return (
                <>
                  <dt className="text-ink-faint">{t(closed ? 'quests.detail.asked' : 'quests.detail.waitsOn')}</dt>
                  <dd className="m-0">
                    <span className="inline-flex flex-wrap items-baseline gap-1.5">
                      {question.quest ? (
                        <button
                          type="button"
                          className="cursor-pointer border-0 bg-transparent p-0 text-left text-body text-accent underline-offset-2 hover:underline"
                          onClick={() => openDetail(question.quest!)}
                        >
                          <span className="font-mono text-meta">#{question.id}</span> {question.quest.title}
                        </button>
                      ) : (
                        <span className="font-mono text-meta">#{question.id}</span>
                      )}
                      {question.quest && (
                        <>
                          <span className="text-meta text-ink-faint">→ {question.quest.to}</span>
                          <Pill tone={QUEST_TONE[question.quest.status]}>{t(`status.${question.quest.status}`)}</Pill>
                        </>
                      )}
                    </span>
                    <span className="mt-0.5 block text-small text-ink-soft">
                      {t(closed ? 'quests.detail.answered' : 'quests.detail.waitsWhy')}
                    </span>
                  </dd>
                </>
              );
            })()}
            {(() => {
              // Why this machine's driver is not starting it, in its own words (D46 §3) — the
              // whole sentence here, where there is room; the Overview row carries it truncated.
              const because = sittingBecause(considered, detail.id);
              // A wait (D79) is said by the row above, with the question itself; the driver's
              // sentence under it would only say it again.
              const sitting = because?.verdict === 'Waiting' ? null : because;
              const held = untrusted.find((hold) => hold.quest === detail.id);
              // The trust hold stands on its own: it arrives in the same tick as the sentence, and the
              // grant must not wait on a second list having arrived too.
              return (sitting || held) && (
                <>
                  <dt className="text-ink-faint">{t('quests.detail.sitting')}</dt>
                  <dd className="m-0">
                    <Inline text={sitting ? sittingSentence(sitting) : t('work.attention.trustWhy')} />
                    {/* The one hold only the person can lift, offered where it is read (D73). */}
                    {held && trustingFor !== detail.id && (
                      <span className="mt-1.5 block">
                        <Button onClick={() => setTrustingFor(detail.id)}>{t('trust.open')}</Button>
                      </span>
                    )}
                    {/* And the other: a quest parked by its strikes, started again on the press
                        (RETRY1). Counted from where it stands, so the next failures park it again. */}
                    {sitting?.verdict === 'Exhausted' && (
                      <span className="mt-1.5 block">
                        <Button
                          disabled={retry.isPending}
                          onClick={() => retry.mutate({ quest: detail.id }, {
                            onSuccess: (state) => notify(t('quests.detail.retried', {
                              id: detail.id, count: state.strikes,
                            })),
                            onError: failure(notify),
                          })}
                        >
                          {t('quests.detail.retry')}
                        </Button>
                      </span>
                    )}
                  </dd>
                </>
              );
            })()}
          </dl>
          {(() => {
            const held = untrusted.find((hold) => hold.quest === detail.id);
            return held && trustingFor === detail.id && (
              <div className="mb-4">
                <TrustAsk
                  hold={held}
                  busy={trust.isPending}
                  onCancel={() => setTrustingFor(null)}
                  onGrant={() => trust.mutate(held, {
                    onSuccess: (granted) => {
                      notify(granted.message, granted.verified ? 'ok' : 'error');
                      setTrustingFor(null);
                    },
                    onError: failure(notify),
                  })}
                />
              </div>
            );
          })()}
          {(detail.conflicts?.length ?? 0) > 0 && (
            /* A move that reached the remote second (D68 §5): kept on the quest for a person and
               never merged, so it sits above the body — it is what this quest is waiting on. The
               note is that session's own words, verbatim. */
            <section
              aria-label={t('quests.detail.conflicts')}
              className="mb-4 rounded-card border border-line border-l-[3px] border-l-st-open bg-raised px-3 py-2.5"
            >
              <SectionTitle>{t('quests.detail.conflicts')}</SectionTitle>
              <p className="m-0 mb-2 text-small text-ink-soft">{t('quests.detail.conflictsHint')}</p>
              <ul className="m-0 grid list-none gap-1.5 p-0">
                {detail.conflicts!.map((conflict) => (
                  <li
                    key={`${conflict.machine}-${conflict.sequence}`}
                    className="flex items-start justify-between gap-3 text-body"
                  >
                    <span className="grid min-w-0 gap-0.5">
                      <span className="inline-flex flex-wrap items-center gap-1.5">
                        <Icon name="conflict" size={12} className="text-warn" />
                        {t('quests.detail.conflictLine', {
                          machine: conflict.machine, attempted: t(`status.${conflict.attempted}`),
                        })}
                        <span className="text-meta text-ink-faint">· {ago(conflict.at)}</span>
                      </span>
                      {conflict.note && <span className="text-small text-ink-soft">{conflict.note}</span>}
                    </span>
                    {/* Seen, and nothing more to do here: the dismissal travels, so every machine
                        stops showing it (SYNC6c). */}
                    <Button
                      variant="ghost"
                      disabled={dismiss.isPending}
                      onClick={() => onDismiss(detail, conflict.machine, conflict.sequence)}
                    >
                      {t('quests.detail.dismiss')}
                    </Button>
                  </li>
                ))}
              </ul>
            </section>
          )}
          <p className="m-0 whitespace-pre-wrap text-body leading-relaxed">{detail.body}</p>
          {(detail.links?.length ?? 0) > 0 && (
            /* The asker's addresses, as links — the service accepted only http and https, so each
               is an address and nothing that would run. */
            <div className="mt-4">
              <SectionTitle>{t('quests.detail.links')}</SectionTitle>
              <ul className="m-0 grid list-none gap-1 p-0">
                {detail.links!.map((link) => (
                  <li key={link} className="min-w-0">
                    <a
                      href={link} target="_blank" rel="noreferrer"
                      className="inline-flex max-w-full items-center gap-1.5 text-body text-accent underline-offset-2 hover:underline"
                    >
                      <Icon name="link" size={12} />
                      <span className="truncate">{link}</span>
                    </a>
                  </li>
                ))}
              </ul>
            </div>
          )}
          {(detail.attachments?.length ?? 0) > 0 && (
            /* The files, by name (D65 §2). One this machine keeps opens through the host's own route
               — never by its path, which a page does not show — and a picture is shown as one. One
               named on the record and kept elsewhere says so, rather than looking like a broken link. */
            <div className="mt-4">
              <SectionTitle>{t('quests.detail.files')}</SectionTitle>
              <ul className="m-0 grid list-none gap-2 p-0">
                {detail.attachments!.map((file) => (
                  <li key={file.sha256} className="min-w-0 text-body">
                    {file.path ? (
                      <a
                        href={api.attachmentUrl(detail.id, file.sha256)} target="_blank" rel="noreferrer"
                        className="group inline-grid max-w-full gap-1.5 text-accent"
                      >
                        <span className="inline-flex min-w-0 items-center gap-1.5">
                          <Icon name="attach" size={12} />
                          <span className="truncate underline-offset-2 group-hover:underline">{file.name}</span>
                          <span className="shrink-0 font-mono text-meta text-ink-faint">{size(file.bytes)}</span>
                        </span>
                        {isImage(file.name) && (
                          <img
                            src={api.attachmentUrl(detail.id, file.sha256)} alt={file.name}
                            className="max-h-40 max-w-full rounded-control border border-line object-contain"
                          />
                        )}
                      </a>
                    ) : (
                      <span className="inline-flex min-w-0 flex-wrap items-center gap-1.5 text-ink-soft">
                        <Icon name="attach" size={12} />
                        <span className="truncate">{file.name}</span>
                        <span className="font-mono text-meta text-ink-faint">
                          {size(file.bytes)} · {t('quests.detail.fileElsewhere')}
                        </span>
                      </span>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          )}
          {(() => {
            /* The chain this quest belongs to (MAP1): the ask, the steps before and after it, what
               its close will still publish (D65 §4, `{parent}` as the asker wrote it), and on what
               each step ran. Only when there is a chain: a lone quest's session is shown below. */
            const chain = buildChain(
              detail.id, everything.data ?? quests.data ?? [detail], sessions.data ?? []);
            return chain.length > 1 && (
              <div className="mt-4">
                <ChainStrip
                  chain={chain}
                  onQuest={openDetail}
                  onSession={onAttend && driver.data ? (session) => onAttend(session.id) : undefined}
                />
              </div>
            );
          })()}
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
              <section className="mt-5" aria-label={t('quests.session.title')}>
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
                        onSuccess: (answer) => notify(t(stopNotice(answer), { id: session.id })),
                        onError: failure(notify),
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
              </section>
            );
          })()}
        </Drawer>
      )}

      {/* 🔴 Nobody to ask: with nothing registered, `from` and `to` offered nobody and a quest
          written in full could never be published. Seen on the installed window, 2026-09-24. */}
      {composing && nobody && (
        <Drawer
          title={t('quests.compose.title')}
          onClose={() => setComposing(false)}
          meta={<span className="font-mono text-meta text-ink-faint">{t('quests.compose.meta')}</span>}
          footer={<Button variant="ghost" onClick={() => setComposing(false)}>{t('common.close')}</Button>}
        >
          <EmptyState icon="projects" headline={t('quests.compose.nobody.headline')} body={t('quests.compose.nobody.body')} />
        </Drawer>
      )}

      {composing && !nobody && (
        <Drawer
          title={t('quests.compose.title')}
          onClose={() => setComposing(false)}
          meta={<span className="font-mono text-meta text-ink-faint">{t('quests.compose.meta')}</span>}
          footer={
            <div className="flex flex-wrap items-center gap-2">
              <Button
                variant="primary"
                disabled={
                  busy || !draft.from || !draft.to || !draft.title.trim() || !draft.body.trim()
                  // A next step started is a next step owed: the form does not offer half of one.
                  || (draft.step !== null && (!draft.step.to || !draft.step.title.trim() || !draft.step.body.trim()))
                }
                onClick={() => void onPublish()}
              >
                {t('quests.compose.publish')}
              </Button>
              <Button variant="ghost" disabled={busy} onClick={() => setComposing(false)}>
                {t('common.cancel')}
              </Button>
            </div>
          }
        >
          <form
            className="grid gap-3"
            onSubmit={(e) => { e.preventDefault(); void onPublish(); }}
            // The whole composer takes a drop and a paste (D65 §2) — aiming at a box inside a drawer
            // is a chore, and the box below lights up to say where the file went.
            {...carry.handlers}
          >
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
                <Inline text={t('quests.compose.caution', { repository: target.repository })} />
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
            <CarryFields
              carry={{ links: draft.links, files: draft.files }}
              filesLabel={t('quests.compose.filesLabel')}
              leftOff={carry.leftOff}
              dragging={carry.dragging}
              busy={busy}
              onLinks={(links) => setDraft({ ...draft, links })}
              onAttach={carry.attach}
              onRemove={carry.remove}
            />
            {/* The chain (D65 §4): one next step, published by the service when this closes done.
                Behind a press, because most asks are one quest and the form should not say otherwise. */}
            {draft.step === null ? (
              <div>
                <Button
                  type="button" variant="ghost" disabled={busy}
                  onClick={() => setDraft({ ...draft, step: { to: '', title: '', body: '' } })}
                >
                  <Icon name="plus" size={12} />{t('quests.compose.addStep')}
                </Button>
              </div>
            ) : (
              <fieldset className="m-0 grid gap-2.5 rounded-control border border-line-strong px-3 pb-3 pt-2">
                <legend className="px-1 text-small text-ink-soft">{t('quests.compose.stepLegend')}</legend>
                <p className="m-0 text-small text-ink-faint">{t('quests.compose.stepHint')}</p>
                <label className="grid gap-1 text-small text-ink-soft">
                  {t('quests.compose.stepTo')}
                  <SelectField
                    value={draft.step.to}
                    onChange={(to) => setDraft({ ...draft, step: { ...draft.step!, to } })}
                    placeholder={t('quests.compose.toPlaceholder')}
                    ariaLabel={t('quests.compose.stepTo')}
                    options={adopterOptions}
                  />
                </label>
                <label className="grid gap-1 text-small text-ink-soft">
                  {t('quests.compose.stepTitle')}
                  <input
                    value={draft.step.title}
                    onChange={(e) => setDraft({ ...draft, step: { ...draft.step!, title: e.target.value } })}
                    className="min-h-[1.9rem] rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
                  />
                </label>
                <label className="grid gap-1 text-small text-ink-soft">
                  {t('quests.compose.stepBody')}
                  <textarea
                    rows={3} value={draft.step.body}
                    onChange={(e) => setDraft({ ...draft, step: { ...draft.step!, body: e.target.value } })}
                    className="resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
                  />
                </label>
                <div>
                  <Button type="button" variant="ghost" disabled={busy} onClick={() => setDraft({ ...draft, step: null })}>
                    <Icon name="x" size={12} />{t('quests.compose.removeStep')}
                  </Button>
                </div>
              </fieldset>
            )}
          </form>
        </Drawer>
      )}
    </section>
  );
}
