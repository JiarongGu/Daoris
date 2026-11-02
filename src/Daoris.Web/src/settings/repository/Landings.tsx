import type { TFunction } from 'i18next';
import { type LandingIntegrate, policySays } from './IntegratePolicy';

/**
 * How a session's work lands (WSR1, D87): merged into the line, or put on a branch the pattern names —
 * whether its tree and branch go once a press lands it (D88), for a branch the plugin that pushes
 * it and opens the pull request (WSR4, D100), whether a quest's done lands it with no press (LAND2a, D145), the
 * shape of the ticket key its pattern's `{ticket}` is found by (PROCESS1b-i), and whether Accept squashes the pull
 * request's branch to one commit (PROCESS1b-ii). The driver answers `squash` false, and `key` absent where none is set.
 */
export type LandingRule = {
  form: string; pattern?: string; tidy?: boolean; plugin?: string; autoAccept?: boolean; key?: string | null; squash?: boolean;
  integrate?: LandingIntegrate | null;
};

export type { LandingIntegrate };

/** Where a repository's rule came from: set for it, set for its workspace, or the default merge. */
export type LandingSource = 'repository' | 'workspace' | 'default';

/** One repository's rule on this machine, as the driver chose it. */
export type RepositoryLanding = { repository: string; workspace: string; source: LandingSource } & LandingRule;

/** The shape a ticket's key is found by where the rule names none: the driver's `LandingRules.DefaultKey`, the CLI's `DEFAULT_KEY`. */
export const DEFAULT_KEY = '[A-Z][A-Z0-9]+-\\d+';

/** Whether a branch pattern names its branch by a ticket, which only then reads a key (PROCESS1b-i). */
const ticketed = (pattern: string | undefined) => (pattern ?? '').includes('{ticket}');

/** What a rule's integrate says in a line: its batch, order and gates as the design's §3 shows, "batch 4, oldest first, gates declared". */
export function landingIntegrateSays(t: TFunction, rule: LandingRule): string[] {
  return rule.form !== 'branch' || !rule.integrate ? [] : [policySays(t, rule.integrate)];
}

/**
 * What a branch rule's row says after its form (DOORS1b): what finds its pattern's `{ticket}`, in the terminal's words (the CLI's
 * `ticketSays`), and what Accept makes where it squashes. Nothing for a merge, which names no branch.
 */
export function landingProcessSays(t: TFunction, rule: LandingRule): string[] {
  if (rule.form !== 'branch') return [];
  return [
    ...(ticketed(rule.pattern)
      ? [rule.key ? t('settings.landing.byKey', { key: rule.key }) : t('settings.landing.byDefaultKey', { key: DEFAULT_KEY })]
      : []),
    ...(rule.squash ? [t('settings.landing.bySquash')] : []),
    ...landingIntegrateSays(t, rule),
  ];
}

/** Where a repository's rule was set and what it lands as, the first of a Setup row's sentences. */
export function landingFrom(t: TFunction, landing: RepositoryLanding): string {
  return landing.source === 'default'
    ? t('settings.landing.from.default')
    : t(`settings.landing.from.${landing.source}.${landing.form === 'branch' ? 'branch' : 'merge'}`, {
        pattern: landing.pattern, workspace: landing.workspace,
      });
}

/**
 * A repository's rule in words alone, each sentence as its Setup row says it (PROCESS1e, a session's *Details*): where it was set
 * and what it lands as, who pushes a branch, whether its quest's done accepts it, what finds its ticket and what its squash makes.
 */
export function landingRuleSays(t: TFunction, landing: RepositoryLanding): string[] {
  const branch = landing.form === 'branch';
  return [
    landingFrom(t, landing),
    ...(branch && landing.plugin ? [t('settings.landing.byPlugin', { plugin: landing.plugin })] : []),
    ...(branch && landing.autoAccept ? [t('settings.landing.byAuto')] : []),
    ...landingProcessSays(t, landing),
  ];
}
