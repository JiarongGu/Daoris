import type { TFunction } from 'i18next';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Segmented, SettingRow } from '../../design/ui';
import { shellWord } from '../../lib/shellWord';
import { Inheritable } from '../../projects/setup/SetupParts';
import { type Answered, InlineConfirm, Refused } from '../../work/InlineConfirm';

/** Where a class's value is set: the same sources as an access pause. */
export type GitAccessSource = 'repository' | 'workspace' | 'machine' | 'default';

/** Whose git access a page shows and sets: a repository's, a workspace's or this machine's. */
export type GitAccessOwner = Exclude<GitAccessSource, 'default'>;

/** One class as it resolves (`LINES`' `gitAccess`): its value, `as-today` for a Write nothing set, and where it is set. */
export type ResolvedGitClass = { class: string; value: string; source: GitAccessSource | (string & {}) };

/** One repository's git access as the driver resolves it; absent where the answer lacks it. */
export type RepositoryGitAccess = { repository: string; workspace: string; classes: ResolvedGitClass[] };

/** One level as it is set (`STATE`'s `gitAccess`, `workspaceGitAccess`, `machineGitAccess`). */
export type GitAccessLevel = { class: string; value: string }[];

/** One change to a level: the classes it sets (a null taking one back to the level above), one value for both, or the level cleared. */
export type GitAccessEdit = { access: Record<string, string | null> | null } | { oneValue: string };

/** The edit for a repository, a workspace or this machine, as `SET_GIT_ACCESS` takes it: exactly one of the three. */
export type GitAccessChange = ({ repository: string } | { workspace: string } | { machine: true }) & GitAccessEdit;

/** The classes a person sets, in the order every door says them. */
const CLASSES = ['read', 'write', 'dangerous'] as const;
/** The classes *Use one value for both…* sets. */
const PAIRED = ['read', 'write'] as const;
const VALUES = ['allow', 'ask', 'deny'] as const;
/** Dangerous is decided at the door and waits on no one: it is allowed or refused, never asked. */
const DANGEROUS_VALUES = ['allow', 'deny'] as const;
export const valuesOf = (cls: string) => (cls === 'dangerous' ? DANGEROUS_VALUES : VALUES);

/** How loose a value is, as the driver's `GitAccessKeys.Looseness`: allow 0, ask 1, Daoris's default 2, deny 3. */
export const looseness = (value: string) => (value === 'allow' ? 0 : value === 'ask' ? 1 : value === 'deny' ? 3 : 2);

/** Each class from the nearest level that sets it, nearest first; else Daoris's default. A repository's own resolution is the driver's answer. */
export function resolveGitAccess(levels: readonly { classes?: GitAccessLevel | null; source: GitAccessSource }[]): ResolvedGitClass[] {
  const defaults: Record<string, string> = { read: 'allow', write: 'as-today', dangerous: 'deny' };
  return CLASSES.map((name) => {
    for (const { classes, source } of levels) {
      const set = classes?.find((each) => each.class === name);
      if (set) return { class: name, value: set.value, source };
    }
    return { class: name, value: defaults[name]!, source: 'default' };
  });
}

/** What a loosening lets happen, in the confirmation's words; null where the change is no loosening. */
export function wideningSays(t: TFunction, name: string, cls: string, now: string, next: string): string | null {
  if (looseness(next) >= looseness(now)) return null;
  const key = cls === 'read' ? 'settings.git.access.confirm.read'
    : cls === 'dangerous' ? 'settings.git.access.confirm.dangerous'
      : `settings.git.access.confirm.write.${next === 'allow' ? 'allow' : 'ask'}`;
  return t(key, { name });
}

export const valueName = (t: TFunction, value: string) => (value === 'as-today' ? t('settings.git.access.asToday') : t(`settings.git.access.value.${value}`, { defaultValue: `\`${value}\`` }));

/** A level's git access folded to a line (a Setup section's summary): each class and its value, Daoris's default named once. */
export function gitAccessSummary(t: TFunction, classes: readonly ResolvedGitClass[]): string {
  return t('settings.git.access.summary', {
    classes: classes.map((each) => t('settings.git.access.summaryClass', {
      class: t(`settings.git.access.class.${each.class}`, { defaultValue: each.class }),
      value: each.value === 'as-today' ? t('settings.git.access.summaryDefault') : valueName(t, each.value),
    })).join(', '),
  });
}

/**
 * **A level's git access rows**: Read, Write and Dangerous (allow or deny), each its
 * value and where it came from, and *Use one value for both…* for Read and Write. The same rows on a repository's Setup, a workspace's and Settings → Git for this machine.
 *
 * @remarks
 * **A loosening is confirmed here**: a choice looser than what stands opens an `InlineConfirm` in the warning tone naming what it
 * lets happen, and only its press sends it; a tightening and a clear send at once. The route carries no flag for it. A refusal is
 * said under the row that is pressed, in the driver's sentence. A molecule: every state is reached by its props.
 */
export function GitAccessRows({ owner, name, workspace, classes, above, busy = false, onChange }: {
  owner: GitAccessOwner;
  /** The repository's or the workspace's name; this machine has none. */
  name?: string;
  /** A repository's workspace, which a value from it names. */
  workspace?: string;
  /** Each class as it resolves at this level, with where it is set. */
  classes: readonly ResolvedGitClass[];
  /** Each class as the level above resolves it (this machine's: Daoris's defaults), which a *Clear* gives the class back to. */
  above: readonly ResolvedGitClass[];
  busy?: boolean;
  /** A change at this level, and how to answer the row it is pressed in. */
  onChange: (edit: GitAccessEdit, answered?: Answered) => void;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState<{ row: string; edit: GitAccessEdit; says: string } | null>(null);
  const [refusals, setRefusals] = useState<Record<string, string>>({});
  const turns = useRef<Record<string, number>>({});
  const target = owner === 'machine' ? t('settings.git.access.confirm.name.machine') : name ?? '';
  const setHere = t(`settings.git.access.setHere.${owner}`);
  const twin = (cls: string) => t(`settings.git.access.${cls === 'dangerous' ? 'twinDangerous' : 'twin'}.${owner}`, {
    class: cls,
    repository: owner === 'repository' && name ? shellWord(name, '<repository>') : undefined,
    workspace: owner === 'workspace' && name ? shellWord(name, '<workspace>') : undefined,
  });
  const sentence = t('projects.setup.sentenceJoin');
  const fromSays = (source: string) => source === owner ? t('settings.git.access.from.own')
    : source === 'workspace' ? t('settings.git.access.from.workspace', { workspace })
      : source === 'machine' ? t('settings.git.access.from.machine') : t('settings.git.access.from.default');

  // A press in a row: its last refusal goes, and the edit is sent with how to answer that row.
  const send = (row: string, edit: GitAccessEdit) => {
    const mine = (turns.current[row] ?? 0) + 1;
    turns.current[row] = mine;
    const say = (said: string | null) => {
      if (turns.current[row] !== mine) return;
      setRefusals(({ [row]: _gone, ...rest }) => (said === null ? rest : { ...rest, [row]: said }));
    };
    say(null);
    onChange(edit, { done: () => say(null), refused: say });
  };
  const choose = (row: string, cls: string, now: string, next: string, edit: GitAccessEdit) => {
    const says = wideningSays(t, target, cls, now, next);
    if (says) setAsking({ row, edit, says });
    else send(row, edit);
  };
  const resolved = (cls: string) => classes.find((each) => each.class === cls);
  const read = resolved('read');
  const write = resolved('write');
  const asked = (row: string) => asking?.row === row ? (
    <InlineConfirm
      className="mt-2"
      tone="warn"
      label={t('settings.git.access.confirm.label', { name: target })}
      says={asking.says}
      meanIt={t('settings.git.access.confirm.meanIt')}
      busy={busy}
      onConfirm={(answered) => onChange(asking.edit, answered)}
      onClose={() => setAsking(null)}
    />
  ) : null;

  return (
    <>
      {CLASSES.map((cls) => {
        const each = resolved(cls);
        if (!each) return null;
        const label = t(`settings.git.access.class.${cls}`);
        const says = [
          each.value === 'as-today' ? t('settings.git.access.asToday') : t(`settings.git.access.says.${cls}`, { value: valueName(t, each.value) }),
          fromSays(each.source),
        ].filter(Boolean).join(sentence);
        return (
          <Inheritable
            key={cls}
            setHere={setHere}
            label={label}
            twin={twin(cls)}
            why={t(`settings.git.access.why.${cls}`)}
            says={says}
            own={each.source === owner}
            busy={busy}
            refusal={refusals[cls] ?? null}
            onClear={() => choose(cls, cls, each.value, above.find((one) => one.class === cls)?.value ?? each.value, { access: { [cls]: null } })}
            editor={() => (
              <Segmented
                label={t('settings.git.access.field', { class: label, name: target })}
                value={each.value}
                options={valuesOf(cls).map((value) => ({ value, label: t(`settings.git.access.value.${value}`) }))}
                onChange={(value) => !busy && choose(cls, cls, each.value, value, { access: { [cls]: value } })}
              />
            )}
          >
            {asked(cls)}
          </Inheritable>
        );
      })}
      {read && write && (
        <SettingRow label={t('settings.git.access.oneValue.label')} hint={twin('read')} why={t('settings.git.access.oneValue.why')}
          control={(
            <Segmented
              label={t('settings.git.access.oneValue.field', { name: target })}
              value={read.value === write.value ? read.value : ''}
              options={VALUES.map((value) => ({ value, label: t(`settings.git.access.value.${value}`) }))}
              onChange={(value) => {
                if (busy) return;
                const loosens = PAIRED.some((cls) => looseness(value) < looseness(resolved(cls)!.value));
                const says = loosens ? wideningSays(t, target, 'write', write.value, value) ?? wideningSays(t, target, 'read', read.value, value) : null;
                if (says) setAsking({ row: 'one', edit: { oneValue: value }, says });
                else send('one', { oneValue: value });
              }}
            />
          )}
        >
          {refusals.one && <Refused className="mt-2" sentence={refusals.one} />}
          {asked('one')}
        </SettingRow>
      )}
    </>
  );
}
