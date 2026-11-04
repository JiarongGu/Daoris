import { type Dispatch, type SetStateAction, useEffect, useState } from 'react';
import type { useTerminals } from '../../bridge/shell';
import type { DockTab } from '../frame/RightDock';
import type { ViewId } from '../frame/placements';
import type { WorkTarget } from '../pausing/pausing';
import type { SessionDoors } from '../sessionActs';
import type { AgentChoice } from '../start/AgentChip';

/**
 * The questions the attended session's header asks now (SESSUX1d, D126 §3.3): a stop, a delete, a hand-over, a helper or a
 * pause, each of the session it was asked of; *Archive what ended…*; and the doors only this frame can open on a session.
 */
export function useHeadAsks({ selected, chosen, attend, setDocked, closeDock, terminal, terminals, openView }: {
  selected: string | null;
  chosen: () => void;
  attend: (id: string) => void;
  setDocked: Dispatch<SetStateAction<Record<string, DockTab>>>;
  closeDock: (shut: boolean) => void;
  terminal: boolean;
  terminals: Pick<ReturnType<typeof useTerminals>, 'open'>;
  openView: (view: ViewId) => void;
}) {
  // *Archive what ended…* pressed in the list's ⋯ (SESSUX1e): its first press stands under the list's header until it
  // archives or the person never minds. Not remembered: it is a question asked now.
  const [archivingEnded, setArchivingEnded] = useState(false);
  // The session whose stop asks under the header (SESSUX1d, D126 §3.3), from the header's *Stop…* or its row's: a
  // question asked now, of that session only, so attending another closes it.
  const [stopAsking, setStopAsking] = useState<string | null>(null);
  // The session whose delete asks under the header (SESSUX1f, D126 §5.4), the stop's way: of that session only.
  const [deleteAsking, setDeleteAsking] = useState<string | null>(null);
  // The session whose hand-over asks under the header (MIXAGENT1b-i, D163), the stop's way: of that session only.
  const [handOverAsking, setHandOverAsking] = useState<string | null>(null);
  // The session whose helper's ask opens under the header (MIXAGENT2b, D163), the stop's way: of that session only.
  const [helperAsking, setHelperAsking] = useState<string | null>(null);
  // The agent and account Daoris's offer named, and why, when *Choose…* opened that ask (MIXAGENT1b-ii): the ask opens with
  // the choice made and its press carries the reason. Cleared by the head's own door, whose press is the person's.
  const [handOverChosen, setHandOverChosen] = useState<{ why: 'near' | 'limit'; choice: AgentChoice } | null>(null);
  // The conversations whose offer to hand over the person put down: for this run of the frame, never stored (MIXAGENT1b-ii).
  const [offerDismissed, setOfferDismissed] = useState<ReadonlySet<string>>(() => new Set());
  // The session whose pause asks under the header (PAUSE1e, D132 §2.6), with the work it pauses: of that session only.
  const [pauseAsking, setPauseAsking] = useState<{ session: string; target: WorkTarget } | null>(null);
  // *Answer…* from a row (D126 §3.1): the session it was pressed for, and a count, so the box at its foot takes the focus
  // once per press, including a second press on the same session.
  const [answerFocus, setAnswerFocus] = useState<{ session: string; at: number } | null>(null);
  // A stop's ask is a question asked now, of the session it was asked of (D126 §3.3): attending another by any door
  // puts it down, so coming back later does not find it still open.
  useEffect(() => {
    setStopAsking((asked) => (asked === selected ? asked : null));
    setDeleteAsking((asked) => (asked === selected ? asked : null));
    setHandOverAsking((asked) => (asked === selected ? asked : null));
    setHelperAsking((asked) => (asked === selected ? asked : null));
    setPauseAsking((asked) => (asked?.session === selected ? asked : null));
  }, [selected]);

  // What only this frame can do with a session (SESSUX1d, D126 §3.1), handed to the one owner of the acts at both doors,
  // the page header and the rows: each attends the session first, as a press on its row would.
  const doors: SessionDoors = {
    answer: (id) => {
      chosen();
      attend(id);
      setAnswerFocus((was) => ({ session: id, at: (was?.at ?? 0) + 1 }));
    },
    stop: (id) => {
      chosen();
      attend(id);
      setDeleteAsking(null);
      setHandOverAsking(null);
      setHelperAsking(null);
      setPauseAsking(null);
      setStopAsking(id);
    },
    // *Delete…* (SESSUX1f): attended, its ask under its header, as a stop asks.
    delete: (id) => {
      chosen();
      attend(id);
      setStopAsking(null);
      setHandOverAsking(null);
      setHelperAsking(null);
      setPauseAsking(null);
      setDeleteAsking(id);
    },
    // *Go on with another agent…* and *Stop and hand over…* (MIXAGENT1b-i): attended, the hand-over's ask under its header.
    handOver: (id) => {
      chosen();
      attend(id);
      setStopAsking(null);
      setDeleteAsking(null);
      setHelperAsking(null);
      setPauseAsking(null);
      setHandOverChosen(null);
      setHandOverAsking(id);
    },
    // *Ask another agent…* (MIXAGENT2b): attended, the helper's ask under its header, as a hand-over asks.
    askHelper: (id) => {
      chosen();
      attend(id);
      setStopAsking(null);
      setDeleteAsking(null);
      setHandOverAsking(null);
      setPauseAsking(null);
      setHelperAsking(id);
    },
    // The conversation a hand-over opened, attended as a start's is (MIXAGENT1b-i).
    attend: (id) => {
      chosen();
      attend(id);
    },
    // *Pause quest…* and *Pause ask…* (PAUSE1e): attended, the pause's ask under its header, as a stop asks.
    pause: (id, target) => {
      chosen();
      attend(id);
      setStopAsking(null);
      setDeleteAsking(null);
      setHandOverAsking(null);
      setHelperAsking(null);
      setPauseAsking({ session: id, target });
    },
    // Its review: attended, with the dock open on its work.
    review: (id) => {
      chosen();
      attend(id);
      setDocked((was) => ({ ...was, [id]: 'review' }));
      closeDock(false);
    },
    // A terminal in its folder (§3.5), only where the terminal is a view here: the panel's, shown if it was hidden.
    ...(terminal ? {
      terminal: (folder: string) => {
        terminals.open({ cwd: folder });
        openView('terminal');
      },
    } : {}),
  };

  return {
    archivingEnded, setArchivingEnded, stopAsking, setStopAsking, deleteAsking, setDeleteAsking, handOverAsking, setHandOverAsking,
    helperAsking, setHelperAsking, handOverChosen, setHandOverChosen, offerDismissed, setOfferDismissed, pauseAsking, setPauseAsking,
    answerFocus, setAnswerFocus, doors,
  };
}
