import { type ReactNode, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { phaseTime } from '../locales/format';
import { DotMark, Icon, Inline } from './ui';

/**
 * A step that waits says its phase and how long (R2, the UI/UX design §2; SIGNIN1 built it for the sign-in, §3.2). A process
 * whose answer is a start and whose end comes later is drawn as its phases: the done ones with the time each took, the one
 * now with its time running, the ones to come by name. A process that looks the same at five seconds and at five minutes
 * cannot be told from a hung one.
 */

/** Where a phase stands: done, worked now, waiting on the person, failed, or still to come. */
export type PhaseState = 'done' | 'now' | 'waits' | 'failed' | 'to-come';

/** One phase as its holder words it: the driver's step, in the person's language. */
export type PhaseRow = {
  key: string;
  /** What the phase is doing, in words. */
  label: string;
  state: PhaseState;
  /** When it began and, done or failed, ended, in ms since the epoch; none for one to come. */
  from?: number | null;
  to?: number | null;
  /** What it says while it is the phase now: a link, a code, a box. */
  body?: ReactNode;
  /** A failed phase's sentence, whole. */
  sentence?: string | null;
};

const STATE_WORD: Record<PhaseState, string> = {
  done: 'phase.state.done',
  now: 'phase.state.now',
  waits: 'phase.state.waits',
  failed: 'phase.state.failed',
  'to-come': 'phase.state.toCome',
};

/**
 * Now, read again at each step of a time that runs from `from`: each second in its first minute and each minute after, at
 * the step's own boundary. Nothing at rest moves (§3, FREEZE1): a stopped time keeps no timer.
 */
function useStep(from: number, running: boolean): number {
  return useClock(running ? from : null, (gone) => {
    const unit = gone < 60_000 ? 1_000 : 60_000;
    return unit - (gone % unit);
  });
}

/** Now, read again when `wait` says the time shown next changes, measured from `from`; null for a time at rest. */
function useClock(from: number | null, wait: (gone: number) => number | null): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (from === null) return undefined;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const step = () => {
      const current = Date.now();
      setNow(current);
      const next = wait(Math.max(0, current - from));
      if (next !== null) timer = setTimeout(step, next);
    };
    step();
    return () => clearTimeout(timer);
    // `wait` is a pure rule of the caller's kind, not a value that changes what is measured.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [from]);
  return now;
}

/**
 * How long something has run: *{time} so far* while it runs, stepping by its own clock, and the time it took once `to` says
 * it ended, which stands still.
 */
export function Elapsed({ from, to, className }: { from: number; to?: number | null; className?: string }) {
  const { t } = useTranslation();
  const now = useStep(from, to == null);
  return to == null
    ? <span className={className}>{t('phase.elapsed', { time: phaseTime(now - from) })}</span>
    : <span className={className}>{phaseTime(to - from)}</span>;
}

/**
 * What is left of a bound a tool said (R2: *a bound is said when one is known, never invented*), worded by `say`, stepping
 * as `Elapsed` does: Codex's *the code expires in 15 minutes*.
 */
export function Remaining({ until, say, className }: { until: number; say: (time: string) => string; className?: string }) {
  // Measured from a moment that never moves, so the wait is to when what is left next shows a smaller number.
  const now = useClock(0, (sinceEpoch) => {
    const left = until - sinceEpoch;
    if (left <= 0) return null;
    const unit = left <= 60_000 ? 1_000 : 60_000;
    return (left % unit) + 1;
  });
  return <span className={className}>{say(phaseTime(Math.max(0, until - now)))}</span>;
}

/** A phase's mark, never hue alone: its state is said in words beside it. */
function PhaseMark({ state }: { state: PhaseState }) {
  return (
    <span aria-hidden className="inline-flex size-4 shrink-0 items-center justify-center">
      {state === 'done' && <Icon name="check" size={12} className="text-ink-done" />}
      {state === 'failed' && <Icon name="x" size={12} className="text-ink-danger" />}
      {state === 'now' && <DotMark tone="live" />}
      {state === 'waits' && <DotMark tone="parked" />}
      {state === 'to-come' && <span className="inline-block size-1.5 rounded-full border border-ink-faint" />}
    </span>
  );
}

const LABEL_INK: Record<PhaseState, string> = {
  done: 'text-ink-soft',
  now: 'text-ink',
  // A phase waiting on the person wears open's hue, theirs alone (R2).
  waits: 'text-ink-open',
  failed: 'text-ink-danger',
  'to-come': 'text-ink-faint',
};

/** One phase's line: its mark, its words, its state in words for a reader, and its time. */
function PhaseLine({ phase, whole }: { phase: PhaseRow; whole?: number | null }) {
  const { t } = useTranslation();
  const running = phase.state === 'now' || phase.state === 'waits';
  return (
    <div className="flex min-w-0 items-center gap-2">
      <PhaseMark state={phase.state} />
      <span className={cn('min-w-0 text-body', LABEL_INK[phase.state], running && 'font-semibold')}>{phase.label}</span>
      <span className="sr-only">{`, ${t(STATE_WORD[phase.state])}`}</span>
      <span className="ml-auto flex shrink-0 items-center gap-2 text-meta text-ink-faint">
        {phase.from != null && phase.state !== 'to-come' && (phase.to != null || running) && (
          <Elapsed from={phase.from} to={running ? null : phase.to} />
        )}
        {whole != null && <Elapsed from={whole} to={running ? null : phase.to ?? null} />}
      </span>
    </div>
  );
}

/** What the phase now says under its line, and a failed one's sentence, whole. */
function PhaseUnder({ phase }: { phase: PhaseRow }) {
  if (phase.state === 'failed' && phase.sentence) {
    return <p role="alert" className="m-0 mt-1 pl-6 text-small text-ink-danger [overflow-wrap:anywhere]"><Inline text={phase.sentence} /></p>;
  }
  if ((phase.state === 'now' || phase.state === 'waits') && phase.body) return <div className="mt-1 min-w-0 pl-6">{phase.body}</div>;
  return null;
}

/**
 * A process's phases (R2's `PhaseList`): the list, each phase a list item named by its words and its state in words, the
 * done ones with their time, the one now with its time and what it says, a failed one with its sentence; or, `shown` as a
 * line, the phase now alone with its time, the whole's and what it says, for a row or a head with no room for a list.
 *
 * @remarks
 * The phase now is told as a status once, by its words and its state, never its ticking time, so a reader hears where it is
 * when it changes and not each second.
 */
export function PhaseList({ phases, label, shown = 'list' }: { phases: PhaseRow[]; label: string; shown?: 'list' | 'line' }) {
  const { t } = useTranslation();
  const current = phases.find((phase) => phase.state === 'failed')
    ?? phases.find((phase) => phase.state === 'now' || phase.state === 'waits')
    ?? null;
  const status = current ? <p role="status" className="sr-only">{`${current.label}, ${t(STATE_WORD[current.state])}`}</p> : null;

  if (shown === 'line') {
    if (!current) return null;
    const whole = phases.find((phase) => phase.from != null)?.from ?? null;
    return (
      <div aria-label={label} className="min-w-0">
        {status}
        <PhaseLine phase={current} whole={whole} />
        <PhaseUnder phase={current} />
      </div>
    );
  }

  return (
    <>
      {status}
      <ol aria-label={label} className="m-0 grid list-none gap-1.5 p-0">
        {phases.map((phase) => (
          <li key={phase.key} className="min-w-0">
            <PhaseLine phase={phase} />
            <PhaseUnder phase={phase} />
          </li>
        ))}
      </ol>
    </>
  );
}
