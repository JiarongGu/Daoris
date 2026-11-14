import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useHarnessRun, WithHarnessRuns } from '../../agents/harnessRuns';
import { useAcceptQuest, useAnswerGoAhead, useAsks, usePublishAsk, useQuests, useSessions } from '../../data/queries';
import {
  retryNotice, useApproveHelper, useAttention, useDeclineHelper, useHarnesses, useNudge, useRefreshHarnesses,
  useOpenWorkRecords, useResolveHeld, useRetryQuest, useTrustFolder, useAnswerPermission, useAnswerAlone, useApproveHandOver,
  useDeclineHandOver, useGitAnswer, useGitIdentity, useGitTryAgain, useQuietCarryOn, useQuietLeave, useRunNodeWord, useStopSession,
  useDecideSessionMemory, useSetAsideRow,
} from '../../bridge/shell';
import { useScope } from '../../data/scope';
import { permissionSaid } from '../waits/PermissionAsks';
import { answerAloneSaid } from '../waits/AnswerAlone';
import { useSay } from '../../bridge/sessions/conversation';
import { useSeeReading } from '../../bridge/helpReadings';
import { SignIn } from '../../agents/SignIn';
import { byTool } from '../../agents/tools';
import { failure, type Notify, SkeletonRows } from '../../design/ui';
import { sentence } from '../../locales/format';
import type { Attention, AttentionActs } from './AttentionRow';
import type { Answered } from '../InlineConfirm';
import { attentionKey, AttentionList, type AttentionDoors, AttentionRegion, NothingNeedsYou } from './AttentionList';
import { attentionOf, needsListed, rowRun } from './attention';
import { useOpinionActs } from '../gates/opinionActs';
import { useReviewActs } from '../gates/reviewActs';
import { ruleHearing, useRuleAnswer } from '../waits/ruleAnswer';
import { helperSaid } from '../sessionActs';
import { handOverRefused } from '../waits/HandOverAgentAsk';
import { runSessionOf } from '../../workflow/run';
import { nodeWordRefused } from '../../workflow/NodeWord';

export type { AttentionDoors } from './AttentionList';

/** The sessions working now, which the line says when nothing needs the person. */
const WORKING: ReadonlySet<string> = new Set(['starting', 'working']);

/** The kinds that are an account's: their acts wait while a sign-in of their agent's runs. */
const ACCOUNT_KINDS: ReadonlySet<Attention['kind']> = new Set(['account-wait', 'signed-out']);

/** What the band's act is on its way for while the stale go-aheads are refused in one press: no row's key. */
const STALE_KEY = 'stale-go-aheads';

/**
 * **What needs you**, whose rows *Notifications* holds.
 *
 * @remarks
 * Overview keeps one summary line per group (`NeedsSummary`), and this band is the rows behind those lines: what holds work,
 * what waits for the person's word, and what is ready for them, in three groups, the longest waiting first in each. Continuous monitoring is the reported source of fatigue, so visibility is
 * load-bearing and *watching* is not the mechanism: the band is what a person checks instead of watching.
 *
 * **Who needs the person is .NET's to judge**: the rows are pushed (`useAttention`), read from memory and
 * asked again by no tick or focus; the band orders them, names each from the records the page holds (the sessions, quests and asks
 * pushed beside them, the roster's last readings for an account), words them and keeps their acts. Before the owner has judged,
 * the band draws its skeleton, never *Nothing needs you*. A plain browser has no Daoris to judge: it says the band is
 * the app's.
 *
 * **It settles what one press can**: a proposed ask is published to what its declarations named, a parked quest
 * tried again, a departure accepted, each on its record's own route with its sentence said back; a go-ahead's answer, a
 * folder's trust and a widening of the rules ask once under the row. Every other row is a door. **No row starts a process
 * to find out**. The stale go-aheads are refused together, with one reason, each through its row's own door.
 *
 * **The accounts**: *Sign in* runs the agent's own sign-in through the application's one running action, the
 * agent page's own, so it outlives leaving Overview and its steps follow under the row; *Read* reads that one account.
 * No row lets an account run: every account of Daoris's serves every workspace.
 *
 * **With nothing waiting it is one line**, *Nothing needs you*, and what is working.
 *
 * The organism: it holds the queries and the acts, so the list and its rows hold none.
 */
export function AttentionBand({ doors = {}, notify = () => {}, onSessions, onRun, inView = false }: {
  /**
   * Drawn in *Notifications*: its rows alone under the view's group head, which says its
   * name and count, and the `confirmation` row left out, since each press is its whole card in the group above.
   */
  inView?: boolean;
  doors?: AttentionDoors;
  /** Where each act's sentence is said: the service's or the driver's, verbatim. */
  notify?: Notify;
  /** Sessions itself, where *Ready for you*'s rows past its fifth are: a shell's alone. */
  onSessions?: () => void;
  /**
   * The door into a run: a row whose step waits on the person opens that step, its session
   * attended and the side bar on its *Workflow*. A shell's alone.
   */
  onRun?: (session: string) => void;
}) {
  // The application's running action where it holds one (a sign-in outlives the view); its own where drawn alone.
  return (
    <WithHarnessRuns notify={notify}>
      <Band doors={doors} notify={notify} onSessions={onSessions} onRun={onRun} inView={inView} />
    </WithHarnessRuns>
  );
}

function Band({ doors, notify, onSessions, onRun, inView }: {
  doors: AttentionDoors; notify: Notify; onSessions?: () => void; onRun?: (session: string) => void; inView: boolean;
}) {
  const { t } = useTranslation();
  // Who needs the person, as the shell pushes it: the rows, judged in .NET.
  const attention = useAttention();
  const shell = attention.state !== 'app';
  // What names each row and what its doors open: every record, ended ones too, the quests and the asks, pushed beside the rows and
  // read from memory; a browser asks none of them for the band.
  const sessions = useSessions(null, true, shell);
  const quests = useQuests(null, true, shell);
  const asks = useAsks(true, shell);
  // The roster as last read, which names an account as the person knows it: never a refresh, which asks the agents.
  const roster = useHarnesses();
  const approveHelper = useApproveHelper();
  const declineHelper = useDeclineHelper();
  const approveHandOver = useApproveHandOver();
  const declineHandOver = useDeclineHandOver();
  const answerPermission = useAnswerPermission();
  const answerAlone = useAnswerAlone();
  const decideMemory = useDecideSessionMemory();
  const resolveHeld = useResolveHeld();
  const openRecords = useOpenWorkRecords();
  const gitTryAgain = useGitTryAgain();
  const gitIdentity = useGitIdentity();
  const gitAnswer = useGitAnswer();
  const seeReading = useSeeReading();
  const quietCarryOn = useQuietCarryOn();
  const quietLeave = useQuietLeave();
  const stopSession = useStopSession();
  const runNodeWord = useRunNodeWord();
  const setAside = useSetAsideRow();
  const { workspace } = useScope();

  const publish = usePublishAsk();
  const answerGoAhead = useAnswerGoAhead();
  const accept = useAcceptQuest();
  const retry = useRetryQuest();
  const trust = useTrustFolder();
  // A proposal's answer, its one owner: the session's conversation and the Agents page press the same.
  const ruleAnswer = useRuleAnswer({ notify });
  // The words to the session that proposed an allowed rule: the conversation's own door, `sessions say`'s.
  const say = useSay();
  const nudge = useNudge();
  const readOne = useRefreshHarnesses();
  const harnessRun = useHarnessRun();
  // A set-up's verdict, from its row: the review's one owner, as the step's page presses it.
  const reviewActs = useReviewActs({ notify });
  // A second opinion's presses' one owner, as the session's page has.
  const opinionActs = useOpinionActs({ notify });
  // The row an act is on its way for: its acts are held until it answers.
  const [acting, setActing] = useState<string | null>(null);

  // A plain browser: the band is the app's, since nothing here judges who needs the person.
  if (attention.state === 'app') {
    return (
      <AttentionRegion>
        <p className="m-0 text-body text-ink-soft">{t('work.attention.needsApp')}</p>
      </AttentionRegion>
    );
  }
  // Not judged yet: loading, never empty.
  if (attention.state === 'reading') {
    return <AttentionRegion bare={inView}><SkeletonRows /></AttentionRegion>;
  }

  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  // In *Notifications* a press is its card, drawn above from the confirmation channel, never its row.
  const rows = inView ? needsListed(attention.rows) : attention.rows;
  const waiting = attentionOf(rows, {
    sessions: sessions.data ?? [], quests: quests.data ?? [], asks: asks.data ?? [], tools: byTool(harnesses),
  });

  if (waiting.length === 0) {
    if (inView) return null;
    return (
      <AttentionRegion resyncing={attention.resyncing}>
        <NothingNeedsYou working={(sessions.data ?? []).filter((session) => WORKING.has(session.state)).length} />
      </AttentionRegion>
    );
  }

  // One owner for each act's press: what it is on its way for, its sentence said, a refusal in its own words, and the row
  // released either way.
  // An ask under the row hands its own `answered`: it stays open while the act runs, and a refusal is said inside it
  // rather than toasted. The row is released first, so the press that opened the ask is enabled again when its focus returns.
  const run = <T,>(item: Attention, act: () => Promise<T>, said: (answer: T) => void, answered?: Answered) => {
    setActing(attentionKey(item));
    act().then(
      (answer) => { said(answer); setActing(null); answered?.done(); },
      (error) => { setActing(null); if (answered) answered.refused(sentence(error)); else failure(notify)(error); },
    );
  };
  // What an ask under a row is told: the row released whichever way the host answered, then the ask answered in its place.
  const releasing = (answered: Answered): Answered => ({
    done: () => { setActing(null); answered.done(); },
    refused: (said) => { setActing(null); answered.refused(said); },
  });
  // A set-up row's presses: the set-up the row drew, its step's record for the session a *not yet* goes to; the
  // row is released whichever way the host answered. A single press toasts a refusal in its words, a stale one's above all;
  // an ask says it inside itself, where the person pressed it.
  const review = (item: Attention, ask?: Answered) => {
    setActing(attentionKey(item));
    const answered = ask ? releasing(ask) : {
      done: () => setActing(null),
      refused: (said: string) => { notify(said, 'error'); setActing(null); },
    };
    return {
      answered,
      acts: reviewActs.actsFor({
        state: 'shown', step: item.id, record: (quests.data ?? []).find((quest) => quest.id === item.id) ?? null,
      }),
    };
  };
  // A second opinion's row's presses: the gate's one owner, the row released whichever way the driver answered, and
  // a refusal said in its words.
  const opinion = (item: Attention, ask?: Answered): Answered => {
    setActing(attentionKey(item));
    return ask ? releasing(ask) : {
      done: () => setActing(null),
      refused: (said: string) => { notify(said, 'error'); setActing(null); },
    };
  };
  const acts: AttentionActs = {
    ...(opinionActs.shell ? {
      opinionAgain: (item: Attention) => opinionActs.actsFor({ session: item.id, gate: item.opinion?.opinion }).ask?.(opinion(item)),
      opinionAnyway: (item: Attention, words: string | undefined, answered: Answered) =>
        opinionActs.actsFor({ session: item.id, gate: item.opinion?.opinion }).anyway?.(words ?? null, opinion(item, answered)),
    } : {}),
    reviewed: (item) => {
      const { acts: pressed, answered } = review(item);
      if (item.setUp && pressed.reviewed) pressed.reviewed(item.setUp, answered);
      else setActing(null);
    },
    // A shell's alone: the words reach the step's session, and the build its tab, through it.
    ...(reviewActs.shell ? {
      notYet: (item: Attention, words: string, ask: Answered) => {
        const { acts: pressed, answered } = review(item, ask);
        if (item.setUp && pressed.notYet) pressed.notYet(item.setUp, words, answered);
        else answered.done();
      },
      showAgain: (item: Attention) => reviewActs.actsFor({ state: 'shown', step: item.id }).showAgain?.(),
    } : {}),
    // Each receiver is its own publish, the ask page's route: said one by one, and stopped at a refusal.
    publish: (item, to, answered) => run(item, async () => {
      for (const receiver of to) {
        const answer = await publish.mutateAsync({ id: item.id, to: receiver });
        notify(answer.message);
      }
    }, () => nudge(), answered),
    // A stale one's refusal sends no park on (`goesOn: false`) and nudges nothing, since no session waits for it: it
    // runs nothing.
    answer: (item, approved, words, answered) => run(
      item,
      () => answerGoAhead.mutateAsync({
        id: item.ask ?? '', number: item.number ?? 0, approved, words, ...(item.stale ? { goesOn: false } : {}),
      }),
      (answer) => { notify(answer.message); if (!item.stale) nudge(); },
      answered,
    ),
    acceptDeparture: (item) => run(item, () => accept.mutateAsync(item.id), (answer) => notify(answer.message)),
    // Any row set aside with no act: kept in the history, and the row leaves as the slice judges again (a shell's alone: a browser keeps no history).
    setAside: (item) => run(
      item,
      () => setAside.mutateAsync({ kind: item.kind, id: item.id, since: item.since ?? undefined, workspace }),
      (written) => { if (!written) notify(t('work.attention.setAsideRefused'), 'error'); },
    ),
    // A driver's verdicts, holds and rules reach only a shell, so in a browser these rows, and these acts, never appear.
    retry: (item) => run(item, () => retry.mutateAsync({ quest: item.id }), (state) => notify(t(...retryNotice(item.id, state)))),
    trust: (item, answered) => {
      const hold = item.trust;
      if (hold) run(item, () => trust.mutateAsync(hold), (granted) => notify(granted.message, granted.verified ? 'ok' : 'error'), answered);
      else answered.done();
    },
    // An *Allow* tells the session that proposed it, as the conversation's does; a proposal naming none tells none.
    acceptRule: (item, answered) => run(item, () => ruleAnswer.settled(
      item.id, true, item.title, 'allow',
      ruleHearing(item.proposal, sessions.data ?? [], (session, text) => say.mutateAsync({ id: session, text })),
    ), (said) => notify(said), answered),
    declineRule: (item) => run(item, () => ruleAnswer.settled(item.id, false, item.title), (said) => notify(said)),
    // The person's word on a plugin step's attempt held for them: recorded, it is said; refused (the desktop not holding
    // the home, the attempt not held now), the driver's own sentence in the error's tone. Its row moves as the slice is judged again.
    // A close found closed only in part takes the person's stop through the same door, on its close's attempt.
    held: (item, resolution) => {
      const end = item.ended;
      const attempt = item.held
        ?? (end?.node && typeof end.attempt === 'number' ? { unit: end.unit, node: end.node, attempt: end.attempt } : null);
      if (!attempt) return;
      run(item, () => resolveHeld.mutateAsync({ unit: attempt.unit, node: attempt.node, attempt: attempt.attempt, resolution }),
        // *Ask for a fix…* publishes an ask rather than recording a word: the driver's sentence names it.
        (answer) => notify(answer.done && resolution !== 'ask-fix' ? t('work.milestone.held.recorded', { node: attempt.node }) : answer.message, answer.done ? undefined : 'error'));
    },
    // The folder a node record that does not read is kept in, opened for the person to mend it; a refusal (no record
    // kept now) in the driver's words. Its row goes once the record reads, as the slice is judged again.
    openRecords: (item) => run(item, () => openRecords.mutateAsync(), () => {}),
    // A held git remote's *Try again*: its repositories' holds lifted in its workspace, said in the
    // page's words; a refusal in the driver's. Its row goes as .NET judges the slice again, which the lift wakes.
    gitTryAgain: (item) => {
      const held = item.git;
      if (!held) return;
      run(item, () => gitTryAgain.mutateAsync({ repositories: held.repositories, ...(item.workspace ? { workspace: item.workspace } : {}) }),
        (answer) => notify(answer.lifted > 0
          ? t('work.attention.gitHeld.lifted', { count: answer.lifted, host: held.host })
          : t('work.attention.gitHeld.liftedNone', { host: held.host })));
    },
    // An agent's ask for a helper said yes or no to, as its session's page says it: its answer said in the page's
    // words, a refusal by its code in the error's tone.
    helper: (item, yes) => {
      const session = item.session;
      if (!session) return;
      run(item, async () => {
        if (yes) {
          const answer = await approveHelper.mutateAsync({ session, ask: item.id });
          return { done: answer.approved, why: answer.why, message: answer.message };
        }
        const answer = await declineHelper.mutateAsync({ session, ask: item.id });
        return { done: answer.declined, why: answer.why, message: answer.message };
      }, ({ done: settled, why, message }) => {
        if (settled) notify(t(yes ? 'work.helper.approved' : 'work.helper.declined'));
        else notify(helperSaid(t, why, message), 'error');
      });
    },
    // An agent's ask to hand its session over said yes or no to, as its session's foot says it, by the ask's
    // own id: its answer said in the page's words, a refusal by its code in the error's tone.
    handOver: (item, yes) => {
      const session = item.session;
      if (!session) return;
      run(item, () => (yes ? approveHandOver : declineHandOver).mutateAsync({ session, ask: item.id }), (answer) => {
        if (!answer.stands) notify(handOverRefused(t, answer.why, answer.message), 'error');
        else notify(yes ? t('work.handOverAsk.approved', { agent: item.agent ?? answer.agent ?? '' }) : t('work.handOverAsk.declined'));
      });
    },
    // A tool call held for the person answered with the choice pressed, as its session's page answers it: its answer
    // said in the page's words, a refusal by its code in the error's tone.
    permission: (item, option) => {
      const held = item.permission;
      if (!held) return;
      const allows = held.choices.some((choice) => choice.id === option && choice.kind === 'allow_once');
      run(item, () => answerPermission.mutateAsync({ session: held.session, request: held.request, option }), (answer) => {
        if (answer.answered) notify(t(allows ? 'work.wait.permission.answered.allow' : 'work.wait.permission.answered.refuse'));
        else notify(permissionSaid(t, answer.why, answer.message), 'error');
      });
    },
    // A held remote's doors: the row's own door, which opens Settings → Git on its host or its first repository's
    // git remote row by its class, where the identity is set up, signed in again or compared, and the person's own sign-ins are kept after
    // the confirmation that names what they let Daoris's git use; a browser has no such door.
    gitSetUp: (item) => doors['git-held']?.(item),
    gitChoose: (item) => doors['git-held']?.(item),
    // A git go-ahead allowed or refused, as its session's head answers it: said in the page's words, a door that has gone
    // said with its Allow kept for the identical retry; a refusal in the driver's words.
    gitAnswer: (item, allow) => {
      run(item, () => gitAnswer.mutateAsync({ ask: item.id, answer: allow ? 'allow' : 'refuse' }),
        (answer) => notify(t(!allow ? 'work.wait.gitAsk.refused' : answer.waiting ? 'work.wait.gitAsk.allowed' : 'work.wait.gitAsk.kept')));
    },
    // A second check's flag acknowledged, kept on its report; the row leaves as the slice judges again.
    secondCheckSeen: (item) => {
      const flag = item.secondCheck;
      if (flag) run(item, () => seeReading.mutateAsync({ session: flag.session, range: flag.range, finding: flag.index }), () => {});
    },
    // The host's own page for SSH keys, in the system's browser, as Settings → Git's link opens it.
    gitKeyPage: (item) => { if (item.git?.keyPage) window.open(item.git.keyPage, '_blank', 'noreferrer'); },
    // The identity a push is refused with marked read only on its host: the hard form of Write deny, a narrowing.
    gitReadOnly: (item) => {
      const held = item.git;
      if (!held?.identityId) return;
      run(item, () => gitIdentity.mutateAsync({ action: 'read-only', identity: held.identityId!, on: true }),
        () => notify(t('work.attention.git.readOnlyDone', { identity: held.identity ?? '' })));
    },
    // A quiet session's presses: *End and carry on* and *Stop…* asked once under the row,
    // *Leave it running* one press, *Hand over…* its session's page, whose head asks it; a refusal said in its words.
    quiet: (item, act, answered) => {
      const session = item.session ?? item.id;
      if (act === 'hand-over') { doors.quiet?.(item); return; }
      if (act === 'stop') {
        run(item, () => stopSession.mutateAsync(session), () => notify(t('work.attention.quietStopped')), answered && releasing(answered));
        return;
      }
      const press = act === 'carry-on' ? quietCarryOn : quietLeave;
      run(item, () => press.mutateAsync(session), (answer) => {
        if (answer.done) notify(t(act === 'carry-on' ? 'work.attention.quietCarried' : 'work.attention.quietLeft'));
        else notify(answer.message || t('work.attention.quietGone'), 'error');
      }, answered && releasing(answered));
    },
    // A run held at a declined request passed over with the person's words, as the run's view passes its node: kept
    // for the driver's next look, which applies it and clears the row; a refusal by its code, in the ask.
    // The run's own request is asked again or gone on without by the same door's other two words.
    // A person's task is pressed by the same door, on its run and node.
    passOver: (item, words, answered, act) => {
      const node = item.task?.node ?? item.declined?.node;
      if (!node) { answered.done(); return; }
      run(item, async () => {
        const answer = await runNodeWord.mutateAsync({ run: item.task?.run ?? item.id, node, act: act ?? 'skip', words: words || null });
        if (!answer.done) throw new Error(nodeWordRefused(t, answer));
      }, () => {}, answered);
    },
    // A third answer alone held for the person let go on or kept, as its session's page answers it: its answer said
    // in the page's words, a refusal by its code in the error's tone.
    answerAlone: (item, goOn) => {
      const session = item.answer?.session ?? item.session;
      if (!session) return;
      run(item, () => answerAlone.mutateAsync({ session, goOn }), (answer) => {
        if (answer.answered) notify(t(goOn ? 'work.wait.answer.wentOn' : 'work.wait.answer.kept'));
        else notify(answerAloneSaid(t, answer.why, answer.message), 'error');
      });
    },
    // An end the sweep wrote, which no session's page holds, decided by its row as the Memory section decides one: the service's
    // words said back, a refusal in the error's tone.
    memoryEnd: (item, approve) => {
      const op = item.memory?.end?.op;
      if (!op) return;
      run(item, () => decideMemory.mutateAsync({ op, approve }), (answer) => notify(answer.message, answer.decided ? undefined : 'error'));
    },
    // The agent's own sign-in through its account-owning door, as its page starts it: its end is said wherever the
    // person is, and the roster read again then, which reads that account.
    signIn: (item, account) => {
      const harness = item.account?.door;
      if (harness) harnessRun.run(harness, 'login', account);
    },
    // *Sign in for Daoris*: the add flow's new sign-in, through the same door, in place. Its end is
    // said wherever the person is; a new account serves by default, so there is no step 3 here.
    signInNew: (item) => {
      const harness = item.account?.door;
      if (harness) harnessRun.run(harness, 'login-new');
    },
    // That one account, read on the press: the reading a person may ask for, one account at a time.
    read: (item, account) => {
      const agent = item.account?.agent;
      if (agent) run(item, () => readOne.mutateAsync({ agent, profile: account }), () => {});
    },
  };

  // A sign-in in flight is shown under the first row that names its account, where the person started it.
  const signing = harnessRun.running?.endsWith(':login') && harnessRun.runningProfile
    ? { harness: harnessRun.running.slice(0, -':login'.length), account: harnessRun.runningProfile }
    : null;
  const signingRow = signing
    ? waiting.find((item) => item.account?.door === signing.harness
      && item.account.named.some((one) => one.id === signing.account))
    : undefined;
  // A new account's sign-in, from the no-account row: under the first row that offers it for that door.
  const signingNewRow = harnessRun.signingInNew
    ? waiting.find((item) => item.account?.noAccount && item.account.door === harnessRun.signingInNew)
    : undefined;
  // A row of What needs you says the sign-in's line alone, its phase now with what it says.
  const ends = {
    refused: harnessRun.refusal, failed: harnessRun.failure?.sentence, failedAt: harnessRun.failure?.at,
    onRetry: harnessRun.retry, onClose: harnessRun.dismiss, shown: 'line' as const,
    // The other way in, where the agent has one: Codex's device code beside its browser.
    other: harnessRun.otherWay, onSwitch: harnessRun.switchWay,
  };
  const below = (item: Attention) => {
    if (signingNewRow && item.id === signingNewRow.id && item.kind === signingNewRow.kind) {
      return <SignIn id={`${signingNewRow.account!.door}:login-new`} harness={signingNewRow.account!.door!} action="login-new" tool={signingNewRow.account!.product} {...ends} />;
    }
    if (!signing || !signingRow || item.id !== signingRow.id || item.kind !== signingRow.kind) return null;
    const label = item.account?.named.find((one) => one.id === signing.account)?.label ?? signing.account;
    return <SignIn id={harnessRun.running!} harness={signing.harness} profile={label} tool={item.account?.product} {...ends} />;
  };

  // Every stale go-ahead refused with the person's one reason, each through its own door, the service's answer, as its
  // row's *Refuse…* sends it: one after another, each its own answer, saying no park goes on, and nothing nudged, since nothing waits
  // for any of them. What the service refused is said inside the ask, beside how many it took; those it took stay taken.
  const refuseStale = (items: readonly Attention[], words: string, answered: Answered) => {
    setActing(STALE_KEY);
    void (async () => {
      let refused = 0;
      const refusals: string[] = [];
      for (const item of items) {
        try {
          await answerGoAhead.mutateAsync({ id: item.ask ?? '', number: item.number ?? 0, approved: false, words, goesOn: false });
          refused += 1;
        } catch (error) {
          refusals.push(sentence(error));
        }
      }
      setActing(null);
      if (refused > 0) notify(t('work.attention.stale.refused', { count: refused }));
      if (refusals.length > 0) answered.refused(refusals.join('\n'));
      else answered.done();
    })();
  };

  // Each row whose step waits on the person opens its run there: the session it names, else its quest's newest here.
  const runDoor = (item: Attention) => {
    const named = onRun ? rowRun(item) : null;
    const session = named ? runSessionOf(named, sessions.data ?? []) : null;
    return session && onRun ? () => onRun(session) : undefined;
  };

  return (
    <AttentionRegion resyncing={attention.resyncing} bare={inView}>
      <AttentionList
        items={waiting}
        doors={doors}
        run={runDoor}
        acts={acts}
        acting={acting}
        held={(item) => ACCOUNT_KINDS.has(item.kind) && harnessRun.busy}
        below={below}
        onSessions={onSessions}
        bare={inView}
        onRefuseStale={refuseStale}
        refusingStale={acting === STALE_KEY}
      />
    </AttentionRegion>
  );
}
