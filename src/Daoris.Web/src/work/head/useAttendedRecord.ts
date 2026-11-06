import { useState } from 'react';
import { useIntegrationHead } from '../../bridge/repositories/integrate';
import {
  useBringTogether, useChannel, useDecideSessionMemory, useDiscardSessionBranch, useForwardChannel, usePostChannel,
  useSessionMemory, useSessionProcess, useSessionRecord, useSessionRelatedRead, useSessionSpent, useSessionWhere, useSweepPlan,
  useTogether, useTogetherAct, useTrace, useWorkState,
} from '../../bridge/shell';
import { useSessionSupervision } from '../../bridge/sessions/supervision';
import type { Session } from '../../data/api';
import type { entryOfSession } from '../entries';

/**
 * What the attended session's page reads of its record beside the session itself (SESS1, D167): what its parts spent, what
 * was kept of it, the operation bringing it together, its memory, its ask's channel, the branch its tree left, the process
 * it runs under, how it came to be and where its work stands.
 */
export function useAttendedRecord({ attended, attendedEntry, channelAsk, where, here, intake }: {
  attended: Session | null;
  attendedEntry: ReturnType<typeof entryOfSession>;
  channelAsk: string | null;
  where: ReturnType<typeof useSessionWhere>;
  here: boolean;
  intake: boolean;
}) {
  // What each of its parts spent (SESSCTX1d), on each part's own row of the strip, from this machine's record.
  const spent = useSessionSpent(attendedEntry?.parts.map((part) => part.session));
  // What the service kept of the attended session's finished tree, once the tree is let go (KEEPSIZE1).
  const { rows: related, own: relatedOwn } = useSessionRelatedRead(attended?.id);
  // LINESTOGETHER1d: the operation bringing sessions together it is in, and the head's presses on it.
  const together = useTogether(attended?.id);
  const bringTogether = useBringTogether();
  const togetherAct = useTogetherAct();
  // Its memory changes and the person's decision on them (KNOWCENTRAL2e): an end awaiting them, or what an ended session left staged.
  const memoryRows = useSessionMemory(attended?.id);
  const decideMemory = useDecideSessionMemory();
  const sessionRecord = useSessionRecord(attended?.id ?? null, attended ? where[attended.id]?.treeGone === true : false);
  const channel = useChannel(channelAsk);
  // *All messages* in the At work view asks the session page's panel open (COWORKVIEW1c): a number, each new one a press.
  const [channelOpened, setChannelOpened] = useState(0);
  const postChannel = usePostChannel();
  const forwardChannel = useForwardChannel();
  // What the attended session's own tree left (SESS1 S10): its branch in the clean-up's list, found by
  // the name the trees give it — `daoris/` and the tree's folder.
  const sweep = useSweepPlan();
  const branch = attended?.tree && Array.isArray(sweep.data?.branches)
    ? sweep.data.branches.find((row) => row.repository === attended.repository
      && row.branch === `daoris/${attended.tree!.split(/[\\/]/).filter(Boolean).pop()}`) ?? null
    : null;
  // That branch discarded once its tree is gone, a failed attempt's (LAND3b): `daoris tree remove … --force`.
  const discardBranch = useDiscardSessionBranch();
  // The process its own tree runs under, for its *Details* (PROCESS1e): this machine's own record, never an intake's, which is told
  // none; the driver answers none for a session with no tree of its own here.
  const told = useSessionProcess(attended && here && !intake ? attended.id : null);
  // Its counts and the acts the person's rules made on it (CONTEXTDRIVE1h), for the head's chip and *Details*: this machine's own record.
  const supervision = useSessionSupervision(attended?.id, Boolean(attended && here && !intake));
  // How the attended session came to be (TRACE1b): read only while its section is open, for the session it was opened
  // on, since the read takes every session record and quest. Attending another opens it folded.
  const [tracing, setTracing] = useState<string | null>(null);
  const trace = useTrace('session', tracing);
  // Where its work stands (FABRIC-MILESTONE1b): the driver's derivation for this session, read only for this machine's own record,
  // whose branch and tree are this machine's. A session no record names a unit for (a chat with no tree) shows none.
  const milestones = useWorkState(attended && here ? attended.id : null, Boolean(attended && here));
  const milestoneUnits = milestones.data?.units ?? [];
  const milestone = (attended && milestoneUnits.length === 1 ? milestoneUnits[0] : milestoneUnits.find((unit) => unit.session === attended?.id)) ?? null;
  // INTEGRATE1h: its unit's integration state, one line in the head's facts, from the integration record.
  const integration = useIntegrationHead(milestone?.unit);

  return {
    spent, related, relatedOwn, together, bringTogether, togetherAct, memoryRows, decideMemory, sessionRecord, channel, channelOpened,
    setChannelOpened, postChannel, forwardChannel, branch, discardBranch, told, tracing, setTracing, trace, milestone, integration,
    supervision,
  };
}
