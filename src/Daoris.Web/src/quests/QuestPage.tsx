import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, type Quest, type Session } from '../api';
import { isImage } from '../attachments';
import { ago, sessionTool, size, stamp } from '../format';
import { ExternalLink } from '../links';
import type { ChainStep } from '../map/chain';
import { ChainStrip } from '../map/ChainStrip';
import { type Consideration, sittingSentence, type TrustHold } from '../signals';
import { Button, Icon, Inline, Pill, Prose, QUEST_TONE, SectionTitle, SESSION_TONE } from '../ui';
import { TrustAsk } from '../work/TrustAsk';
import { type MainNotice, PageHead, PageSection, ViewMain } from '../work/ViewMain';

/** A record's row of facts: its name, and what it holds. */
function Fact({ name, children }: { name: string; children: ReactNode }) {
  return (
    <>
      <dt className="text-ink-faint">{name}</dt>
      <dd className="m-0">{children}</dd>
    </>
  );
}

/**
 * **A quest's page** (FRAME1d, D118 §3d): its record in Quests' main area, where it was a drawer that lay
 * over the side bar and the panel. Its acts are in its header and the loud one is its next step (UX5 U31):
 * *Take* while it is open, *Mark done* once it is taken. Then what it is waiting on, the person's two holds
 * (a folder to trust, D73; a quest parked by its strikes, RETRY1), a conflict a person must see (D68 §5), the
 * ask itself, what it carries (D65 §2), its chain (MAP1) and its driven session's record (D46 §4).
 *
 * @remarks
 * **A molecule**: every state is reached by its props, and every press goes out. What asks under the header —
 * *Decline…*'s reason, *Delete…*'s sentence — is its own, so it resets with the quest it was asked of.
 *
 * - **A delete asks once** (D95): its first press opens a sentence saying the second removes the record, which
 *   nothing gives back; offered only where the service says it may go (`deletable`).
 * - **A decline needs its reason**, which the service refuses without; the form does not offer the mistake.
 * - **What the service and the driver say is said verbatim**: the session's note and evidence, a conflict's
 *   note, the driver's sentence about why it sits (translated by its verdict, never by its words, U27).
 * - **A page never prints a machine path it was answered** (D47 §4): a kept file opens through the host's own
 *   route, and one kept elsewhere says so.
 */
export function QuestPage({
  quest, lanes, question, sitting, hold, chain = [], session, running = false,
  busy = false, retrying = false, trusting = false, granting = false, dismissing = false, stopping = false,
  onRespond, onDelete, onDismiss, onRetry, onTrusting, onGrant, onStop, onOpenQuest, onAttend,
}: {
  quest: Quest;
  /** Its lanes as its repository declares them (D115 §2.2), each named where its registration says. */
  lanes?: string;
  /** What its taker asked another repository and waits on (D79), with the quest where the page holds it. */
  question?: { id: string; quest?: Quest } | null;
  /** Why this machine's driver leaves it waiting, in the driver's words (D46 §3). */
  sitting?: Consideration | null;
  /** A start the driver holds for the agent's trust where it would run (D73). */
  hold?: TrustHold | null;
  /** The chain it belongs to (MAP1); drawn only where there is more than this quest. */
  chain?: ChainStep[];
  /** Its driven session's freshest record: a retry is its own record, and the page says where things stand now. */
  session?: Session | null;
  /** That session's process runs on this machine, so it can be stopped here (D46 §6). */
  running?: boolean;
  busy?: boolean;
  retrying?: boolean;
  /** The agent's trust question is open for the hold (D73), asked only on the press. */
  trusting?: boolean;
  granting?: boolean;
  dismissing?: boolean;
  stopping?: boolean;
  onRespond: (action: 'take' | 'done' | 'decline', reason?: string) => void;
  /** Delete the quest (D95) — absent where there is no door to do it. */
  onDelete?: () => void;
  onDismiss: (machine: string, sequence: number) => void;
  /** Start a quest parked by its strikes again (RETRY1) — the shell's. */
  onRetry?: () => void;
  /** Open or put down the trust question (D73) — the shell's, which closes it once the grant is written. */
  onTrusting?: (open: boolean) => void;
  /** Grant the agent's trust for the folder held (D73) — the shell's. */
  onGrant?: (hold: TrustHold) => void;
  /** Stop its session's process — only where it runs here. */
  onStop?: () => void;
  onOpenQuest: (id: string) => void;
  /** The door into Sessions: absent where there are none, a browser, or no driver is attached. */
  onAttend?: (session: string) => void;
}) {
  const { t } = useTranslation();
  const [declining, setDeclining] = useState(false);
  const [reason, setReason] = useState('');
  // A delete asks once (D95): the first press arms it, and only the second removes the record.
  const [deleting, setDeleting] = useState(false);
  const moving = quest.status === 'Open' || quest.status === 'Taken';
  const deletable = quest.deletable === true && onDelete !== undefined;
  const answered = question?.quest?.status === 'Done' || question?.quest?.status === 'Declined';
  // A wait (D79) is said by its own row, with the question; the driver's sentence under it would say it again.
  const because = sitting?.verdict === 'Waiting' ? null : sitting ?? null;

  const acts = moving && (
    <>
      {/* The one loud control is the quest's next step (UX5 U31): taking it while it is open, closing it once
          it is taken. Done led an open quest too, with taking it offered as the quiet choice. */}
      {quest.status === 'Open' && (
        <Button variant="primary" disabled={busy} onClick={() => onRespond('take')}>{t('quests.detail.take')}</Button>
      )}
      <Button variant={quest.status === 'Taken' ? 'primary' : 'default'} disabled={busy} onClick={() => onRespond('done')}>
        {t('quests.detail.done')}
      </Button>
      {/* While one asks under the header, its first press is not offered twice. */}
      {!declining && (
        <Button variant="ghost" disabled={busy} onClick={() => { setDeclining(true); setDeleting(false); }}>
          {t('quests.detail.decline')}
        </Button>
      )}
      {deletable && !deleting && (
        <Button variant="ghost" disabled={busy} onClick={() => { setDeleting(true); setDeclining(false); }}>
          <Icon name="remove" size={13} />
          {t('quests.detail.delete')}
        </Button>
      )}
    </>
  );

  const head = (
    <PageHead
      title={quest.title}
      pills={<Pill tone={QUEST_TONE[quest.status]}>{t(`status.${quest.status}`)}</Pill>}
      id={`#${quest.id}`}
      line={t(`statusHint.${quest.status}`)}
      acts={acts || undefined}
    />
  );

  return (
    <ViewMain header={head}>
      {deleting && deletable && (
        /* 🔴 A delete removes the record, which nothing gives back (D95) — so the first press only asks, the
           way removing an account does, and the second is the one that deletes. */
        <div
          role="group"
          aria-label={t('quests.detail.deleteTitle')}
          className="mb-4 flex flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
        >
          <span className="min-w-0 flex-1 basis-64 text-small text-ink-soft">{t('quests.detail.deleteConfirm')}</span>
          <Button variant="danger" disabled={busy} onClick={() => { setDeleting(false); onDelete!(); }}>
            {t('quests.detail.deleteMeanIt')}
          </Button>
          <Button variant="ghost" disabled={busy} onClick={() => setDeleting(false)}>{t('common.cancel')}</Button>
        </div>
      )}

      {declining && moving && (
        <div className="mb-4 flex max-w-3xl flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2">
          <input
            autoFocus
            aria-label={t('quests.detail.declinePlaceholder')}
            placeholder={t('quests.detail.declinePlaceholder')}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            className="min-h-[1.9rem] flex-1 basis-56 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
          />
          {/* Declining without a reason is refused by the service; the form does not offer the mistake. */}
          <Button variant="danger" disabled={busy || !reason.trim()} onClick={() => onRespond('decline', reason)}>
            {t('quests.detail.declineConfirm')}
          </Button>
          <Button variant="ghost" disabled={busy} onClick={() => { setDeclining(false); setReason(''); }}>
            {t('common.cancel')}
          </Button>
        </div>
      )}

      <dl className="m-0 mb-4 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-body">
        <Fact name={t('quests.detail.from')}>{quest.from}</Fact>
        <Fact name={t('quests.detail.to')}>{quest.to}</Fact>
        {/* Each lane as the repository declares it, named where its registration says (D115 §2.2). */}
        {lanes && <Fact name={t('quests.detail.lanes')}>{lanes}</Fact>}
        <Fact name={t('quests.detail.filed')}>{stamp(quest.filed)} · {ago(quest.filed)}</Fact>
        {quest.updated !== quest.filed && (
          <Fact name={t('quests.detail.moved')}>{stamp(quest.updated)} · {ago(quest.updated)}</Fact>
        )}
        {question && (
          // What its taker asked and waits on (D79), opened in place: the answer is read there.
          <Fact name={t(answered ? 'quests.detail.asked' : 'quests.detail.waitsOn')}>
            <span className="inline-flex flex-wrap items-baseline gap-1.5">
              {question.quest ? (
                <button
                  type="button"
                  className="cursor-pointer border-0 bg-transparent p-0 text-left text-body text-accent underline-offset-2 hover:underline"
                  onClick={() => onOpenQuest(question.id)}
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
              {t(answered ? 'quests.detail.answered' : 'quests.detail.waitsWhy')}
            </span>
          </Fact>
        )}
        {(because || hold) && (
          // Why this machine's driver is not starting it, in its own words (D46 §3) — the whole sentence here,
          // where there is room. The trust hold stands on its own: it arrives in the same tick as the sentence,
          // and the grant must not wait on a second list having arrived too.
          <Fact name={t('quests.detail.sitting')}>
            <Inline text={because ? sittingSentence(because) : t('work.attention.trustWhy')} />
            {/* The one hold only the person can lift, offered where it is read (D73). */}
            {hold && onGrant && onTrusting && !trusting && (
              <span className="mt-1.5 block"><Button onClick={() => onTrusting(true)}>{t('trust.open')}</Button></span>
            )}
            {/* And the other: a quest parked by its strikes, started again on the press (RETRY1). Counted from
                where it stands, so the next failures park it again. */}
            {because?.verdict === 'Exhausted' && onRetry && (
              <span className="mt-1.5 block">
                <Button disabled={retrying} onClick={onRetry}>{t('quests.detail.retry')}</Button>
              </span>
            )}
          </Fact>
        )}
      </dl>

      {hold && onGrant && trusting && (
        <div className="mb-4 max-w-3xl">
          <TrustAsk hold={hold} busy={granting} onCancel={onTrusting && (() => onTrusting(false))} onGrant={() => onGrant(hold)} />
        </div>
      )}

      {(quest.conflicts?.length ?? 0) > 0 && (
        /* A move that reached the remote second (D68 §5): kept on the quest for a person and never merged, so
           it sits above the body — it is what this quest is waiting on. The note is that session's own words. */
        <section
          aria-label={t('quests.detail.conflicts')}
          className="mb-4 max-w-3xl rounded-card border border-line border-l-[3px] border-l-st-open bg-raised px-3 py-2.5"
        >
          <SectionTitle>{t('quests.detail.conflicts')}</SectionTitle>
          <p className="m-0 mb-2 text-small text-ink-soft">{t('quests.detail.conflictsHint')}</p>
          <ul className="m-0 grid list-none gap-1.5 p-0">
            {quest.conflicts!.map((conflict) => (
              <li key={`${conflict.machine}-${conflict.sequence}`} className="flex items-start justify-between gap-3 text-body">
                <span className="grid min-w-0 gap-0.5">
                  <span className="inline-flex flex-wrap items-center gap-1.5">
                    <Icon name="conflict" size={12} className="text-warn" />
                    {t('quests.detail.conflictLine', { machine: conflict.machine, attempted: t(`status.${conflict.attempted}`) })}
                    <span className="text-meta text-ink-faint">· {ago(conflict.at)}</span>
                  </span>
                  {conflict.note && <span className="text-small text-ink-soft">{conflict.note}</span>}
                </span>
                {/* Seen, and nothing more to do here: the dismissal travels, so every machine stops showing it
                    (SYNC6c). */}
                <Button variant="ghost" disabled={dismissing} onClick={() => onDismiss(conflict.machine, conflict.sequence)}>
                  {t('quests.detail.dismiss')}
                </Button>
              </li>
            ))}
          </ul>
        </section>
      )}

      {/* The ask itself, as the asker wrote it: content, at a measure a person can read. */}
      <p className="m-0 max-w-prose whitespace-pre-wrap text-body leading-relaxed">{quest.body}</p>

      {(quest.links?.length ?? 0) > 0 && (
        /* The asker's addresses, as links — the service accepted only http and https, so each is an address
           and nothing that would run. They open where the person chose, their browser or Daoris's (BRW7). */
        <div className="mt-4">
          <PageSection title={t('quests.detail.links')}>
            <ul className="m-0 grid list-none gap-1 p-0">
              {quest.links!.map((link) => (
                <li key={link} className="min-w-0">
                  <ExternalLink
                    href={link}
                    className="inline-flex max-w-full items-center gap-1.5 text-body text-accent underline-offset-2 hover:underline"
                  >
                    <Icon name="link" size={12} />
                    <span className="truncate">{link}</span>
                  </ExternalLink>
                </li>
              ))}
            </ul>
          </PageSection>
        </div>
      )}

      {(quest.attachments?.length ?? 0) > 0 && (
        /* The files, by name (D65 §2). One this machine keeps opens through the host's own route — never by its
           path, which a page does not show — and a picture is shown as one. One named on the record and kept
           elsewhere says so, rather than looking like a broken link. */
        <div className="mt-4">
          <PageSection title={t('quests.detail.files')}>
            <ul className="m-0 grid list-none gap-2 p-0">
              {quest.attachments!.map((file) => (
                <li key={file.sha256} className="min-w-0 text-body">
                  {file.path ? (
                    <ExternalLink href={api.attachmentUrl(quest.id, file.sha256)} className="group inline-grid max-w-full gap-1.5 text-accent">
                      <span className="inline-flex min-w-0 items-center gap-1.5">
                        <Icon name="attach" size={12} />
                        <span className="truncate underline-offset-2 group-hover:underline">{file.name}</span>
                        <span className="shrink-0 font-mono text-meta text-ink-faint">{size(file.bytes)}</span>
                      </span>
                      {isImage(file.name) && (
                        <img
                          src={api.attachmentUrl(quest.id, file.sha256)} alt={file.name}
                          className="max-h-60 max-w-full rounded-control border border-line object-contain"
                        />
                      )}
                    </ExternalLink>
                  ) : (
                    <span className="inline-flex min-w-0 flex-wrap items-center gap-1.5 text-ink-soft">
                      <Icon name="attach" size={12} />
                      <span className="truncate">{file.name}</span>
                      <span className="font-mono text-meta text-ink-faint">{size(file.bytes)} · {t('quests.detail.fileElsewhere')}</span>
                    </span>
                  )}
                </li>
              ))}
            </ul>
          </PageSection>
        </div>
      )}

      {chain.length > 1 && (
        /* The chain this quest belongs to (MAP1): the ask, the steps before and after it, what its close will
           still publish (D65 §4, `{parent}` as the asker wrote it), and on what each step ran. */
        <div className="mt-4 max-w-3xl">
          <ChainStrip
            chain={chain}
            onQuest={(other) => onOpenQuest(other.id)}
            onSession={onAttend ? (ran) => onAttend(ran.id) : undefined}
          />
        </div>
      )}

      {quest.note && <p className="mt-4 max-w-prose rounded-control bg-accent-soft px-3 py-2.5 text-body italic">{quest.note}</p>}

      {session && (
        /* The driven session's RECORD (D46 §4) — read-only here: the process, and the person's controls over
           it, live where a driver is attached, which is the desktop. The note and evidence are the driver's
           observations and render verbatim, like every system sentence. */
        <section className="mt-5" aria-label={t('quests.session.title')}>
          <SectionTitle>{t('quests.session.title')}</SectionTitle>
          <div className="flex flex-wrap items-center gap-2">
            <Pill tone={SESSION_TONE[session.state]}>{t(`sessionState.${session.state}`)}</Pill>
            <span className="font-mono text-meta text-ink-faint">
              {session.id} · {sessionTool(session)}
              {' · '}{t('quests.session.moved', { ago: ago(session.updated) })}
            </span>
            {/* Stop reaches a PROCESS, so it renders only where one is actually running — the shell's driver —
                never in a browser that could only wish (D46 §6). */}
            {running && onStop && (
              <Button variant="danger" disabled={stopping} onClick={onStop}>{t('quests.session.stop')}</Button>
            )}
          </div>
          {session.note && <p className="mt-2 mb-0 max-w-prose text-body text-ink-soft">{session.note}</p>}
          {session.evidence && (
            <pre className="mt-2 mb-0 overflow-x-auto whitespace-pre-wrap rounded-control border border-line-strong bg-raised px-3 py-2.5 font-mono text-small">
              {session.evidence}
            </pre>
          )}
          {/* One home for the stream (design §3, D55): a session's console is the frame's panel. This keeps the
              record summary and becomes a DOOR — which is only offered where Sessions exists at all. */}
          {onAttend && <Button className="mt-2.5" onClick={() => onAttend(session.id)}>{t('work.open')}</Button>}
          <p className="mt-2 mb-0 text-small text-ink-faint">{t('quests.session.hint')}</p>
        </section>
      )}
    </ViewMain>
  );
}

/**
 * Quests' main area with no record to show (D118 §3b): **nothing chosen** says how to choose and offers the
 * list's `＋` kinds, *Ask* first; **gone** says the chosen quest or ask is no longer here; **loading** is skeleton
 * rows, never the empty state; and a record that never had an answer says the error's sentence in place.
 */
export function QuestsMainNotice({ state, gone, actions = [], sentence, onAct }: {
  state: 'none' | 'gone' | 'loading' | 'unanswered';
  /** What has gone: a quest, or an ask. */
  gone?: 'quest' | 'ask';
  /** The list's `＋` kinds, offered with nothing chosen. */
  actions?: { id: string; label: string }[];
  /** The sentence for a record that has never had an answer. */
  sentence?: string;
  onAct?: (id: string) => void;
}) {
  const { t } = useTranslation();
  if (state === 'unanswered') return <ViewMain><Prose><Inline text={sentence ?? ''} /></Prose></ViewMain>;
  const none: MainNotice = {
    icon: 'quests',
    headline: t('quests.none.headline'),
    body: t('quests.none.body'),
    action: actions.length > 0 && (
      <div className="flex flex-wrap justify-center gap-2">
        {actions.map((action) => <Button key={action.id} onClick={() => onAct?.(action.id)}>{action.label}</Button>)}
      </div>
    ),
  };
  const went: MainNotice = gone === 'ask'
    ? { icon: 'quests', headline: t('asks.gone.headline'), body: t('asks.gone.body') }
    : { icon: 'quests', headline: t('quests.gone.headline'), body: t('quests.gone.body') };
  return <ViewMain state={state} none={none} gone={went} />;
}
