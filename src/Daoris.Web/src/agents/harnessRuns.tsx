import { createContext, type ReactNode, useContext, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useQueryClient } from '@tanstack/react-query';
import { sentence } from '../locales/format';
import { type HarnessRoster, useHarnessAction, useHarnessEnded } from '../bridge/shell';
import { byTool } from './tools';
import type { SignInWay } from './SignInSteps';
import { accountLabel } from './agents';
import { keys } from '../data/queries';
import type { Notify } from '../design/ui';
import type { Answered } from '../work/InlineConfirm';

/** A process action a person started on a tool: an install, an update, a pin, a sign-in, or an account removed. */
export type HarnessRunAction = 'install' | 'update' | 'login' | 'login-new' | 'pin' | 'unpin' | 'profile-remove';

/**
 * A new account a sign-in kept (UX7b, ACCT1): which tool, its id and who signed in. It joined Daoris's accounts as it was
 * made (D197), and is held until the person answers the add flow's last step, its name.
 */
export type AccountAdded = {
  harness: string; profile: string; account: string | null;
  /** The name the sign-in's end offered it (ACCOUNTID1b, `HARNESS_ENDED.offered`); undefined from an older shell. */
  offered?: string | null;
};

/** Which action runs on which tool, and for whom, while it runs — and how to start one. */
export type HarnessRun = {
  /** The new account the last sign-in to another account kept, waiting on its name; null once answered. */
  added: AccountAdded | null;
  /** The add flow's last step answered or set aside: the account is no longer waiting on it. */
  settleAdded: () => void;
  /** `<harness>:<action>` of the action last started, whose console is shown under the tool doing it. */
  running: string | null;
  /** The action still waiting on its end (a process answers `started` and ends as news), or null. */
  inFlight: string | null;
  /** Which account a sign-in is for, while it runs, so it lands on that row. */
  runningProfile: string | null;
  /** Which tool a sign-in to another account runs for (D66 §3), while it runs. */
  signingInNew: string | null;
  /** Whether anything is on its way or running: one at a time, by construction. */
  busy: boolean;
  /**
   * Start one. An ask that pressed it (*Remove…*'s, UXFIX2) is told how it ended: `done` once it ended well or started a
   * process whose end is news, `refused` with the sentence that would have been its toast.
   */
  run: (harness: string, action: HarnessRunAction, profile?: string, version?: string, answered?: Answered) => void;
  /**
   * Follow an action the machine already started for the person (HELP6: Ask Daoris's Apply of an update
   * or a pin): its console shows under the tool doing it, and its end is said, as for one started here.
   */
  follow: (harness: string, action: 'update' | 'pin') => void;
  /**
   * The driver's sentence where a sign-in's start was refused (CODEXSIGNIN2), or null. The sign-in's panel stays up and says it,
   * since a refusal is said where its action was started; `retry` starts the same sign-in again, `dismiss` closes the panel.
   */
  refusal: string | null;
  retry: () => void;
  dismiss: () => void;
  /**
   * When the running action was pressed (SIGNIN1), so a sign-in's panel says how long it has run where its host writes no
   * phases of its own; null with nothing pressed.
   */
  pressedAt: number | null;
  /**
   * A sign-in that ended without signing in (SIGNIN1, the UI/UX design §3.2's *Failed at a phase*): its sentence and when it
   * ended. Its panel stays up and says it, with *Try again* and *Close*, as a refused start's does; null otherwise.
   */
  failure: { sentence: string; at: number } | null;
  /**
   * The way the last sign-in's agent has beside the one it ran (CODEXLOGIN1): the device code where it ran the browser, the
   * browser where it ran the device code; null where the agent has one way (the roster's `deviceCode`) or no sign-in was pressed.
   */
  otherWay: SignInWay | null;
  /**
   * The same sign-in the other way: while one runs, the driver stops it and starts this one in its place (`switch`), and the
   * stopped one's end says nothing; after one failed or was refused, it starts as a press would.
   */
  switchWay: () => void;
};

const Runs = createContext<HarnessRun | null>(null);

/** The state of a tool's running action, and the one listener for its end. */
function useRunState(notify: Notify): HarnessRun {
  const { t } = useTranslation();
  const client = useQueryClient();
  const act = useHarnessAction();
  const [running, setRunning] = useState<string | null>(null);
  // 🔴 "Running" lasts until the END, not the request (REV3). A process action answers `started` at
  // once, so gating on the request re-enabled every button while a login still waited on a browser; a
  // second press overwrote which action was being followed, and the first's end closed the second's
  // panel. The host refuses a second one too.
  const [inFlight, setInFlight] = useState<string | null>(null);
  const [runningProfile, setRunningProfile] = useState<string | null>(null);
  const [signingInNew, setSigningInNew] = useState<string | null>(null);
  const [added, setAdded] = useState<AccountAdded | null>(null);
  // The same key, readable from the event handler without re-subscribing on every render.
  const runningRef = useRef<string | null>(null);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [failure, setFailure] = useState<{ sentence: string; at: number } | null>(null);
  const [pressedAt, setPressedAt] = useState<number | null>(null);
  const lastSignIn = useRef<{ harness: string; action: HarnessRunAction; profile?: string; way: SignInWay } | null>(null);
  // The last sign-in's way and whether its agent has the other (CODEXLOGIN1), as state, so its panel draws the switch; and
  // whether the one a switch replaces was heard stopped.
  const [signedWay, setSignedWay] = useState<{ way: SignInWay; deviceCode: boolean } | null>(null);
  const stopped = useRef(false);

  // Whether a door's agent signs in by a device code beside its own sign-in, read from the roster the page already holds.
  const deviceCodeOf = (harness: string) => {
    const roster = client.getQueryData<HarnessRoster>(keys.harnesses);
    return Boolean(roster?.harnesses.find((each) => each.harness === harness)?.deviceCode);
  };

  // Read the roster at the moment an action ends: a rename or a view change can happen while a sign-in runs.
  // Looking in the shared cache asks no agent to read its sign-in again.
  const called = (harness: string, profile: string) => {
    const roster = client.getQueryData<HarnessRoster>(keys.harnesses);
    const tool = byTool(roster?.harnesses ?? []).find((each) => each.doors.some((door) => door.harness === harness));
    const account = tool?.accounts.find((each) => each.name === profile);
    return account ? accountLabel(account, tool) : null;
  };

  /**
   * What an action's end means to the person — said once, whether it ended inside the request (a file
   * edit) or later as news (a process). The harness's own exit code decides which it was: Daoris ran
   * somebody else's tool and reports what it did. A sign-in's sentence names the account, because
   * that is what the person came for.
   */
  const ended = (
    action: string, profile: string | undefined, exitCode: number, problem: string | null,
    account?: string | null, kept?: boolean | null, offered?: string | null, cancelled?: boolean | null,
  ) => {
    const signIn = action === 'login' || action === 'login-new';
    // SIGNIN1: a sign-in that did not sign in says so in its own panel, which stays up with its way on (the UI/UX design
    // §3.2's *Failed at a phase*), not as a toast with the panel gone; one the person cancelled closes, saying nothing.
    const failed = !signIn || cancelled ? null
      : problem ?? (exitCode !== 0
        ? t(action === 'login-new' ? 'harness.loginNew.failed' : 'harness.login.failed', { code: exitCode })
        : action === 'login-new' && !kept ? t('harness.loginNew.nobody') : null);
    if (failed) {
      setFailure({ sentence: failed, at: Date.now() });
      return;
    }
    setRunningProfile(null);
    setSigningInNew(null);
    if (signIn && cancelled) return;
    if (problem) {
      notify(problem, 'error');
      return;
    }
    // Another account (D66 §3): kept only when the sign-in finished, and named by who signed in. It joined Daoris's accounts
    // as it was made, every workspace's work may run on it (D197; the account-pool design §4.1), which the agent's page says
    // where it asks its name (UX7b); no account is ever nowhere.
    if (action === 'login-new') {
      if (exitCode !== 0) notify(t('harness.loginNew.failed', { code: exitCode }), 'error');
      else if (!kept) notify(t('harness.loginNew.nobody'), 'error');
      else {
        if (account) notify(t('harness.loginNew.done', { account }));
        else notify(t('harness.loginNew.unnamed', { profile }));
        const harness = runningRef.current?.split(':')[0];
        if (harness && profile) setAdded({ harness, profile, account: account ?? null, offered });
      }
      return;
    }
    if (action === 'login') {
      const harness = runningRef.current?.split(':')[0];
      const name = harness && profile ? called(harness, profile) : null;
      if (exitCode === 0) notify(t('harness.login.done', { profile: name ?? account ?? profile }));
      else notify(t('harness.login.failed', { code: exitCode }), 'error');
      return;
    }
    const harness = runningRef.current?.split(':')[0];
    if (exitCode === 0) notify(t('harness.done', { harness, action: t(`harness.${action}`) }));
    else notify(t('harness.failed', { harness, action: t(`harness.${action}`), code: exitCode }), 'error');
  };

  // 🔴 A process action ends as NEWS (2026-09-23): a sign-in waits on a person in a browser, longer
  // than a request may take on the bridge. Only the action this state started is this state's.
  useHarnessEnded((news) => {
    if (runningRef.current !== `${news.harness}:${news.action}`) return;
    // Stopped for the same sign-in by the other way (CODEXLOGIN1), which runs on in the same panel: nothing to say.
    if (news.switched) {
      stopped.current = true;
      return;
    }
    setInFlight(null);
    ended(news.action, news.profile ?? undefined, news.exitCode, news.problem, news.account, news.kept, news.offered, news.cancelled);
  });

  /**
   * Start an action; a sign-in by `way`, the first where none is named (CODEXLOGIN1), and with `switching`, in the place of the
   * same sign-in running another way, which the driver stops first.
   */
  const start = (
    harness: string, action: HarnessRunAction, profile?: string, version?: string, answered?: Answered,
    way: SignInWay = 'browser', switching = false,
  ) => {
    // Never over one still running: whose end the news belongs to is the one thing this must not lose. A switch takes the
    // running sign-in's place, its end told as switched and said by nobody.
    if (inFlight !== null && !switching) return;
    stopped.current = false;
    setRefusal(null);
    setFailure(null);
    setPressedAt(Date.now());
    const signIn = action === 'login' || action === 'login-new';
    if (signIn) {
      lastSignIn.current = { harness, action, profile, way };
      setSignedWay({ way, deviceCode: deviceCodeOf(harness) });
    }
    setRunning(`${harness}:${action}`);
    runningRef.current = `${harness}:${action}`;
    setRunningProfile(action === 'login' ? profile ?? null : null);
    setSigningInNew(action === 'login-new' ? harness : null);
    // Another new account starts over: the last one's question was set aside.
    if (action === 'login-new') setAdded(null);
    // The way only where it is not the first or it switches, so a press says what it always said.
    const how = signIn && (way !== 'browser' || switching) ? { way, ...(switching ? { switch: true } : {}) } : {};
    act.mutate({ harness, action, profile, version, ...how }, {
      // A file edit ends inside the request; a process answers `started` and ends as news, heard
      // above. Either way the end is said once, by the same sentence.
      onSuccess: (result) => {
        if (result.started) {
          setInFlight(`${harness}:${action}`);
          // Its end is news, said wherever the person is: the ask that pressed it has nothing more to wait for.
          answered?.done();
          return;
        }
        // Ended inside the request: an ask that pressed it says a failed end inside itself, in the toast's own words, and
        // closes on a good one, whose sentence is the toast's as before.
        if (answered) {
          const code = result.exitCode ?? 0;
          if (code !== 0) {
            setRunningProfile(null);
            setSigningInNew(null);
            answered.refused(t('harness.failed', { harness, action: t(`harness.${action}`), code }));
            return;
          }
          answered.done();
        }
        ended(action, profile, result.exitCode ?? 0, null);
      },
      onError: (error: unknown) => {
        // A sign-in's refusal is said in its own panel, which stays up with its way on (CODEXSIGNIN2), not as a toast. A
        // switch refused after the driver stopped the one running leaves nothing running; one refused before it did (its
        // way's own refusal) leaves that one running, still followed (CODEXLOGIN1).
        if (!answered && signIn) {
          if (!switching || stopped.current) setInFlight(null);
          setRefusal(sentence(error));
          return;
        }
        setRunningProfile(null);
        setSigningInNew(null);
        if (answered) answered.refused(sentence(error));
        else notify(sentence(error), 'error');
      },
    });
  };

  const run: HarnessRun['run'] = (harness, action, profile, version, answered) =>
    start(harness, action, profile, version, answered);

  // The same sign-in again, by the same way, from its refused or failed panel; or the panel closed with nothing left of the
  // press. Over one still running, again or the other way takes its place (CODEXLOGIN1).
  const retry = () => {
    const last = lastSignIn.current;
    if (last) start(last.harness, last.action, last.profile, undefined, undefined, last.way, inFlight !== null);
  };
  const switchWay = () => {
    const last = lastSignIn.current;
    if (!last) return;
    start(last.harness, last.action, last.profile, undefined, undefined,
      last.way === 'device-code' ? 'browser' : 'device-code', inFlight !== null);
  };
  const otherWay: SignInWay | null = signedWay?.deviceCode ? (signedWay.way === 'device-code' ? 'browser' : 'device-code') : null;
  const dismiss = () => {
    setRefusal(null);
    setFailure(null);
    setRunning(null);
    runningRef.current = null;
    setRunningProfile(null);
    setSigningInNew(null);
  };

  // Already started, so nothing is sent: only the end is waited for, as a process started here waits.
  const follow: HarnessRun['follow'] = (harness, action) => {
    if (inFlight !== null) return;
    setPressedAt(Date.now());
    setRunning(`${harness}:${action}`);
    runningRef.current = `${harness}:${action}`;
    setInFlight(`${harness}:${action}`);
  };

  return {
    running, inFlight, runningProfile, signingInNew, busy: act.isPending || inFlight !== null, run, follow,
    refusal, retry, dismiss, pressedAt, failure, otherWay, switchWay,
    added, settleAdded: () => setAdded(null),
  };
}

/**
 * A tool's running action, held above every view (SIGNIN1). It was the Agents domain's own state, and
 * its end was heard only while that domain was on screen: leaving mid-sign-in lost the code panel and
 * the sentence saying how it ended. Held here, the sign-in outlives the page it started on — coming
 * back finds its panel where it was, and its end is said wherever the person is.
 */
export function HarnessRuns({ notify, children }: { notify: Notify; children: ReactNode }) {
  const state = useRunState(notify);
  return <Runs.Provider value={state}>{children}</Runs.Provider>;
}

/**
 * The application's running action where it holds one, else one of its own for what is inside — a
 * surface rendered alone (a test, a story) still works. Never two: two would each say the end.
 */
export function WithHarnessRuns({ notify, children }: { notify: Notify; children: ReactNode }) {
  return useContext(Runs) ? children : <HarnessRuns notify={notify}>{children}</HarnessRuns>;
}

/** The running action held above (`HarnessRuns`, or `WithHarnessRuns` around the one surface that asks). */
export function useHarnessRun(): HarnessRun {
  const held = useContext(Runs);
  if (!held) throw new Error('useHarnessRun needs HarnessRuns above it.');
  return held;
}

/**
 * The running action held above, or null where nothing holds one — Ask Daoris's conversation drawn alone
 * (a test, Quick Ask's box) still applies a card; it only has no Agents screen to show the console on.
 */
export function useHeldHarnessRun(): HarnessRun | null {
  return useContext(Runs);
}
