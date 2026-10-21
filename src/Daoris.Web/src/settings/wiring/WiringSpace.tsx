import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { KeepSet, SpaceKind, SystemSpace } from '../../data/wiring';
import { Button, Card, CheckField, SettingRow, SkeletonRows } from '../../design/ui';
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

/**
 * The kinds whose rule is one of Daoris's runs, which keep a *last* in `keeping.json` (KEEP1a, KEEP1b; the logs' daily prune, KEEP1d);
 * history's only once the person set an age (KEEP1g).
 */
const RUNS = new Set(['set-aside', 'downloads', 'logs']);

/** Whether a kind's rule is one of Daoris's runs: history's while an age is set. */
const runs = (kind: SpaceKind) => RUNS.has(kind.id) || (kind.id === 'history' && kind.rule.kind === 'days');

/** What a rule's save came to, for the editor that sent it: done closes it, a refusal is said under it with the editor kept open (R3). */
export type KeepAnswered = { done: () => void; refused: (words: string) => void };

type T = (key: string, options?: Record<string, unknown>) => string;

/** A kind's rule in words: the one that runs, or whose it is. */
function ruleWords(t: T, kind: SpaceKind): string {
  const { rule } = kind;
  if (rule.kind === 'days' && kind.id === 'downloads') return t('settings.daoris.space.rule.downloads', { count: rule.days });
  if (rule.kind === 'days' && kind.id === 'history') return t('settings.daoris.space.rule.history', { count: rule.days });
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

/**
 * A kind's *Change…* editor, inline under its row (KEEP1d, the cleanup design §4.1): its days within the bounds the driver answers, and
 * the choices the kind has, *keep each folder whole* or *until I remove it*; *Save*, *The default* and *Never mind*. A value that is no
 * whole number is said here; one out of its bounds is the driver's refusal, said under the editor, which stays open.
 */
function KeepEditor({ kind, onKeep, onClose }: { kind: SpaceKind; onKeep: (set: KeepSet, answered: KeepAnswered) => void; onClose: () => void }) {
  const { t } = useTranslation();
  const bounds = kind.bounds!;
  const id = kind.id as KeepSet['kind'];
  const [days, setDays] = useState(kind.rule.days === null ? '' : String(kind.rule.days));
  const [whole, setWhole] = useState(kind.rule.whole === true);
  // History's press alone is its `never` (KEEP1g).
  const [never, setNever] = useState(kind.rule.kind === 'never' || kind.rule.kind === 'press');
  const [saving, setSaving] = useState(false);
  const [said, setSaid] = useState<string | null>(null);
  const send = (set: KeepSet) => {
    setSaving(true);
    setSaid(null);
    onKeep(set, {
      done: () => { setSaving(false); onClose(); },
      refused: (words) => { setSaving(false); setSaid(words); },
    });
  };
  const save = () => {
    if (never) return send({ kind: id, never: true });
    const count = Number(days);
    if (days.trim() === '' || !Number.isInteger(count)) {
      setSaid(t('settings.daoris.space.edit.notADay'));
      return;
    }
    send(bounds.whole ? { kind: id, days: count, whole } : { kind: id, days: count });
  };
  return (
    <div className="grid gap-2 rounded-control border border-line bg-page/60 px-3 py-2" role="group" aria-label={t('settings.daoris.button.change')}>
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <span className="inline-flex items-center gap-1.5">
          <input
            type="number"
            min={bounds.min}
            max={bounds.max}
            aria-label={t('settings.daoris.space.edit.days')}
            value={days}
            disabled={saving || never}
            onChange={(event) => setDays(event.target.value)}
            className="w-[4.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink"
          />
          <span className="text-small text-ink-soft">{t('settings.daoris.space.edit.unit')}</span>
          <span className="text-small text-ink-faint">{t('settings.daoris.space.edit.bounds', { min: bounds.min, max: bounds.max })}</span>
        </span>
        {bounds.whole && <CheckField checked={whole} onChange={setWhole} disabled={saving} label={t('settings.daoris.space.edit.whole')} />}
        {bounds.never && (
          <CheckField
            checked={never}
            onChange={setNever}
            disabled={saving}
            label={t(id === 'history' ? 'settings.daoris.space.edit.neverClear' : 'settings.daoris.space.edit.never')}
          />
        )}
      </div>
      {id === 'history' && !never && <p className="m-0 text-small text-ink-soft">{t('settings.daoris.space.edit.historyWarn')}</p>}
      <div className="flex flex-wrap items-center gap-2">
        <Button variant="primary" disabled={saving} onClick={save}>
          {saving ? t('settings.daoris.space.edit.saving') : t('settings.daoris.space.edit.save')}
        </Button>
        <Button disabled={saving || kind.rule.default} onClick={() => send({ kind: id, default: true })}>{t('settings.daoris.space.edit.default')}</Button>
        <Button variant="ghost" disabled={saving} onClick={onClose}>{t('common.cancel')}</Button>
      </div>
      {said && <p className="m-0 text-small text-ink-danger" role="alert">{said}</p>}
    </div>
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
  if (runs(kind)) {
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
 * day, never by the page's open. The Downloads row opens the list the Folders card draws (CACHECLEAR1), under its own row here. The
 * rules the person sets carry *Change…* (KEEP1d), the editor under the row. KEEP1e: *Free up space…* in the head lists first, under the
 * head, then presses; a run's line (R2) says the kind it is on, then what went and what was kept by reason. A run under way elsewhere
 * disables it.
 *
 * @remarks
 * A molecule: drawn from props, so every state is reachable without a shell. `space` absent is the first load; `older` a shell that
 * answers no `SYSTEM_SPACE`.
 */
export function WiringSpace({ space, older = false, busy = false, onCount, downloads, onDownloads, onKeep, editing: opened, onFreeUp, freeing }: {
  space?: SystemSpace;
  older?: boolean;
  busy?: boolean;
  /** Count the kinds named, or every kind. */
  onCount: (kinds?: string[]) => void;
  /** The Downloads row's clearing, open (CACHECLEAR1), drawn under that row in place of its press. */
  downloads?: ReactNode;
  onDownloads?: () => void;
  /** Set one kind's rule (KEEP1d); absent, no row offers *Change…*. */
  onKeep?: (set: KeepSet, answered: KeepAnswered) => void;
  /** The kind whose editor is open at first: a story's, so the editor is drawn without a press. */
  editing?: string;
  /** *Free up space…*'s first press, which lists (KEEP1e); absent draws no press. */
  onFreeUp?: () => void;
  /** The list, open, drawn under the card's head. */
  freeing?: ReactNode;
}) {
  const { t } = useTranslation();
  const [editing, setEditing] = useState<string | null>(opened ?? null);
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
          hint={kind.settable && kind.bounds ? t('settings.daoris.space.keepTerminal', { kind: kind.id }) : undefined}
          control={<Sized kind={kind} onCount={() => onCount([kind.id])} />}
        >
          <div className="grid gap-1">
            <p className="m-0 flex flex-wrap items-baseline gap-x-2 text-small text-ink-soft">
              {ruleWords(t, kind)}
              {onKeep && kind.settable && kind.bounds && editing !== kind.id && (
                <Button variant="ghost" onClick={() => setEditing(kind.id)}>{t('settings.daoris.button.change')}</Button>
              )}
            </p>
            {onKeep && kind.settable && kind.bounds && editing === kind.id && (
              <KeepEditor kind={kind} onKeep={onKeep} onClose={() => setEditing(null)} />
            )}
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
