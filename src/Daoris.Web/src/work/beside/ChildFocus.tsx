import { Fragment, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { span } from '../../locales/format';
import { useNow } from '../useNow';
import type { Beside } from '../conversation/conversationTypes';
import { type BesideHome, BesideHomes } from './BesideRows';
import { callsIn, type ChildRead, lifetimeOf, ranFor, standing } from './besides';
import { childTurns } from './childTurns';

/**
 * A subagent in the panel's focus: a breadcrumb from the session through the subagents that started it, each a press back, and its
 * own timeline in the session's place, its own subagents opening in place under their calls as the session's do. Never a tab, a
 * drawer or another page.
 */
export function ChildFocus({ path, home, onBack }: {
  /** The way from the session to the subagent in focus, the session's own child first. */
  path: readonly string[];
  home: BesideHome;
  /** Go back to a step of the way: 0 is the session itself. */
  onBack: (to: number) => void;
}) {
  const { t } = useTranslation();
  const id = path[path.length - 1]!;
  const beside = lifetimeOf(home.lifetimes, id) ?? { kind: 'child' as const, id };
  const Child = home.reader?.Child;
  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-3">
      <Crumbs steps={path.map((step) => lifetimeOf(home.lifetimes, step)?.name ?? step)} onBack={onBack} />
      {Child
        ? <Child child={id} running={!beside.end}>{(read) => <Focused beside={beside} read={read} home={home} />}</Child>
        : <p className="m-0 text-small text-ink-faint">{t('work.beside.unkept')}</p>}
    </div>
  );
}

/** A background task in the panel's focus: a breadcrumb back to the session, and its whole console in the session's place. */
export function TaskFocus({ task, home, onBack }: { task: string; home: BesideHome; onBack: () => void }) {
  const { t } = useTranslation();
  const name = home.lifetimes.find((event) => event.kind === 'task' && event.id === task && event.name)?.name ?? task;
  const Stream = home.reader?.Stream;
  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-3">
      <Crumbs steps={[name]} onBack={onBack} />
      {Stream ? <Stream task={task} /> : <p className="m-0 text-small text-ink-faint">{t('work.beside.unkept')}</p>}
    </div>
  );
}

function Crumbs({ steps, onBack }: { steps: readonly string[]; onBack: (to: number) => void }) {
  const { t } = useTranslation();
  const crumb = 'border-0 bg-transparent p-0 text-small text-ink-soft underline decoration-dotted underline-offset-2 hover:text-ink';
  return (
    <nav aria-label={t('work.beside.where')} className="flex min-w-0 flex-wrap items-baseline gap-x-1.5 border-b border-line pb-1.5">
      <span aria-hidden className="text-ink-faint">‹</span>
      <button type="button" className={crumb} onClick={() => onBack(0)}>{t('work.beside.session')}</button>
      {steps.map((step, index) => (
        <Fragment key={`${index}:${step}`}>
          <span aria-hidden className="text-ink-faint">›</span>
          <button
            type="button"
            className={crumb}
            aria-current={index === steps.length - 1 ? 'location' : undefined}
            onClick={() => onBack(index + 1)}
          >
            {step}
          </button>
        </Fragment>
      ))}
    </nav>
  );
}

function Focused({ beside, read, home }: { beside: Beside; read: ChildRead; home: BesideHome }) {
  const { t } = useTranslation();
  const now = useNow(beside.end ? null : 10_000);
  const ran = ranFor(beside, now);
  const turns = useMemo(
    () => (read.events ? childTurns(beside.id, read.events, home.lifetimes, !beside.end) : null),
    [beside.id, beside.end, read.events, home.lifetimes]);
  const facts = [
    t('work.beside.child'), t(standing(beside)), ran !== null && span(ran), read.events && t('work.beside.calls', { count: callsIn(read.events) }),
  ].filter(Boolean).join(' · ');
  return (
    <>
      <p className="m-0 text-meta text-ink-faint" title={beside.task ?? undefined}>{facts}</p>
      {!read.loaded
        ? <p className="m-0 text-small text-ink-faint">{t('work.beside.reading')}</p>
        : turns === null
          ? <p className="m-0 text-small text-ink-faint">{t('work.beside.unkept')}</p>
          : <BesideHomes.Provider value={{ ...home, nested: false }}>{home.draw(turns)}</BesideHomes.Provider>}
    </>
  );
}
