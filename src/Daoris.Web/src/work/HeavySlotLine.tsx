import { Fragment } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { elapsed } from '../locales/format';
import { Dot } from '../design/ui';
import { PhaseList, type PhaseRow, type PhaseState } from '../design/phases';
import type { DetailRow } from './head/DetailsFold';
import { type HeavyHolder, type HeavyPart, heavyPartOf, type HeavySlot } from './heavySlot';
import { useNow } from './useNow';

/**
 * A heavy run seen running and waiting (HEAVYSLOT1, the UI/UX design §3.5): who holds a repository's heavy-run slot, at which gate,
 * and who waits behind it, as the driver read the records its runs keep beside the slot (`HEAVY_SLOT`), never a run's output. A
 * holder or a waiter with no session is a run from a terminal, said with no door.
 *
 * @remarks
 * **A molecule**: every state is reached by its props. A wait is neutral, since it waits on another session and not on the person,
 * so it is never a row of *What needs you*; the gate running wears the live mark (R2).
 */

/** The line in words, for the rail's line and the head's facts: `titleOf` names a session by its title. */
export function heavyLine(t: TFunction, part: HeavyPart, titleOf: (session: string) => string): string {
  switch (part.kind) {
    case 'running':
      return t('work.heavy.running', { n: part.n, total: part.total, gate: part.gate, time: elapsed(part.since) });
    case 'failed':
      return t('work.heavy.failed', { gate: part.gate });
    case 'waiting':
      return t('work.heavy.waiting', {
        session: part.behind ? titleOf(part.behind) : t('projects.heavy.terminal'),
        time: elapsed(part.since),
      });
  }
}

/** A session named as a door where it has one and something to open it, else its words alone. */
function SessionDoor({ session, titleOf, onOpen }: {
  session: string | null | undefined; titleOf: (session: string) => string; onOpen?: (session: string) => void;
}) {
  const { t } = useTranslation();
  if (!session) return <span>{t('projects.heavy.terminal')}</span>;
  const title = titleOf(session);
  if (!onOpen) return <span>{title}</span>;
  return (
    <button
      type="button"
      onClick={() => onOpen(session)}
      title={title}
      className="max-w-full truncate align-bottom text-accent underline-offset-2 hover:underline focus-visible:underline"
    >
      {title}
    </button>
  );
}

/**
 * A session's line in its repository's slot, with its mark: the gate running wears the live mark, a failed gate the failed one, and
 * a wait the neutral one, its *behind* a door to the holder's session where there is one.
 */
export function HeavySlotLine({ part, titleOf, onOpen }: {
  part: HeavyPart;
  titleOf: (session: string) => string;
  onOpen?: (session: string) => void;
}) {
  const { t } = useTranslation();
  useNow(part.kind === 'failed' ? null : 15_000);
  if (part.kind !== 'waiting') {
    return <Dot tone={part.kind === 'running' ? 'live' : 'failed'} label={heavyLine(t, part, titleOf)} className="whitespace-normal" />;
  }
  // The sentence with its session in place, in either language: split where the session goes.
  const [before, after] = t('work.heavy.waiting', { session: GAP, time: elapsed(part.since) }).split(GAP);
  return (
    <span title={t('work.heavy.waitingTip')} className="inline-flex flex-wrap items-baseline gap-x-1.5 text-meta text-ink-soft">
      <Dot tone="waits" label={before.trimEnd()} className="whitespace-normal" />
      <SessionDoor session={part.behind} titleOf={titleOf} onOpen={onOpen} />
      {after && <span>{after.trimStart()}</span>}
    </span>
  );
}

const GAP = '⁣';

/**
 * The holder's gates as R2's phases: those before the gate running done, the gate running now from its start (or failed with its
 * exit), the rest to come by name. The record keeps no time for a gate done, so none is said.
 */
export function heavyPhases(t: TFunction, holder: HeavyHolder): PhaseRow[] {
  const at = holder.now?.at ?? 0;
  return holder.gates.map((gate, index) => {
    const failed = holder.failed && index === at ? holder.failed : null;
    const state: PhaseState = index < at ? 'done' : index > at ? 'to-come' : failed ? 'failed' : 'now';
    return {
      key: `${index}-${gate}`,
      label: gate,
      state,
      from: index === at ? Date.parse(holder.now?.since ?? holder.since) : null,
      to: null,
      sentence: failed ? t('work.heavy.exit', { gate: failed.gate, exit: failed.exit }) : null,
    };
  });
}

/**
 * The session's *Details* row, *Heavy gates* (§3.5: the head's line opens to the phase list here): the holder's gates as phases, or
 * its wait with the holder a door; no row where the session has no part in its repository's slot.
 */
export function heavyRow(
  t: TFunction, slot: HeavySlot | null | undefined, session: string, titleOf: (session: string) => string, onOpen?: (session: string) => void,
): DetailRow | null {
  const part = heavyPartOf(slot, session);
  if (!part || !slot) return null;
  const value = part.kind === 'waiting' || !slot.holder
    ? <HeavySlotLine part={part} titleOf={titleOf} onOpen={onOpen} />
    : <PhaseList phases={heavyPhases(t, slot.holder)} label={t('work.heavy.label')} />;
  return { label: t('work.heavy.label'), value };
}

/**
 * A repository's *Heavy run* row on its Details (§3.5): who holds the slot and at which gate, for how long, and how many wait, each a
 * door to its session; *No heavy run now* otherwise. Its tip names the terminal's twin, `run-heavy --look`.
 */
export function HeavySlotRow({ slot, titleOf, onOpen }: {
  slot: HeavySlot;
  titleOf: (session: string) => string;
  onOpen?: (session: string) => void;
}) {
  const { t } = useTranslation();
  const { holder, waiters } = slot;
  useNow(holder || waiters.length > 0 ? 15_000 : null);
  if (!holder && waiters.length === 0) {
    return <span className="text-small text-ink-soft">{slot.refusal ?? t('projects.heavy.none')}</span>;
  }
  const failed = holder?.failed ?? null;
  const n = (holder?.now?.at ?? 0) + 1;
  const gate = holder?.now?.gate ?? holder?.gates[0] ?? '';
  const [waitsBefore] = t('projects.heavy.waits', { count: waiters.length, sessions: GAP }).split(GAP);
  return (
    <span className="grid min-w-0 gap-1 text-small text-ink-soft">
      {holder && (
        <span className="flex min-w-0 flex-wrap items-baseline gap-x-1.5">
          <SessionDoor session={holder.session} titleOf={titleOf} onOpen={onOpen} />
          <Dot
            tone={failed ? 'failed' : 'live'}
            label={failed
              ? t('work.heavy.failed', { gate: failed.gate })
              : t('projects.heavy.at', { n, total: Math.max(holder.gates.length, 1), gate, time: elapsed(holder.now?.since ?? holder.since) })}
            className="whitespace-normal"
          />
        </span>
      )}
      {waiters.length > 0 && (
        <span className="flex min-w-0 flex-wrap items-baseline gap-x-1.5">
          <Dot tone="waits" label={waitsBefore.trimEnd()} />
          {waiters.map((waiter, at) => (
            <Fragment key={`${waiter.session ?? 'terminal'}-${waiter.since}`}>
              {at > 0 && <span aria-hidden>·</span>}
              <SessionDoor session={waiter.session} titleOf={titleOf} onOpen={onOpen} />
              <span className="text-ink-faint">{elapsed(waiter.since)}</span>
            </Fragment>
          ))}
        </span>
      )}
    </span>
  );
}
