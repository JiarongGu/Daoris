import { useTranslation } from 'react-i18next';
import { useRuleProposal, useSay } from '../bridge/shell';
import type { Notify } from '../design/ui';
import { sentence } from '../locales/format';
import type { RuleProposal } from '../settings/AgentRules';
import { proposalChange } from '../settings/proposals';
import type { Answered } from './InlineConfirm';

/**
 * **The one owner of the person's answer to an agent's rule proposal** (PERM2, D74; PROPOSEHERE1b): the Agents page's card, *What
 * needs you* and the session's own conversation each press this, so a proposal is settled one way whichever door pressed it, as
 * `reviewActs.ts` owns a review's verdict.
 *
 * @remarks
 * **An organism**: it holds the bridge's hooks, so the card, the band's rows and `ProposedRules`, which press it, hold none.
 *
 * **Settled first, then said** (`settled`): the driver's `RULE_PROPOSAL`, then the sentence of what became of it, by the press
 * the person made: *Accept* on the Agents page, *Allow* where a widening waits (the glossary's `accept`, PROPOSEHERE1a). A
 * refusal is the driver's own, raised for the door that pressed it to say where it was pressed (UXFIX2).
 *
 * **The session that proposed it is told, once it is allowed** (`answer`, D74's PROPOSEHERE1b note): words to it through the door
 * the terminal's `sessions say` takes (`SESSION_INPUT`), after the rule is kept, so the turn they start is composed with it
 * (`SpawnSettings.Write` composes the rules at each spawn). Where nothing took them, the person is told to say it themselves.
 */
export function useRuleAnswer({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const settle = useRuleProposal();
  const say = useSay();

  /** Settle a proposal; resolves with the sentence of what became of it, said as the press was named. */
  const settled = (id: string, accept: boolean, change: string, press: 'accept' | 'allow' = 'allow') =>
    settle.mutateAsync({ id, accept }).then(() => t(
      !accept ? 'settings.rules.proposals.declined' : press === 'allow' ? 'work.attention.allowedRule' : 'settings.rules.proposals.accepted',
      { change },
    ));

  /** Tell a session the rule it proposed is allowed now, as its next turn. */
  const tell = (session: string, change: string) => say.mutate({ id: session, text: t('work.wait.rule.said', { change }) }, {
    onSuccess: (result) => notify(t(result.sent ? 'work.wait.rule.told' : 'work.wait.rule.untold', { session }), result.sent ? 'ok' : 'error'),
    onError: () => notify(t('work.wait.rule.untold', { session }), 'error'),
  });

  /**
   * The conversation's answer (PROPOSEHERE1b): settled, its sentence said, the ask told how it ended, and on an *Allow* the
   * session named by `told` told so; null where it takes no words.
   */
  const answer = (proposal: RuleProposal, accept: boolean, answered: Answered, told: string | null) => {
    const change = proposalChange(proposal);
    settled(proposal.id, accept, change).then(
      (said) => {
        notify(said);
        answered.done();
        if (accept && told) tell(told, change);
      },
      (error: unknown) => answered.refused(sentence(error)),
    );
  };

  return {
    settled,
    answer,
    /** An answer on its way: every press on a proposal waits for it. */
    busy: settle.isPending,
  };
}
