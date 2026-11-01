import type { Meta, StoryObj } from '@storybook/react-vite';
import { atWidths, inDark, inLight } from '../../design/flowFrames';
import { chinese } from '../../design/storyLanguage';
import { AtWorkView } from './AtWorkView';
import {
  ASKED_BY, BOUND, CHAIN_AND_QUESTION, CHAT_TOOK_A_QUEST, CODEX, CODEX_NONE, CONSIDERED, CONTINUED, ELSEWHERE, EMPTY, FAILED, HISTORY, IDS_ONLY,
  JOINED_EACH_WAY, LANES_ALONGSIDE_AND_UNTAKEN, LEAD_WAITING_ON_HELPER, LEAD_WAITING_ON_LANE, LOST, NO_PARENT, NOW, OVERFLOW_AND_EARLIER, PARTS,
  PUBLISHED_AND_WAITS_ON_NONE, QUESTS_EACH_TAKER, READING, RESUMED_AND_EARLIER, STALE, THREE, TURN_WAITS, UNCERTAIN, UNREADABLE,
} from './atWork.fixtures';

// Who is at work (COWORKVIEW1a, the cowork view design §5.3): each state the view draws, at the side bar's floor (300 px), a roomier
// side bar (430 px) and the whole frame under 768 px (680 px); each in English in light and in 中文 in dark.

const nothing = () => {};

const meta: Meta<typeof AtWorkView> = {
  title: 'Work/AtWorkView',
  component: AtWorkView,
  args: { held: THREE, now: NOW, onOpen: nothing, onRetry: nothing },
};
export default meta;

type Story = StoryObj<typeof AtWorkView>;

const WIDTHS = [300, 430, 680] as const;
const light = [atWidths(WIDTHS), inLight];
const zhDark = [chinese, atWidths(WIDTHS), inDark];

/** A chat with three subagents: one working, one completed, one failed. */
export const ThreeSubagents: Story = { decorators: light };
export const ThreeSubagentsChinese: Story = { decorators: zhDark };

/** A nested child whose parent no harness said. */
export const ParentNotSaid: Story = { args: { held: NO_PARENT }, decorators: light };
export const ParentNotSaidChinese: Story = { args: { held: NO_PARENT }, decorators: zhDark };

/** A Codex lead: a child first seen whose end is not said, counted in no *working*. */
export const CodexByCalls: Story = { args: { held: CODEX }, decorators: light };
export const CodexByCallsChinese: Story = { args: { held: CODEX }, decorators: zhDark };

/** A Codex lead with no lineage yet: its *appear only when they call Daoris* line where *Nobody* would be. */
export const CodexNoLineage: Story = { args: { held: CODEX_NONE }, decorators: light };
export const CodexNoLineageChinese: Story = { args: { held: CODEX_NONE }, decorators: zhDark };

/** A lost child beside one whose agent's exit this window saw before its end said how. */
export const LostAndAgentEnded: Story = { args: { held: LOST }, decorators: light };
export const LostAndAgentEndedChinese: Story = { args: { held: LOST }, decorators: zhDark };

/** Agent ids alone, unmatched and tied to no process: their counts, never *Nobody*. */
export const IdsOnly: Story = { args: { held: IDS_ONLY }, decorators: light };
export const IdsOnlyChinese: Story = { args: { held: IDS_ONLY }, decorators: zhDark };

/** Another Daoris process's run, *not known whether it still runs*, beside one working and one lost. */
export const NotKnown: Story = { args: { held: UNCERTAIN }, decorators: light };
export const NotKnownChinese: Story = { args: { held: UNCERTAIN }, decorators: zhDark };

/** Reading, failed and stale over a held answer, and a part whose lineage could not be read. */
export const Reading: Story = { args: { held: READING }, decorators: light };
export const ReadingChinese: Story = { args: { held: READING }, decorators: zhDark };
export const Failed: Story = { args: { held: FAILED }, decorators: light };
export const FailedChinese: Story = { args: { held: FAILED }, decorators: zhDark };
export const Stale: Story = { args: { held: STALE }, decorators: light };
export const StaleChinese: Story = { args: { held: STALE }, decorators: zhDark };
export const Unreadable: Story = { args: { held: UNREADABLE }, decorators: light };
export const UnreadableChinese: Story = { args: { held: UNREADABLE }, decorators: zhDark };

/** An entry of three parts, the attended first, one a teammate's, and more past the bound. */
export const ThreeParts: Story = { args: { held: PARTS }, decorators: light };
export const ThreePartsChinese: Story = { args: { held: PARTS }, decorators: zhDark };

/** The bound: forty working drawn, one more not drawn, the heading *41 working*, three ended folded. */
export const TheBound: Story = { args: { held: BOUND }, decorators: light };
export const TheBoundChinese: Story = { args: { held: BOUND }, decorators: zhDark };

/** The empty view: *Nobody else is working on this*. */
export const Nobody: Story = { args: { held: EMPTY }, decorators: light };
export const NobodyChinese: Story = { args: { held: EMPTY }, decorators: zhDark };

/** On the protocol door, its turn waiting for the child working. */
export const TurnWaits: Story = { args: { held: TURN_WAITS }, decorators: light };
export const TurnWaitsChinese: Story = { args: { held: TURN_WAITS }, decorators: zhDark };

/** A driven record's history: an end not recorded beside ends the harness said, *finished* and *stopped* among them. */
export const DrivenHistory: Story = { args: { held: HISTORY }, decorators: light };
export const DrivenHistoryChinese: Story = { args: { held: HISTORY }, decorators: zhDark };

/** An attended teammate's part: its subagents not visible on this machine. */
export const Elsewhere: Story = { args: { held: ELSEWHERE }, decorators: light };
export const ElsewhereChinese: Story = { args: { held: ELSEWHERE }, decorators: zhDark };

// COWORKVIEW1b-i: the routing half's stories at the main area's three widths (80, 39 and 25rem: `storyWidths.tsx`) and the side bar's
// 300 px floor, each in English in light and in 中文 in dark.
const ROUTING_WIDTHS = [300, 400, 624, 1280] as const;
const routingLight = [atWidths(ROUTING_WIDTHS), inLight];
const routingZh = [chinese, atWidths(ROUTING_WIDTHS), inDark];

/** A lead waiting on its own lane part: the *Waiting on* line first, the lane part alongside below it, never folded. */
export const LeadWaitingOnLanePart: Story = { args: { held: LEAD_WAITING_ON_LANE }, decorators: routingLight };
export const LeadWaitingOnLanePartChinese: Story = { args: { held: LEAD_WAITING_ON_LANE }, decorators: routingZh };

/** A lead waiting on its helper, which the strip words *awaiting helper*. */
export const LeadWaitingOnHelper: Story = { args: { held: LEAD_WAITING_ON_HELPER }, decorators: routingLight };
export const LeadWaitingOnHelperChinese: Story = { args: { held: LEAD_WAITING_ON_HELPER }, decorators: routingZh };

/** A chat that took a quest, waiting on the question the quest asked. */
export const ChatTookAQuest: Story = { args: { held: CHAT_TOOK_A_QUEST }, decorators: routingLight };
export const ChatTookAQuestChinese: Story = { args: { held: CHAT_TOOK_A_QUEST }, decorators: routingZh };

/** A session with two lane parts alongside, having published three quests and waiting on none. */
export const PublishedAndWaitsOnNone: Story = { args: { held: PUBLISHED_AND_WAITS_ON_NONE }, decorators: routingLight };
export const PublishedAndWaitsOnNoneChinese: Story = { args: { held: PUBLISHED_AND_WAITS_ON_NONE }, decorators: routingZh };

/** The resumed record first, the one that waited among four earlier parts. */
export const ResumedBesideItsEarlierParts: Story = { args: { held: RESUMED_AND_EARLIER }, decorators: routingLight };
export const ResumedBesideItsEarlierPartsChinese: Story = { args: { held: RESUMED_AND_EARLIER }, decorators: routingZh };

/** Each door a part joined by, in the design's words. */
export const JoinedEachWay: Story = { args: { held: JOINED_EACH_WAY }, decorators: routingLight };
export const JoinedEachWayChinese: Story = { args: { held: JOINED_EACH_WAY }, decorators: routingZh };

/** The limit's overflow counted apart from the fold: *2 earlier parts* and *10 more parts*. */
export const OverflowBesideTheFold: Story = { args: { held: OVERFLOW_AND_EARLIER }, decorators: routingLight };
export const OverflowBesideTheFoldChinese: Story = { args: { held: OVERFLOW_AND_EARLIER }, decorators: routingZh };

// COWORKVIEW1b-ii: the quests a part asked and what asked it, at the same widths (80, 39 and 25rem and the side bar's 300 px floor), in
// English in light and in 中文 in dark. The tick's reasons are the page's `considered`.

/** Two lane parts alongside the chat that published them, and a third lane quest untaken with the tick's reason beside it. */
export const TwoLanePartsAndOneUntaken: Story = { args: { held: LANES_ALONGSIDE_AND_UNTAKEN, considered: CONSIDERED }, decorators: routingLight };
export const TwoLanePartsAndOneUntakenChinese: Story = { args: { held: LANES_ALONGSIDE_AND_UNTAKEN, considered: CONSIDERED }, decorators: routingZh };

/** The same untaken quest before the driver's first look: *not taken yet*, no reason. */
export const UntakenBeforeTheFirstLook: Story = { args: { held: LANES_ALONGSIDE_AND_UNTAKEN, considered: [] }, decorators: routingLight };

/** Each taker: a session here, a chat through its connector, another machine, no session recorded here, nobody yet, and a quest declined. */
export const EachTaker: Story = { args: { held: QUESTS_EACH_TAKER, considered: CONSIDERED }, decorators: routingLight };
export const EachTakerChinese: Story = { args: { held: QUESTS_EACH_TAKER, considered: CONSIDERED }, decorators: routingZh };

/** A quest to another repository whose taker waits on a question, under the step it follows. */
export const QuestWaitingOnAQuestion: Story = { args: { held: CHAIN_AND_QUESTION }, decorators: routingLight };
export const QuestWaitingOnAQuestionChinese: Story = { args: { held: CHAIN_AND_QUESTION }, decorators: routingZh };

/** What asked the attended part's quest: a session of the entry, and the person. */
export const AskedBySession: Story = { args: { held: ASKED_BY({ session: 's1' }) }, decorators: routingLight };
export const AskedBySessionChinese: Story = { args: { held: ASKED_BY({ session: 's1' }) }, decorators: routingZh };
export const AskedByYou: Story = { args: { held: ASKED_BY({ person: true }) }, decorators: routingLight };
export const AskedByYouChinese: Story = { args: { held: ASKED_BY({ person: true }) }, decorators: routingZh };

/** An ended first process's child *ended with its agent* beside its continuation's child working. */
export const EndedBesideItsContinuation: Story = { args: { held: CONTINUED }, decorators: light };
export const EndedBesideItsContinuationChinese: Story = { args: { held: CONTINUED }, decorators: zhDark };
