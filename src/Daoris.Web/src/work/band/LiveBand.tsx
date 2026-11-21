import { type ReactNode, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon, type IconName } from '../../design/ui';
import { span } from '../../locales/format';
import { useNow } from '../useNow';
import type { Beside, SessionEvent } from '../conversation/conversationTypes';
import { type BesideFocus, type BesideReader, pathTo, ranFor } from '../beside/besides';
import { actsIn, type BandAct, quietFor, runningIn } from './bandRows';
import type { AtWorkExchange } from '../atWork/types';
import { aliveIn, coworkerName } from '../exchanges/rows';

const NONE: readonly AtWorkExchange[] = [];

/**
 * The live band above the dock: one line for each thing running now, the agent's act first, then each subagent and background task
 * whose start the record keeps and no end, a subagent another started drawn under its parent and indented one past it. Each line says its kind and name, its time ticking from
 * its start on the page's clock, its last line of output or last act, and *no output 6 m* past the quiet threshold; *Follow* pins its
 * last three lines under it, *Open* puts it in the panel's focus, *Stop* stands where its door can stop it, asked once in place, and
 * reads *stopping* until its end is read; elsewhere its tip says why. Empty, it says *Nothing running*; in a short window it folds to
 * its count, a press away from its lines.
 *
 * A molecule: what runs, the readers of each one's own record and console, and the presses out. The organism above holds the bridge.
 */
export function LiveBand({ lifetimes, act, reader, stoppable, onStop, onOpen, short = false, exchanges = NONE, onOpenSession, nameOf }: {
  nameOf?: (agent: string) => string;
  /** The record's starts and ends of what the session runs beside itself. */
  lifetimes: readonly SessionEvent[];
  /** The line's requests: each it waits on that a session took up is a line, a helper or a reviewer at work for it, opened by its session. */
  exchanges?: readonly AtWorkExchange[];
  onOpenSession?: (session: string) => void;
  /** The agent's act while a turn runs; null between turns. */
  act: BandAct | null;
  reader?: BesideReader;
  /** The tasks its door can stop now, by id: a protocol-door task its harness marks `canStop`. */
  stoppable: ReadonlySet<string>;
  /** Stop a task, by its id and name: false where it did not stop, which takes its *stopping* back. */
  onStop?: (task: string, name: string) => Promise<boolean> | void;
  onOpen?: (focus: BesideFocus) => void;
  short?: boolean;
}) {
  const { t } = useTranslation();
  const now = useNow(1000);
  const [unfolded, setUnfolded] = useState(false);
  const running = useMemo(() => runningIn(lifetimes), [lifetimes]);
  const working = useMemo(() => aliveIn(exchanges), [exchanges]);
  const count = running.length + working.length + (act ? 1 : 0);
  const said = count === 0 ? t('work.band.nothing') : t('work.band.running', { count });
  const shown = count > 0 && (!short || unfolded);
  return (
    <section aria-label={said} data-band className="mb-2 grid min-w-0 gap-0.5 border-b border-line pb-1.5 text-small">
      {short && count > 0
        ? (
          <button
            type="button"
            aria-expanded={unfolded}
            onClick={() => setUnfolded(!unfolded)}
            className="flex items-center gap-1.5 justify-self-start border-0 bg-transparent p-0 text-caption text-ink-soft hover:text-ink"
          >
            <Icon name={unfolded ? 'chevronDown' : 'chevronRight'} size={12} />
            {said}
          </button>
        )
        : <p className="m-0 text-caption text-ink-faint">{said}</p>}
      {shown && act && (
        <Line id="act" icon="atWork" kind={t('work.band.agent')} name={act.title ?? t('work.band.thinking')}
          ran={act.since ? now - Date.parse(act.since) : null} />
      )}
      {shown && running.map((beside) => (beside.kind === 'child'
        ? <ChildLine key={`child:${beside.id}`} beside={beside} lifetimes={lifetimes} reader={reader} now={now} onOpen={onOpen} />
        : <TaskLine key={`task:${beside.id}`} beside={beside} reader={reader} now={now} onOpen={onOpen}
            onStop={stoppable.has(beside.id) ? onStop : undefined} />))}
      {shown && working.map((exchange) => (
        <Line
          key={exchange.id} id={exchange.id} icon="atWork" kind={coworkerName(t, exchange, nameOf)} name=""
          ran={exchange.at ? now - Date.parse(exchange.at) : null}
          onOpen={onOpenSession && exchange.session ? () => onOpenSession(exchange.session!) : undefined}
        />
      ))}
    </section>
  );
}

const PRESS = 'shrink-0 border-0 bg-transparent p-0 text-caption text-accent-ink underline decoration-dotted underline-offset-2 hover:text-ink';

/** A subagent's line: its last act from its own record, quiet from that record's last moment, opened by the way to it. */
function ChildLine({ beside, lifetimes, reader, now, onOpen }: {
  beside: Beside; lifetimes: readonly SessionEvent[]; reader?: BesideReader; now: number; onOpen?: (focus: BesideFocus) => void;
}) {
  const { t } = useTranslation();
  const line = (events: readonly SessionEvent[] | null) => {
    const acts = events ? actsIn(events) : [];
    const last = events?.[events.length - 1]?.at ?? beside.started;
    return (
      <Line
        id={beside.id} icon="atWork" kind={t('work.beside.child')} name={beside.name ?? beside.task ?? beside.id} depth={beside.depth}
        ran={ranFor(beside, now)} last={acts[acts.length - 1]} tail={acts.slice(-3)}
        quiet={quietFor(last ? Date.parse(last) : null, now)} why={t('work.band.why.child')}
        onOpen={onOpen && (() => onOpen({ child: pathTo(lifetimes, beside.id) }))}
      />
    );
  };
  const Child = reader?.Child;
  return Child ? <Child child={beside.id} running>{(read) => line(read.events)}</Child> : line(null);
}

/** A background task's line: its console's last line, quiet from when the page heard it, stopped where its door can. */
function TaskLine({ beside, reader, now, onOpen, onStop }: {
  beside: Beside; reader?: BesideReader; now: number; onOpen?: (focus: BesideFocus) => void;
  onStop?: (task: string, name: string) => Promise<boolean> | void;
}) {
  const { t } = useTranslation();
  const name = beside.name ?? beside.id;
  const line = (lines: readonly string[], heard: number | null) => (
    <Line
      id={beside.id} icon="execute" kind={t('work.beside.task')} name={name} ran={ranFor(beside, now)}
      last={lines[lines.length - 1]} tail={lines.slice(-3)}
      quiet={quietFor(heard ?? (beside.started ? Date.parse(beside.started) : null), now)}
      why={onStop ? undefined : t('work.band.why.task')}
      onOpen={onOpen && (() => onOpen({ task: beside.id }))}
      onStop={onStop && (() => onStop(beside.id, name))}
    />
  );
  const Tail = reader?.Tail;
  return Tail ? <Tail task={beside.id}>{line}</Tail> : line([], null);
}

/** One line of the band, its presses after its words, its tail under it while followed, its stop asked in place. */
function Line({ id, icon, kind, name, depth = 1, ran, last, tail, quiet = null, why, onOpen, onStop }: {
  id: string; icon: IconName; kind: string; name: string; depth?: number; ran: number | null; last?: string;
  tail?: readonly string[]; quiet?: number | null; why?: string; onOpen?: () => void;
  onStop?: () => Promise<boolean> | void;
}) {
  const { t } = useTranslation();
  const [following, setFollowing] = useState(false);
  const [asking, setAsking] = useState(false);
  const [stopping, setStopping] = useState(false);
  const stop = () => {
    setAsking(false);
    setStopping(true);
    void Promise.resolve(onStop?.()).then((stopped) => { if (stopped === false) setStopping(false); });
  };
  const words: ReactNode[] = [kind, name, ran !== null && ran >= 0 && span(ran)].filter(Boolean);
  return (
    <div data-band-row={id} className="grid min-w-0 gap-0.5" style={depth > 1 ? { marginLeft: `${(depth - 1) * 16}px` } : undefined}>
      <p className="m-0 flex min-w-0 items-center gap-x-2 text-ink-soft">
        <Icon name={icon} size={14} className="shrink-0 text-ink-faint" />
        <span className="shrink-0">{words.join(' · ')}</span>
        {last && <span className="min-w-0 truncate text-ink-faint">“{last}”</span>}
        {quiet !== null && <span className="shrink-0 text-caption text-warn">{t('work.band.quiet', { span: span(quiet) })}</span>}
        {stopping && <span className="shrink-0 text-caption text-ink-faint">{t('work.band.stopping')}</span>}
        <span className="ml-auto flex shrink-0 items-center gap-2">
          {tail && tail.length > 0 && (
            <button type="button" aria-pressed={following} onClick={() => setFollowing(!following)} className={PRESS}>
              {t('work.band.follow')}
            </button>
          )}
          {onOpen && <button type="button" onClick={onOpen} className={PRESS}>{t('work.band.open')}</button>}
          {onStop && !stopping && !asking && (
            <button type="button" aria-label={t('work.band.stop', { name })} onClick={() => setAsking(true)} className={PRESS}>
              {t('work.band.stopShort')}
            </button>
          )}
          {why && <span data-why title={why} aria-label={why} className="text-ink-faint"><Icon name="info" size={12} /></span>}
        </span>
      </p>
      {asking && (
        <p className="m-0 ml-5 flex flex-wrap items-center gap-x-2 text-caption text-ink-soft">
          <span>{t('work.band.ask', { name })}</span>
          <button type="button" onClick={stop} className={PRESS}>{t('work.band.stopIt')}</button>
          <button type="button" onClick={() => setAsking(false)} className={PRESS}>{t('work.band.keep')}</button>
        </p>
      )}
      {following && tail && (
        <pre className="m-0 ml-5 max-h-16 overflow-hidden whitespace-pre-wrap font-mono text-caption text-ink-faint">{tail.join('\n')}</pre>
      )}
    </div>
  );
}
