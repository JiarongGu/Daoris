import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { type ContextOffer, contextOffer } from '../menus/press';
import { Button, DotMark, Pill, StripMark } from '../ui';
import type { AgentRow } from './agents';

/**
 * **The Agents place's list** (UX6e, D150 §5.1): each agent this build knows once, whatever doors reach it, the installed in
 * the roster's order and then *Not installed* as a group of its own (AGENTS2, D152 §4.6). A row is its name and maker, and its
 * accounts in a phrase (*3 accounts · 2 signed out*, *1 account*, *not installed*); a row with an account the person must
 * act on wears the waiting mark, and one not installed carries its *Install* beside it.
 *
 * @remarks
 * **A molecule**: the rows arrive worded (`agentRows`), and every press goes out. Each row is a row of its list
 * (`data-list-row`), so ↑, ↓, Home and End move along it. The list has no `＋`: an account is made on its agent's page, and
 * an agent arrives by its own installer or a plugin (D64). Adding an agent adds a row, never a screen.
 *
 * **No fold for the agents not installed** (AGENTS2): the list's ⋯ hid them, and Codex, which this build drives, went
 * unseen behind it. An agent's *Install* is its own installer, the one `daoris agent install` runs.
 */
export function AgentList({ rows, chosen, onChoose, onInstall }: {
  rows: readonly AgentRow[];
  /** The list's chosen item: an agent's id. */
  chosen: string | null;
  onChoose: (agent: string) => void;
  /** Install an agent not installed, by its id; absent offers no Install. */
  onInstall?: (agent: string) => void;
}) {
  const { t } = useTranslation();
  const absentFrom = installedFirst(rows).findIndex((row) => !row.installed);
  return (
    <ul className="m-0 list-none p-0" aria-label={t('nav.agents')}>
      {installedFirst(rows).map((row, at) => {
        const install = !row.installed && onInstall ? () => onInstall(row.name) : undefined;
        return [
          // The agents not installed, a group of their own under the installed (D152 §4.6), named once at its head.
          at === absentFrom && (
            <li key="absent" role="presentation" className="px-2.5 pb-0.5 pt-2.5 text-meta font-medium uppercase tracking-[0.04em] text-ink-faint">
              {t('agents.list.absent')}
            </li>
          ),
          <li key={row.name} data-list-row="" className="flex items-center" {...contextOffer(rowMenu(t, row, () => onChoose(row.name), install))}>
            <div className="min-w-0 flex-1">
              <RowDoor chosen={chosen === row.name} onPress={() => onChoose(row.name)}>
                <span className="flex min-w-0 items-baseline gap-2">
                  <span className={cn('min-w-0 truncate text-body font-medium', row.installed ? 'text-ink' : 'text-ink-soft')}>{row.product}</span>
                  {row.maker && <span className="shrink-0 truncate text-meta text-ink-faint">{row.maker}</span>}
                  {row.waiting > 0 && (
                    <span className="ml-auto shrink-0">
                      <DotMark tone="parked" />
                      <span className="sr-only">{t('agents.list.waiting', { count: row.waiting })}</span>
                    </span>
                  )}
                </span>
                <span className="flex min-w-0 items-center gap-1.5 text-small text-ink-soft">
                  {row.installed ? <span className="min-w-0 truncate">{row.phrase}</span> : <Pill tone="neutral">{row.phrase}</Pill>}
                </span>
              </RowDoor>
            </div>
            {/* Beside the row and not inside it, as a plugin offer's is: a button inside the row's button is no button. */}
            {install && (
              <Button
                variant="ghost"
                aria-label={t('agents.list.install', { agent: row.product })}
                onClick={install}
                className="mr-1.5 shrink-0 px-2 py-0.5 text-small"
              >
                {t('harness.install')}
              </Button>
            )}
          </li>,
        ];
      })}
    </ul>
  );
}

/** The installed agents first, then those not installed, each in the roster's order (D152 §4.6). */
function installedFirst(rows: readonly AgentRow[]): AgentRow[] {
  return [...rows.filter((row) => row.installed), ...rows.filter((row) => !row.installed)];
}

/**
 * What a row offers a right-click (CTX1, D138 §4): opening it, its Install where its row carries one, and its id. What
 * else is done to an agent is its page's.
 */
function rowMenu(t: (key: string) => string, row: AgentRow, open: () => void, install?: () => void): ContextOffer {
  return {
    label: row.product,
    acts: [
      { id: 'open', label: t('contextMenu.act.open'), onSelect: open },
      ...(install ? [{ id: 'install', label: t('harness.install'), icon: 'plus' as const, onSelect: install }] : []),
      { id: 'copy', label: t('contextMenu.act.copyAgent'), icon: 'copy', copy: row.name },
    ],
  };
}

/** A row's door: the whole row chooses it, wearing the list's selection as the plugin list's rows do. */
function RowDoor({ chosen, onPress, children }: { chosen: boolean; onPress: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      aria-current={chosen || undefined}
      onClick={onPress}
      className={cn(
        'block w-full min-w-0 border-l-[3px] px-2.5 py-1.5 text-left transition-colors duration-(--speed)',
        'hover:bg-accent-soft/50',
        chosen ? 'border-l-accent bg-accent-soft' : 'border-l-transparent',
      )}
    >
      {children}
    </button>
  );
}

/**
 * The list closed to its strip: each agent's initial, the waiting mark on one with an account the person must act on, and
 * one not installed drawn faint (D118 §3a).
 */
export function AgentStrip({ rows, chosen, onChoose }: {
  rows: readonly AgentRow[];
  chosen: string | null;
  onChoose: (agent: string) => void;
}) {
  const { t } = useTranslation();
  return (
    <ul className="m-0 grid list-none justify-items-center gap-1 px-0 py-1.5">
      {installedFirst(rows).map((row) => (
        <StripMark
          key={row.name}
          label={row.waiting > 0 ? `${row.product} · ${t('agents.list.waiting', { count: row.waiting })}` : `${row.product} · ${row.phrase}`}
          initialOf={row.product}
          tone={row.waiting > 0 ? 'parked' : undefined}
          dimmed={!row.installed}
          current={chosen === row.name}
          onPress={() => onChoose(row.name)}
        />
      ))}
    </ul>
  );
}
