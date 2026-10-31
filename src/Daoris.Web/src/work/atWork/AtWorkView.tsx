import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { cn } from '../../lib/cn';
import { clockOf } from '../../locales/format';
import { Dot, SectionTitle } from '../../design/ui';
import type { AtWorkA, AtWorkChild, AtWorkHeld, AtWorkPart, AtWorkSubagents } from './types';
import { CHILD_STATE_KEY, childTone, LAST_SEEN, partStateKey } from './words';

export type AtWorkViewProps = {
  /** What the read holds; undefined before its first answer, which draws as reading. */
  held: AtWorkHeld | undefined;
  /** A query in error (the bridge refused): its words, drawn failed where nothing is held and stale over an answer. */
  error?: string | null;
  /** Open a part's page. */
  onOpen?: (session: string) => void;
  onRetry?: () => void;
  /** The clock a time is read against; the window's now by default. */
  now?: Date;
};

/**
 * Who is at work on a session (COWORKVIEW1a, the cowork view design §5.2): the session's parts as the strip lists them, the
 * attended one first, and its subagents, each with its state and its end, read from Daoris's records and never from the
 * conversation's text. Reading is never *nobody*, a failure says its words, and a stale answer keeps its rows dimmed.
 */
export function AtWorkView({ held, error, onOpen, onRetry, now }: AtWorkViewProps) {
  const { t } = useTranslation();
  if (!held || 'reading' in held) {
    if (!held && error) return <Failed why={error} onRetry={onRetry} />;
    return (
      <div aria-busy="true" className="grid gap-2 p-3">
        <p className="m-0 text-small text-ink-faint">{t('work.atWork.reading')}</p>
        <div className="h-4 w-2/3 rounded-control bg-line/60" />
        <div className="h-4 w-1/2 rounded-control bg-line/60" />
      </div>
    );
  }
  if ('failed' in held) return <Failed why={held.failed.why || (error ?? '')} onRetry={onRetry} />;

  const stale = held.stale?.why ?? error ?? null;
  const { answer } = held;
  return (
    <div className="grid min-w-0 gap-3 p-3">
      {stale !== null && (
        <p role="status" className="m-0 flex flex-wrap items-baseline gap-2 text-small text-ink-soft">
          {t('work.atWork.stale', { why: stale })}
          {onRetry && <button type="button" className="text-accent underline" onClick={onRetry}>{t('work.atWork.retry')}</button>}
        </p>
      )}
      <div className={cn('grid min-w-0 gap-3', stale !== null && 'opacity-60')}>
        <Parts answer={answer} onOpen={onOpen} />
        <Subagents answer={answer} quiet={stale !== null} now={now} />
      </div>
    </div>
  );
}

function Failed({ why, onRetry }: { why: string; onRetry?: () => void }) {
  const { t } = useTranslation();
  return (
    <p role="alert" className="m-0 flex flex-wrap items-baseline gap-2 p-3 text-small text-ink-soft">
      {t('work.atWork.failed', { why })}
      {onRetry && <button type="button" className="text-accent underline" onClick={onRetry}>{t('work.atWork.retry')}</button>}
    </p>
  );
}

/** The entry's parts, each one button opening its part: its agent, its record's own state and its subagents' count. */
function Parts({ answer, onOpen }: { answer: AtWorkA; onOpen?: (session: string) => void }) {
  const { t } = useTranslation();
  const more = Object.values(answer.moreParts ?? {}).reduce((sum, count) => sum + count, 0);
  if (answer.parts.length < 2 && more === 0) return null;
  return (
    <section className="grid min-w-0 gap-1">
      <SectionTitle level={3}>{t('work.atWork.group.parts')}</SectionTitle>
      <ol className="m-0 grid list-none gap-0.5 p-0">
        {answer.parts.map((part) => (
          <li key={part.key}>
            <button
              type="button"
              aria-current={part.key === answer.attended || undefined}
              onClick={() => onOpen?.(part.key)}
              className={cn(
                'flex w-full min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 rounded-control border-l-[3px] px-2 py-1 text-left text-small',
                'hover:bg-accent-soft/50',
                part.key === answer.attended ? 'border-l-accent bg-accent-soft text-ink' : 'border-l-transparent text-ink-soft',
              )}
            >
              {part.agent && <span className="font-mono text-meta text-ink-faint">{part.agent}</span>}
              {part.state && <Dot tone={part.state === 'working' ? 'live' : 'ended'} label={t(partStateKey(part.state))} />}
              <PartChildren t={t} part={part} />
            </button>
          </li>
        ))}
      </ol>
      {more > 0 && <p className="m-0 text-meta text-ink-faint">{t('work.atWork.part.more', { count: more })}</p>}
    </section>
  );
}

function PartChildren({ t, part }: { t: TFunction; part: AtWorkPart }) {
  const { children } = part;
  if (children.elsewhere) return <span className="text-meta text-ink-faint">{t('work.atWork.elsewhere')}</span>;
  if (children.unreadable) return <span className="text-meta text-ink-faint">{t('work.atWork.unreadable')}</span>;
  if (!children.count) return null;
  return (
    <span className="text-meta text-ink-faint">
      {t('work.atWork.part.children', { count: children.count, working: children.working ?? 0 })}
    </span>
  );
}

/** Whether *Nobody else is working on this* may be said (§5.2, rule 16): only where every source was read and holds nobody. */
export function nobody(answer: AtWorkA): boolean {
  const { subagents, parts } = answer;
  return answer.parts.length === 1 && !answer.moreParts
    && (subagents.read === 'read' || subagents.read === 'none')
    && subagents.children.length === 0 && !subagents.observed && !subagents.unplaced && !subagents.seenByCalls
    && parts.every((part) => !part.children.unreadable && !part.children.elsewhere);
}

/** The attended part's subagents as a tree, the bound's counts under them. */
function Subagents({ answer, quiet, now }: { answer: AtWorkA; quiet: boolean; now?: Date }) {
  const { t } = useTranslation();
  const subagents: AtWorkSubagents = answer.subagents;
  if (subagents.read === 'elsewhere') return <p className="m-0 text-small text-ink-faint">{t('work.atWork.elsewhere')}</p>;
  if (!quiet && nobody(answer)) return <p className="m-0 text-small text-ink-faint">{t('work.atWork.empty')}</p>;

  const more = Object.values(subagents.more ?? {}).reduce((sum, count) => sum + count, 0);
  const moreWorking = subagents.more?.working ?? 0;
  return (
    <section className="grid min-w-0 gap-1">
      <div className="flex flex-wrap items-baseline gap-x-2">
        <SectionTitle level={3}>{t('work.atWork.group.subagents')}</SectionTitle>
        {subagents.working > 0 && <span className="text-meta text-accent">{t('work.atWork.subagentsWorking', { count: subagents.working })}</span>}
        {subagents.turnWaits && <span className="text-meta text-ink-soft">{t('work.atWork.subagentsTurnWaits')}</span>}
      </div>
      <p className="m-0 text-meta text-ink-faint">{t('work.atWork.subagentsNote')}</p>
      {subagents.read === 'unreadable' && <p className="m-0 text-small text-ink-soft">{t('work.atWork.unreadable')}</p>}
      {subagents.seenByCalls && <p className="m-0 text-small text-ink-faint">{t('work.atWork.subagentsByCalls')}</p>}
      {(subagents.observed ?? 0) > 0 && <p className="m-0 text-meta text-ink-faint">{t('work.atWork.observed', { count: subagents.observed })}</p>}
      {(subagents.unplaced ?? 0) > 0 && <p className="m-0 text-meta text-ink-faint">{t('work.atWork.unplaced', { count: subagents.unplaced })}</p>}
      {subagents.children.length > 0 && (
        <ol className="m-0 grid list-none gap-0.5 p-0">
          {subagents.children.map((child) => <ChildRow key={child.key} child={child} now={now} />)}
        </ol>
      )}
      {(subagents.folded ?? 0) > 0 && <p className="m-0 text-meta text-ink-faint">{t('work.atWork.child.folded', { count: subagents.folded })}</p>}
      {more > 0 && (
        <p className="m-0 text-meta text-ink-faint">
          {moreWorking > 0 ? t('work.atWork.moreWorking', { count: more, working: moreWorking }) : t('work.atWork.more', { count: more })}
        </p>
      )}
    </section>
  );
}

/** How far a row is set in: at most two steps, a deeper child saying its depth. */
const STEPS = 2;

function ChildRow({ child, now }: { child: AtWorkChild; now?: Date }) {
  const { t, i18n } = useTranslation();
  const time = (iso: string | undefined) => (iso ? clockOf(iso, now, i18n.language) : '');
  const depth = child.depth ?? 1;
  const tone = childTone(child.state);
  const word = t(CHILD_STATE_KEY[child.state]);
  const said = (() => {
    if (child.state === 'unsaid') return `${t('work.atWork.child.firstSeen', { time: time(child.since) })} · ${word}`;
    if (LAST_SEEN.has(child.state)) return child.lastSeen ? `${word} · ${t('work.atWork.child.lastSeen', { time: time(child.lastSeen) })}` : word;
    if (child.state === 'working') return child.startSaid || !child.since ? word : `${word} · ${t('work.atWork.child.firstSeen', { time: time(child.since) })}`;
    return child.ended ? `${word} ${time(child.ended)}` : word;
  })();
  return (
    <li
      className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 py-0.5 text-small"
      style={{ paddingInlineStart: `${Math.min(Math.max(depth - 1, 0), STEPS) * 12}px` }}
    >
      {child.type && <span className="text-ink">{child.type}</span>}
      {child.label && <span className="min-w-0 truncate text-ink-soft" title={child.label.text}>{child.label.text}</span>}
      {!child.label && !child.type && <span className="font-mono text-meta text-ink-faint">{child.key}</span>}
      {tone === 'live'
        ? <Dot tone="live" label={said} />
        : <span className={cn('text-meta', tone === 'warn' ? 'text-warn' : tone === 'failed' ? 'text-ink-danger' : 'text-ink-soft')}>{said}</span>}
      {child.parentKey === undefined && child.depth === undefined && (
        <span className="text-meta text-ink-faint">{t('work.atWork.child.noParent')}</span>
      )}
      {depth - 1 > STEPS && <span className="text-meta text-ink-faint">{t('work.atWork.child.depth', { depth })}</span>}
    </li>
  );
}
