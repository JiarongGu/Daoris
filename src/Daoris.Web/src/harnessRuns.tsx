import { createContext, type ReactNode, useContext, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from './format';
import { useHarnessAction, useHarnessEnded } from './shell';
import type { Notify } from './ui';

/** A process action a person started on a tool: an install, an update, a pin, or a sign-in. */
export type HarnessRunAction = 'install' | 'update' | 'login' | 'login-new' | 'pin' | 'unpin' | 'profile-remove' | 'profile-default';

/** Which action runs on which tool, and for whom, while it runs — and how to start one. */
export type HarnessRun = {
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
};

const Runs = createContext<HarnessRun | null>(null);

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
    account?: string | null, kept?: boolean | null,
  ) => {
    setRunningProfile(null);
    setSigningInNew(null);
    if (problem) {
      notify(problem, 'error');
      return;
    }
    // Another account (D66 §3): kept only when the sign-in finished, and named by who signed in.
    if (action === 'login-new') {
      if (exitCode !== 0) notify(t('harness.loginNew.failed', { code: exitCode }), 'error');
      else if (!kept) notify(t('harness.loginNew.nobody'), 'error');
      else if (account) notify(t('harness.loginNew.done', { account }));
      else notify(t('harness.loginNew.unnamed', { profile }));
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
    ended(news.action, news.profile ?? undefined, news.exitCode, news.problem, news.account, news.kept);
  });

  const run: HarnessRun['run'] = (harness, action, profile, version, workspace) => {
    // Never over one still running: whose end the news belongs to is the one thing this must not lose.
    if (inFlight !== null) return;
    setRunning(`${harness}:${action}`);
    runningRef.current = `${harness}:${action}`;
    setRunningProfile(action === 'login' ? profile ?? null : null);
    setSigningInNew(action === 'login-new' ? harness : null);
    act.mutate({ harness, action, profile, version, workspace }, {
      // A file edit ends inside the request; a process answers `started` and ends as news, heard
      // above. Either way the end is said once, by the same sentence.
      onSuccess: (result) => {
        if (result.started) {
          setInFlight(`${harness}:${action}`);
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

  return { running, inFlight, runningProfile, signingInNew, busy: act.isPending || inFlight !== null, run };
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
