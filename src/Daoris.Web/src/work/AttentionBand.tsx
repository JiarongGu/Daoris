import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { runsForLine } from '../agents/agents';
import { useHarnessRun, WithHarnessRuns } from '../agents/harnessRuns';
import {
  useAcceptQuest, useAnswerGoAhead, useAsks, usePublishAsk, useQuests, useRegistry, useSessions,
} from '../data/queries';
import {
  retryNotice, useAccounts, useApproveHelper, useConsidered, useDeclineHelper, useHarnessAction, useHarnesses, useHelperWaits, useNudge,
  useOpinionWaits, useRefreshHarnesses, useResolveHeld, useWorkState, useRetryQuest, useRules, useSessionGroups, useTrustFolder, useUntrusted,
  useWaits,
} from '../bridge/shell';
import { useSay } from '../bridge/conversation';
import { SignIn } from '../agents/SignIn';
import { byTool } from '../agents/tools';
import { failure, type Notify } from '../design/ui';
import type { AccountsKnown } from './accountAttention';
import { sentence } from '../locales/format';
import type { Attention, AttentionActs } from './AttentionRow';
import type { Answered } from './InlineConfirm';
import { attentionKey, AttentionList, type AttentionDoors, AttentionRegion, NothingNeedsYou } from './AttentionList';
import { needsAPerson, rowRun } from './attention';
import { useOpinionActs } from './opinionActs';
import { useReviewActs } from './reviewActs';
import { ruleHearing, useRuleAnswer } from './ruleAnswer';
import { helperSaid } from './sessionActs';
import { runSessionOf } from '../workflow/run';

export type { AttentionDoors } from './AttentionList';

/** The sessions working now, which the line says when nothing needs the person (design §6.1). */
const WORKING: ReadonlySet<string> = new Set(['starting', 'working']);

/** The kinds that are an account's (UX6d): their acts wait while a sign-in of their agent's runs. */
const ACCOUNT_KINDS: ReadonlySet<Attention['kind']> = new Set(['account-wait', 'signed-out']);

/**
 * What the band reads of the accounts (UX6d): the roster as last read and the accounts' files, which the frame already
 * holds (the Agents place and its badge read the same two), and the tick's waits. Never a refresh: opening Overview asks
 * no agent about an account (D150 point 5).
 */
export function useAccountsKnown(): AccountsKnown {
  const roster = useHarnesses();
  const accounts = useAccounts();
  const waits = useWaits();
  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  return { tools: byTool(harnesses), use: accounts.data, waits: waits.data ?? [] };
}

/**
 * Overview's **what needs you**, the page's lead (UX6c, design §6).
 *
 * @remarks
 * **Overview keeps the landing** (D40 — "is anything sitting" is still the first question), and since UX6c this is its
 * first content: what holds work, what waits for the person's word, and what is ready for them, in three groups, the
 * longest waiting first in each. Continuous monitoring is the reported source of fatigue (research §2), so visibility is
 * load-bearing and *watching* is not the mechanism: the band is what a person checks instead of watching.
 *
 * **It settles what one press can** (§6.3): a proposed ask is published to what its declarations named, a parked quest
 * tried again, a departure accepted, each on its record's own route with its sentence said back; a go-ahead's answer, a
 * folder's trust and a widening of the rules ask once under the row. Every other row is a door. **No row starts a process
 * to find out**: every fact here is a list the page already holds or the tick hands it (`needsAPerson`), the session
 * groups included, which the frame reads on every view of a shell and this reads under the same key.
 *
 * **The accounts** (UX6d): a start waiting on its accounts and a signed-out account a list holds, from the tick's waits,
 * the roster's last readings and the accounts' files. *Sign in* runs the agent's own sign-in through the application's one
 * running action (SIGNIN1), the agent page's own, so it outlives leaving Overview and its steps follow under the row;
 * *Read* reads that one account (ROSTER1); *Let … run …* joins the list (ACCT1's `profile-join`) after its question.
 *
 * **With nothing waiting it is one line**, *Nothing needs you*, and what is working (§6.1).
 *
 * The organism: it holds the queries and the acts, so the list and its rows hold none (components §2).
 */
export function AttentionBand({ doors = {}, notify = () => {}, onSessions, onRun }: {
  doors?: AttentionDoors;
  /** Where each act's sentence is said: the service's or the driver's, verbatim (frontend §4a). */
  notify?: Notify;
  /** Sessions itself, where *Ready for you*'s rows past its fifth are: a shell's alone. */
  onSessions?: () => void;
  /**
   * The door into a run (WORKFLOW1c, the workflow design §7): a row whose step waits on the person opens that step, its session
   * attended and the side bar on its *Workflow*. A shell's alone.
   */
  onRun?: (session: string) => void;
}) {
  // The application's running action where it holds one (a sign-in outlives the view, SIGNIN1); its own where drawn alone.
  return (
    <WithHarnessRuns notify={notify}>
      <Band doors={doors} notify={notify} onSessions={onSessions} onRun={onRun} />
    </WithHarnessRuns>
  );
}

function Band({ doors, notify, onSessions, onRun }: {
  doors: AttentionDoors; notify: Notify; onSessions?: () => void; onRun?: (session: string) => void;
}) {
  const { t } = useTranslation();
  const sessions = useSessions(null, false);
  const quests = useQuests(null, false);
  const registry = useRegistry();
  // The asks still waiting (INT4d) — the Quests view's own list, so one fetch serves both. Each carries its go-aheads.
  const asks = useAsks(false);
  // The folders the driver is holding for the agent's trust (D73) — the shell's tick, so a browser has none.
  const untrusted = useUntrusted();
  // What agents proposed about the rules (PERM2) — the machine's file, so a browser has none.
  const rules = useRules();
  // An older shell never sends proposals, and one that answers RULES with something else sends none.
  const proposals = rules.data?.proposals;
  // The quests the driver's last look parked on their failed sessions (SESSUX1i) — the shell's tick, so a browser has none.
  const considered = useConsidered();
  // Where Sessions' list places each session (D126): the answer the frame already holds on every view, under its key.
  const groups = useSessionGroups();
  // A session to review has ended, so its record is in the list with the ended ones, which the frame holds too; asked only
  // while there is one to name, so a browser, which has no groups, asks nothing more.
  const reviewing = (groups.data ?? []).some((placed) => placed.group === 'review');
  // And every record, ended ones too, where a row's door opens its run (WORKFLOW1c): a held done's or a set-up's quest is
  // found its newest session here. The frame holds the same list on every view of a shell, under the same key.
  // A rule's proposer is found here too (PROPOSEHERE1c), so the list is asked while a proposal names a session.
  const proposed = Array.isArray(proposals) && proposals.some((one) => one.session);
  const ended = useSessions(null, true, reviewing || onRun !== undefined || proposed);
  const opinionWaits = useOpinionWaits(reviewing);
  // Each unit's milestone (FABRIC-MILESTONE1b): the band lists only a unit with something waiting on the person, as the driver's
  // derivation says it. Every unit is read, one answer the page shares with Branches; a browser has none.
  const workState = useWorkState();
  // The agents' asks for a helper that wait on the person's yes (MIXAGENT2c): the driver's own list, so a browser has none.
  const helperWaits = useHelperWaits();
  const approveHelper = useApproveHelper();
  const declineHelper = useDeclineHelper();
  const resolveHeld = useResolveHeld();
  // The accounts as last known and the tick's waits (UX6d): a browser has none.
  const accounts = useAccountsKnown();

  const publish = usePublishAsk();
  const answerGoAhead = useAnswerGoAhead();
  const accept = useAcceptQuest();
  const retry = useRetryQuest();
  const trust = useTrustFolder();
  // A proposal's answer, its one owner (PROPOSEHERE1b): the session's conversation and the Agents page press the same.
  const ruleAnswer = useRuleAnswer({ notify });
  // The words to the session that proposed an allowed rule (PROPOSEHERE1c): the conversation's own door, `sessions say`'s.
  const say = useSay();
  const nudge = useNudge();
  const readOne = useRefreshHarnesses();
  const harnessAct = useHarnessAction();
  const harnessRun = useHarnessRun();
  // A set-up's verdict, from its row (REVIEWENV1g): the review's one owner, as the step's page presses it.
  const reviewActs = useReviewActs({ notify });
  // What of the second opinion's gates waits on the person (XAGENT1g), and its presses' one owner, as the session's page has.
  // An opinion is owed only on work Sessions' list places To review, so it is asked only while there is one to name.
  const opinionActs = useOpinionActs({ notify });
  // The row an act is on its way for: its acts are held until it answers.
  const [acting, setActing] = useState<string | null>(null);

  const waiting = needsAPerson(
    (reviewing ? ended.data : undefined) ?? sessions.data ?? [], quests.data ?? [], registry.data ?? [], asks.data ?? [],
    untrusted.data ?? [], Array.isArray(proposals) ? proposals : [], considered.data ?? [], groups.data ?? [], accounts,
    opinionWaits.data ?? [], helperWaits.data ?? [], workState.data?.units ?? [], workState.data?.held ?? []);

  if (waiting.length === 0) {
    return (
      <AttentionRegion>
        <NothingNeedsYou working={(sessions.data ?? []).filter((session) => WORKING.has(session.state)).length} />
      </AttentionRegion>
    );
  }

  // One owner for each act's press: what it is on its way for, its sentence said, a refusal in its own words, and the row
  // released either way.
  // An ask under the row hands its own `answered` (UXFIX2b3a): it stays open while the act runs, and a refusal is said inside it
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
  // A set-up row's presses (REVIEWENV1g): the set-up the row drew, its step's record for the session a *not yet* goes to; the
  // row is released whichever way the host answered. A single press toasts a refusal in its words, a stale one's above all;
  // an ask says it inside itself, where it was pressed.
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
  // A second opinion's row's presses (XAGENT1g): the gate's one owner, the row released whichever way the driver answered, and
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
    answer: (item, approved, words, answered) => run(
      item,
      () => answerGoAhead.mutateAsync({ id: item.ask ?? '', number: item.number ?? 0, approved, words }),
      (answer) => { notify(answer.message); nudge(); },
      answered,
    ),
    acceptDeparture: (item) => run(item, () => accept.mutateAsync(item.id), (answer) => notify(answer.message)),
    // A driver's verdicts, holds and rules reach only a shell, so in a browser these rows, and these acts, never appear.
    retry: (item) => run(item, () => retry.mutateAsync({ quest: item.id }), (state) => notify(t(...retryNotice(item.id, state)))),
    trust: (item, answered) => {
      const hold = item.trust;
      if (hold) run(item, () => trust.mutateAsync(hold), (granted) => notify(granted.message, granted.verified ? 'ok' : 'error'), answered);
      else answered.done();
    },
    // An *Allow* tells the session that proposed it, as the conversation's does (PROPOSEHERE1c); a proposal naming none tells none.
    acceptRule: (item, answered) => run(item, () => ruleAnswer.settled(
      item.id, true, item.title, 'allow',
      ruleHearing((Array.isArray(proposals) ? proposals : []).find((one) => one.id === item.id), ended.data ?? sessions.data ?? [],
        (session, text) => say.mutateAsync({ id: session, text })),
    ), (said) => notify(said), answered),
    declineRule: (item) => run(item, () => ruleAnswer.settled(item.id, false, item.title), (said) => notify(said)),
    // The person's word on a plugin step's attempt held for them (FABRIC-PERFORM1g): recorded, it is said; refused (the desktop not holding
    // the home, the attempt no longer held), the driver's own sentence in the error's tone. The hub reads the work state again (D169).
    held: (item, resolution) => {
      const attempt = item.held;
      if (!attempt) return;
      run(item, () => resolveHeld.mutateAsync({ unit: attempt.unit, node: attempt.node, attempt: attempt.attempt, resolution }),
        (answer) => notify(answer.done ? t('work.milestone.held.recorded', { node: attempt.node }) : answer.message, answer.done ? undefined : 'error'));
    },
    // An agent's ask for a helper said yes or no to, as its session's page says it (MIXAGENT2c): its answer said in the page's
    // words, a refusal by its code in the error's tone. The hub reads the waits again (D169).
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
    // The agent's own sign-in through its account-owning door, as its page starts it (SIGNIN1): its end is said wherever the
    // person is, and the roster read again then, which reads that account (ROSTER1).
    signIn: (item, account) => {
      const harness = item.account?.harness;
      if (harness) harnessRun.run(harness, 'login', account);
    },
    // *Sign in for Daoris* (OWNSPACE1b-B, D175 point 7): the add flow's new sign-in, through the same door, in place. Its end is
    // said wherever the person is; a new account serves by default, so there is no step 3 here.
    signInNew: (item) => {
      const harness = item.account?.harness;
      if (harness) harnessRun.run(harness, 'login-new');
    },
    // That one account, read on the press (ROSTER1): the reading a person may ask for, one account at a time.
    read: (item, account) => {
      const agent = item.account?.agent;
      if (agent) run(item, () => readOne.mutateAsync({ agent, profile: account }), () => {});
    },
    // Into the list the waiting start reads (D130 §3.3), as the agent page's *Add to …'s list* (ACCT1): said where it runs now.
    letRun: (item, account, answered) => {
      const facts = item.account;
      const outside = facts?.outside;
      if (!facts?.harness || !outside || outside.id !== account) { answered.done(); return; }
      const harness = facts.harness;
      run(
        item,
        () => harnessAct.mutateAsync({ harness, action: 'profile-join', profile: account, join: [outside.list] }),
        // ACCTUX4b: no place marked first, since *first* is the scope whose next start takes it, which the join's answer never names.
        (answer) => notify(t('agents.joined', {
          account: outside.label,
          places: runsForLine((answer.places ?? []).map((place) => ({ workspace: place.workspace ?? null, first: false }))),
        })),
        answered,
      );
    },
  };

  // A sign-in in flight is shown under the first row that names its account, where it was started (platform language §4).
  const signing = harnessRun.running?.endsWith(':login') && harnessRun.runningProfile
    ? { harness: harnessRun.running.slice(0, -':login'.length), account: harnessRun.runningProfile }
    : null;
  const signingRow = signing
    ? waiting.find((item) => item.account?.harness === signing.harness
      && item.account.named.some((one) => one.id === signing.account))
    : undefined;
  // A new account's sign-in, from the no-account row (OWNSPACE1b-B): under the first row that offers it for that door.
  const signingNewRow = harnessRun.signingInNew
    ? waiting.find((item) => item.account?.noAccount && item.account.harness === harnessRun.signingInNew)
    : undefined;
  const below = (item: Attention) => {
    if (signingNewRow && item === signingNewRow) {
      return <SignIn id={`${signingNewRow.account!.harness}:login-new`} harness={signingNewRow.account!.harness!} action="login-new" tool={signingNewRow.account!.product} />;
    }
    if (!signing || item !== signingRow) return null;
    const label = item.account?.named.find((one) => one.id === signing.account)?.label ?? signing.account;
    return <SignIn id={harnessRun.running!} harness={signing.harness} profile={label} />;
  };

  // Each row whose step waits on the person opens its run there (WORKFLOW1c): the session it names, else its quest's newest here.
  const runDoor = (item: Attention) => {
    const named = onRun ? rowRun(item) : null;
    const session = named ? runSessionOf(named, ended.data ?? sessions.data ?? []) : null;
    return session && onRun ? () => onRun(session) : undefined;
  };

  return (
    <AttentionRegion>
      <AttentionList
        items={waiting}
        doors={doors}
        run={runDoor}
        acts={acts}
        acting={acting}
        held={(item) => ACCOUNT_KINDS.has(item.kind) && harnessRun.busy}
        below={below}
        onSessions={onSessions}
      />
    </AttentionRegion>
  );
}
