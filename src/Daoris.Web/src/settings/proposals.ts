import i18n from '../i18n';
import type { RuleProposal } from './AgentRules';

// An agent's proposal to change the rules (PERM2, D74), said in the person's language. Pure, so the
// rules card and *What needs you* cannot disagree about what a proposal would do or who made it —
// and structured on the wire rather than a sentence, because a sentence would arrive in English.

/** The states a person still answers: the driver has not judged it, or it is holding a widening. */
export const OPEN_STATES: ReadonlySet<RuleProposal['state']> = new Set(['proposed', 'waiting']);

/** What it would change, in a line — the rule in the harness's own words. */
export function proposalChange(proposal: RuleProposal): string {
  const where = proposal.scope === 'machine'
    ? i18n.t('settings.rules.proposals.everywhere')
    // NAME1b: a scope's section title is sentence case; inside a sentence it is a fragment of its own.
    : i18n.t(proposal.scope === 'workspace' ? 'settings.rules.where.workspace' : 'settings.rules.where.repository', {
      name: proposal.name ?? '',
    });
  switch (proposal.action) {
    case 'add':
      return i18n.t('settings.rules.proposals.add', {
        list: i18n.t(`settings.rules.list.${proposal.list ?? 'allow'}`), rule: proposal.rule ?? '', where,
      });
    case 'remove':
      return i18n.t('settings.rules.proposals.remove', { rule: proposal.rule ?? '', where });
    default:
      return i18n.t('settings.rules.proposals.default', {
        id: proposal.default ?? '', state: i18n.t(proposal.on ? 'settings.rules.on' : 'settings.rules.off'),
      });
  }
}

/** Who proposed it: the session the driver started, with its ask when it is an intake. */
export function proposalAuthor(proposal: RuleProposal): string {
  if (!proposal.session) return i18n.t('settings.rules.proposals.byUnknown');
  return proposal.ask
    ? i18n.t('settings.rules.proposals.byIntake', { session: proposal.session, ask: proposal.ask })
    : i18n.t('settings.rules.proposals.bySession', { session: proposal.session });
}
