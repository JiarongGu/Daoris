import * as Menu from '@radix-ui/react-dropdown-menu';
import { useTranslation } from 'react-i18next';
import type { SyncStanding } from '../api';
import { ago } from '../format';
import { cn } from '../lib/cn';
import { Icon, Tip } from '../ui';
import { STATUS_PRESSABLE } from './frame';

/** A quest in conflict, with its title where this page already holds it — the id alone otherwise. */
export type SyncConflictRow = { id: string; title?: string };

/**
 * Where a circle stands with its remote, as the status bar's sync item (SYNC6b, sync design §6).
 *
 * @remarks
 * **Icon and number, not prose** (platform UX §4, from a real VS Code window): up for what waits to
 * be pushed, down for what the last pass could not bring level, a warning for what needs a person.
 * A level circle says so in one word. Pressing it opens the detail — when it last synced, the wall if
 * the last try hit one, the quests in conflict — and *Sync now*.
 *
 * **Two doors, one pass** (D50): `onSyncNow` is the shell running the tick's own pass, the same one
 * `daoris-driver sync` runs. A browser reads the standing over HTTP like anything else and simply is
 * not given it, so the menu has no *Sync now* rather than one that cannot work.
 *
 * The wall is the host's sentence and renders verbatim (frontend architecture §3): it names what
 * could not be reached, and a paraphrase would be a different answer.
 *
 * **Props only, no hook** (components §2): every state is reachable by passing it.
 */
export function SyncStatus({
  workspace, standing, conflicts, syncing = false, onSyncNow, onOpenQuest, onRemotes, defaultOpen,
}: {
  workspace: string;
  standing: Pick<SyncStanding, 'ahead' | 'behind' | 'synced' | 'tried' | 'problem'>;
  conflicts: SyncConflictRow[];
  /** A pass this page asked for is running — the item says so and does not start another. */
  syncing?: boolean;
  /** The shell's pass. Absent in a browser. */
  onSyncNow?: () => void;
  onOpenQuest: (id: string) => void;
  /** Where this circle's wiring is set. */
  onRemotes?: () => void;
  /** Open on first render — what a story and a test use to show the detail. */
  defaultOpen?: boolean;
}) {
  const { t } = useTranslation();
  const { ahead, behind, synced, tried, problem } = standing;
  const level = ahead === 0 && behind.length === 0 && conflicts.length === 0 && !problem;
  const when = synced ? t('work.sync.syncedAt', { when: ago(synced) }) : t('work.sync.never');

  return (
    // `modal={false}`: a menu in the status bar has no business making the page inert (AppMenu's rule).
    <Menu.Root modal={false} defaultOpen={defaultOpen}>
      <Tip content={`${t('work.sync.label')} — ${t('work.sync.tip', { workspace })}`}>
        <Menu.Trigger asChild>
          <button type="button" aria-label={t('work.sync.label')} className={STATUS_PRESSABLE}>
            <span className="flex h-full items-center gap-1.5 px-2">
              <Icon name={problem ? 'cloudOff' : 'cloud'} size={12} className={problem ? 'text-warn' : undefined} />
              {ahead > 0 && (
                <span className="flex items-center gap-0.5 tabular-nums text-ink">
                  <Icon name="ahead" size={11} />{ahead}
                </span>
              )}
              {behind.length > 0 && (
                <span className="flex items-center gap-0.5 tabular-nums text-ink">
                  <Icon name="behind" size={11} />{behind.length}
                </span>
              )}
              {conflicts.length > 0 && (
                <span className="flex items-center gap-0.5 tabular-nums text-warn">
                  <Icon name="conflict" size={11} />{conflicts.length}
                </span>
              )}
              {problem && <span className="text-warn">{t('work.sync.walled')}</span>}
              {level && <span className="text-ink">{synced ? t('work.sync.level') : t('work.sync.never')}</span>}
            </span>
          </button>
        </Menu.Trigger>
      </Tip>

      <Menu.Portal>
        <Menu.Content
          side="top"
          align="start"
          sideOffset={4}
          collisionPadding={8}
          className="z-30 w-72 max-w-[calc(100vw-2rem)] rounded-control border border-line bg-overlay p-1 text-small shadow-lg"
        >
          {/* What is TRUE, first and not pressable: the circle, when it last synced, and the wall. */}
          <div className="grid gap-1 px-2 py-1.5">
            <div className="flex items-baseline justify-between gap-2">
              <span className="truncate text-ink">{workspace}</span>
              <span className="shrink-0 text-meta text-ink-faint">{when}</span>
            </div>
            {problem && (
              <p className="m-0 text-meta text-warn">
                {t('work.sync.lastTry', { when: tried ? ago(tried) : '' })}{' '}
                <span className="text-ink-soft">{problem}</span>
              </p>
            )}
            <p className="m-0 text-meta tabular-nums text-ink-soft">
              {t('work.sync.aheadLine', { count: ahead })} · {t('work.sync.behindLine', { count: behind.length })}
            </p>
          </div>

          {conflicts.length > 0 && (
            <>
              <Menu.Separator className="my-1 h-px bg-line" />
              <Menu.Label className="px-2 py-1 text-meta text-ink-faint">{t('work.sync.inConflict')}</Menu.Label>
              {conflicts.map((conflict) => (
                <Menu.Item
                  key={conflict.id}
                  onSelect={() => onOpenQuest(conflict.id)}
                  className={ROW}
                >
                  <Icon name="conflict" size={12} className="shrink-0 text-warn" aria-hidden />
                  <span className="shrink-0 font-mono text-meta text-ink-faint">#{conflict.id.slice(0, 6)}</span>
                  <span className="truncate">{conflict.title ?? t('work.sync.untitled')}</span>
                </Menu.Item>
              ))}
            </>
          )}

          {(onSyncNow || onRemotes) && <Menu.Separator className="my-1 h-px bg-line" />}
          {onSyncNow && (
            <Menu.Item disabled={syncing} onSelect={onSyncNow} className={ROW}>
              <Icon name="refresh" size={12} className={cn('shrink-0', syncing && 'animate-spin')} aria-hidden />
              {syncing ? t('work.sync.syncing') : t('work.sync.now')}
            </Menu.Item>
          )}
          {onRemotes && (
            <Menu.Item onSelect={onRemotes} className={ROW}>
              <Icon name="cloud" size={12} className="shrink-0" aria-hidden />
              {t('work.sync.remotes')}
            </Menu.Item>
          )}
        </Menu.Content>
      </Menu.Portal>
    </Menu.Root>
  );
}

/** One row of the menu — AppMenu's row, so the two menus in this window read as one kind of thing. */
const ROW = cn(
  'flex cursor-pointer items-center gap-2 rounded-[4px] px-2 py-1.5 text-small text-ink-soft outline-none',
  'data-[highlighted]:bg-raised data-[highlighted]:text-ink',
  'data-[disabled]:cursor-default data-[disabled]:opacity-60',
);
