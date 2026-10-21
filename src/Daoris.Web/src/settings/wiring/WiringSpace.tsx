import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { SpaceKind, SystemSpace } from '../../data/wiring';
import { Button, Card, SettingRow, SkeletonRows } from '../../design/ui';
import { clockOf, dayOf, size } from '../../locales/format';
import { WiringCard } from './WiringParts';
import { SpaceRunLine } from './WiringSpaceFree';

/** Each kind's word in `settings.daoris.space.kind.*`, by its id. */
const KIND_WORD: Record<string, string> = {
  'set-aside': 'setAside', downloads: 'downloads', 'browser-cache': 'browserCache', logs: 'logs', history: 'history', trees: 'trees',
  'sign-ins': 'signIns', 'left-over': 'leftOver', rest: 'rest',
};

/** What a kind's *next* counts, in `settings.daoris.space.what.*`. */
const WHAT_WORD: Record<string, string> = { 'set-aside': 'setAside', downloads: 'downloads', logs: 'logs' };

/** The kinds whose rule is one of Daoris's runs, which keep a *last* in `keeping.json` (KEEP1a, KEEP1b). */
const RUNS = new Set(['set-aside', 'downloads']);

type T = (key: string, options?: Record<string, unknown>) => string;

/** A kind's rule in words: the one that runs, or whose it is. */
function ruleWords(t: T, kind: SpaceKind): string {
  const { rule } = kind;
  if (rule.kind === 'days' && kind.id === 'downloads') return t('settings.daoris.space.rule.downloads', { count: rule.days });
  if (rule.kind === 'days' && kind.id === 'set-aside') {
    return t('settings.daoris.space.rule.joined', {
      days: t('settings.daoris.space.rule.days', { count: rule.days }),
      then: t(rule.whole ? 'settings.daoris.space.rule.whole' : 'settings.daoris.space.rule.slim'),
    });
  }
  if (rule.kind === 'days') return t('settings.daoris.space.rule.days', { count: rule.days });
  if (rule.kind === 'own') return t(`settings.daoris.space.whose.${rule.whose ?? 'each'}`, { defaultValue: t('settings.daoris.space.whose.each') });
  return t(`settings.daoris.space.rule.${rule.kind}`);
}

/** A kind's size, with when it was counted; or that it is counting, or not counted yet with the press that counts it. */
function Sized({ kind, onCount }: { kind: SpaceKind; onCount: () => void }) {
  const { t } = useTranslation();
  if (kind.bytes === null) {
    if (kind.counting) return <span className="text-small text-ink-faint">{t('settings.daoris.space.counting')}</span>;
    return (
      <span className="inline-flex flex-wrap items-baseline gap-x-2 text-small text-ink-faint">
        {t('settings.daoris.space.notCounted')}
        <Button variant="ghost" onClick={onCount}>{t('settings.daoris.space.count')}</Button>
      </span>
    );
  }
  const value = kind.partial ? t('settings.daoris.space.atLeast', { size: size(kind.bytes) }) : size(kind.bytes);
  return (
    <span className="text-small text-ink-soft">
      {value}
      {kind.partial && kind.partialWhy && (
        <span className="text-ink-faint">{' · '}{t(`settings.daoris.space.partial.${kind.partialWhy}`, { defaultValue: t('settings.daoris.space.partial.unreadable') })}</span>
      )}
      {kind.measured && <span className="text-ink-faint">{' · '}{t('settings.daoris.space.counted', { when: clockOf(kind.measured) })}</span>}
      {kind.counting && <span className="text-ink-faint">{' · '}{t('settings.daoris.space.counting')}</span>}
    </span>
  );
}

/** A kind's *next* and *last*, on one line: when its rule next takes something, and what its last run removed. */
function NextAndLast({ kind }: { kind: SpaceKind }) {
  const { t } = useTranslation();
  const parts: string[] = [];
  if (kind.next) {
    const what = t(`settings.daoris.space.what.${WHAT_WORD[kind.id] ?? 'setAside'}`, { count: kind.next.count });
    parts.push(t('settings.daoris.space.next', {
      what: kind.next.bytes === null ? what : t('settings.daoris.space.whatSized', { what, size: size(kind.next.bytes) }),
      when: dayOf(kind.next.at),
    }));
  } else if (kind.nextWhy) {
    parts.push(t('settings.daoris.space.nextUnread'));
  }
  if (RUNS.has(kind.id)) {
    if (kind.last) {
      parts.push(t('settings.daoris.space.last', { when: clockOf(kind.last.at), count: kind.last.removed.count, size: size(kind.last.removed.bytes) }));
    } else {
      parts.push(t(kind.lastWhy ? 'settings.daoris.space.lastUnread' : 'settings.daoris.space.nothingYet'));
    }
  }
  if (parts.length === 0) return null;
  return <p className="m-0 text-small text-ink-faint">{parts.join(' · ')}</p>;
}

/**
 * Disk space (KEEP1c, D195 point 5; the cleanup design §4.1, §4.2): the free space of the Daoris folder's volume, then each kind Daoris
 * keeps with its size and when it was counted, its rule in words, and its *next* and *last* clean. Every rule it shows already runs; a
 * kind whose rule is not Daoris's says whose it is. A size is counted only when asked (*Count*, *Measure again*) or by the look once a
 * day, never by the page's open. The Downloads row opens the list the Folders card draws (CACHECLEAR1), under its own row here. Each
 * rule's *Change…* arrives with the row that sets it (KEEP1d). KEEP1e: *Free up space…* in the head lists first, under the head, then
 * presses; a run's line (R2) says the kind it is on, then what went and what was kept by reason. A run under way elsewhere disables it.
 *
 * @remarks
 * A molecule: drawn from props, so every state is reachable without a shell. `space` absent is the first load; `older` a shell that
 * answers no `SYSTEM_SPACE`.
 */
export function WiringSpace({ space, older = false, busy = false, onCount, downloads, onDownloads, onFreeUp, freeing }: {
  space?: SystemSpace;
  older?: boolean;
  busy?: boolean;
  /** Count the kinds named, or every kind. */
  onCount: (kinds?: string[]) => void;
  /** The Downloads row's clearing, open (CACHECLEAR1), drawn under that row in place of its press. */
  downloads?: ReactNode;
  onDownloads?: () => void;
  /** *Free up space…*'s first press, which lists (KEEP1e); absent draws no press. */
  onFreeUp?: () => void;
  /** The list, open, drawn under the card's head. */
  freeing?: ReactNode;
}) {
  const { t } = useTranslation();
  const title = t('settings.daoris.card.space');
  if (older) {
    return (
      <WiringCard id="settings-daoris-space" title={title}>
        <p className="m-0 py-2 text-small text-ink-soft">{t('settings.daoris.space.older')}</p>
      </WiringCard>
    );
  }
  if (!space) {
    return (
      <div aria-busy="true">
        <Card id="settings-daoris-space"><SkeletonRows rows={5} /></Card>
      </div>
    );
  }
  const counting = space.kinds.some((kind) => kind.counting);
  const door = (named: string) => t(`settings.daoris.space.door.${named}`, { defaultValue: named });
  // A run of this window's process under way is said by its own line; the lock's holder then is that run.
  const running = space.run != null && !space.run.outcome;
  const heldElsewhere = space.held != null && !space.held.gone && !running;
  const press = onFreeUp && {
    label: t('settings.daoris.button.freeUp'),
    disabled: heldElsewhere || running,
    why: heldElsewhere ? t('settings.daoris.space.held', { door: door(space.held!.door), when: clockOf(space.held!.since) }) : undefined,
    primary: space.volume?.below === true,
    onPress: onFreeUp,
  };
  return (
    <WiringCard
      id="settings-daoris-space"
      title={title}
      press={press || undefined}
      head={freeing}
      aside={{ label: t('settings.daoris.button.measure'), busy: busy || counting, onPress: () => onCount() }}
    >
      {space.run && <SpaceRunLine run={space.run} />}
      {space.held && !running && (
        <p className="m-0 border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft" role="status">
          {t(space.held.gone ? 'settings.daoris.space.stopped' : 'settings.daoris.space.held', { door: door(space.held.door), when: clockOf(space.held.since) })}
        </p>
      )}
      <SettingRow
        label={t('settings.daoris.space.free')}
        hint={t('settings.daoris.space.terminal')}
        control={(
          <span className="text-small text-ink-soft">
            {space.volume
              ? t('settings.daoris.space.ofVolume', { free: size(space.volume.free), total: size(space.volume.total) })
              : t('settings.daoris.space.volumeUnread')}
          </span>
        )}
      />
      {space.kinds.map((kind) => (
        <SettingRow
          key={kind.id}
          label={t(`settings.daoris.space.kind.${KIND_WORD[kind.id] ?? 'rest'}`)}
          control={<Sized kind={kind} onCount={() => onCount([kind.id])} />}
        >
          <div className="grid gap-1">
            <p className="m-0 text-small text-ink-soft">{ruleWords(t, kind)}</p>
            <NextAndLast kind={kind} />
            {kind.id === 'downloads' && (downloads ?? (onDownloads && (
              <p className="m-0"><Button variant="ghost" onClick={onDownloads}>{t('settings.daoris.downloads.open')}</Button></p>
            )))}
          </div>
        </SettingRow>
      ))}
    </WiringCard>
  );
}
