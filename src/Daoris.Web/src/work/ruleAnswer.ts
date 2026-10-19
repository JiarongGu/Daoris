import { useTranslation } from 'react-i18next';
import { useRuleProposal } from '../bridge/shell';
import type { Session } from '../data/api';
import type { Notify } from '../design/ui';
import { sentence } from '../locales/format';
import type { RuleProposal } from '../settings/repository/AgentRules';
import { proposalChange } from '../settings/repository/proposals';
import { isIntake } from './identity';
import type { Answered } from './InlineConfirm';

/**
 * The session a conversation's *Allow* tells, and the frame's own door to it (`SESSION_INPUT`, as the rebase words take it):
 * what it answers, sent or not.
 */
export type RuleTell = { session: string; say: (text: string) => Promise<{ sent: boolean }> };

/** Who an *Allow* from the band or the Agents page tells (PROPOSEHERE1c): a session's door, one that takes no words, or no one. */
export type RuleHearing = RuleTell | { unheard: string } | null;

/**
 * The session that proposed a rule, found in the list the page holds, and the door to it (PROPOSEHERE1c, D74's PROPOSEHERE1b
 * note). Nothing where the proposal names none (a pipe-door quest session's), the list does not hold it, or no door was given;
 * an intake takes no words (INT4b), so it is named, to be said so. An ended session's words are kept on its record by the driver.
 */
export function ruleHearing(
  proposal: RuleProposal | undefined,
  sessions: readonly Session[],
  say: ((session: string, text: string) => Promise<{ sent: boolean }>) | undefined,
): RuleHearing {
  const session = proposal?.session ? sessions.find((one) => one.id === proposal.session) : undefined;
  if (!session || !say) return null;
  if (isIntake(session)) return { unheard: session.id };
  return { session: session.id, say: (text) => say(session.id, text) };
}

/**
 * **The one owner of the person's answer to an agent's rule proposal** (PERM2, D74; PROPOSEHERE1b): the Agents page's card, *What
 * needs you* and the session's own conversation each press this, so a proposal is settled one way whichever door pressed it, as
 * `reviewActs.ts` owns a review's verdict.
 *
 * @remarks
 * **An organism**: it holds the bridge's proposal hook, so the card, the band's rows and `ProposedRules`, which press it, hold
 * none. The Agents place reads it with everything its view imports, so it presses that hook alone: the words to a session go
 * through the door its caller hands it.
 *
 * **Settled first, then said** (`settled`): the driver's `RULE_PROPOSAL`, then the sentence of what became of it, by the press
 * the person made: *Accept* on the Agents page, *Allow* where a widening waits (the glossary's `accept`, PROPOSEHERE1a). A
 * refusal is the driver's own, raised for the door that pressed it to say where it was pressed (UXFIX2).
 *
 * **The session that proposed it is told, once it is allowed** (`answer`, D74's PROPOSEHERE1b note): words to it after the rule
 * is kept, so the turn they start is composed with it (`SpawnSettings.Write` composes the rules at each spawn). Where nothing
 * took them, the person is told to say it themselves.
 */
export function useRuleAnswer({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const settle = useRuleProposal();

  /** Tell the session the rule it proposed is allowed now, as its next turn. */
  const told = ({ session, say }: RuleTell, change: string) => say(t('work.wait.rule.said', { change })).then(
    (result) => notify(t(result.sent ? 'work.wait.rule.told' : 'work.wait.rule.untold', { session }), result.sent ? 'ok' : 'error'),
    () => notify(t('work.wait.rule.untold', { session }), 'error'),
  );

  /**
   * Settle a proposal; resolves with the sentence of what became of it, said as the press was named.
   *
   * Where the press is an *Allow* and the caller found the session that proposed it (`ruleHearing`, PROPOSEHERE1c), that session
   * is told once the rule is kept, as the conversation's own *Allow* does; one that takes no words says so in the sentence.
   */
  const settled = (id: string, accept: boolean, change: string, press: 'accept' | 'allow' = 'allow', hearing: RuleHearing = null) =>
    settle.mutateAsync({ id, accept }).then(() => {
      const said = t(
        !accept ? 'settings.rules.proposals.declined' : press === 'allow' ? 'work.attention.allowedRule' : 'settings.rules.proposals.accepted',
        { change },
      );
      if (!accept || !hearing) return said;
      if ('unheard' in hearing) return `${said} ${t('work.wait.rule.notHeard', { session: hearing.unheard })}`;
      void told(hearing, change);
      return said;
    });

  /**
   * The conversation's answer (PROPOSEHERE1b): settled, its sentence said, the ask told how it ended, and on an *Allow* the
   * session told through `tell`; null where it takes no words.
   */
  const answer = (proposal: RuleProposal, accept: boolean, answered: Answered, tell: RuleTell | null) => {
    const change = proposalChange(proposal);
    settled(proposal.id, accept, change).then(
      (said) => {
        notify(said);
        answered.done();
        if (accept && tell) void told(tell, change);
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
