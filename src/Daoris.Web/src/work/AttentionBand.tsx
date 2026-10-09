import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { runsForLine } from '../agents/agents';
import { useHarnessRun, WithHarnessRuns } from '../harnessRuns';
import {
  useAcceptQuest, useAnswerGoAhead, useAsks, usePublishAsk, useQuests, useRegistry, useSessions,
} from '../queries';
import {
  retryNotice, useAccounts, useConsidered, useHarnessAction, useHarnesses, useNudge, useRefreshHarnesses, useRetryQuest,
  useRuleProposal, useRules, useSessionGroups, useTrustFolder, useUntrusted, useWaits,
} from '../shell';
import { SignIn } from '../SignIn';
import { byTool } from '../tools';
import { failure, type Notify } from '../ui';
import type { AccountsKnown } from './accountAttention';
import type { Attention, AttentionActs } from './AttentionRow';
import { attentionKey, AttentionList, type AttentionDoors, AttentionRegion, NothingNeedsYou } from './AttentionList';
import { needsAPerson, rowRun } from './attention';
import { useReviewActs } from './reviewActs';
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
  const ended = useSessions(null, true, reviewing || onRun !== undefined);
  // The accounts as last known and the tick's waits (UX6d): a browser has none.
  const accounts = useAccountsKnown();

  const publish = usePublishAsk();
  const answerGoAhead = useAnswerGoAhead();
  const accept = useAcceptQuest();
  const retry = useRetryQuest();
  const trust = useTrustFolder();
  const settle = useRuleProposal();
  const nudge = useNudge();
  const readOne = useRefreshHarnesses();
  const harnessAct = useHarnessAction();
  const harnessRun = useHarnessRun();
  // A set-up's verdict, from its row (REVIEWENV1g): the review's one owner, as the step's page presses it.
  const reviewActs = useReviewActs({ notify });
  // The row an act is on its way for: its acts are held until it answers.
  const [acting, setActing] = useState<string | null>(null);

  const waiting = needsAPerson(
    (reviewing ? ended.data : undefined) ?? sessions.data ?? [], quests.data ?? [], registry.data ?? [], asks.data ?? [],
    untrusted.data ?? [], Array.isArray(proposals) ? proposals : [], considered.data ?? [], groups.data ?? [], accounts);

  if (waiting.length === 0) {
    return (
      <AttentionRegion>
        <NothingNeedsYou working={(sessions.data ?? []).filter((session) => WORKING.has(session.state)).length} />
      </AttentionRegion>
    );
  }

  // One owner for each act's press: what it is on its way for, its sentence said, a refusal in its own words, and the row
  // released either way.
  const run = <T,>(item: Attention, act: () => Promise<T>, said: (answer: T) => void) => {
    setActing(attentionKey(item));
    act().then(said, failure(notify)).finally(() => setActing(null));
  };
  // A set-up row's presses (REVIEWENV1g): the set-up the row drew, its step's record for the session a *not yet* goes to; the
  // row is released whichever way the host answered, and a refusal is said in its words, a stale one's above all.
  const review = (item: Attention) => {
    setActing(attentionKey(item));
    const answered = {
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
  const acts: AttentionActs = {
    reviewed: (item) => {
      const { acts: pressed, answered } = review(item);
      if (item.setUp && pressed.reviewed) pressed.reviewed(item.setUp, answered);
      else setActing(null);
    },
    // A shell's alone: the words reach the step's session, and the build its tab, through it.
    ...(reviewActs.shell ? {
      notYet: (item: Attention, words: string) => {
        const { acts: pressed, answered } = review(item);
        if (item.setUp && pressed.notYet) pressed.notYet(item.setUp, words, answered);
        else setActing(null);
      },
      showAgain: (item: Attention) => reviewActs.actsFor({ state: 'shown', step: item.id }).showAgain?.(),
    } : {}),
    // Each receiver is its own publish, the ask page's route: said one by one, and stopped at a refusal.
    publish: (item, to) => run(item, async () => {
      for (const receiver of to) {
        const answer = await publish.mutateAsync({ id: item.id, to: receiver });
        notify(answer.message);
      }
    }, () => nudge()),
    answer: (item, approved, words) => run(
      item,
      () => answerGoAhead.mutateAsync({ id: item.ask ?? '', number: item.number ?? 0, approved, words }),
      (answer) => { notify(answer.message); nudge(); },
    ),
    acceptDeparture: (item) => run(item, () => accept.mutateAsync(item.id), (answer) => notify(answer.message)),
    // A driver's verdicts, holds and rules reach only a shell, so in a browser these rows, and these acts, never appear.
    retry: (item) => run(item, () => retry.mutateAsync({ quest: item.id }), (state) => notify(t(...retryNotice(item.id, state)))),
    trust: (item) => {
      const hold = item.trust;
      if (hold) run(item, () => trust.mutateAsync(hold), (granted) => notify(granted.message, granted.verified ? 'ok' : 'error'));
    },
    acceptRule: (item) => run(item, () => settle.mutateAsync({ id: item.id, accept: true }),
      () => notify(t('settings.rules.proposals.accepted', { change: item.title }))),
    declineRule: (item) => run(item, () => settle.mutateAsync({ id: item.id, accept: false }),
      () => notify(t('settings.rules.proposals.declined', { change: item.title }))),
    // The agent's own sign-in through its account-owning door, as its page starts it (SIGNIN1): its end is said wherever the
    // person is, and the roster read again then, which reads that account (ROSTER1).
    signIn: (item, account) => {
      const harness = item.account?.harness;
      if (harness) harnessRun.run(harness, 'login', account);
    },
    // That one account, read on the press (ROSTER1): the reading a person may ask for, one account at a time.
    read: (item, account) => {
      const agent = item.account?.agent;
      if (agent) run(item, () => readOne.mutateAsync({ agent, profile: account }), () => {});
    },
    // Into the list the waiting start reads (D130 §3.3), as the agent page's *Add to …'s list* (ACCT1): said where it runs now.
    letRun: (item, account) => {
      const facts = item.account;
      const outside = facts?.outside;
      if (!facts?.harness || !outside || outside.id !== account) return;
      const harness = facts.harness;
      run(
        item,
        () => harnessAct.mutateAsync({ harness, action: 'profile-join', profile: account, join: [outside.list] }),
        (answer) => notify(t('agents.joined', {
          account: outside.label,
          places: runsForLine((answer.places ?? []).map((place) => ({ workspace: place.workspace ?? null, first: place.default }))),
        })),
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
  const below = (item: Attention) => {
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
