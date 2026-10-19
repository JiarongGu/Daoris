import type { TFunction } from 'i18next';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CheckField, Segmented, SettingRow } from '../design/ui';
import { shellWord } from '../lib/shellWord';
import { Inheritable } from '../projects/setup/SetupParts';
import { type Answered, InlineConfirm } from '../work/InlineConfirm';

/**
 * Where a pause's value was set (AUTONOMY1a, D184 point 1): for the repository, its workspace, this machine, or nowhere, which is
 * today's behaviour (`AccessRules`' sources).
 */
export type AccessSource = 'repository' | 'workspace' | 'machine' | 'default';

/** Whose setup a page shows and sets: a repository's, a workspace's or this machine's. */
export type AccessOwner = Exclude<AccessSource, 'default'>;

/** One pause as it resolves (`LINES`' `access`): its name, its value and where that was set; a newer driver may name others. */
export type ResolvedPause = { pause: string; value: string; source: AccessSource | (string & {}) };

/**
 * One repository's access setup as the driver resolves it (`LINES`' `access`, AUTONOMY1a): whether every pause is open, and each
 * pause in the order every door says them. Never null for a repository the driver judged; absent on a shell older than it.
 */
export type RepositoryAccess = { repository: string; workspace: string; full: boolean; pauses: ResolvedPause[] };

/** One level as it is set (`STATE`'s `access`, `workspaceAccess`, `machineAccess`): full access, or the pauses it names. */
export type AccessLevel = { full: boolean; pauses: { pause: string; value: string }[] };

/**
 * One change to a level (`SET_ACCESS`'s `access`): `full`, the pauses it sets (a null value taking one back to the level above),
 * or null to clear the level.
 */
export type AccessEdit = { access: 'full' | Record<string, string | null> | null };

/** The edit for a repository, a workspace or this machine, as `SET_ACCESS` takes it: exactly one of the three. */
export type AccessChange = ({ repository: string } | { workspace: string } | { machine: true }) & AccessEdit;

/**
 * The pauses, each with its values, today's and the open one, in the order every door says them: the twins' `AccessRules.Pauses`
 * (D184 point 3). *Questions* has one value, every setup's since AUTONOMY1e, so it is shown and never chosen.
 */
const PAUSES: Record<string, { values: readonly string[]; today: string; open: string }> = {
  permissions: { values: ['asking', 'auto', 'unrestricted'], today: 'auto', open: 'unrestricted' },
  routing: { values: ['person', 'intake'], today: 'person', open: 'intake' },
  landing: { values: ['person', 'rule'], today: 'person', open: 'rule' },
  goAheads: { values: ['named', 'never'], today: 'named', open: 'never' },
  questions: { values: ['claude-code'], today: 'claude-code', open: 'claude-code' },
};

const ORDER = Object.keys(PAUSES);

/** The pauses a person chooses, each at today's value: what turning full access off sets where the level above is full. */
export const TODAY: Record<string, string> = Object.fromEntries(
  ORDER.filter((pause) => PAUSES[pause]!.values.length > 1).map((pause) => [pause, PAUSES[pause]!.today]),
);

/**
 * Each pause from the nearest level that sets it, nearest first, a full level giving every pause its open value; else today's,
 * from `default`. The driver's `AccessRules.ResolveLevels`, for the levels a page holds as set: a workspace's over this machine's,
 * and what stands above a repository. A repository's own resolution is the driver's answer (`LINES`), read rather than recomputed.
 */
export function resolveAccess(levels: readonly { setting?: AccessLevel | null; source: AccessSource }[]): ResolvedPause[] {
  return ORDER.map((pause) => {
    for (const { setting, source } of levels) {
      if (!setting) continue;
      if (setting.full) return { pause, value: PAUSES[pause]!.open, source };
      const set = setting.pauses.find((each) => each.pause === pause);
      if (set) return { pause, value: set.value, source };
    }
    return { pause, value: PAUSES[pause]!.today, source: 'default' };
  });
}

/** Whether every pause is open: full access. A pause this page does not know is never read as open, so nothing reads looser. */
export const isFull = (pauses: readonly ResolvedPause[]) =>
  pauses.length > 0 && pauses.every((each) => PAUSES[each.pause]?.open === each.value);

/** The pauses this page knows, in their order; one a newer driver names is passed over, as the driver passes over one it does not. */
const known = (pauses: readonly ResolvedPause[]) => pauses.filter((each) => each.pause in PAUSES);

/** The one place every pause came from, or null where they came from several. */
const sourceOf = (pauses: readonly ResolvedPause[]) => {
  const sources = new Set(pauses.map((each) => each.source));
  return sources.size === 1 ? [...sources][0]! : null;
};

/** A value from above, marked with where it came from, as a folded line marks one; the owner's own is unmarked. */
function marked(t: TFunction, value: string, source: string | null, owner: AccessOwner): string {
  if (source === 'workspace' && owner !== 'workspace') return t('projects.setup.marked.workspace', { value });
  if (source === 'machine' && owner !== 'machine') return t('projects.setup.marked.machine', { value });
  if (source === 'default') return t('projects.setup.marked.daoris', { value });
  return value;
}

/** A known value's catalogue part, or null for one a newer driver answers. */
const valueKey = (pause: string, value: string) => (PAUSES[pause]?.values.includes(value) ? `${pause}.${value}` : null);

/** A value's short name, the choice's label: the value itself, as code, where this page does not know it. */
const valueName = (t: TFunction, pause: string, value: string) => {
  const key = valueKey(pause, value);
  return key ? t(`settings.access.value.${key}`) : `\`${value}\``;
};

/**
 * A setup in a folded section's line: full access, else each chosen pause's value, marked where it came from above; one mark at
 * the end where every pause came from one place.
 */
export function accessSummary(t: TFunction, pauses: readonly ResolvedPause[], owner: AccessOwner): string {
  const shown = known(pauses);
  if (isFull(pauses)) return marked(t, t('settings.access.summary.full'), sourceOf(shown), owner);
  const chosen = shown.filter((each) => PAUSES[each.pause]!.values.length > 1);
  const word = (each: ResolvedPause) => {
    const key = valueKey(each.pause, each.value);
    return key ? t(`settings.access.summary.${key}`) : `${t(`settings.access.pause.${each.pause}`)} \`${each.value}\``;
  };
  const join = t('settings.access.summary.join');
  const one = sourceOf(chosen);
  return one
    ? marked(t, chosen.map(word).join(join), one, owner)
    : chosen.map((each) => marked(t, word(each), each.source, owner)).join(join);
}

/** What a change said once the driver took it; `machine` names this machine, whose sentences say *on* it. */
export function accessToast(t: TFunction, name: string, edit: AccessEdit, machine: boolean): string {
  const access = edit.access;
  if (access === 'full') return t(machine ? 'settings.access.savedFull.machine' : 'settings.access.savedFull.named', { name });
  if (access === null) return t(machine ? 'settings.access.cleared.machine' : 'settings.access.cleared.named', { name });
  const entries = Object.entries(access);
  if (entries.length === Object.keys(TODAY).length && entries.every(([pause, value]) => TODAY[pause] === value)) {
    return t(machine ? 'settings.access.savedToday.machine' : 'settings.access.savedToday.named', { name });
  }
  const [pause, value] = entries[0] ?? ['', null];
  const label = t(`settings.access.pause.${pause}`);
  return value === null
    ? t('settings.access.pauseCleared', { pause: label, name })
    : t('settings.access.saved', { pause: label, name, value: valueName(t, pause, value).replace(/`/g, '') });
}

/** Where a value came from, said in the row, its owner's own as its own. */
function fromSays(t: TFunction, source: string | null, owner: AccessOwner, workspace?: string): string {
  if (source === owner) return t(`settings.access.from.own.${owner}`);
  if (source === 'workspace') return t('settings.access.from.workspace', { workspace });
  if (source === 'machine') return t('settings.access.from.machine');
  if (source === 'default') return t('settings.access.from.default');
  return source === null ? t('settings.access.from.mixed') : '';
}

const WARN_LINE = 'm-0 border-l-[3px] border-warn bg-raised px-3 py-1.5 text-small text-ink';

/**
 * **A level's access rows** (AUTONOMY1d, D184 points 1-3, 5): the full-access switch, then each pause with what it means, its
 * value and where that was set, the same rows on a repository's Setup, a workspace's and Settings → Driver for this machine.
 *
 * @remarks
 * **Full access asks once** (the Ask Daoris modes design §5's shape): its press opens an inline ask saying what it turns off and
 * what stays, and only *Enable full access* sends it, waiting for the driver and saying a refusal inside the ask. Turning it off
 * asks nothing, since nothing loosens: it clears this level, or where the level above is full too, which clearing would leave in
 * force, sets today's pauses here. On, it wears the warning colour.
 *
 * **Each pause is an `Inheritable`**: one from above says where it came from and offers *Set for this …*, which opens its choice
 * in place; one set here carries its choice and *Clear*, which takes it back to the level above. *Questions* is shown with no
 * choice, since every setup asks as Claude Code does (AUTONOMY1e). Each row's hint is its terminal twin, `daoris-driver access`.
 *
 * A molecule: every state is reached by its props, and every press goes out.
 */
export function AccessRows({ owner, name, workspace, pauses, aboveFull, busy = false, setHere, onChange }: {
  owner: AccessOwner;
  /** The repository's or the workspace's name; this machine has none. */
  name?: string;
  /** A repository's workspace, which a value from it names. */
  workspace?: string;
  /** Each pause as it resolves at this level, with where it was set. */
  pauses: readonly ResolvedPause[];
  /** Whether what stands above this level is full access, so turning it off here sets today's pauses rather than clearing. */
  aboveFull: boolean;
  busy?: boolean;
  /** The press that sets a pause at this level: *Set for this repository*, *… workspace*, *… machine*. */
  setHere: string;
  /** A change at this level; full access's carries how to answer its ask. */
  onChange: (edit: AccessEdit, answered?: Answered) => void;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const shown = known(pauses);
  const full = isFull(pauses);
  const target = owner === 'machine' ? t('settings.access.thisMachine') : name ?? '';
  const twin = (kind: 'full' | 'pause', values: Record<string, string> = {}) => t(`settings.access.twin.${kind}.${owner}`, {
    ...values,
    repository: owner === 'repository' && name ? shellWord(name, '<repository>') : undefined,
    workspace: owner === 'workspace' && name ? shellWord(name, '<workspace>') : undefined,
  });
  const sentence = t('projects.setup.sentenceJoin');

  return (
    <>
      <SettingRow
        label={t('settings.access.full.label')}
        hint={twin('full')}
        why={t('settings.access.full.body')}
        control={(
          <CheckField
            hideLabel
            checked={full}
            disabled={busy || asking}
            label={t('settings.access.full.label')}
            onChange={(on) => (on ? setAsking(true) : onChange({ access: aboveFull ? { ...TODAY } : null }))}
          />
        )}
      >
        <p className={full ? WARN_LINE : 'm-0 text-small text-ink-soft'}>
          {full
            ? [t('settings.access.full.on'), fromSays(t, sourceOf(shown), owner, workspace)].filter(Boolean).join(sentence)
            : t('settings.access.full.off')}
        </p>
        <p className="m-0 mt-1 text-small text-ink-faint">
          {[t('settings.access.full.offs'), t('settings.access.full.stays')].join(sentence)}
        </p>
        {asking && (
          <InlineConfirm
            className="mt-2 border-l-[3px] border-l-warn"
            tone="primary"
            label={t('settings.access.confirm.label', { name: target })}
            says={t(owner === 'machine' ? 'settings.access.confirm.machine' : 'settings.access.confirm.named', { name: target })}
            meanIt={t('settings.access.confirm.enable')}
            busy={busy}
            onConfirm={(answered) => onChange({ access: 'full' }, answered)}
            onClose={() => setAsking(false)}
          />
        )}
      </SettingRow>
      {shown.map((each) => {
        const pause = PAUSES[each.pause]!;
        const label = t(`settings.access.pause.${each.pause}`);
        const key = valueKey(each.pause, each.value);
        const says = key ? t(`settings.access.says.${key}`) : `${label}: \`${each.value}\``;
        if (pause.values.length === 1) {
          return (
            <SettingRow key={each.pause} label={label}>
              <p className="m-0 text-small text-ink-soft">{t('settings.access.says.questions')}</p>
            </SettingRow>
          );
        }
        return (
          <Inheritable
            key={each.pause}
            setHere={setHere}
            label={label}
            twin={twin('pause', { pause: each.pause, values: pause.values.join('|') })}
            why={t(`settings.access.why.${each.pause}`)}
            says={[says, fromSays(t, each.source, owner, workspace)].filter(Boolean).join(sentence)}
            own={each.source === owner}
            busy={busy}
            onClear={() => onChange({ access: { [each.pause]: null } })}
            editor={() => (
              <Segmented
                label={t('settings.access.field', { pause: label, name: target })}
                value={each.value}
                options={pause.values.map((value) => ({ value, label: valueName(t, each.pause, value) }))}
                onChange={(value) => !busy && onChange({ access: { [each.pause]: value } })}
              />
            )}
          />
        );
      })}
    </>
  );
}
