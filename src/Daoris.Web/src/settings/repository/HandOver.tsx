import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import { Segmented } from '../../design/ui';

/**
 * A hand-over rule as the driver answers it (MIXAGENT1c, D163 point 3): what an agent's ask to hand its session over to another
 * agent does, `ask` (the default: it waits for the person's yes or no on the session's page), `on` (taken when the agent's turn ends,
 * to the next agent in the person's order) or `off` (refused). An object, so a later field is added beside the mode.
 */
export type HandOverRule = { mode: string };

/** Where a repository's rule was set: for it, for its workspace, or nowhere, which is the default's. */
export type HandOverSource = 'repository' | 'workspace' | 'default';

/** One repository's rule as the driver resolves it (`LINES`' `handOver`): never null, its source `default` where nothing is set. */
export type RepositoryHandOver = { repository: string; workspace: string; rule?: HandOverRule | null; source?: HandOverSource | null };

/** One change to a hand-over rule: set its mode, or clear. */
export type HandOverEdit = { set?: { mode: string }; clear?: true };

/** The edit for a repository or a workspace, as `SET_HAND_OVER` takes it: the twins' edit. */
export type HandOverChange = { repository?: string; workspace?: string } & HandOverEdit;

/** What stands where nothing is set: the twins' `HandOverRule.Default`. */
export const HAND_OVER_DEFAULT: HandOverRule = { mode: 'ask' };

const MODES = ['ask', 'on', 'off'] as const;

/** A mode's catalogue word, the default's where the driver answered one this page does not know. */
const modeKey = (mode: string) => ((MODES as readonly string[]).includes(mode) ? mode : 'ask');

/** What each door says of a rule, in the page's language: the terminal's sentence for its mode (`driver handover`). */
export function handOverSays(t: TFunction, rule: HandOverRule): string {
  return t(`settings.handOver.says.${modeKey(rule.mode)}`);
}

/** What a Setup row says of the rule standing: the door's sentence, then where it was set, the default included. */
export function handOverRowSays(t: TFunction, rule: HandOverRule, from: { source?: HandOverSource | null; workspace?: string }): string {
  return [
    handOverSays(t, rule),
    from.source === 'repository' ? t('settings.handOver.from.repository')
      : from.source === 'workspace' ? t('settings.handOver.from.workspace', { workspace: from.workspace })
        : t('settings.handOver.from.default'),
  ].join(t('projects.setup.sentenceJoin'));
}

/** A rule in a folded section's line: its mode. */
export function handOverSummary(t: TFunction, rule: HandOverRule): string {
  return t(`settings.handOver.summary.${modeKey(rule.mode)}`);
}

/** What a change said once the driver took it. */
export function handOverToast(t: TFunction, name: string, edit: HandOverEdit): string {
  if (edit.clear) return t('settings.handOver.cleared', { name });
  return t(`settings.handOver.saved.${modeKey(edit.set?.mode ?? 'ask')}`, { name });
}

/**
 * **One hand-over rule's control** (MIXAGENT1c, D163 point 3): its mode, starting from the rule set here, else from what stands above
 * it. A press goes out as the twins' edit at once, judged by the driver, whose refusal is said in its words; choosing the mode that
 * is set here already sends nothing. Props only (components §2).
 */
export function HandOverField({ name, set, inherited, busy = false, onChange }: {
  /** The repository or workspace, for the control's accessible name. */
  name: string;
  /** The rule set here; undefined where nothing of its own is set. */
  set?: HandOverRule;
  /** What stands without this row's own rule: its workspace's, else Daoris's default. */
  inherited: HandOverRule;
  busy?: boolean;
  onChange: (edit: HandOverEdit) => void;
}) {
  const { t } = useTranslation();
  const standing = modeKey((set ?? inherited).mode);
  return (
    <Segmented
      label={t('settings.handOver.mode', { name })}
      value={standing}
      options={MODES.map((each) => ({ value: each, label: t(`settings.handOver.modes.${each}`) }))}
      onChange={(chosen) => {
        if (busy || (set && chosen === modeKey(set.mode))) return;
        onChange({ set: { mode: chosen } });
      }}
    />
  );
}
