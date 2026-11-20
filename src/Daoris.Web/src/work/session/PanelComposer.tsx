import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

/**
 * When the agent reads the person's words: now (between turns), after the current step (while it works), when the session goes on
 * (parked or held), or never again in this session (finished: the words go on with the work).
 */
export type Reads = 'now' | 'next' | 'back' | 'finished';

const READS_LINE: Readonly<Record<Reads, string>> = {
  now: 'work.session.composer.readsNow',
  next: 'work.session.composer.readsNext',
  back: 'work.session.composer.readsWhenBack',
  finished: 'work.session.composer.readsWhenBack',
};

/**
 * **The composer** at the panel's foot: the session's box (`children`: the shared `Composer`, or the box the attended session is
 * offered, `AttendedComposer`) over one line naming the agent and the model a default resolves to (`chip`) and saying when the
 * agent reads the words. The box keeps its own presses: files, `@`, what waits with its edits and levels, the stops, *Finish* and
 * the model's options.
 */
export function PanelComposer({ reads, chip, children }: { reads: Reads; chip: string; children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <div className="grid min-w-0 gap-1">
      {children}
      <p className="m-0 flex min-w-0 flex-wrap items-center gap-2">
        <span className="rounded-control border border-line px-1.5 py-0.5 text-meta text-ink-soft">{chip}</span>
        <span className="min-w-0 flex-1 truncate text-meta text-ink-faint">{t(READS_LINE[reads])}</span>
      </p>
    </div>
  );
}
