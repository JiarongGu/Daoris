import { useTranslation } from 'react-i18next';
import { usePassHelper, useAskHelper, useSessionHelpers } from '../../bridge/sessions/sessionHelpers';
import type { Session } from '../../data/api';
import type { Answered } from '../InlineConfirm';
import type { BringInChoice } from './BringInAgent';
import type { MemberThreadProps } from './MemberThread';
import { askOfMember, teamSaid } from './teamRows';

/** What the head's *Co-workers* and a member's thread are handed beyond the asks (COWORKUX1): the frame's facts and presses. */
export type TeamFacts = {
  lead: string;
  onPass?: (ask: string, answered: Answered) => void;
  passing?: boolean;
  /** *Bring in another agent…*'s choices, each its own door's. */
  choices?: readonly BringInChoice[];
  /** Where the attended record is a helper: its thread head. */
  thread?: MemberThreadProps | null;
};

/**
 * The team's facts for the attended session (COWORKUX1b, d, f): *Pass to the lead* on its own asks, the dialog's doors, and, where it is
 * itself a helper, its thread: the lead's asks read to find the one it answers, *Back to lead*, *Ask again…* and the pass, each on the
 * lead. This machine's own files, so only for its own record.
 */
export function useTeam({ attended, here, nameOf, attend, open }: {
  attended: Session | null | undefined;
  here: boolean;
  nameOf: (agent: string) => string;
  attend: (session: string) => void;
  /** Open one of the attended session's own doors (*Ask another agent…*, *Go on with another agent…*). */
  open: (door: 'askHelper' | 'handOver') => void;
}): TeamFacts | null {
  const { t } = useTranslation();
  const pass = usePassHelper();
  const ask = useAskHelper();
  const member = attended && here && attended.serves && !attended.opinion ? attended : null;
  const lead = member?.serves ?? null;
  const leadAsks = useSessionHelpers(lead);
  if (!attended || !here) return null;

  const passOn = (session: string) => (id: string, answered: Answered) => pass.mutate(
    { session, ask: id },
    {
      onSuccess: (answer) => (answer.passed ? answered.done() : answered.refused(teamSaid(t, answer.why, answer.message))),
      onError: (error) => answered.refused(error instanceof Error ? error.message : String(error)),
    },
  );
  const answered = member && lead ? askOfMember(leadAsks.data, member.id) : null;
  const thread: MemberThreadProps | null = member && lead ? {
    // Its id here; the head says the lead's title where the frame holds it (`helps`).
    lead,
    ask: answered,
    working: member.state === 'working' || member.state === 'starting',
    onBack: () => attend(lead),
    onPass: answered ? (told) => passOn(lead)(answered.id, told) : undefined,
    onAskAgain: answered ? (asked, told) => ask.mutate(
      { session: lead, question: asked.question, agent: answered.startedOn ?? answered.agent, after: answered.id, toPerson: !asked.forLead },
      {
        onSuccess: (answer) => (answer.asked ? told.done() : told.refused(teamSaid(t, answer.why, answer.message))),
        onError: (error) => told.refused(error instanceof Error ? error.message : String(error)),
      },
    ) : undefined,
    busy: pass.isPending || ask.isPending,
  } : null;

  // A helper, a reviewer, an intake or Ask Daoris brings in nobody; the doors judge the rest by their own rules.
  const leads = !attended.serves && !attended.opinion && !attended.ask;
  return {
    lead: nameOf(attended.adapter),
    onPass: passOn(attended.id),
    passing: pass.isPending,
    choices: leads ? [
      { door: 'question', onChoose: () => open('askHelper') },
      { door: 'goOn', onChoose: () => open('handOver') },
    ] : [],
    thread,
  };
}
