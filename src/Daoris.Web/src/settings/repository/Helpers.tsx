import type { TFunction } from 'i18next';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Segmented } from '../../design/ui';

/**
 * A helpers rule as the driver answers it (MIXAGENT2a-i, D163 point 3): what a helper a session's agent asks for does, `ask`
 * (the default: it waits for the person's yes), `on` (it starts at once within the limits) or `off` (none is offered), and the
 * agents that may be asked, in the order they are tried.
 */
export type HelperRule = { mode: string; agents: string[] };

/** Where a repository's rule was set: for it, for its workspace, or nowhere, which is the default's. */
export type HelperSource = 'repository' | 'workspace' | 'default';

/** One repository's rule as the driver resolves it (`LINES`' `helpers`): never null, its source `default` where nothing is set. */
export type RepositoryHelpers = { repository: string; workspace: string; rule?: HelperRule | null; source?: HelperSource | null };

/** What a set changes, each part only where it is said: the twins' edit. */
export type HelperSet = { mode?: string; agents?: string[] };

/** One change to a helpers rule: set what it names over the rule set there, or clear. */
export type HelperEdit = { set?: HelperSet; clear?: true };

/** The edit for a repository or a workspace, as `SET_HELPERS` takes it. */
export type HelperChange = { repository?: string; workspace?: string } & HelperEdit;

/** What stands where nothing is set: the twins' `HelperRule.Default`. */
export const HELPER_DEFAULT: HelperRule = { mode: 'ask', agents: [] };

const MODES = ['ask', 'on', 'off'] as const;

/** A mode's catalogue word, the default's where the driver answered one this page does not know. */
const modeKey = (mode: string) => ((MODES as readonly string[]).includes(mode) ? mode : 'ask');

/**
 * What each door says of a rule (DOORS1b), in the page's language: its mode in the terminal's sentence (`driver helpers`), then
 * who may be asked as the driver's choice walks them (D163's MIXAGENT2a-ii note): the agents named, in order, else the one the
 * session names or another maker's. Off offers none, so it names no one.
 */
export function helperSays(t: TFunction, rule: HelperRule): string[] {
  const mode = modeKey(rule.mode);
  if (mode === 'off') return [t('settings.helpers.says.off')];
  const named = rule.agents.map((agent) => `\`${agent}\``);
  return [
    t(`settings.helpers.says.${mode}`),
    named.length === 0 ? t('settings.helpers.says.anyAgent')
      : named.length === 1 ? t('settings.helpers.says.agentsOne', { agents: named[0] })
        : t('settings.helpers.says.agentsMany', { agents: named.join(t('settings.helpers.elseJoin')) }),
  ];
}

/** What a Setup row says of the rule standing: the door's sentences, then where it was set, the default included. */
export function helperRowSays(t: TFunction, rule: HelperRule, from: { source?: HelperSource | null; workspace?: string }): string {
  return [
    ...helperSays(t, rule),
    from.source === 'repository' ? t('settings.helpers.from.repository')
      : from.source === 'workspace' ? t('settings.helpers.from.workspace', { workspace: from.workspace })
        : t('settings.helpers.from.default'),
  ].join(t('projects.setup.sentenceJoin'));
}

/** A rule in a folded section's line: its mode. */
export function helperSummary(t: TFunction, rule: HelperRule): string {
  return t(`settings.helpers.summary.${modeKey(rule.mode)}`);
}

/** What a change said once the driver took it. */
export function helperToast(t: TFunction, name: string, edit: HelperEdit): string {
  if (edit.clear) return t('settings.helpers.cleared', { name });
  return t(`settings.helpers.saved.${modeKey(edit.set?.mode ?? 'ask')}`, { name });
}

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

/** The agents as typed: none where the field is blank, else each between commas as written, for the driver to judge. */
const agentsOf = (typed: string) => (typed.trim() === '' ? [] : typed.split(',').map((each) => each.trim()));

/**
 * **One helpers rule's control** (DOORS1b, D163 point 3): its mode, and the agents that may be asked in the order they are
 * tried, starting from the rule set here, else from what stands above it, so *Set for this …* saves it as it stands. Every
 * press goes out as the twins' edit, mode and agents whole, judged by the driver, whose refusal is said in its words. The
 * agents are not checked against this machine's, as the terminal's and Ask Daoris's are not. Props only (components §2).
 */
export function HelperField({ name, set, inherited, busy = false, onChange }: {
  /** The repository or workspace, for each control's accessible name. */
  name: string;
  /** The rule set here; undefined where nothing of its own is set. */
  set?: HelperRule;
  /** What stands without this row's own rule: its workspace's, else Daoris's default. */
  inherited: HelperRule;
  busy?: boolean;
  onChange: (edit: HelperEdit) => void;
}) {
  const { t } = useTranslation();
  const start = set ?? inherited;
  const [mode, setMode] = useState(start.mode);
  const [agents, setAgents] = useState(start.agents.join(', '));
  // Set or cleared by either door, the form starts again from what stands; keyed by what stands, as text.
  const standing = JSON.stringify(start);
  useEffect(() => {
    setMode(start.mode);
    setAgents(start.agents.join(', '));
  }, [standing]);

  const named = agentsOf(agents);
  const changed = !set || mode !== set.mode || named.join('\n') !== set.agents.join('\n');

  return (
    <form
      className="grid max-w-[40rem] gap-2"
      onSubmit={(event) => {
        event.preventDefault();
        if (changed) onChange({ set: { mode, agents: named } });
      }}
    >
      <Segmented
        label={t('settings.helpers.mode', { name })}
        value={mode}
        options={MODES.map((each) => ({ value: each, label: t(`settings.helpers.modes.${each}`) }))}
        onChange={(chosen) => !busy && setMode(chosen)}
      />
      <div className="flex flex-wrap items-center gap-2">
        <input
          aria-label={t('settings.helpers.agents', { name })}
          value={agents}
          onChange={(event) => setAgents(event.target.value)}
          placeholder={t('settings.helpers.agentsPlaceholder')}
          spellCheck={false}
          className={`${FIELD} flex-1`}
        />
        <Button type="submit" disabled={busy || !changed}>{t('settings.helpers.save')}</Button>
      </div>
    </form>
  );
}
