import type * as React from 'react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { sentence } from '../../locales/format';
import { useEveryAsk, useEveryQuest, useEverySession, useStandingsMark } from '../../data/queries';
import { type HelpSettled, useHelpProposals, useHelpPublished, useSettleHelp } from '../../bridge/shell';
import { useHeldHarnessRun } from '../../agents/harnessRuns';
import { draftDoor, placeDoor } from '../places';
import { type HelpProposal, ProposalCard } from '../ProposalCard';
import { publishedCounts, publishedItems } from '../published';
import { PublishedList } from '../PublishedList';
import { type StarterDoor } from '../starters';
import { type Session } from '../../data/api';
import { type TFunction } from 'i18next';

export function useHelpFollow({ shown, live, setRefusal, onGo, t }: {
  shown: Session | null;
  live: boolean;
  setRefusal: React.Dispatch<React.SetStateAction<string | null>>;
  onGo: ((door: StarterDoor) => void) | undefined;
  t: TFunction<"translation", undefined>;
}) {
  // What the conversation proposes (HELP1c): each card is judged by the route before it arrives, and
  // Apply or Not now goes back into the conversation as the person's next message.
  const proposals = useHelpProposals(shown?.id ?? null, live);
  const settle = useSettleHelp();
  // The Agents screen's running action, where the application holds one (HELP6).
  const runs = useHeldHarnessRun();
  const cards = proposals.data ?? [];
  // What an Apply did beyond the driver's sentence (HELP6): a go opens its place through the starters'
  // door, and an update or a pin is followed by the Agents screen, its console and its end said there.
  const applied = (answer: HelpSettled, proposal?: HelpProposal) => {
    // The plan moved since the card was drawn (HELPAPPLY1): nothing was applied, and the card is drawn again with the new plan.
    if (answer.changed) setRefusal(answer.message);
    // A draft card's go is the place's Workflow, which the page opens with its editor on the draft the card is against (FLOWCHART2g).
    const draft = proposal?.kind === 'workflow' && proposal.workflow && answer.go ? draftDoor(proposal.workflow, answer.go) : null;
    const door = draft ?? (answer.go ? placeDoor(answer.go) : null);
    if (door) onGo?.(door);
    if (answer.harnessAction) runs?.follow(answer.harnessAction.harness, answer.harnessAction.action);
  };
  // ASKQUESTS1: a go card's sentence says where its quest, ask or session stands (since ASKDISMISS1), so the plan its Apply judges
  // moves with them. While one is shown it is drawn again at each push that moves a standing, never on a clock, so its Apply finds
  // the plan it shows rather than answering that the plan changed.
  const goShown = cards.some((card) => card.kind === 'go');
  const standings = useStandingsMark(goShown);
  const drawnAt = useRef<string | null>(null);
  useEffect(() => {
    if (standings === null) {
      drawnAt.current = null;
      return;
    }
    if (drawnAt.current !== null && drawnAt.current !== standings) void proposals.refetch();
    drawnAt.current = standings;
    // Asked again only as the standings move; the query's handle is read from this render.
  }, [standings]);

  // What the conversation published (ASKQUESTS1, the design §9 item 12): its own list, by id, each one's state read from the lists the
  // shell pushes, every workspace's, never asked on a clock. Read only where it published something; a row opens its record on Quests,
  // the door a go uses, and adds no act.
  const publishedRead = useHelpPublished(shown?.id ?? null);
  const following = (publishedRead.data?.length ?? 0) > 0;
  const everyQuest = useEveryQuest(following);
  const everyAsk = useEveryAsk(following);
  const everySession = useEverySession(following);
  const [publishedOpen, setPublishedOpen] = useState(false);
  useEffect(() => {
    setPublishedOpen(false);
  }, [shown?.id]);
  const publishedRows = useMemo(
    () => publishedItems(publishedRead.data ?? [], { quests: everyQuest.data, asks: everyAsk.data, sessions: everySession.data }),
    [publishedRead.data, everyQuest.data, everyAsk.data, everySession.data],
  );
  const published = following ? (
    <PublishedList
      items={publishedRows}
      counts={publishedCounts(publishedRows)}
      failed={everyQuest.isError || everyAsk.isError}
      open={publishedOpen}
      onToggle={() => setPublishedOpen((was) => !was)}
      onOpen={(door) => onGo?.(door)}
    />
  ) : undefined;

  const proposed = cards.length > 0 ? (
    <ul aria-label={t('help.proposal.list')} className="m-0 mt-3 grid list-none gap-2.5 p-0">
      {cards.map((proposal) => (
        <ProposalCard
          key={proposal.id}
          proposal={proposal}
          pending={settle.isPending}
          onApply={(id) => settle.mutate({ id, apply: true }, { onSuccess: (answer) => applied(answer, proposal), onError: (error) => setRefusal(sentence(error)) })}
          onDismiss={(id) => settle.mutate({ id, apply: false }, { onError: (error) => setRefusal(sentence(error)) })}
        />
      ))}
    </ul>
  ) : null;
  return { proposed, published };
}
