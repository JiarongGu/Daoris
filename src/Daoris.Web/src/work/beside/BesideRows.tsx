import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { span } from '../../locales/format';
import { Icon } from '../../design/ui';
import { useNow } from '../useNow';
import type { Beside, Block, SessionEvent, Turn } from '../conversation/conversationTypes';
import { type BesideReader, callsIn, type ChildRead, closingIn, ranFor, standing } from './besides';
import { childTurns } from './childTurns';

/**
 * What the rows of a session's subagents and background tasks are drawn with: the reader of what each kept, the session's record of
 * their starts and ends, how a timeline's turns are drawn, and the jump to a block. Absent a reader, a row says what the session's
 * record holds and opens nothing.
 */
export type BesideHome = {
  reader?: BesideReader;
  lifetimes: readonly SessionEvent[];
  /** Drawn inside a subagent opened in place: a subagent here takes the panel's focus, never a second level in place. */
  nested: boolean;
  /** Put a subagent in the panel's focus. */
  onFocus: (child: string) => void;
  /** The conversation's own drawing of a timeline's turns. */
  draw: (turns: Turn[]) => ReactNode;
  /** Bring the panel to a block, by its key. */
  onShow?: (key: string) => void;
};

export const BesideHomes = createContext<BesideHome | null>(null);

const UNREAD: ChildRead = { events: null, loaded: false };

/** The row a call's subagent or task stands as under the call that started it. */
export function BesideStart({ beside }: { beside: Beside }) {
  return beside.kind === 'child' ? <ChildRow beside={beside} /> : <TaskStart beside={beside} />;
}

/**
 * A subagent under the call that spawned it: its name (its task words on hover), how it stands, its calls, how long it ran and,
 * ended, its closing words. Pressed, it opens in place to its own turns one level in; one drawn inside another opened so takes the
 * panel's focus instead, so a timeline is never indented twice.
 */
function ChildRow({ beside }: { beside: Beside }) {
  const home = useContext(BesideHomes);
  const Child = home?.reader?.Child;
  if (!home || !Child) return <ChildLine beside={beside} read={UNREAD} />;
  return <Child child={beside.id} running={!beside.end}>{(read) => <ChildLine beside={beside} read={read} home={home} />}</Child>;
}

function ChildLine({ beside, read, home }: { beside: Beside; read: ChildRead; home?: BesideHome }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const now = useNow(beside.end ? null : 10_000);
  const ran = ranFor(beside, now);
  const closing = beside.end && read.events ? closingIn(read.events) : undefined;
  const said = [
    t('work.beside.child'), beside.name ?? beside.task ?? beside.id, t(standing(beside)),
    read.events && t('work.beside.calls', { count: callsIn(read.events) }), ran !== null && span(ran),
  ].filter(Boolean).join(' · ');
  const opens = Boolean(home?.reader);
  return (
    <div data-beside-child={beside.id} className="ml-4 grid min-w-0 gap-0.5">
      <button
        type="button"
        disabled={!opens}
        aria-expanded={opens && !home?.nested ? open : undefined}
        title={beside.task ?? undefined}
        onClick={() => (home?.nested ? home.onFocus(beside.id) : setOpen(!open))}
        className="flex min-w-0 items-center gap-1.5 justify-self-start border-0 bg-transparent p-0 text-left text-small text-ink-soft hover:text-ink disabled:cursor-default"
      >
        <Icon name={open && !home?.nested ? 'chevronDown' : 'chevronRight'} size={13} className="shrink-0 text-ink-faint" />
        <Icon name="atWork" size={14} className="shrink-0 text-ink-faint" />
        <span className="min-w-0 truncate">{said}</span>
      </button>
      {closing && <p className="m-0 ml-5 truncate text-caption text-ink-faint">{closing}</p>}
      {open && home && !home.nested && <ChildInPlace beside={beside} read={read} home={home} />}
    </div>
  );
}

/** A subagent's own turns, drawn in place one level in under its row: what it said and did, its runs folded. */
function ChildInPlace({ beside, read, home }: { beside: Beside; read: ChildRead; home: BesideHome }) {
  const { t } = useTranslation();
  const turns = useMemo(
    () => (read.events ? childTurns(beside.id, read.events, home.lifetimes, !beside.end) : null),
    [beside.id, beside.end, read.events, home.lifetimes]);
  return (
    <div data-child={beside.id} data-depth={beside.depth ?? 1} className="ml-1.5 mt-1 grid min-w-0 grid-cols-[minmax(0,1fr)] gap-1.5 border-l border-line pl-3">
      {!read.loaded
        ? <p className="m-0 text-small text-ink-faint">{t('work.beside.reading')}</p>
        : turns === null
          ? <p className="m-0 text-small text-ink-faint">{t('work.beside.unkept')}</p>
          : <BesideHomes.Provider value={{ ...home, nested: true }}>{home.draw(turns)}</BesideHomes.Provider>}
    </div>
  );
}

/** A background task where it started, under its call: its name, how it stands, and once ended a press to where it ended. */
function TaskStart({ beside }: { beside: Beside }) {
  const { t } = useTranslation();
  const home = useContext(BesideHomes);
  return (
    <p data-beside-start={beside.id} className="m-0 ml-4 flex min-w-0 flex-wrap items-center gap-x-2 text-small text-ink-soft">
      <Icon name="execute" size={14} className="shrink-0 text-ink-faint" />
      <span className="min-w-0 truncate">{[t('work.beside.task'), beside.name, t(standing(beside))].filter(Boolean).join(' · ')}</span>
      {beside.endKey && home?.onShow && (
        <button type="button" onClick={() => home.onShow!(beside.endKey!)} className="border-0 bg-transparent p-0 text-caption text-accent-ink underline">
          {t('work.beside.toEnd')}
        </button>
      )}
    </p>
  );
}

/** A background task's row where it ended: its name, its end's word, how long it ran, its last lines, and a press back to its start. */
export function TaskEnd({ block }: { block: Block }) {
  const { t } = useTranslation();
  const home = useContext(BesideHomes);
  const now = useNow(null);
  const beside = block.beside;
  if (!beside) return null;
  const ran = ranFor(beside, now);
  const Tail = home?.reader?.Tail;
  return (
    <div data-beside-end={beside.id} className="grid min-w-0 gap-0.5">
      <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 text-small text-ink-soft">
        <Icon name="execute" size={14} className="shrink-0 text-ink-faint" />
        <span className="min-w-0 truncate">
          {[t('work.beside.task'), beside.name, t(standing(beside)), ran !== null && t('work.beside.ran', { span: span(ran) })].filter(Boolean).join(' · ')}
        </span>
        {beside.startKey && home?.onShow && (
          <button type="button" onClick={() => home.onShow!(beside.startKey!)} className="border-0 bg-transparent p-0 text-caption text-accent-ink underline">
            {t('work.beside.toStart')}
          </button>
        )}
      </p>
      {Tail && (
        <Tail task={beside.id}>
          {(lines) => (lines.length > 0
            ? <pre className="m-0 ml-5 max-h-20 overflow-hidden whitespace-pre-wrap font-mono text-caption text-ink-faint">{lines.slice(-3).join('\n')}</pre>
            : null)}
        </Tail>
      )}
    </div>
  );
}
