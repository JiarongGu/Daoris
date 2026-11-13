import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Ask, DeclarationMatch, Quest, Session } from '../data/api';
import { ago, sessionTool, size, stamp } from '../locales/format';
import { ExternalLink } from '../design/links';
import type { Consideration } from '../data/signals';
import type { AccountNamer } from '../agents/tools';
import { Button, Icon, type IconName, Inline, Menu, Pill, Prose, SelectField, SESSION_TONE } from '../design/ui';
import { ClearAsk } from '../work/history/ClearAsk';
import type { AskLine } from '../work/entries';
import { AskLines } from './AskLines';
import { type ChannelAnswered, type ChannelForward, ChannelSection } from './ChannelSection';
import type { Channel, ChannelItem, ChannelPost } from '../work/channel';
import type { AskPlan } from './askPlan';
import { AskPlanSection } from './AskPlanSection';
import type { PlanSettling } from './PlanSettle';
import { AtWorkAskView, type AtWorkAskViewProps } from '../work/atWork/AtWorkAskView';
import { clearOffered, type HistoryDoor } from '../work/history/history';
import { type Answered, InlineConfirm } from '../work/InlineConfirm';
import { askName } from '../work/identity';
import { Note } from '../work/Note';
import { lastAbandon, pauseAsk, type WorkDoor, workOffers, type WorkPlan, type WorkTarget } from '../work/pausing/pausing';
import { PageHead, PageSection, ViewMain } from '../work/frame/ViewMain';
import { AbandonAsk, AbandonedWork, PauseAsk } from '../work/pausing/WorkAsks';
import type { RunLineDoor } from '../workflow/run';
import { WorkflowLine } from '../workflow/WorkflowLine';
import { askFacts, askWaits, LONG_TITLE } from './askFacts';
import { NextDoor } from '../work/head/NextDoor';
import { ASK_TONE, firstLine, tierWords } from './AskRow';
import { AskReview } from './AskReview';
import { AskThrough } from './AskThrough';
import { AskWorkflowKind } from './AskWorkflowKind';
import { AskWork } from './AskWork';
import { workTree } from './workTree';
import { type GoAheadAnswer, GoAheadList } from './GoAheadList';
import type { StaleSince } from '../work/attention/attention';

/** The acts in an ask's header: its buttons and its page's right-click draw this one list. */
type AskAct = 'resume' | 'pause' | 'close' | 'abandon' | 'delete';

/**
 * Whether a proposal exists because the ask's sentence names its repository: its evidence is that name alone,
 * which is what the declarations tier writes for one, so it reads as named rather than as sharing its own name.
 * Without case, as the tier finds the name; the driver tells the intake by the same rule (`Asks.IsNamed`).
 */
const namedInAsk = ({ repository, matched }: DeclarationMatch) =>
  matched.length === 1 && matched[0]?.toLowerCase() === repository.toLowerCase();

/** Each act's name, its button's look (the loud act is *Resume*) and its glyph in a menu. */
const ASK_ACT: Record<AskAct, { label: string; variant: 'primary' | 'default' | 'ghost'; icon?: IconName }> = {
  resume: { label: 'asks.record.resume', variant: 'primary', icon: 'resume' },
  pause: { label: 'asks.record.pause', variant: 'default', icon: 'pause' },
  close: { label: 'asks.record.close', variant: 'default' },
  abandon: { label: 'asks.record.abandon', variant: 'ghost' },
  delete: { label: 'asks.record.delete', variant: 'ghost', icon: 'remove' },
};

/**
 * An ask's page: the record in Quests' main area, the screen twin of
 * `daoris ask`'s answer — what is asked and with what, which tier answered, what it proposed, the
 * quests it became — with *Close ask* and *Delete…* in its header and *publish to…* where the proposal is
 * read (`ask --publish <id> --to` and `ask --close <id> --reason` are the terminal's).
 *
 * @remarks
 * **A record is the main area**, not a drawer, which would lie over the side bar and
 * the panel that hold Ask Daoris and the attended session. The composer is a drawer, since it is a form.
 *
 * **A proposal is a person's to accept**: the declarations tier publishes nothing, so each
 * repository it proposed is offered as a publish, and any other adopter in the ask's circle can be
 * chosen instead. The service judges every publish; its sentence reaches the person verbatim, through
 * the holder.
 *
 * **The record says who answered**: the tier in words, and the intake session that served
 * it, if one did. On the desktop that session is a door into Sessions, where its question is on its
 * transcript. A browser has no Sessions, so there it is named and nothing pretends to open it.
 *
 * **A closed ask becomes nothing more** — its reason stays and no act is offered but *delete*.
 *
 * **An ask made by mistake can be deleted**, with every quest asked by it — offered only where
 * the service says it may go (`deletable`), and asked once under the header, because nothing gives the
 * record back. `daoris ask delete <id>` is the terminal's twin.
 *
 * **The go-aheads its sessions asked** come first among its sections, each once, by its act: one
 * waiting is answered here, yes or no with the person's words, and every session on the ask is handed the answer.
 * `daoris ask go-ahead` is the terminal's twin.
 *
 * The ask's files are named and never located: the host answers their path to this machine only, and a
 * page does not show a machine path.
 *
 * **Its work is paused, resumed and abandoned here**, from this machine's driver's plan of it, each
 * act where it applies and absent where it does not: *Pause…* while a pause would hold something, asking once where it
 * ends work in flight; *Resume*, the loud act, while it is paused, with *paused* beside its state; *Abandon…*,
 * quiet, while the abandon would take anything, listing first and abandoning on its second press with the person's reason.
 * After an abandon it says when, *What went* and *What stayed*. Closing says it leaves the quests. A
 * browser has no driver, so it offers none of the three and names the terminal's commands.
 *
 * **It shows its work**: once the plan answers, the quests section lists each quest of the work with its
 * state and why it sits, the questions its sessions asked under the quest whose session asked them, and those sessions as
 * doors into Sessions (`AskWork`), so what a pause or an abandon reaches is seen before either is pressed. While the plan is
 * on its way, and in a browser, it lists the quests the ask became, as the service names them.
 *
 * **Its finished work is cleared from this machine here**: *Clear from this
 * machine…* in its header's ⋯ once it is done or closed and its plan says its work may go, whole or not at all, listing under
 * the header first and clearing on the second press exactly what it listed. A browser has no driver, and offers none.
 *
 * Props only, no hook from the query layer or the shell.
 */
export function AskPage({
  ask, receivers, questTitles, quests, intake = null, onAttend, busy = false, onPublish, onClose, onDelete, onOpenQuest, onAnswerGoAhead,
  work, history, considered = [], nameOf, environments, onChooseReview, onTakeBackThrough, onChooseKind, workflow, lines = [], channel, staleGoAheads, atWork,
  plan, onSettlePlan,
}: {
  /** Its plan as the shell reads it (`useAskPlan`): the parts and the quest each became. Absent in a browser or before anyone wrote one, and then no section is drawn. */
  plan?: AskPlan | null;
  /** The person's answer to a question of its delivery plan, or their waiver of a release order; absent where there is no door to give it. */
  onSettlePlan?: PlanSettling;
  /**
   * Who is at work on the ask: its read as the query holds it, and the doors the section
   * presses. Drawn above the ask's quests and replacing nothing. `words` is the channel's newest three messages, the page's own read
   * under the channel section, whose *All messages* brings that section into view. Absent in a browser, which has no driver.
   */
  atWork?: Omit<AtWorkAskViewProps, 'channel' | 'considered'> & { words?: readonly ChannelItem[] };
  /**
   * Its go-aheads nothing on it could still use, by number, as What needs you's rows say: each marked stale with how
   * long, its refusal the one answer offered. Absent where nothing judges them, a browser.
   */
  staleGoAheads?: Readonly<Record<number, StaleSince>>;
  /**
   * Its channel: as the shell answers it (null while it is read), the person's post and the
   * move of their mark. Absent where no driver answers one, a browser, and then no section is drawn.
   */
  channel?: {
    channel: Channel | null;
    onPost: (post: ChannelPost, answered: ChannelAnswered) => void;
    onRead: (through: string) => void;
    /** *Send to its session*: a message sent on to a repository's session here, the person's own press. */
    onForward?: ChannelForward;
  };
  /**
   * Its sessions by entry: the lines of work it began, one per repository, from the driver's reader
   * (`askLines`); none where no host answers entries, and then the section is not drawn.
   */
  lines?: readonly AskLine[];
  /**
   * An intake's kind proposal applied on the person's press, with the workflow the latest choice
   * already has. Absent where there is no door to set it through.
   */
  onChooseKind?: (kind: string, workflow: string | null | undefined, answered: Answered) => void;
  /**
   * Where its work stands in its workflow: each run, one per chain and repository, said
   * in one line among the head's facts whose door opens it. Absent in a browser, which has no driver to read one.
   */
  workflow?: RunLineDoor;
  /** The review environments the ask's circle declares, by name; none where no shell says. */
  environments?: string[];
  /**
   * The person's review choice for the ask's work, or its intake's proposal applied, with their words.
   * Absent where there is no door to set it through. Answered where it is pressed: a refusal is said under that press.
   */
  onChooseReview?: (choice: string, words: string | undefined, answered: Answered) => void;
  /** Take back the person's through words on the ask; absent where nothing can. */
  onTakeBackThrough?: (answered: Answered) => void;
  /** What a person calls an account, from the roster; absent, the intake's record's id is said. */
  nameOf?: AccountNamer;
  ask: Ask;
  /** Whom the ask can be published to: the repositories the host says can be asked, in its circle. */
  receivers: string[];
  /** Titles of the quests this page holds, by id — one it has not loaded is named by its id, and not opened. */
  questTitles: Record<string, string>;
  /** The quests this page holds, by id: what its work reads a quest's submission and hold from. */
  quests?: Readonly<Record<string, Quest>>;
  /** The record of the intake session that served it, when the page holds it. */
  intake?: Session | null;
  /** The door into Sessions — absent where there are none, which is a browser. */
  onAttend?: (session: string) => void;
  busy?: boolean;
  onPublish: (to: string) => void;
  /** Close the ask with the person's reason, told back to its ask: open until it answers. */
  onClose: (reason: string, answered: Answered) => void;
  /**
   * Delete the ask with every quest asked by it, told back to its ask — absent where there is no door to do
   * it.
   */
  onDelete?: (answered: Answered) => void;
  onOpenQuest: (id: string) => void;
  /** The person's yes or no to a go-ahead its sessions asked — absent where there is no door to give it. */
  onAnswerGoAhead?: GoAheadAnswer;
  /** This machine's driver's half: the plan and the three presses. Absent in a browser, which has no driver. */
  work?: WorkDoor;
  /**
   * Clearing its finished work from this machine: the plan of its work, and the second press. Absent in a
   * browser, which has no driver and no home.
   */
  history?: HistoryDoor;
  /** The driver's last look at each quest (the tick's `considered`), which says why one of its work sits. */
  considered?: readonly Consideration[];
}) {
  const { t } = useTranslation();
  const [another, setAnother] = useState('');
  // One question asks under the header at a time: closing, deleting, pausing, the abandon's list, or the clear's.
  const [asking, setAsking] = useState<'close' | 'delete' | 'pause' | 'abandon' | 'clear' | null>(null);
  // The plan the abandon's list showed, held from when it opened: the second press sends exactly what it listed (§3.1).
  const [listed, setListed] = useState<WorkPlan | null>(null);
  const [reason, setReason] = useState('');
  const live = ask.state !== 'Closed';
  const deletable = ask.deletable === true && onDelete !== undefined;
  const rest = ask.sentence.split('\n').slice(1).join('\n').trim();
  const target: WorkTarget = { scope: 'ask', id: ask.id };
  const offers = workOffers(work?.plan);
  const waiting = busy || work?.busy === true || history?.busy === true;
  // Its clear, once its work closed and its plan says it may go (§6.1): absent, never disabled, anywhere else.
  const clearable = (ask.state === 'Done' || ask.state === 'Closed') && clearOffered(history?.plan) && asking !== 'clear';
  const pauseLines = work?.plan ? pauseAsk(work.plan, { wired: work.wired }) : null;
  const abandoned = lastAbandon(work);
  // Its work as the plan answers it; none while the plan is on its way, or in a browser.
  const items = work?.plan ? workTree(work.plan) : [];
  const closing = asking === 'close';
  const deleting = asking === 'delete';

  // A pause that stops nothing applies at once, with its notice, since nothing is lost (§2.6).
  const onPauseFirst = () => {
    if (pauseLines === null) work?.onPause();
    else setAsking('pause');
  };

  // The acts in the header; while one asks under it, its first press is not offered twice. The loud act is the
  // next step: *Resume* while paused. One list for its buttons and the page's right-click.
  const headActs: AskAct[] = [
    ...(offers.resume ? ['resume' as const] : []),
    ...(offers.pause && asking !== 'pause' ? ['pause' as const] : []),
    ...(live && !closing ? ['close' as const] : []),
    ...(offers.abandon && asking !== 'abandon' ? ['abandon' as const] : []),
    ...(deletable && !deleting ? ['delete' as const] : []),
  ];
  const press = (act: AskAct) => {
    switch (act) {
      case 'resume': work?.onResume(); return;
      case 'pause': onPauseFirst(); return;
      case 'close': setAsking('close'); return;
      case 'abandon': setListed(work!.plan); setAsking('abandon'); return;
      case 'delete': setAsking('delete'); return;
    }
  };
  // The clear, in the header's ⋯ (§6.1): drawn only where it is offered, so a page with nothing to clear keeps its header.
  const clearAct = { id: 'clear', label: t('asks.record.clear'), disabled: waiting, onSelect: () => setAsking('clear') };
  const copyId = { id: 'copy', label: t('contextMenu.act.copyAsk'), icon: 'copy' as const, copy: ask.id };
  // The head draws the ask's next step, *Resume* while it is paused, and the rest are its ⋯'s with its id
  // (the quest's head folds its own the same way): one list, so the buttons, the ⋯ and the right-click never disagree.
  const drawn: AskAct[] = headActs.filter((act) => act === 'resume');
  const folded = headActs.filter((act) => !drawn.includes(act));
  const acts = (
    <>
      {drawn.map((act) => (
        <Button key={act} variant={ASK_ACT[act].variant} disabled={waiting} onClick={() => press(act)}>
          {t(ASK_ACT[act].label)}
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
                id: act, label: t(ASK_ACT[act].label), icon: ASK_ACT[act].icon, disabled: waiting, onSelect: () => press(act),
              })),
              ...(clearable ? [clearAct] : []),
              copyId,
            ]}
            onCopy={(text) => { void navigator.clipboard?.writeText(text).catch(() => {}); }}
          />
        </Menu.Content>
      </Menu.Root>
    </>
  );
  const menu = {
    label: askName(ask),
    acts: [
      ...headActs.map((act) => ({
        id: act, label: t(ASK_ACT[act].label), icon: ASK_ACT[act].icon, disabled: waiting, onSelect: () => press(act),
      })),
      ...(clearable ? [clearAct] : []),
      copyId,
    ],
  };

  // The title is the request's first line on two lines at most; a long one is read whole, as written, in the body's first
  // block, which *View full request* moves the focus to. Content is shown as it is: nothing is rewritten.
  // Where the asker gave a short title the head says it, as a quest's does, and the body opens on the
  // whole request once; the head then cuts nothing, so *View full request* has nothing to move to.
  const title = askName(ask);
  const titled = title !== firstLine(ask.sentence);
  const long = !titled && title.length > LONG_TITLE;
  const request = useRef<HTMLParagraphElement>(null);
  const goAheads = useRef<HTMLDivElement>(null);
  const channelSection = useRef<HTMLDivElement>(null);
  const head = (
    <PageHead
      lead={(
        <>
          <Pill tone={ASK_TONE[ask.state]}>{t(`asks.state.${ask.state}`)}</Pill>
          {/* The ask keeps its state, and says it is paused beside it (§2.2): a pause is this machine's, never a state. */}
          {offers.paused && <span title={t('asks.record.pausedTip')}><Pill tone="neutral">{t('asks.record.paused')}</Pill></span>}
        </>
      )}
      title={title}
      clamp={2}
      wholeElsewhere={long}
      facts={askFacts(t, ask, { working: lines.some((line) => line.shown === 'working') })}
      acts={acts}
      fact={(
        <>
          {askWaits(ask) && onAnswerGoAhead && (
            <NextDoor
              press={t('work.facts.press.answer')}
              onPress={() => { goAheads.current?.scrollIntoView?.({ block: 'start' }); goAheads.current?.focus(); }}
            />
          )}
          {workflow && workflow.runs.length > 0 && <WorkflowLine runs={workflow.runs} onOpen={workflow.onOpen} />}
          {long && (
            <Button variant="ghost" className="ml-1 px-0 py-0 text-small" onClick={() => request.current?.focus()}>
              {t('work.head.viewRequest')}
            </Button>
          )}
        </>
      )}
    />
  );

  return (
    <ViewMain header={head} menu={menu}>
      {asking === 'pause' && work && pauseLines && (
        <PauseAsk
          className="mb-4"
          target={target}
          lines={pauseLines}
          meanIt={t('asks.record.pauseMeanIt')}
          busy={waiting}
          onPause={(answered) => work.onPause(answered)}
          onClose={() => setAsking(null)}
        />
      )}

      {asking === 'abandon' && work && listed && (
        <AbandonAsk
          target={target}
          plan={listed}
          meanIt={t('asks.record.abandonMeanIt')}
          placeholder={t('asks.record.abandonWhy')}
          busy={waiting}
          onAbandon={(why, answered) => work.onAbandon(why, listed.abandon.pieces, answered)}
          onClose={() => { setAsking(null); setListed(null); }}
        />
      )}

      {asking === 'clear' && history?.plan && (
        /* 🔴 A clear removes what this machine kept of the ask's work, which nothing gives back: the first press
           lists it, held from here, and the second sends exactly that (§5). */
        <ClearAsk
          className="mb-4"
          target={{ scope: 'ask', id: ask.id }}
          plan={history.plan}
          meanIt={t('asks.record.clearMeanIt')}
          busy={waiting}
          onClear={(units, answered) => history.onClear({ scope: 'ask', id: ask.id }, units, answered)}
          onClose={() => setAsking(null)}
        />
      )}

      {deleting && deletable && (
        /* 🔴 Nothing gives a deleted record back: the first press only asks, and says the quests
           go too, the way removing an account says what it deletes. It stays open until the service answers, and
           says a refusal inside itself. */
        <InlineConfirm
          className="mb-4"
          label={t('asks.record.deleteTitle')}
          says={t('asks.record.deleteConfirm')}
          meanIt={t('asks.record.deleteMeanIt')}
          busy={busy}
          onConfirm={onDelete!}
          onClose={() => setAsking((was) => (was === 'delete' ? null : was))}
        />
      )}

      {live && closing && (
        /* A close is the person's word that the ask is answered, and leaves its quests as they are (§6.5). It stays open
           until the service answers, and says a refusal inside itself. */
        <InlineConfirm
          className="mb-4"
          label={t('asks.record.closeTitle')}
          says={t('asks.record.closeLeaves')}
          meanIt={t('asks.record.closeConfirm')}
          busy={busy}
          ready={reason.trim() !== ''}
          onConfirm={(answered) => onClose(reason.trim(), answered)}
          onClose={() => { setAsking((was) => (was === 'close' ? null : was)); setReason(''); }}
        >
          <textarea
            aria-label={t('asks.record.closeWhy')}
            placeholder={t('asks.record.closeWhy')}
            rows={2} value={reason}
            onChange={(e) => setReason(e.target.value)}
            className="min-w-0 basis-full resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
          />
        </InlineConfirm>
      )}

      {/* The first line is the page's title, so the body is what follows it — a one-line ask would be its title
          and then its body, word for word. The title wraps, so nothing is lost. */}
      {(titled || long || rest) && (
        <p
          ref={request}
          id={`request-${ask.id}`}
          tabIndex={-1}
          className="m-0 mb-4 whitespace-pre-wrap text-body leading-relaxed wrap-anywhere focus-visible:outline-none"
        >
          {titled || long ? ask.sentence : rest}
        </p>
      )}

      <dl className="m-0 mb-4 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1.5 text-body">
        <dt className="text-ink-faint">{t('work.head.id')}</dt><dd className="m-0 font-mono text-meta">#{ask.id}</dd>
        <dt className="text-ink-faint">{t('asks.record.circle')}</dt><dd className="m-0">{ask.workspace}</dd>
        <dt className="text-ink-faint">{t('asks.record.asked')}</dt>
        <dd className="m-0">{stamp(ask.asked)} · {ago(ask.asked)}</dd>
        {ask.asker && (
          <><dt className="text-ink-faint">{t('asks.record.asker')}</dt><dd className="m-0">{ask.asker}</dd></>
        )}
        <dt className="text-ink-faint">{t('asks.record.tier')}</dt>
        <dd className="m-0">
          {/* An intake that parks published nothing, so the tier stays the declarations' — whose words
              say no intake ran, which the section below would contradict. */}
          {ask.tier === 'declarations' && ask.intake ? t('asks.record.tierIntakeUnpublished') : tierWords(t, ask.tier)}
        </dd>
        {ask.note && (
          /* Why it closed, or the service's sentence about the receiver it named — verbatim either way. */
          <>
            <dt className="text-ink-faint">{t(live ? 'asks.record.notPublished' : 'asks.record.closedBecause')}</dt>
            <dd className="m-0 whitespace-pre-wrap">{ask.note}</dd>
          </>
        )}
        {abandoned && (
          /* When it is abandoned on this machine; the reason is the close's note above, verbatim. */
          <>
            <dt className="text-ink-faint">{t('asks.record.abandonedAt')}</dt>
            <dd className="m-0">{stamp(abandoned.at)} · {ago(abandoned.at)}</dd>
          </>
        )}
      </dl>

      {abandoned && (
        <div className="mb-4">
          <AbandonedWork target={target} outcome={abandoned.outcome} went={t('asks.record.went')} stayed={t('asks.record.stayed')} />
        </div>
      )}

      {!work && live && (
        /* A browser has no driver: none of the three is offered, and the terminal's commands are named. */
        <Prose className="mb-4 text-small"><Inline text={t('asks.record.noDriver', { id: ask.id })} /></Prose>
      )}

      {ask.goAheads && ask.goAheads.length > 0 && (
        /* What its sessions asked the person for, each once: first among the sections, since one waiting
           holds a session until the person answers it. */
        <PageSection title={t('asks.record.goAheads')}>
          <div ref={goAheads} tabIndex={-1} className="outline-none">
            <GoAheadList goAheads={ask.goAheads} busy={busy} onAnswer={onAnswerGoAhead} stale={staleGoAheads} />
          </div>
        </PageSection>
      )}

      {(live || (ask.reviewChoices?.length ?? 0) > 0) && (
        /* What the person chose for the review of its work, and what its intake proposed: the
           choice is theirs alone, and a proposal applies only on their press. */
        <PageSection title={t('asks.record.review')}>
          <AskReview ask={ask} environments={environments} busy={busy} onChoose={onChooseReview} />
        </PageSection>
      )}

      {ask.throughWords?.at(-1)?.words && (
        /* The person's words asking for its work to be delivered with no press of theirs, with their take-back. */
        <PageSection title={t('asks.record.throughTitle')}>
          <AskThrough ask={ask} busy={busy} onTakeBack={onTakeBackThrough} />
        </PageSection>
      )}

      {(live || (ask.workflowChoices?.length ?? 0) > 0 || (ask.workflowProposals?.length ?? 0) > 0) && (
        /* The kind of its work the person chose, and what its intake proposed: a proposal applies
           only on their press. */
        <PageSection title={t('asks.record.kind')}>
          <AskWorkflowKind ask={ask} busy={busy} onChoose={onChooseKind} />
        </PageSection>
      )}

      {ask.intake && (
        /* Who answered: the session the intake ran as — its state, then its tool, and its note,
           Daoris's lines in the reader's language and someone's words as written. A door into
           Sessions only where Sessions exists, and only onto a record the page holds; one it does not is
           named by its id. */
        <PageSection title={t('asks.record.intake')}>
          {intake ? (
            <>
              <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
                <Pill tone={SESSION_TONE[intake.state]}>{t(`sessionState.${intake.state}`)}</Pill>
                {onAttend ? (
                  <button
                    type="button"
                    onClick={() => onAttend(intake.id)}
                    className="min-w-0 cursor-pointer truncate border-0 bg-transparent p-0 text-left font-mono text-meta text-accent underline-offset-2 hover:underline"
                  >
                    {sessionTool(intake, false, nameOf)}
                  </button>
                ) : (
                  <span className="min-w-0 truncate font-mono text-meta text-ink-soft">{sessionTool(intake, false, nameOf)}</span>
                )}
                <span className="font-mono text-meta text-ink-faint">#{intake.id.slice(0, 6)} · {ago(intake.updated)}</span>
              </p>
              <Note parts={intake.noteParts} className="mt-1.5 text-body text-ink-soft" />
            </>
          ) : (
            <span className="font-mono text-meta text-ink-soft">#{ask.intake.slice(0, 6)}</span>
          )}
        </PageSection>
      )}

      {atWork && (
        /* Who is at work on it: its lines of work folded to counts that open their own view, and its quests as a
           session's view draws them; above the work's own list, which it replaces nothing of. */
        <PageSection title={t('work.atWork.view')}>
          <AtWorkAskView
            held={atWork.held}
            error={atWork.error}
            onOpenEntry={atWork.onOpenEntry}
            onOpenQuest={atWork.onOpenQuest ?? onOpenQuest}
            considered={considered}
            onRetry={atWork.onRetry}
            channel={channel && atWork.words
              ? {
                words: atWork.words,
                onOpen: () => {
                  channelSection.current?.scrollIntoView?.({ block: 'start' });
                  channelSection.current?.focus();
                },
              }
              : undefined}
          />
        </PageSection>
      )}

      {items.length > 0 ? (
        /* What a pause or an abandon reaches, before either is pressed. */
        <PageSection title={t('asks.record.quests')}>
          <AskWork
            items={items} ask={ask.id} considered={considered} questTitles={questTitles} quests={quests} onOpenQuest={onOpenQuest} onAttend={onAttend}
          />
        </PageSection>
      ) : ask.quests.length > 0 && (
        <PageSection title={t('asks.record.quests')}>
          <ul className="m-0 grid list-none gap-1 p-0">
            {ask.quests.map((id) => (
              <li key={id}>
                {/* A door only onto a quest the page holds: just after a publish the answer names the
                    quest before the list has it, and a door that opens nothing is a dead click. */}
                {questTitles[id] ? (
                  <button
                    type="button"
                    onClick={() => onOpenQuest(id)}
                    className="inline-flex max-w-full items-baseline gap-2 text-left text-body text-accent underline-offset-2 hover:underline"
                  >
                    <span className="shrink-0 font-mono text-meta">#{id.slice(0, 6)}</span>
                    <span className="truncate">{questTitles[id]}</span>
                  </button>
                ) : (
                  <span className="font-mono text-meta text-ink-soft">#{id.slice(0, 6)}</span>
                )}
              </li>
            ))}
          </ul>
        </PageSection>
      )}

      {/* Its plan, once someone wrote one: the parts, each the quest it became, and the order between them. */}
      {plan && plan.plans.length > 0 && (
        <PageSection title={t('asks.plan.title')}>
          <AskPlanSection plan={plan} onOpenQuest={onOpenQuest} onSettle={onSettlePlan} />
        </PageSection>
      )}

      {/* Its sessions by entry: the lines of work it began, one per repository, however many records. */}
      {lines.length > 0 && (
        <PageSection title={t('asks.record.sessions')}>
          <AskLines lines={lines} onAttend={onAttend} />
        </PageSection>
      )}

      {/* Where its sessions and the person talk: shell-only, so a browser draws none. */}
      {channel && (
        <PageSection title={t('asks.channel.title')}>
          <div ref={channelSection} tabIndex={-1} className="outline-none" />
          <ChannelSection
            channel={channel.channel}
            repositories={lines.map((line) => line.repository)}
            onPost={channel.onPost}
            onRead={channel.onRead}
            onForward={channel.onForward}
            onOpenQuest={onOpenQuest}
          />
          <p className="m-0 mt-2 text-small text-ink-faint"><Inline text={t('asks.channel.twin', { id: ask.id })} /></p>
        </PageSection>
      )}

      {live && (
        <PageSection title={t('asks.record.proposal')}>
          {ask.proposal.length === 0 ? (
            <p className="m-0 mb-2 text-body text-ink-soft">{t('asks.record.noProposal')}</p>
          ) : (
            <ul className="m-0 mb-3 grid list-none gap-2 p-0">
              {ask.proposal.map((match) => (
                <li key={match.repository} className="flex items-center justify-between gap-3">
                  <span className="grid min-w-0 gap-0.5">
                    <span className="text-body font-semibold">{match.repository}</span>
                    <span className="truncate text-small text-ink-soft">
                      {namedInAsk(match)
                        ? t('asks.record.named')
                        : t('asks.record.matched', { words: match.matched.join(', ') })}
                    </span>
                  </span>
                  <Button disabled={busy} onClick={() => onPublish(match.repository)}>
                    {t('asks.record.publishTo', { repository: match.repository })}
                  </Button>
                </li>
              ))}
            </ul>
          )}
          {/* Any adopter in its circle, not only the proposed: the declarations propose, a person
              decides — and a repository the declarations missed is still somebody's to name. */}
          <div className="flex flex-wrap items-center gap-2">
            <SelectField
              value={another}
              onChange={setAnother}
              ariaLabel={t('asks.record.another')}
              placeholder={t('asks.record.anotherPlaceholder', { circle: ask.workspace })}
              options={receivers.map((name) => ({ value: name, label: name }))}
            />
            <Button disabled={busy || !another} onClick={() => onPublish(another)}>{t('asks.record.publish')}</Button>
          </div>
        </PageSection>
      )}

      {ask.links.length > 0 && (
        <PageSection title={t('asks.record.links')}>
          <ul className="m-0 grid list-none gap-1 p-0">
            {ask.links.map((link) => (
              <li key={link} className="min-w-0">
                {/* Where the person chose, their browser or Daoris's. */}
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
      )}

      {ask.attachments.length > 0 && (
        <PageSection title={t('asks.record.files')}>
          <ul className="m-0 grid list-none gap-1 p-0">
            {ask.attachments.map((file) => (
              <li key={file.sha256} className="inline-flex min-w-0 flex-wrap items-center gap-1.5 text-body text-ink-soft">
                <Icon name="attach" size={12} />
                <span className="truncate">{file.name}</span>
                <span className="font-mono text-meta text-ink-faint">{size(file.bytes)} · {t('asks.record.fileKept')}</span>
              </li>
            ))}
          </ul>
        </PageSection>
      )}
    </ViewMain>
  );
}
