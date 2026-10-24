import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { SpacePlan, SpaceRun, SpaceStarted, SpaceUnit } from '../../data/wiring';
import { Button, Prose } from '../../design/ui';
import { clockOf, size } from '../../locales/format';

type T = (key: string, options?: Record<string, unknown>) => string;

/** Each kind's and group's word in `settings.daoris.space.kind.*`, by its id. */
export const FREE_KIND: Record<string, string> = {
  'set-aside': 'setAside', downloads: 'downloads', logs: 'logs', 'browser-cache': 'browserCache', 'left-over': 'leftOver',
  'update-failed': 'updateFailed',
};

/** Why a run kept a unit, in `settings.daoris.space.kept.*`; a code this page does not know is said as it came. */
export const KEPT_WORD = new Set([
  'changed', 'gone', 'refused', 'failed', 'running', 'no-shell', 'in-use', 'named', 'updating', 'written', 'linked', 'unread', 'pinned',
  'used', 'recent',
]);

const total = (units: readonly SpaceUnit[]) => (units.every((unit) => unit.bytes !== null) ? units.reduce((sum, unit) => sum + (unit.bytes ?? 0), 0) : null);

/** The size of some units, where every one is known; nothing where one is not. */
function sized(t: T, words: string, units: readonly SpaceUnit[]) {
  const bytes = total(units);
  return bytes === null ? words : t('settings.daoris.space.whatSized', { what: words, size: size(bytes) });
}

/** What a rule takes now of one kind, in words: its folders past their days, a slim's package folders, versions, days of the log. */
function nowWords(t: T, kind: string, units: readonly SpaceUnit[]): string[] {
  if (kind === 'set-aside') {
    const past = units.filter((unit) => unit.why === 'past');
    const slim = units.filter((unit) => unit.why === 'slim');
    return [
      ...(past.length ? [t('settings.daoris.space.freeing.now.past', { count: past.length })] : []),
      ...(slim.length ? [t('settings.daoris.space.freeing.now.slim', { count: slim.length })] : []),
    ];
  }
  return [t(`settings.daoris.space.freeing.now.${FREE_KIND[kind] ?? 'setAside'}`, { count: units.length })];
}

/** A kept reason in words, with how many. */
export function keptWords(t: T, kept: Record<string, number>): string {
  return Object.entries(kept)
    .map(([why, count]) => t('settings.daoris.space.kept.counted', {
      count,
      why: KEPT_WORD.has(why) ? t(`settings.daoris.space.kept.${why}`) : why,
    }))
    .join('; ');
}

/**
 * A run's line (R2, the cleanup design §4.2): under way, the kind it is on, how many of the run's kinds, and since when; ended, what it
 * removed and what it kept by reason. The time is the driver's, never a clock of the page's own (FREEZE1).
 */
export function SpaceRunLine({ run }: { run: SpaceRun }) {
  const { t } = useTranslation();
  const kind = (id: string) => t(`settings.daoris.space.kind.${FREE_KIND[id] ?? 'rest'}`);
  const removed = run.kinds.reduce((sum, each) => sum + each.removed.count, 0);
  const bytes = run.kinds.reduce((sum, each) => sum + each.removed.bytes, 0);
  const kept: Record<string, number> = {};
  for (const each of run.kinds) for (const [why, count] of Object.entries(each.kept)) kept[why] = (kept[why] ?? 0) + count;
  const keptCount = Object.values(kept).reduce((sum, count) => sum + count, 0);
  if (!run.outcome) {
    const now = run.kinds.find((each) => each.state === 'running') ?? run.kinds.find((each) => each.state === 'waiting');
    const at = run.kinds.filter((each) => each.state === 'done').length + 1;
    return (
      <p className="m-0 border-l-[3px] border-accent bg-page/60 px-3.5 py-2 text-body text-ink-soft" role="status">
        <span aria-hidden className="mr-1.5 inline-block h-2 w-2 rounded-full bg-accent" />
        {t('settings.daoris.space.freeing.running', {
          kind: now ? kind(now.id) : '', at: Math.min(at, run.kinds.length), of: run.kinds.length, when: clockOf(run.started),
        })}
      </p>
    );
  }
  const ended = run.ended ? clockOf(run.ended) : '';
  return (
    <div className="grid gap-0.5 border-l-[3px] border-line bg-page/60 px-3.5 py-2 text-body text-ink-soft" role="status">
      <p className="m-0">
        {run.outcome === 'stopped'
          ? t('settings.daoris.space.freeing.stopped', { when: ended, count: removed, size: size(bytes) })
          : keptCount > 0
            ? t('settings.daoris.space.freeing.kept', { when: ended, removed, count: removed + keptCount, size: size(bytes) })
            : t('settings.daoris.space.freeing.done', { when: ended, count: removed, size: size(bytes) })}
      </p>
      {keptCount > 0 && <p className="m-0 text-small text-ink-faint">{keptWords(t, kept)}</p>}
    </div>
  );
}

/** One early group: its words, its size, and its own press, said first in its own words. */
function EarlyGroup({ group, busy, onRemove }: { group: SpacePlan['early'][number]; busy: boolean; onRemove: (units: SpaceUnit[]) => void }) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const word = FREE_KIND[group.group] ?? 'setAside';
  const details: string[] = [];
  // KEEPSIZE1b: a folder awaiting its record, or whose manifest does not read, has no date, so it is not counted among those before theirs
  // (the second maker's read, finding 9; the last fixes' check, finding 9).
  const dated = group.count - (group.classes?.awaiting ?? 0) - (group.classes?.unread ?? 0);
  if (dated > 0) details.push(t(`settings.daoris.space.freeing.early.${word}`, { count: dated }));
  // An earlier build's folders are named by their paths, which are content and stay as written.
  if (group.group === 'left-over') details.push(group.units.map((unit) => unit.id).join(', '));
  if (group.own > 0) details.push(t('settings.daoris.space.freeing.early.own', { count: group.own }));
  // KEEPSIZE1b: each class the list can say of the set-aside folders, so the person weighs what goes before pressing.
  for (const each of ['recorded', 'awaiting', 'stands', 'stopped', 'unheld', 'unread', 'unmarked'] as const) {
    const count = group.classes?.[each] ?? 0;
    if (count > 0) details.push(t(`settings.daoris.space.freeing.early.class.${each}`, { count }));
  }
  // KEEPSIZE1b: and each folder with what the list can say of it, its holder named (Codex's read of the branch, finding 12). The folder's
  // name and the ref are content, as written.
  const folders = group.group === 'set-aside' ? group.units.filter((unit) => unit.class) : [];
  return (
    <li className="grid gap-1">
      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1 text-small">
        <span className="text-ink">{t(`settings.daoris.space.kind.${word}`)}</span>
        <span className="text-ink-soft">{details.join(' · ')}</span>
        {group.bytes !== null && <span className="text-ink-faint">{size(group.bytes)}</span>}
        {!asking && <Button variant="ghost" disabled={busy} onClick={() => setAsking(true)}>{t('settings.daoris.space.freeing.remove')}</Button>}
      </div>
      {folders.length > 0 && (
        <ul className="m-0 grid list-none gap-0.5 p-0 text-small text-ink-faint">
          {folders.map((unit) => (
            <li key={unit.id}>
              {t(`settings.daoris.space.freeing.folder.${unit.class === 'recorded' && !unit.ref ? 'recordedLine' : unit.class}`, {
                folder: unit.id, ref: unit.ref ?? '',
                defaultValue: t('settings.daoris.space.freeing.folder.unmarked', { folder: unit.id }),
              })}
            </li>
          ))}
        </ul>
      )}
      {asking && (
        <div className="grid gap-1.5 border-l-[3px] border-warn pl-3">
          <Prose className="text-small text-ink-soft">{t(`settings.daoris.space.freeing.say.${word}`)}</Prose>
          <div className="flex flex-wrap gap-2">
            <Button variant="danger" disabled={busy} onClick={() => { setAsking(false); onRemove(group.units); }}>
              {t('settings.daoris.space.freeing.confirm', { count: group.count })}
            </Button>
            <Button variant="ghost" disabled={busy} onClick={() => setAsking(false)}>{t('settings.daoris.space.freeing.neverMind')}</Button>
          </div>
        </div>
      )}
    </li>
  );
}

/**
 * *Free up space…*, listed (KEEP1e, the cleanup design §3.3, §4.1): drawn under the card's head as the Downloads row's list is drawn
 * under its row. What each rule takes now, with one press for all of it; what could go early by the person's word, each group with its
 * own press said first in its own words; and the kinds with a press of their own. Each press sends exactly the units shown; the driver
 * judges each again, so the run's line may say one was kept since. A refusal is said here, where the press was made (R3).
 *
 * @remarks A molecule: drawn from props, so every state is reachable without a shell. `plan` absent is the list on its way.
 */
export function WiringSpaceFree({ plan, busy = false, refused = null, onFree, onClose }: {
  plan?: SpacePlan;
  busy?: boolean;
  /** The last press's refusal: another run holds Daoris's keeping. */
  refused?: SpaceStarted | null;
  onFree: (units: { now: SpaceUnit[]; early: SpaceUnit[] }) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const now = plan?.now ?? [];
  const kinds = [...new Set(now.map((unit) => unit.kind))];
  const nowBytes = total(now);
  const items = t('settings.daoris.space.freeing.items', { count: now.length });
  return (
    <section aria-label={t('settings.daoris.button.freeUp')} className="grid gap-3 border-t border-line pt-2">
      {!plan && <p className="m-0 text-small text-ink-faint">{t('settings.daoris.space.freeing.listing')}</p>}
      {plan && (
        <>
          <div className="grid gap-1.5">
            <div className="flex flex-wrap items-baseline justify-between gap-x-4">
              <h4 className="m-0 text-small font-semibold text-ink">{t('settings.daoris.space.freeing.nowTitle')}</h4>
              {now.length > 0 && <span className="text-small text-ink-soft">{nowBytes === null ? items : t('settings.daoris.space.whatSized', { what: items, size: size(nowBytes) })}</span>}
            </div>
            {now.length === 0 && <p className="m-0 text-small text-ink-soft">{t('settings.daoris.space.freeing.nowNone')}</p>}
            {kinds.length > 0 && (
              <ul className="m-0 grid list-none gap-1 p-0">
                {kinds.map((kind) => {
                  const units = now.filter((unit) => unit.kind === kind);
                  return (
                    <li key={kind} className="flex flex-wrap items-baseline gap-x-3 text-small">
                      <span className="text-ink">{t(`settings.daoris.space.kind.${FREE_KIND[kind] ?? 'rest'}`)}</span>
                      <span className="text-ink-soft">{sized(t, nowWords(t, kind, units).join(' · '), units)}</span>
                    </li>
                  );
                })}
              </ul>
            )}
            <div className="flex flex-wrap gap-2">
              <Button variant="primary" disabled={busy || now.length === 0} onClick={() => onFree({ now, early: [] })}>
                {t('settings.daoris.space.freeing.removeNow', { count: now.length })}
              </Button>
              <Button variant="ghost" disabled={busy} onClick={onClose}>{t('settings.daoris.space.freeing.neverMind')}</Button>
            </div>
          </div>
          <div className="grid gap-1.5">
            <h4 className="m-0 text-small font-semibold text-ink">{t('settings.daoris.space.freeing.earlyTitle')}</h4>
            {plan.early.length === 0
              ? <p className="m-0 text-small text-ink-soft">{t('settings.daoris.space.freeing.earlyNone')}</p>
              : (
                <ul className="m-0 grid list-none gap-1.5 p-0">
                  {plan.early.map((group) => (
                    <EarlyGroup key={group.group} group={group} busy={busy} onRemove={(units) => onFree({ now: [], early: units })} />
                  ))}
                </ul>
              )}
          </div>
          {Object.keys(plan.unread).map((kind) => (
            <p key={kind} className="m-0 text-small text-ink-faint">
              {t('settings.daoris.space.freeing.unread', { kind: t(`settings.daoris.space.kind.${FREE_KIND[kind] ?? 'rest'}`) })}
            </p>
          ))}
          {plan.doors.length > 0 && <p className="m-0 text-small text-ink-faint">{t('settings.daoris.space.freeing.doors')}</p>}
        </>
      )}
      {refused?.why === 'held' && (
        <p className="m-0 text-small text-warn" role="alert">
          {t('settings.daoris.space.freeing.held', {
            door: t(`settings.daoris.space.door.${refused.held?.door ?? 'look'}`, { defaultValue: refused.held?.door ?? '' }),
            when: refused.held ? clockOf(refused.held.since) : '',
          })}
        </p>
      )}
    </section>
  );
}
