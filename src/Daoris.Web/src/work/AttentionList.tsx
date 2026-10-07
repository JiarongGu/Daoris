import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { SectionTitle, Tip } from '../ui';
import { type Attention, type AttentionActs, AttentionRow } from './AttentionRow';
import { attentionGroups } from './attention';

/**
 * Where each kind of row leads, when it leads anywhere. A kind with no door here is shown, and is
 * not a button: a parked session in a browser has no Sessions to open in.
 */
export type AttentionDoors = Partial<Record<Attention['kind'], (item: Attention) => void>>;

/** How many rows *Ready for you* shows before it sends the rest to Sessions (design §6.2): review can be many. */
export const READY_SHOWN = 5;

/** The row an act is on its way for, by kind and id: its acts are held until it answers. */
export const attentionKey = (item: Attention) => `${item.kind}:${item.id}`;

/**
 * *What needs you* with something in it (UX6c, design §6.2): its title and count, then a group per kind of wait that holds
 * any, each named, its rows in the order `needsAPerson` gave them. *Ready for you* shows five, then counts the rest with a
 * door into Sessions, where they are.
 *
 * @remarks
 * Its kind wears open's hue on the card's edge and on each row's (D41 §3: waiting on the person is open's hue, everywhere
 * it is shown). A molecule: handed the rows, their doors and acts, it draws them and reports each press.
 */
export function AttentionList({ items, doors = {}, acts, acting = null, held, below, onSessions }: {
  items: readonly Attention[];
  doors?: AttentionDoors;
  acts?: AttentionActs;
  /** The row an act is on its way for (`attentionKey`). */
  acting?: string | null;
  /** Rows whose acts are held by something on its way elsewhere: an account's while one of its agent's sign-ins runs (UX6d). */
  held?: (item: Attention) => boolean;
  /** What a row shows beneath itself while the band follows one of its acts: an account's sign-in, step by step. */
  below?: (item: Attention) => ReactNode;
  /** Sessions itself, where *Ready for you*'s rows past its fifth are; absent, the count is words. */
  onSessions?: () => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="rounded-card border border-line border-l-[3px] border-l-st-open bg-raised px-0 pb-2 pt-3.5">
      <header className="flex items-baseline justify-between gap-4 px-[1.15rem] pb-1">
        <Tip content={t('work.attention.hint')}>
          <h2 className="m-0 text-title font-semibold">{t('work.attention.title')}</h2>
        </Tip>
        <span className="font-mono text-meta tabular-nums text-ink-open">{items.length}</span>
      </header>
      {attentionGroups(items).map(({ group, items: rows }) => {
        const shown = group === 'ready' ? rows.slice(0, READY_SHOWN) : rows;
        const more = rows.length - shown.length;
        return (
          <div key={group} role="group" aria-label={t(`work.attention.group.${group}`)} className="mt-2">
            <div className="px-[1.15rem]"><SectionTitle level={3}>{t(`work.attention.group.${group}`)}</SectionTitle></div>
            <ul className="m-0 grid list-none gap-1 p-0">
              {shown.map((item) => (
                <AttentionRow
                  key={attentionKey(item)}
                  item={item}
                  onOpen={doors[item.kind]}
                  acts={acts}
                  busy={acting === attentionKey(item) || (held?.(item) ?? false)}
                  below={below?.(item)}
                />
              ))}
            </ul>
            {more > 0 && (
              <p className="m-0 px-[1.15rem] pt-1 text-small">
                {onSessions
                  ? (
                    <button type="button" onClick={onSessions} className="text-accent underline-offset-2 hover:underline">
                      {t('work.attention.more', { count: more })}
                    </button>
                  )
                  : <span className="text-ink-faint">{t('work.attention.more', { count: more })}</span>}
              </p>
            )}
          </div>
        );
      })}
    </div>
  );
}

/**
 * *What needs you* with nothing in it (design §6.1): one line, *Nothing needs you*, and what is working, never a card
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
export function AttentionRegion({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  return <section aria-label={t('work.attention.title')} className="mb-5">{children}</section>;
}
