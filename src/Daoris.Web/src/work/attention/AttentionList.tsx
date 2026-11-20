import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { cardEdge, cardFrame, SectionTitle, Tip } from '../../design/ui';
import { cn } from '../../lib/cn';
import type { StepHoldBridge } from '../../workflow/wait/StepHold';
import { type Attention, type AttentionActs, AttentionRow } from './AttentionRow';
import { attentionGroups, staleGoAheads } from './attention';
import { repeatedKinds } from './repeatedKinds';
import type { Answered } from '../InlineConfirm';
import { SetAsideKind } from './SetAsideKind';
import { STALE_AT_ONCE, StaleGoAheads } from './StaleGoAheads';

/**
 * Where each kind of row leads, when it leads anywhere. A kind with no door here is shown, and is
 * not a button: a parked session in a browser has no Sessions to open in.
 */
export type AttentionDoors = Partial<Record<Attention['kind'], (item: Attention) => void>>;

/** How many rows *Ready for you* shows before it sends the rest to Sessions: review can be many. */
export const READY_SHOWN = 5;

/** The row an act is on its way for, by kind and id: its acts are held until it answers. */
export const attentionKey = (item: Attention) => `${item.kind}:${item.id}`;

/**
 * *What needs you* with something in it: its title and the owner's count, then a group per kind of wait that holds
 * any, each named, its rows in the order `attentionInOrder` gave them. *Ready for you* shows five, then counts the rest with a
 * door into Sessions, where they are. The agents' rows follow apart, *With the agents*, in a plain frame with no edge, uncounted.
 *
 * @remarks
 * Its kind wears open's hue on the card's edge and on each row's (waiting on the person is open's hue, everywhere
 * it is shown). A molecule: handed the rows, their doors and acts, it draws them and reports each press.
 */
export function AttentionList({
  items, doors = {}, run, acts, acting = null, held, below, onSessions, bare = false, onRefuseStale, refusingStale = false,
  onSetAsideKind, settingAside = false, stepHold,
}: {
  items: readonly Attention[];
  /** Every row of one kind a group holds many of set aside in one press; offered where a group holds `ASIDE_AT_ONCE` or more. Absent, none is offered. */
  onSetAsideKind?: (kind: Attention['kind'], count: number, answered: Answered) => void;
  /** The one press's act is on its way. */
  settingAside?: boolean;
  /**
   * Every stale go-ahead a group holds refused in one press, with one reason, each through its own row's door; offered
   * where a group holds two or more. Absent, none is offered.
   */
  onRefuseStale?: (items: readonly Attention[], words: string, answered: Answered) => void;
  /** The stale go-aheads' refusals are on their way. */
  refusingStale?: boolean;
  /**
   * Its groups alone, with no card and no head of its own: *Notifications*' group is its head, which says its name and
   * its count.
   */
  bare?: boolean;
  doors?: AttentionDoors;
  /** A row's door into the run whose step it waits at, where it has one. */
  run?: (item: Attention) => (() => void) | undefined;
  acts?: AttentionActs;
  /** The row an act is on its way for (`attentionKey`). */
  acting?: string | null;
  /** Rows whose acts are held by something on its way elsewhere: an account's while one of its agent's sign-ins runs. */
  held?: (item: Attention) => boolean;
  /** What a row shows beneath itself: an account's sign-in, step by step, while the band follows one; a release step's approval card. */
  below?: (item: Attention) => ReactNode;
  /** Sessions itself, where *Ready for you*'s rows past its fifth are; absent, the count is words. */
  onSessions?: () => void;
  /** The bridge's calls for a step's holder presses, handed down by the band to each row. */
  stepHold?: StepHoldBridge;
}) {
  const { t } = useTranslation();
  const groups = attentionGroups(items);
  const owners = groups.filter(({ group }) => group !== 'agents');
  const agents = groups.find(({ group }) => group === 'agents');
  const counted = owners.reduce((sum, { items: rows }) => sum + rows.length, 0);
  const drawn = ({ group, items: rows }: (typeof groups)[number]) => {
    const shown = group === 'ready' ? rows.slice(0, READY_SHOWN) : rows;
    const more = rows.length - shown.length;
    const stale = onRefuseStale ? staleGoAheads(rows) : [];
    return (
      <div key={group} role="group" aria-label={t(`work.attention.group.${group}`)} className={bare ? 'mt-1 first:mt-0' : 'mt-2'}>
        <div className="px-4"><SectionTitle level={3}>{t(`work.attention.group.${group}`)}</SectionTitle></div>
        {onRefuseStale && stale.length >= STALE_AT_ONCE && (
          <StaleGoAheads items={stale} busy={refusingStale} onRefuse={onRefuseStale} />
        )}
        {onSetAsideKind && repeatedKinds(rows).map(({ kind, count }) => (
          <SetAsideKind key={kind} kind={kind} count={count} busy={settingAside} onSetAside={onSetAsideKind} />
        ))}
        <ul className="m-0 grid list-none gap-1 p-0">
          {shown.map((item) => (
            <AttentionRow
              key={attentionKey(item)}
              item={item}
              onOpen={doors[item.kind]}
              onRun={run?.(item)}
              acts={acts}
              busy={acting === attentionKey(item) || (held?.(item) ?? false)}
              below={below?.(item)}
              stepHold={stepHold}
            />
          ))}
        </ul>
        {more > 0 && (
          <p className="m-0 px-4 pt-1 text-small">
            {onSessions
              ? (
                <button type="button" onClick={onSessions} className="text-accent-ink underline-offset-2 hover:underline">
                  {t('work.attention.more', { count: more })}
                </button>
              )
              : <span className="text-ink-faint">{t('work.attention.more', { count: more })}</span>}
          </p>
        )}
      </div>
    );
  };
  return (
    <>
      {owners.length > 0 && (
        <div className={bare ? cn('pb-1', cardEdge('wait')) : cn(cardFrame('wait'), 'px-0 pb-2')}>
          {!bare && (
            <header className="flex items-baseline justify-between gap-4 px-4 pb-1">
              <Tip content={t('work.attention.hint')}>
                <h2 className="m-0 text-heading font-semibold">{t('work.attention.title')}</h2>
              </Tip>
              <span className="font-mono text-caption tabular-nums text-ink-open">{counted}</span>
            </header>
          )}
          {owners.map(drawn)}
        </div>
      )}
      {agents && (
        <div data-agents-group className={cn(bare ? 'pb-1' : cn(cardFrame(), 'px-0 pb-2'), owners.length > 0 && 'mt-3')}>
          {drawn(agents)}
          <p className="m-0 px-4 pt-1 text-small text-ink-faint">{t('work.attention.agentsNote')}</p>
        </div>
      )}
    </>
  );
}

/**
 * *What needs you* with nothing in it: one line, *Nothing needs you*, and what is working, never a card
 * saying all clear, which people stop reading.
 */
export function NothingNeedsYou({ working }: { working: number }) {
  const { t } = useTranslation();
  return (
    <p className="m-0 flex flex-wrap items-baseline gap-x-2 text-body text-ink-soft">
      <span>{t('work.attention.nothing')}</span>
      {working > 0 && (
        <>
          <span aria-hidden className="text-ink-faint">·</span>
          <span className="text-ink-faint">{t('work.attention.working', { count: working })}</span>
        </>
      )}
    </p>
  );
}

/**
 * The band's region, one element whatever it holds: the line while nothing waits and the list once something does are
 * one landmark, so a reader and a test holding it are not left with a node the page replaced as the answers arrived.
 */
export function AttentionRegion({ children, resyncing = false, bare = false }: {
  children: ReactNode;
  /** Who needs the person read again whole: the last rows held at reduced opacity until it lands. */
  resyncing?: boolean;
  /** Within *Notifications*' group, which is the region: no landmark and no margin of its own. */
  bare?: boolean;
}) {
  const { t } = useTranslation();
  if (bare) {
    return (
      <div aria-busy={resyncing || undefined} className={cn(resyncing && 'opacity-60 transition-opacity duration-(--duration-quick)')}>
        {children}
      </div>
    );
  }
  return (
    <section
      aria-label={t('work.attention.title')}
      aria-busy={resyncing || undefined}
      className={cn('mb-5', resyncing && 'opacity-60 transition-opacity duration-(--duration-quick)')}
    >
      {children}
    </section>
  );
}
