import { createContext, type ReactNode, useContext, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from './format';
import { type DefaultStanding, useHarnessAction, useHarnessEnded } from './shell';
import type { AccountPlace } from './tools';
import type { Notify } from './ui';

/** A process action a person started on a tool: an install, an update, a pin, or a sign-in. */
export type HarnessRunAction = 'install' | 'update' | 'login' | 'login-new' | 'pin' | 'unpin' | 'profile-remove' | 'profile-default';

/**
 * A new account a sign-in kept (UX7b, ACCT1): which tool, its id, who signed in, and where it runs, which is none for an
 * account just made. Held until the person answers the add flow's last step, its name and the lists it joins.
 */
export type AccountAdded = { harness: string; profile: string; account: string | null; places: AccountPlace[] };

/** Which action runs on which tool, and for whom, while it runs — and how to start one. */
export type HarnessRun = {
  /** The new account the last sign-in to another account kept, waiting on its name and lists; null once answered. */
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
  run: (harness: string, action: HarnessRunAction, profile?: string, version?: string, workspace?: string) => void;
  /**
   * Follow an action the machine already started for the person (HELP6: Ask Daoris's Apply of an update
   * or a pin): its console shows under the tool doing it, and its end is said, as for one started here.
   */
  follow: (harness: string, action: 'update' | 'pin') => void;
};

const Runs = createContext<HarnessRun | null>(null);

/**
 * What sessions run as where a default was edited, in the sentence `daoris agent profile default … [--clear]` prints
 * (LOOK2c, D50): the machine's account or its own home again, a workspace's own account, or a workspace naming none that
 * runs as the machine's default or in the agent's own home.
 */
export const defaultSaid = (
  t: ReturnType<typeof useTranslation>['t'], harness: string, standing: DefaultStanding,
): string => {
  const { workspace, account } = standing;
  if (!workspace) {
    return standing.from === 'own' || !account
      ? t('harness.default.machineOwn', { harness })
      : t('harness.default.machine', { harness, account });
  }
  if (standing.from === 'workspace' && account) return t('harness.default.workspace', { harness, workspace, account });
  return standing.from === 'machine' && account
    ? t('harness.default.workspaceMachine', { harness, workspace, account })
    : t('harness.default.workspaceOwn', { harness, workspace });
};

/** The state of a tool's running action, and the one listener for its end. */
function useRunState(notify: Notify): HarnessRun {
  const { t } = useTranslation();
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

  /**
   * What an action's end means to the person — said once, whether it ended inside the request (a file
   * edit) or later as news (a process). The harness's own exit code decides which it was: Daoris ran
   * somebody else's tool and reports what it did. A sign-in's sentence names the account, because
   * that is what the person came for.
   */
  const ended = (
    action: string, profile: string | undefined, exitCode: number, problem: string | null,
    account?: string | null, kept?: boolean | null, places?: AccountPlace[] | null,
  ) => {
    setRunningProfile(null);
    setSigningInNew(null);
    if (problem) {
      notify(problem, 'error');
      return;
    }
    // Another account (D66 §3): kept only when the sign-in finished, and named by who signed in. One no list holds runs no
    // start, which is how the install's work kept waiting on another account (ACCT1), so the sentence says so and the
    // agent's page asks where it runs (UX7b).
    if (action === 'login-new') {
      if (exitCode !== 0) notify(t('harness.loginNew.failed', { code: exitCode }), 'error');
      else if (!kept) notify(t('harness.loginNew.nobody'), 'error');
      else {
        const nowhere = Array.isArray(places) && places.length === 0;
        if (account) notify(t(nowhere ? 'harness.loginNew.doneNowhere' : 'harness.loginNew.done', { account }));
        else notify(t(nowhere ? 'harness.loginNew.unnamedNowhere' : 'harness.loginNew.unnamed', { profile }));
        const harness = runningRef.current?.split(':')[0];
        if (harness && profile) setAdded({ harness, profile, account: account ?? null, places: places ?? [] });
      }
      return;
    }
    if (action === 'login') {
      if (exitCode === 0) notify(t('harness.login.done', { profile: account ?? profile }));
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
    setInFlight(null);
    ended(news.action, news.profile ?? undefined, news.exitCode, news.problem, news.account, news.kept, news.places);
  });

  const run: HarnessRun['run'] = (harness, action, profile, version, workspace) => {
    // Never over one still running: whose end the news belongs to is the one thing this must not lose.
    if (inFlight !== null) return;
    setRunning(`${harness}:${action}`);
    runningRef.current = `${harness}:${action}`;
    setRunningProfile(action === 'login' ? profile ?? null : null);
    setSigningInNew(action === 'login-new' ? harness : null);
    // Another new account starts over: the last one's question was set aside.
    if (action === 'login-new') setAdded(null);
    act.mutate({ harness, action, profile, version, workspace }, {
      // A file edit ends inside the request; a process answers `started` and ends as news, heard
      // above. Either way the end is said once, by the same sentence.
      onSuccess: (result) => {
        if (result.started) {
          setInFlight(`${harness}:${action}`);
          return;
        }
        // A default's edit says what sessions there run as now (LOOK2c), as the terminal's verb prints it: a workspace
        // cleared on the tool's own row runs as the machine's default where one is set, not in the tool's own home.
        if (action === 'profile-default' && result.default && (result.exitCode ?? 0) === 0) {
          setRunningProfile(null);
          setSigningInNew(null);
          notify(defaultSaid(t, harness, result.default));
          return;
        }
        ended(action, profile, result.exitCode ?? 0, null);
      },
      onError: (error: unknown) => {
        setRunningProfile(null);
        setSigningInNew(null);
        notify(sentence(error), 'error');
      },
    });
  };

  // Already started, so nothing is sent: only the end is waited for, as a process started here waits.
  const follow: HarnessRun['follow'] = (harness, action) => {
    if (inFlight !== null) return;
    setRunning(`${harness}:${action}`);
    runningRef.current = `${harness}:${action}`;
    setInFlight(`${harness}:${action}`);
  };

  return {
    running, inFlight, runningProfile, signingInNew, busy: act.isPending || inFlight !== null, run, follow,
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
