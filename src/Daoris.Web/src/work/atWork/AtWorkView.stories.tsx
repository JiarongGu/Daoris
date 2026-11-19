import type { Meta, StoryObj } from '@storybook/react-vite';
import { atWidths, inDark, inLight } from '../../design/flowFrames';
import { chinese } from '../../design/storyLanguage';
import { AtWorkView } from './AtWorkView';
import {
  ASKED_BY, CHAIN_AND_QUESTION, CHAT_TOOK_A_QUEST, CONSIDERED, COWORKERS, EMPTY, FAILED, HINTED, JOINED_EACH_WAY, LANES_ALONGSIDE_AND_UNTAKEN,
  LEAD_WAITING_ON_HELPER, LEAD_WAITING_ON_LANE, LINE_SAID, OVERFLOW_AND_EARLIER, PARTS, PUBLISHED_AND_WAITS_ON_NONE, QUESTS_EACH_TAKER, READING,
  RESUMED_AND_EARLIER, REVIEWERS, SAID, STALE,
} from './atWork.fixtures';

// Who is at work: each state the view draws, at the side bar's floor (300 px), a roomier
// side bar (430 px) and the whole frame under 768 px (680 px); each in English in light and in 中文 in dark.

const nothing = () => {};

const meta: Meta<typeof AtWorkView> = {
  title: 'Work/AtWorkView',
  component: AtWorkView,
  args: { held: PARTS, onOpen: nothing, onRetry: nothing },
};
export default meta;

type Story = StoryObj<typeof AtWorkView>;

const WIDTHS = [300, 430, 680] as const;
const light = [atWidths(WIDTHS), inLight];
const zhDark = [chinese, atWidths(WIDTHS), inDark];

/** Reading, failed, and stale over a held answer. */
export const Reading: Story = { args: { held: READING }, decorators: light };
export const ReadingChinese: Story = { args: { held: READING }, decorators: zhDark };
export const Failed: Story = { args: { held: FAILED }, decorators: light };
export const FailedChinese: Story = { args: { held: FAILED }, decorators: zhDark };
export const Stale: Story = { args: { held: STALE }, decorators: light };
export const StaleChinese: Story = { args: { held: STALE }, decorators: zhDark };

/** An entry of three parts, the attended first, one a teammate's, each with its subagents counted, and more past the bound. */
export const ThreeParts: Story = { args: { held: PARTS }, decorators: light };
export const ThreePartsChinese: Story = { args: { held: PARTS }, decorators: zhDark };

/** The empty view: *Nobody else is working on this*. */
export const Nobody: Story = { args: { held: EMPTY }, decorators: light };
export const NobodyChinese: Story = { args: { held: EMPTY }, decorators: zhDark };

// The routing half's stories at the main area's three widths (80, 39 and 25rem: `storyWidths.tsx`) and the side bar's
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

/** Each door a part joined by. */
export const JoinedEachWay: Story = { args: { held: JOINED_EACH_WAY }, decorators: routingLight };
export const JoinedEachWayChinese: Story = { args: { held: JOINED_EACH_WAY }, decorators: routingZh };

/** The limit's overflow counted apart from the fold: *2 earlier parts* and *10 more parts*. */
export const OverflowBesideTheFold: Story = { args: { held: OVERFLOW_AND_EARLIER }, decorators: routingLight };
export const OverflowBesideTheFoldChinese: Story = { args: { held: OVERFLOW_AND_EARLIER }, decorators: routingZh };

// The quests a part asked and what asked it, at the same widths (80, 39 and 25rem and the side bar's 300 px floor), in
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

// The channel's last words, each on the part that posted it, the person's in the block, and *All messages*.

/** The channel's newest three: a lane part's on its row, the lead's on its own, and the person's under *On the channel*. */
export const ChannelLastWords: Story = { args: { held: LANES_ALONGSIDE_AND_UNTAKEN, channel: { words: SAID, onOpen: nothing } }, decorators: routingLight };
export const ChannelLastWordsChinese: Story = { args: { held: LANES_ALONGSIDE_AND_UNTAKEN, channel: { words: SAID, onOpen: nothing } }, decorators: routingZh };

/** A line on no ask, its own channel: two alongside parts to each other alone, each on its row, and no *All messages*. */
export const LineOwnChannel: Story = { args: { held: LANES_ALONGSIDE_AND_UNTAKEN, channel: { words: LINE_SAID } }, decorators: routingLight };
export const LineOwnChannelChinese: Story = { args: { held: LANES_ALONGSIDE_AND_UNTAKEN, channel: { words: LINE_SAID } }, decorators: routingZh };

/** A session with an ask and nothing said on its channel yet. */
export const ChannelNothingSaid: Story = { args: { held: LANES_ALONGSIDE_AND_UNTAKEN, channel: { words: [], onOpen: nothing } }, decorators: routingLight };

// The hint, one story per standing, at the same widths.

/** Two subagents ran in a repository that is not driven here. */
export const HintNotDriven: Story = { args: { held: HINTED('not-driven') }, decorators: routingLight };
export const HintNotDrivenChinese: Story = { args: { held: HINTED('not-driven') }, decorators: routingZh };

/** Two subagents ran in a driven repository that declares no lanes. */
export const HintNoLanes: Story = { args: { held: HINTED('no-lanes') }, decorators: routingLight };
export const HintNoLanesChinese: Story = { args: { held: HINTED('no-lanes') }, decorators: routingZh };

/** Two subagents ran in a repository whose sessions run one at a time here. */
export const HintOneAtATime: Story = { args: { held: HINTED('one-at-a-time') }, decorators: routingLight };
export const HintOneAtATimeChinese: Story = { args: { held: HINTED('one-at-a-time') }, decorators: routingZh };

// The line's co-workers, a member in each state (asked, starting, reading, answered and taken, refused, a successor asked by an
// earlier part with its delivery not certain) and a source not read, at the main area's three widths and the 300 px dock floor.
const NAMES: Record<string, string> = { 'codex-acp': 'Codex' };
const named = (agent: string) => NAMES[agent] ?? agent;

/** Each co-worker state, and the *Waiting on* line that reads the same requests. */
export const Coworkers: Story = { args: { held: COWORKERS, nameOf: named }, decorators: routingLight };
export const CoworkersChinese: Story = { args: { held: COWORKERS, nameOf: named }, decorators: routingZh };
/** The line's reviewers among its co-workers, one reading, one ended, one failed. */
export const Reviewers: Story = { args: { held: REVIEWERS, nameOf: named }, decorators: routingLight };
export const ReviewersChinese: Story = { args: { held: REVIEWERS, nameOf: named }, decorators: routingZh };
/** Opened from the head's *Waiting on*, at the helper the line waits on, its row outlined and holding the focus. */
export const OpenedAtTheRequest: Story = { args: { held: COWORKERS, nameOf: named, seek: { exchange: 'helper:h7', n: 1 } }, decorators: routingLight };
export const OpenedAtTheRequestChinese: Story = { args: { held: COWORKERS, nameOf: named, seek: { exchange: 'helper:h7', n: 1 } }, decorators: routingZh };
