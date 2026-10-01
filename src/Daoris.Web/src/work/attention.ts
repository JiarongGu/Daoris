import i18n from '../i18n';
import type { Ask, Quest, Registration, Session } from '../api';
import { firstLine } from '../asks/AskRow';
import type { RuleProposal } from '../settings/AgentRules';
import { proposalAuthor, proposalChange } from '../settings/proposals';
import type { TrustHold } from '../signals';
import { sessionTitle } from './identity';
import type { Attention } from './AttentionRow';

/** The intake states in which an ask is the harness's to answer, not yet the person's (D65 §1b). */
const INTAKE_BUSY: ReadonlySet<Session['state']> = new Set(['queued', 'starting', 'working']);

/**
 * How many of Sessions' own sessions wait on the person: the Sessions icon's badge (UX5 U20).
 *
 * @remarks
 * **A badge counts what its place holds** (D76 as amended, the owner's choice). The icon carried the
 * whole of {@link needsAPerson}, six on the scratch window, while Sessions held one of them: the asks
 * are on Quests and in Overview's band, so the press the badge invited showed a sixth of what it
 * counted. Overview carries the whole now. A parked intake counts here, because it is in the rail,
 * even though the band lets its ask stand for it.
 */
export function waitingInSessions(sessions: readonly Session[]): number {
  return sessions.filter((session) => session.state === 'awaiting-person').length;
}

/**
 * What is waiting on a person, oldest first within each kind (design §4).
 *
 * @remarks
 * **Parked first, because a parked session is holding a working tree while it waits.** Then the asks
 * waiting on a person (INT4d), because nothing downstream moves until the person settles one and an
 * ask holds nothing while it waits. Then the quests nobody here can take — which sit forever and
 * which nothing else surfaces, since the quest list shows them among every other open one.
 *
 * **An ask waits on the person unless its intake is busy with it.** Proposed with nothing serving it
 * (or its intake ended without publishing), only a person publishes, so it is a `proposal`. An intake
 * that parked asking the person makes it `intake`. The page cannot know whether this machine names
 * an intake harness, so an ask the driver has not picked up yet is a proposal until it is. It is the
 * person's to take either way, and the band never guesses what a driver will do next.
 *
 * **One thing is counted once.** A parked intake is a session awaiting a person too, and its ask's
 * row stands for it, because the answer is on the ask (publish or close). It leaves the parked list
 * only while its ask is in hand and live, so a list that did not arrive never hides it.
 *
 * **A quest is unanswerable when this deployment has no registration for its receiver.** The
 * publish door already refuses a quest addressed to a non-adopter, so the way one comes to exist is
 * afterwards: the receiver retired, or the registration never arrived here. Either way no agent
 * will pull it, and "who cannot be asked" is the same question as "who can" (the Projects view
 * already argues this for repositories).
 *
 * **A folder waiting on the person's trust (D73) sits after the parked sessions.** The driver is
 * holding a start there because the agent ignores that folder's own permissions until trusted, and
 * the grant is the person's alone. It holds no tree, but nothing it holds can start until it is
 * granted. One row per folder, since the oldest thing it holds. Only the shell's tick says so, so a
 * browser has none.
 *
 * **An agent's proposal to widen the rules (PERM2, D74) sits after the asks.** 🔴 A widening never
 * applies without the person, so the driver holds it as `waiting` and only they can settle it. The
 * session that proposed it carries on meanwhile, so it holds nothing up the way an ask does. A narrowing
 * applied itself, and one the driver has not judged yet may be one, so neither is here. Only the shell
 * reads the rules, so a browser has none.
 *
 * **The design's middle category — finished work nobody has looked at — is deliberately absent.**
 * Nothing records that anybody looked, so any row here would be a guess. SURF6's *viewed* mark is
 * not kept, so it arrives when looking is recorded, and the band says so rather than leaving the gap
 * silent.
 */
export function needsAPerson(
  sessions: readonly Session[],
  quests: readonly Quest[],
  registry: readonly Registration[],
  asks: readonly Ask[],
  untrusted: readonly TrustHold[] = [],
  proposals: readonly RuleProposal[] = [],
): Attention[] {
  const live = asks.filter((ask) => ask.state === 'Open' || ask.state === 'Proposed');
  const intakeOf = (ask: Ask) =>
    ask.intake ? sessions.find((session) => session.id === ask.intake) : undefined;

  const waitingAsks = live
    .filter((ask) => !INTAKE_BUSY.has(intakeOf(ask)?.state ?? 'completed'))
    .map((ask): Attention => {
      const intake = intakeOf(ask);
      const asked = intake?.state === 'awaiting-person';
      return {
        id: ask.id,
        kind: asked ? 'intake' : 'proposal',
        title: firstLine(ask.sentence),
        where: ask.workspace,
        since: asked ? intake.updated : ask.asked,
        detail: asked
          ? intake.note
          // A refused receiver's sentence is the service's, verbatim; otherwise what was proposed.
          : ask.note ?? (ask.proposal.length > 0
            ? i18n.t('work.attention.proposalWhy', {
              repositories: ask.proposal.map((match) => match.repository).join(', '),
            })
            : i18n.t('work.attention.proposalNobody')),
      };
    });

  const standsFor = new Set(live.map((ask) => ask.intake).filter(Boolean));
  const parked = sessions
    .filter((session) => session.state === 'awaiting-person' && !standsFor.has(session.id))
    .map((session): Attention => ({
      id: session.id,
      kind: 'parked',
      title: sessionTitle(session, quests.find((quest) => quest.id === session.quest)),
      where: session.repository,
      since: session.updated,
      detail: session.note,
    }));

  const registered = new Set(registry.map((row) => row.repository.toLowerCase()));
  const unanswerable = quests
    .filter((quest) => quest.status === 'Open' && !registered.has(quest.to.toLowerCase()))
    .map((quest): Attention => ({
      id: quest.id,
      kind: 'unanswerable',
      title: quest.title,
      where: quest.to,
      since: quest.filed,
      detail: i18n.t('work.attention.unanswerableWhy', { repository: quest.to }),
    }));

  // One row per folder in one file: two quests held on one untrusted tree are one grant.
  const folders = new Map<string, Attention>();
  for (const hold of untrusted) {
    const held = hold.quest ? quests.find((quest) => quest.id === hold.quest) : undefined;
    const asked = hold.ask ? asks.find((one) => one.id === hold.ask) : undefined;
    const where = held?.to
      ?? (hold.ask ? i18n.t('work.attention.askWhere', { id: hold.ask }) : `#${hold.quest ?? ''}`);
    const since = held?.filed ?? asked?.asked ?? new Date().toISOString();
    const key = `${hold.trustFile}\n${hold.folder}`;
    const seen = folders.get(key);
    if (seen) {
      if (since.localeCompare(seen.since) < 0) seen.since = since;
      continue;
    }
    folders.set(key, {
      // The row's identity is what makes it one row: the file AND the folder (REV3). The folder alone
      // gave one folder held in two accounts' files two rows with one key.
      id: key,
      kind: 'trust',
      title: hold.folder,
      where,
      since,
      detail: i18n.t('work.attention.trustWhy'),
      trust: { folder: hold.folder, trustFile: hold.trustFile },
    });
  }

  const widenings = proposals
    .filter((proposal) => proposal.state === 'waiting')
    .map((proposal): Attention => ({
      id: proposal.id,
      kind: 'rule',
      title: proposalChange(proposal),
      where: proposalAuthor(proposal),
      since: proposal.proposed,
      // The session's own reason, verbatim.
      detail: proposal.why,
    }));

  const oldestFirst = (a: Attention, b: Attention) => a.since.localeCompare(b.since);
  return [
    ...parked.sort(oldestFirst),
    ...[...folders.values()].sort(oldestFirst),
    ...waitingAsks.sort(oldestFirst),
    ...widenings.sort(oldestFirst),
    ...unanswerable.sort(oldestFirst),
  ];
}
