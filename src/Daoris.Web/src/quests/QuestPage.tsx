import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, type Quest, type Session } from '../api';
import { isImage } from '../attachments';
import { ago, sessionTool, size, stamp } from '../format';
import { ExternalLink } from '../links';
import type { ChainStep } from '../map/chain';
import { ChainStrip } from '../map/ChainStrip';
import type { AccountNamer } from '../tools';
import { type Consideration, sittingSentence, type TrustHold, waitsForAccount } from '../signals';
import { Button, Icon, type IconName, Inline, Menu, Pill, Prose, QUEST_TONE, SectionTitle, SESSION_TONE } from '../ui';
import { lastAbandon, pauseAsk, type WorkDoor, workOffers, type WorkPlan, type WorkTarget } from '../work/pausing';
import { DetailsFold } from '../work/DetailsFold';
import { questFacts } from '../work/headFacts';
import { HowItCameToBe, type TraceDoor } from '../work/HowItCameToBe';
import { questName } from '../work/identity';
import { Note } from '../work/Note';
import { QuestRequirements } from './Requirements';
import { TrustAsk } from '../work/TrustAsk';
import { type MainNotice, PageHead, PageSection, ViewMain } from '../work/ViewMain';
import { AbandonAsk, AbandonedWork, PauseAsk } from '../work/WorkAsks';

/** The acts a quest's page offers: its header's, then its body's (CTX1, D138 §4). */
type QuestAct = 'take' | 'done' | 'pause' | 'decline' | 'abandon' | 'delete' | 'accept' | 'resume' | 'retry' | 'trust' | 'session';

/** Each act's name, the one its button says, and its glyph in a menu. */
const QUEST_ACT: Record<QuestAct, { label: string; icon?: IconName }> = {
  take: { label: 'quests.detail.take' },
  done: { label: 'quests.detail.done', icon: 'check' },
  pause: { label: 'quests.detail.pause', icon: 'pause' },
  decline: { label: 'quests.detail.decline' },
  abandon: { label: 'quests.detail.abandon' },
  delete: { label: 'quests.detail.delete', icon: 'remove' },
  accept: { label: 'quests.requirements.accept', icon: 'check' },
  resume: { label: 'quests.detail.resume', icon: 'resume' },
  retry: { label: 'quests.detail.retry', icon: 'refresh' },
  trust: { label: 'trust.open', icon: 'shield' },
  session: { label: 'work.open', icon: 'frameWork' },
};

/**
 * A header act's button: the one loud control is the quest's next step (UX5 U31), taking it while it is open and closing
 * it once it is taken; Pause… plain; the rest quiet.
 */
function buttonOf(act: QuestAct, status: Quest['status']): 'primary' | 'default' | 'ghost' {
  if (act === 'take') return 'primary';
  if (act === 'done') return status === 'Taken' ? 'primary' : 'default';
  if (act === 'pause') return 'default';
  return 'ghost';
}

/**
 * A quest's body as its page shows it under the head (UX7c, D152 §7): its first line left out where it is the title the
 * head said, as the ask page leaves its first line out (POLISH4); where the head said a short title instead, the body as
 * written, opening on the whole title, which is prepended where the body does not already open on it. Content is never
 * rewritten otherwise. Empty where nothing is left to say.
 */
export function bodyUnderHead(title: string, body: string, shortTitled: boolean): string {
  const [first = '', ...rest] = body.split('\n');
  const opensOnTitle = first.trim() === title.trim();
  if (shortTitled) return opensOnTitle ? body : `${title}\n\n${body}`;
  return opensOnTitle ? rest.join('\n').replace(/^\s*\n/, '') : body;
}

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
 * *Take* while it is open, *Mark done* once it is taken. Then what it is waiting on, the holds only the person lifts
 * (a folder to trust, D73; a quest parked by its strikes, RETRY1; one their stop holds, D126 §3.4), a conflict a person
 * must see (D68 §5), the ask itself, what it carries (D65 §2), its chain (MAP1) and its driven session's record (D46 §4).
 *
 * @remarks
 * **A molecule**: every state is reached by its props, and every press goes out. What asks under the header —
 * *Decline…*'s reason, *Delete…*'s sentence — is its own, so it resets with the quest it was asked of.
 *
 * - **A delete asks once** (D95): its first press opens a sentence saying the second removes the record, which
 *   nothing gives back; offered only where the service says it may go (`deletable`).
 * - **A decline needs its reason**, which the service refuses without; the form does not offer the mistake.
 * - **What the service and the driver say is said verbatim**: the session's evidence, a conflict's note, the driver's
 *   sentence about why it sits (translated by its verdict, never by its words, U27). The session's note is `Note`'s
 *   (LANG1b): Daoris's lines by their codes in the reader's language, someone's words as written.
 * - **A page never prints a machine path it was answered** (D47 §4): a kept file opens through the host's own
 *   route, and one kept elsewhere says so.
 * - **Its work is paused and abandoned here** (PAUSE1e, D132 §7.1), from this machine's driver's plan: *Pause…* while open
 *   or taken and not paused on its own, asking once where it ends work in flight (§2.6); *Abandon…* beside *Decline…*,
 *   listing first and abandoning on its second press with the person's reason (§3.1). *Resume* stands under *Sitting* for
 *   its own pause, where *Try again* stands for a stop; for another's pause the sentence names whose, and a door opens it.
 *   After an abandon it says when, *What went* and *What stayed* (§4.2). A browser names the terminal's commands instead.
 * - **What the person required, and how its done answered** (DRIFT1d2, D133 §3–§4): each requirement in their words with
 *   its check, met or departed with the reason and the words it relied on, after the body. A departure that holds the
 *   quest for their yes is what it waits on, so it moves above the body, as a conflict sits, with *Accept the departure*:
 *   one press, the service's accept door, and its header says *awaits your yes*.
 */
export function QuestPage({
  quest, lanes, question, sitting, hold, chain = [], session,
  busy = false, retrying = false, trusting = false, granting = false, dismissing = false, accepting = false,
  onRespond, onDelete, onDismiss, onRetry, onTrusting, onGrant, onOpenQuest, onAttend, onOpenAsk, onAccept, work, trace, nameOf,
}: {
  /**
   * What a person calls an account (ACCTNAME1, D152 §4.2), from the roster its organism holds: its session line, its chain
   * and its trace say each account by it. Absent, as in a browser, each record's id is said.
   */
  nameOf?: AccountNamer;
  /**
   * How it came to be (TRACE1b, D143, D50), folded at the foot of its page: the page's organism holds the fold and the read.
   * Absent in a browser, which has no driver to read this machine's records (D47 §4).
   */
  trace?: TraceDoor;
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
  /**
   * Its driven session's freshest record: a retry is its own record, and the page says where things stand now. Read
   * here and managed in Sessions (D126 §3.6): its stop is its page header's there, behind the door below.
   */
  session?: Session | null;
  busy?: boolean;
  retrying?: boolean;
  /** The agent's trust question is open for the hold (D73), asked only on the press. */
  trusting?: boolean;
  granting?: boolean;
  dismissing?: boolean;
  /** The person's yes to a departure is on its way (DRIFT1d2). */
  accepting?: boolean;
  onRespond: (action: 'take' | 'done' | 'decline', reason?: string) => void;
  /** Delete the quest (D95) — absent where there is no door to do it. */
  onDelete?: () => void;
  onDismiss: (machine: string, sequence: number) => void;
  /**
   * *Try again* (RETRY1; D126 §3.4): start a quest parked by its strikes again, or release one the person's stop holds
   * — the shell's, which the driver judges by its verdict.
   */
  onRetry?: () => void;
  /** Open or put down the trust question (D73) — the shell's, which closes it once the grant is written. */
  onTrusting?: (open: boolean) => void;
  /** Grant the agent's trust for the folder held (D73) — the shell's. */
  onGrant?: (hold: TrustHold) => void;
  onOpenQuest: (id: string) => void;
  /** The door into Sessions: absent where there are none, a browser, or no driver is attached. */
  onAttend?: (session: string) => void;
  /** Open an ask's page: where the pause that holds this quest is resumed (PAUSE1e). */
  onOpenAsk?: (id: string) => void;
  /**
   * The person's yes to a done's departure from what they required (DRIFT1d2, D133 §4): the service's accept door, which
   * lets what the departure held go on. Absent where there is no door to say it through.
   */
  onAccept?: () => void;
  /** This machine's driver's half (PAUSE1e): the plan and the three presses. Absent in a browser, which has no driver. */
  work?: WorkDoor;
}) {
  const { t } = useTranslation();
  // One question asks under the header at a time: a decline's reason, a delete's sentence, a pause's, the abandon's list.
  const [asking, setAsking] = useState<'decline' | 'delete' | 'pause' | 'abandon' | null>(null);
  // The plan the abandon's list showed, held from when it opened: the second press sends exactly what it listed (§3.1).
  const [listed, setListed] = useState<WorkPlan | null>(null);
  const [reason, setReason] = useState('');
  const declining = asking === 'decline';
  // A delete asks once (D95): the first press arms it, and only the second removes the record.
  const deleting = asking === 'delete';
  const moving = quest.status === 'Open' || quest.status === 'Taken';
  const deletable = quest.deletable === true && onDelete !== undefined;
  const answered = question?.quest?.status === 'Done' || question?.quest?.status === 'Declined';
  // A wait (D79) is said by its own row, with the question; the driver's sentence under it would say it again.
  const because = sitting?.verdict === 'Waiting' ? null : sitting ?? null;
  const target: WorkTarget = { scope: 'quest', id: quest.id };
  const offers = workOffers(work?.plan);
  const waiting = busy || work?.busy === true;
  const pauseLines = work?.plan ? pauseAsk(work.plan, { wired: work.wired }) : null;
  const abandoned = lastAbandon(work);
  // Whose pause holds it, from the tick's verdict: its own is resumed here, an ask's or another quest's on that page.
  const pausedBy = because?.verdict === 'Paused' ? because.pausedBy ?? null : null;
  const ownPause = pausedBy?.scope === 'quest' && pausedBy.id === quest.id;
  // Its own pause where the tick has no verdict for it (its session waits on you, D132 §2.1): said from the plan, with Resume.
  const pausedUnseen = offers.paused && !pausedBy;

  // A pause that stops nothing applies at once, with its notice, since nothing is lost (§2.6).
  const onPauseFirst = () => {
    if (pauseLines === null) work?.onPause(() => {});
    else setAsking('pause');
  };

  // The header's acts, in its order (D118 §3b), each where it applies. While one asks under the header, its first press
  // is not offered twice. One list for its buttons and the page's right-click (CTX1), so the two never disagree.
  const headActs: QuestAct[] = !(moving || offers.abandon) ? [] : [
    ...(quest.status === 'Open' ? ['take' as const] : []),
    ...(moving ? ['done' as const] : []),
    ...(offers.pause && moving && asking !== 'pause' ? ['pause' as const] : []),
    ...(moving && !declining ? ['decline' as const] : []),
    ...(offers.abandon && asking !== 'abandon' ? ['abandon' as const] : []),
    ...(deletable && !deleting ? ['delete' as const] : []),
  ];
  // What its body offers (the sitting line's Resume, Try again and trust, its session's door), which the right-click
  // offers after the header's.
  const ownResume = (ownPause || pausedUnseen) && work !== undefined;
  const retryable = (because?.verdict === 'Exhausted' || because?.verdict === 'Stopped') && onRetry !== undefined;
  const trustable = Boolean(hold && onGrant && onTrusting && !trusting);
  // A departure that holds it for the person's yes (DRIFT1d2): what it waits on, so the yes leads its body's acts.
  const held = quest.held === true;
  const requirements = quest.requirements ?? [];
  const acceptable = held && onAccept !== undefined;
  const bodyActs: QuestAct[] = [
    ...(acceptable ? ['accept' as const] : []),
    ...(ownResume ? ['resume' as const] : []),
    ...(retryable ? ['retry' as const] : []),
    ...(trustable ? ['trust' as const] : []),
    ...(session && onAttend ? ['session' as const] : []),
  ];

  const press = (act: QuestAct) => {
    switch (act) {
      case 'take': onRespond('take'); return;
      case 'done': onRespond('done'); return;
      case 'pause': onPauseFirst(); return;
      case 'decline': setAsking('decline'); return;
      case 'abandon': setListed(work!.plan); setAsking('abandon'); return;
      case 'delete': setAsking('delete'); return;
      case 'accept': onAccept?.(); return;
      case 'resume': work?.onResume(); return;
      case 'retry': onRetry?.(); return;
      case 'trust': onTrusting?.(true); return;
      case 'session': if (session) onAttend?.(session.id); return;
    }
  };

  // The head's acts as the UX7 design §5.3 draws them (UX7c, D152 §7): its next step loud (*Take* while open, *Mark done*
  // once taken) and *Decline…* beside it; the rest of the same list, and its id, in its ⋯. One list still, so the buttons,
  // the ⋯ and the right-click never disagree (CTX1).
  const lead: QuestAct | null = headActs.includes('take') ? 'take' : quest.status === 'Taken' && headActs.includes('done') ? 'done' : null;
  const drawn = headActs.filter((act) => act === lead || act === 'decline');
  const folded = headActs.filter((act) => !drawn.includes(act));
  const copyId = { id: 'copy', label: t('contextMenu.act.copyQuest'), icon: 'copy' as const, copy: quest.id };
  const acts = (
    <>
      {drawn.map((act) => (
        <Button key={act} variant={buttonOf(act, quest.status)} disabled={waiting} onClick={() => press(act)}>
          {t(QUEST_ACT[act].label)}
        </Button>
      ))}
      <Menu.Root>
        <Menu.Trigger asChild>
          <Button variant="ghost" aria-label={t('work.head.more')} className="h-[1.9rem] w-[1.9rem] justify-center px-0">
            <Icon name="more" size={15} />
          </Button>
        </Menu.Trigger>
        <Menu.Content side="bottom" align="end" className="min-w-48">
          <Menu.Acts
            acts={[
              ...folded.map((act) => ({
                id: act, label: t(QUEST_ACT[act].label), icon: QUEST_ACT[act].icon, disabled: waiting, onSelect: () => press(act),
              })),
              copyId,
            ]}
            onCopy={(text) => { void navigator.clipboard?.writeText(text).catch(() => {}); }}
          />
        </Menu.Content>
      </Menu.Root>
    </>
  );

  const menu = {
    label: questName(quest),
    acts: [
      ...[...headActs, ...bodyActs].map((act) => ({
        id: act, label: t(QUEST_ACT[act].label), icon: QUEST_ACT[act].icon,
        // What waits on a press waits in the menu too: the header's on the quest's own, Try again on its own.
        disabled: act === 'retry' ? retrying : act === 'accept' ? accepting : act === 'session' || act === 'trust' ? false : waiting,
        onSelect: () => press(act),
      })),
      copyId,
    ],
  };

  // Titles first (UX7c, D152 §7; the UX7 design §5.3): its state leads, its name follows on two lines at most, and one line
  // of facts says whom it asks, who asked, when, and the one live fact. The state line, the id and the fact rows went: the
  // state's sentence is its pill's tip, and the id, the full times and the lanes are its *Details*.
  const named = questName(quest);
  const head = (
    <PageHead
      title={named}
      lead={(
        <>
          <Pill tone={QUEST_TONE[quest.status]} title={t(`statusHint.${quest.status}`)}>{t(`status.${quest.status}`)}</Pill>
          {/* Open's hue is the person's (D126 §2.3): a departure waits on their yes (DRIFT1d2). */}
          {held && <Pill tone="open" title={t('quests.card.heldHint')}>{t('quests.card.held')}</Pill>}
        </>
      )}
      clamp={2}
      facts={questFacts(t, { quest, session, sitting })}
      acts={acts}
    />
  );

  // The body, its first line dropped where it is the title the head already said (POLISH4's rule, as the ask page drops
  // it); where the head said a short title, the body opens on the whole title, once.
  const body = bodyUnderHead(quest.title, quest.body, named !== quest.title);

  return (
    <ViewMain header={head} menu={menu}>
      {asking === 'pause' && work && pauseLines && (
        <PauseAsk
          className="mb-4"
          target={target}
          lines={pauseLines}
          meanIt={t('quests.detail.pauseMeanIt')}
          busy={waiting}
          onPause={() => work.onPause(() => setAsking(null))}
          onCancel={() => setAsking(null)}
        />
      )}

      {asking === 'abandon' && work && listed && (
        <AbandonAsk
          target={target}
          plan={listed}
          meanIt={t('quests.detail.abandonMeanIt')}
          placeholder={t('quests.detail.abandonWhy')}
          busy={waiting}
          onAbandon={(why) => work.onAbandon(why, listed.abandon.pieces, () => { setAsking(null); setListed(null); })}
          onCancel={() => { setAsking(null); setListed(null); }}
        />
      )}

      {deleting && deletable && (
        /* 🔴 A delete removes the record, which nothing gives back (D95) — so the first press only asks, the
           way removing an account does, and the second is the one that deletes. */
        <div
          role="group"
          aria-label={t('quests.detail.deleteTitle')}
          className="mb-4 flex flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
        >
          <span className="min-w-0 flex-1 basis-64 text-small text-ink-soft">{t('quests.detail.deleteConfirm')}</span>
          <Button variant="danger" disabled={busy} onClick={() => { setAsking(null); onDelete!(); }}>
            {t('quests.detail.deleteMeanIt')}
          </Button>
          <Button variant="ghost" disabled={busy} onClick={() => setAsking(null)}>{t('common.cancel')}</Button>
        </div>
      )}

      {declining && moving && (
        <div className="mb-4 flex flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2">
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
          <Button variant="ghost" disabled={busy} onClick={() => { setAsking(null); setReason(''); }}>
            {t('common.cancel')}
          </Button>
        </div>
      )}

      {/* What its head leaves out (UX7c, D152 §7): its id with *Copy*, whom it asks and who asked, its lanes, and the full
          times. Folded, since these are looked up rather than read. */}
      <DetailsFold
        className="mb-4"
        summary={[
          `#${quest.id}`,
          t('quests.facts.filedAt', { at: stamp(quest.filed) }),
          ...(quest.updated !== quest.filed ? [t('quests.facts.movedAt', { at: stamp(quest.updated) })] : []),
        ].join(' · ')}
        rows={[
          { label: t('work.head.id'), value: `#${quest.id}`, mono: true, copy: quest.id },
          { label: t('quests.detail.from'), value: quest.from },
          { label: t('quests.detail.to'), value: quest.to },
          // Each lane as the repository declares it, named where its registration says (D115 §2.2).
          { label: t('quests.detail.lanes'), value: lanes || null },
          { label: t('quests.detail.filed'), value: `${stamp(quest.filed)} · ${ago(quest.filed)}` },
          { label: t('quests.detail.moved'), value: quest.updated !== quest.filed ? `${stamp(quest.updated)} · ${ago(quest.updated)}` : null },
          // The short title its publisher gave (SESSUX1j), where the head says it and the whole title stands below.
          { label: t('quests.field.short'), value: quest.short && quest.short !== quest.title ? quest.short : null },
        ]}
      />

      <dl className="m-0 mb-4 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-body empty:hidden">
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
        {(because || hold || pausedUnseen) && (
          // Why this machine's driver is not starting it, in its own words (D46 §3) — the whole sentence here,
          // where there is room. The trust hold stands on its own: it arrives in the same tick as the sentence,
          // and the grant must not wait on a second list having arrived too.
          <Fact name={t('quests.detail.sitting')}>
            {/* A wait for an account (TOOL4g, D125 §4): not parked, so no Retry; it starts by itself at the reset. */}
            {waitsForAccount(because) && (
              <span className="mb-1 block"><Pill tone="neutral">{t('work.waitsForAccount')}</Pill></span>
            )}
            <Inline
              text={because ? sittingSentence(because) : pausedUnseen ? t('quests.detail.pausedOwn') : t('work.attention.trustWhy')}
            />
            {/* A pause comes before every other reason (D132 §2.3): its own is resumed here, where Try again stands for a
                stop; an ask's, or the quest's whose question this is, is resumed on that page, a door away. */}
            {(ownPause || pausedUnseen) && work && (
              <span className="mt-1.5 block">
                <Button disabled={waiting} onClick={() => press('resume')}>{t('quests.detail.resume')}</Button>
              </span>
            )}
            {pausedBy?.scope === 'ask' && onOpenAsk && (
              <span className="mt-1.5 block">
                <Button variant="ghost" onClick={() => onOpenAsk(pausedBy.id)}>{t('quests.detail.openAsk', { id: pausedBy.id })}</Button>
              </span>
            )}
            {pausedBy?.scope === 'quest' && !ownPause && (
              <span className="mt-1.5 block">
                <Button variant="ghost" onClick={() => onOpenQuest(pausedBy.id)}>{t('quests.detail.openPausing', { id: pausedBy.id })}</Button>
              </span>
            )}
            {/* The one hold only the person can lift, offered where it is read (D73). */}
            {hold && onGrant && onTrusting && !trusting && (
              <span className="mt-1.5 block"><Button onClick={() => press('trust')}>{t('trust.open')}</Button></span>
            )}
            {/* And the other two: a quest parked by its strikes, started again on the press (RETRY1), counted from
                where it stands so the next failures park it again; and one the person's stop holds, released from that
                stop (SESSUX1b, D126 §3.4). One act and one word, on this page and on the session that holds it. */}
            {(because?.verdict === 'Exhausted' || because?.verdict === 'Stopped') && onRetry && (
              <span className="mt-1.5 block">
                <Button disabled={retrying} onClick={() => press('retry')}>{t('quests.detail.retry')}</Button>
              </span>
            )}
          </Fact>
        )}
        {abandoned && (
          /* When its work was abandoned on this machine (§4.2); the reason is the decline's note, on the record below. */
          <Fact name={t('quests.detail.abandonedAt')}>{stamp(abandoned.at)} · {ago(abandoned.at)}</Fact>
        )}
      </dl>

      {abandoned && (
        <div className="mb-4">
          <AbandonedWork target={target} outcome={abandoned.outcome} went={t('quests.detail.went')} stayed={t('quests.detail.stayed')} />
        </div>
      )}

      {!work && moving && (
        /* A browser has no driver (D47 §4): none of the three is offered, and the terminal's commands are named. */
        <Prose className="mb-4 text-small"><Inline text={t('quests.detail.noDriver', { id: quest.id })} /></Prose>
      )}

      {hold && onGrant && trusting && (
        <div className="mb-4">
          <TrustAsk hold={hold} busy={granting} onCancel={onTrusting && (() => onTrusting(false))} onGrant={() => onGrant(hold)} />
        </div>
      )}

      {held && requirements.length > 0 && (
        /* A departure that holds the quest for the person's yes (DRIFT1d2, D133 §4): what it waits on, so above the body,
           as a conflict sits, with the yes where the departure is read. */
        <QuestRequirements
          className="mb-4"
          id={quest.id}
          requirements={requirements}
          answers={quest.answers}
          status={quest.status}
          held
          next={quest.then?.[0]}
          accepting={accepting}
          onAccept={acceptable ? () => press('accept') : undefined}
        />
      )}

      {(quest.conflicts?.length ?? 0) > 0 && (
        /* A move that reached the remote second (D68 §5): kept on the quest for a person and never merged, so
           it sits above the body — it is what this quest is waiting on. The note is that session's own words. */
        <section
          aria-label={t('quests.detail.conflicts')}
          className="mb-4 rounded-card border border-line border-l-[3px] border-l-st-open bg-raised px-3 py-2.5"
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

      {/* The ask itself, as the asker wrote it: content, wrapping at the column's edge with the title above it (D141: a
          65ch body beside a title that ran the pane left 750 px empty on the install). Its first line is left out where
          the head already said it (UX7c). */}
      {body && <p className="m-0 whitespace-pre-wrap text-body leading-relaxed">{body}</p>}

      {!held && requirements.length > 0 && (
        /* What the person required (DRIFT1c, D133 §3), in their words: the measure, where the body above is the intake's
           reading of the ask. Then how its done answered each, and when a departure was accepted. */
        <QuestRequirements
          className="mt-4"
          id={quest.id}
          requirements={requirements}
          answers={quest.answers}
          status={quest.status}
          accepted={quest.accepted}
        />
      )}

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
        <div className="mt-4">
          <ChainStrip
            chain={chain}
            onQuest={(other) => onOpenQuest(other.id)}
            onSession={onAttend ? (ran) => onAttend(ran.id) : undefined}
            nameOf={nameOf}
          />
        </div>
      )}

      {quest.note && <p className="mt-4 rounded-control bg-accent-soft px-3 py-2.5 text-body italic">{quest.note}</p>}

      {session && (
        /* The driven session's RECORD (D46 §4) — read-only here: a quest is decided on its page, and its sessions
           are managed on theirs (D126 §3.6), so the stop is the session's page header's, behind the door below. Its
           note's Daoris lines are worded in the reader's language and the agent's words shown as written (LANG1b);
           the evidence is the driver's observation and renders verbatim. */
        <section className="mt-5" aria-label={t('quests.session.title')}>
          <SectionTitle>{t('quests.session.title')}</SectionTitle>
          <div className="flex flex-wrap items-center gap-2">
            <Pill tone={SESSION_TONE[session.state]}>{t(`sessionState.${session.state}`)}</Pill>
            <span className="font-mono text-meta text-ink-faint">
              {session.id} · {sessionTool(session, false, nameOf)}
              {' · '}{t('quests.session.moved', { ago: ago(session.updated) })}
            </span>
          </div>
          <Note note={session.note} parts={session.noteParts} className="mt-2 text-body text-ink-soft" />
          {session.evidence && (
            <pre className="mt-2 mb-0 overflow-x-auto whitespace-pre-wrap rounded-control border border-line-strong bg-raised px-3 py-2.5 font-mono text-small">
              {session.evidence}
            </pre>
          )}
          {/* One home for the stream (design §3, D55): a session's console is the frame's panel. This keeps the
              record summary and becomes a DOOR — which is only offered where Sessions exists at all. */}
          {onAttend && <Button className="mt-2.5" onClick={() => press('session')}>{t('work.open')}</Button>}
          <p className="mt-2 mb-0 text-small text-ink-faint">{t('quests.session.hint')}</p>
        </section>
      )}

      {trace && (
        /* How it came to be (TRACE1b): folded, so nothing is read until the person opens it. Its sessions open in
           Sessions, its other quests here and its ask on its page, each where this page already has the door. */
        <HowItCameToBe
          className="mt-5"
          kind="quest"
          id={quest.id}
          {...trace}
          onSession={onAttend}
          onQuest={onOpenQuest}
          onAsk={onOpenAsk}
          nameOf={nameOf}
        />
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
