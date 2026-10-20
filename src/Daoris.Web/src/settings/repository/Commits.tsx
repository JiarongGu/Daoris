import type { TFunction } from 'i18next';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Segmented } from '../../design/ui';

/**
 * A commit rule as the driver answers it (PROCESS1a, D164's amendment point 1): how a session commits its work in its own tree,
 * `as-you-go` (the default), `ask` (each commit waits for the person's yes) or `at-end` (one commit when the work is done), and
 * the person's convention, how a commit message is written, quoted to each session as written. None is left out.
 */
export type CommitRule = { mode: string; convention?: string | null };

/** Where a repository's rule was set: for it, for its workspace, or nowhere, which is the default's. */
export type CommitSource = 'repository' | 'workspace' | 'default';

/** One repository's rule as the driver resolves it (`LINES`' `commits`): never null, its source `default` where nothing is set. */
export type RepositoryCommits = { repository: string; workspace: string; rule?: CommitRule | null; source?: CommitSource | null };

/** What a set changes, each part only where it is said: the twins' edit; the empty convention takes it away. */
export type CommitSet = { mode?: string; convention?: string };

/** One change to a commit rule: set what it names over the rule set there, or clear. */
export type CommitEdit = { set?: CommitSet; clear?: true };

/** The edit for a repository or a workspace, as `SET_COMMITS` takes it. */
export type CommitChange = { repository?: string; workspace?: string } & CommitEdit;

/** What stands where nothing is set: the twins' `CommitRule.Default`. */
export const COMMIT_DEFAULT: CommitRule = { mode: 'as-you-go' };

/** The longest convention the twins keep, in characters: `CommitRules.ConventionLimit`. */
const CONVENTION_LIMIT = 400;

const MODES = ['as-you-go', 'ask', 'at-end'] as const;

/** A mode's catalogue word: the catalogue's keys are its own names, never the wire's spelling. */
const MODE_KEY: Record<string, string> = { 'as-you-go': 'asYouGo', ask: 'ask', 'at-end': 'atEnd' };
const modeKey = (mode: string) => MODE_KEY[mode] ?? 'asYouGo';

/**
 * What each door says of a rule (DOORS1b), in the page's language: its mode in the terminal's sentence (`driver commits`), then
 * the person's convention, quoted as written.
 */
export function commitSays(t: TFunction, rule: CommitRule): string[] {
  return [
    t(`settings.commits.says.${modeKey(rule.mode)}`),
    ...(rule.convention ? [t('settings.commits.says.convention', { convention: rule.convention })] : []),
  ];
}

/** What a Setup row says of the rule standing: the door's sentences, then where it was set, the default included. */
export function commitRowSays(t: TFunction, rule: CommitRule, from: { source?: CommitSource | null; workspace?: string }): string {
  return [
    ...commitSays(t, rule),
    from.source === 'repository' ? t('settings.commits.from.repository')
      : from.source === 'workspace' ? t('settings.commits.from.workspace', { workspace: from.workspace })
        : t('settings.commits.from.default'),
  ].join(t('projects.setup.sentenceJoin'));
}

/** A rule in a folded section's line: its mode. */
export function commitSummary(t: TFunction, rule: CommitRule): string {
  return t(`settings.commits.summary.${modeKey(rule.mode)}`);
}

/** What a change said once the driver took it. */
export function commitToast(t: TFunction, name: string, edit: CommitEdit): string {
  if (edit.clear) return t('settings.commits.cleared', { name });
  return t(`settings.commits.saved.${modeKey(edit.set?.mode ?? 'as-you-go')}`, { name });
}

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

/**
 * **One commit rule's control** (DOORS1b, D164's amendment point 3): its mode, and the person's convention as one paragraph,
 * starting from the rule set here, else from what stands above it, so *Set for this …* saves it as it stands. Every press goes
 * out as the twins' edit, mode and convention whole, judged by the driver, whose refusal is said in its words: a line break or
 * more than 400 characters in the convention is refused there, as the terminal refuses it. Props only (components §2).
 */
export function CommitField({ name, set, inherited, busy = false, onChange }: {
  /** The repository or workspace, for each control's accessible name. */
  name: string;
  /** The rule set here; undefined where nothing of its own is set. */
  set?: CommitRule;
  /** What stands without this row's own rule: its workspace's, else Daoris's default. */
  inherited: CommitRule;
  busy?: boolean;
  onChange: (edit: CommitEdit) => void;
}) {
  const { t } = useTranslation();
  const start = set ?? inherited;
  const [mode, setMode] = useState(start.mode);
  const [convention, setConvention] = useState(start.convention ?? '');
  // Set or cleared by either door, the form starts again from what stands.
  useEffect(() => {
    setMode(start.mode);
    setConvention(start.convention ?? '');
  }, [start.mode, start.convention]);

  const words = convention.trim();
  // A rule of its own is Saved as it stands; one set here only once something changed.
  const changed = !set || mode !== set.mode || words !== (set.convention ?? '');

  return (
    <form
      // Sized to its content up to the pane (R1, UIFIT1d): the convention is a sentence, which takes the form's width.
      className="grid gap-2"
      onSubmit={(event) => {
        event.preventDefault();
        if (changed) onChange({ set: { mode, convention: words } });
      }}
    >
      <Segmented
        label={t('settings.commits.mode', { name })}
        value={mode}
        options={MODES.map((each) => ({ value: each, label: t(`settings.commits.modes.${modeKey(each)}`) }))}
        onChange={(chosen) => !busy && setMode(chosen)}
      />
      <textarea
        aria-label={t('settings.commits.convention', { name })}
        value={convention}
        onChange={(event) => setConvention(event.target.value)}
        placeholder={t('settings.commits.conventionPlaceholder')}
        rows={2}
        className={`${FIELD} resize-y font-sans`}
      />
      <div className="flex flex-wrap items-center gap-2">
        <span className={words.length > CONVENTION_LIMIT ? 'text-small text-warn' : 'text-small text-ink-faint'}>
          {t('settings.commits.conventionCount', { used: words.length, limit: CONVENTION_LIMIT })}
        </span>
        <Button type="submit" className="ml-auto" disabled={busy || !changed}>{t('settings.commits.save')}</Button>
      </div>
    </form>
  );
}
