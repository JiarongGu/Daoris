import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { chinese } from '../../design/storyLanguage';
import { sessionWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { ConversationView } from './ConversationView';
import { ShowEarlier } from './EarlierParts';
import { beginsByMove, type SessionEvent, settle, toTurns } from './conversation';

// A session's page draws its line as one conversation (SWITCHCTX1c-i; the switch-context design §2.7): the parts the attended one
// continues above it, oldest first, each with its own record's liveness and read-only, one line on each side of a move and a
// part's opening line folded under its boundary; where a part began by no move, the strip's *began* words are its boundary; a
// line longer than three earlier parts draws the nearest three and a press for more. Built by the fold the organism uses, over the
// events a move writes (`moved.on` and `moved.from`, `chat.moved` and `chat.moved-from`, `chat.handed-over` and `chat.taken-over`).
// Drawn at the session page's widths (the UI/UX design R6, STORYGAP1), in both languages.

let seq = 0;
const ev = (over: Partial<SessionEvent>): SessionEvent =>
  ({ seq: ++seq, at: new Date(Date.UTC(2026, 9, 25, 9) + seq * 1500).toISOString(), kind: 'message', ...over });
const note = (code: string, values: Record<string, unknown>): SessionEvent =>
  ev({ kind: 'note', text: code, parts: [{ code, values, text: code }] });
const asked = (text: string) => ev({ kind: 'user', origin: 'person', text });
const answer = (text: string) => ev({ kind: 'message', text });
const done = () => ev({ kind: 'turn', stopReason: 'end_turn' });

/** One part of a line as the page draws it: its events, its own record's liveness, and how it began where no move says so. */
type Part = { events: SessionEvent[]; live?: boolean; began?: string };

/** The line the organism draws, from the parts' events: the press first, then each part, the attended one last. */
function Line({ parts, more = 0 }: { parts: Part[]; more?: number }) {
  return (
    <Tooltip.Provider>
      <section aria-label="line" className="grid gap-3">
        {more > 0 && <ShowEarlier count={more} onPress={() => {}} />}
        {parts.map((part, index) => {
          const turns = settle(toTurns(part.events).turns, Boolean(part.live));
          return (
            <ConversationView
              key={index}
              turns={turns}
              chat
              live={Boolean(part.live)}
              began={beginsByMove(turns) ? undefined : part.began}
            />
          );
        })}
      </section>
    </Tooltip.Provider>
  );
}

const meta: Meta<typeof Line> = {
  title: 'Work/LineView',
  component: Line,
  decorators: [sessionWidths.wide],
};
export default meta;

type Story = StoryObj<typeof Line>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const WRITTEN = { session: 'c0000002', agent: 'claude-code', account: 'work', why: 'limit', tokens: 182000, count: 4096 };

/** A conversation handed to another agent: the earlier part's turns, then both ends of the hand-over, and the new agent's words. */
const HANDED: Part[] = [
  { events: [asked('Fix the loader.'), answer('Read the loader; the cache key ignores the locale.'), done(),
    note('chat.handed-over', { agent: 'claude-code', session: 'c0000002' })] },
  { events: [note('chat.taken-over', { agent: 'codex-acp', session: 'c0000001', why: 'press' }), answer('Going on from Daoris’s record: I will key the cache by locale.')], live: true },
];
export const HandedToAnotherAgent: Story = { args: { parts: HANDED } };
export const HandedToAnotherAgentChinese: Story = { ...HandedToAnotherAgent, decorators: [chinese, sessionWidths.wide] };
export const HandedToAnotherAgentNarrow: Story = { ...HandedToAnotherAgent, decorators: [sessionWidths.narrow] };
export const HandedToAnotherAgentDark: Story = { ...HandedToAnotherAgent, decorators: [dark, sessionWidths.wide] };

/** The curve's move: one line on each side, and the new part’s opening line folded under its boundary (a press shows it). */
const FIGURES = { window: 'weekly', used: 62, curve: 45 };
const CURVE: Part[] = [
  { events: [asked('Fix the loader.'), answer('Reading the loader.'), done(), note('chat.moved', { to: 'work', from: 'personal', ...FIGURES })] },
  { events: [
    note('opening.opened', { account: 'work', why: 'locked', agent: 'claude-code', over: 'personal' }),
    note('chat.moved-from', { from: 'personal', ...FIGURES }),
    answer('Going on on the other account.'),
  ], live: true },
];
export const CurveMove: Story = { args: { parts: CURVE } };
export const CurveMoveChinese: Story = { ...CurveMove, decorators: [chinese, sessionWidths.wide] };

/** A driven line’s briefed move, written before the rewording: the old words’ values, whole in the new words, tokens and count shown. */
const BRIEFED: Part[] = [
  { events: [answer('Cut off by the account’s limit.'), note('moved.on', WRITTEN)] },
  { events: [note('moved.from', { ...WRITTEN, session: 'c0000001' }), answer('Going on from the brief.')], live: true },
];
export const BriefedMoveWrittenBefore: Story = { args: { parts: BRIEFED } };
export const BriefedMoveWrittenBeforeChinese: Story = { ...BriefedMoveWrittenBefore, decorators: [chinese, sessionWidths.wide] };

/** A part that began by no move: the strip's *began* words are its one boundary line, no separator of the page’s own. */
export const BoundedByItsBeganWords: Story = {
  args: {
    parts: [
      { events: [asked('Fix the loader.'), answer('The first try failed on the cache key.'), done()] },
      { events: [answer('Trying again from the failed part.')], began: 'retried after part 1', live: true },
    ],
  },
};
export const BoundedByItsBeganWordsChinese: Story = { ...BoundedByItsBeganWords, decorators: [chinese, sessionWidths.wide] };

/** An earlier part still live is drawn live: its last turn not cut, its calls not stopped. */
export const AnEarlierPartStillLive: Story = {
  args: {
    parts: [
      { events: [asked('Run the suite.'), answer('Running it.'),
        ev({ kind: 'tool', id: 'x1', title: 'npm test', toolKind: 'execute', status: 'in_progress' })], live: true },
      { events: [answer('The new part, going on beside it.')], began: 'carried on from part 1', live: true },
    ],
  },
};

/** A line of five earlier parts draws the nearest three, and the press says how many more it draws. */
const part = (n: number): Part => ({
  events: [asked(`Part ${n}: what next?`), answer(`Part ${n} answers.`), done()], began: n === 1 ? undefined : `carried on from part ${n - 1}`,
});
export const FiveEarlierPartsDrawThree: Story = { args: { parts: [part(3), part(4), part(5), { ...part(6), live: true }], more: 2 } };
export const FiveEarlierPartsDrawThreeChinese: Story = { ...FiveEarlierPartsDrawThree, decorators: [chinese, sessionWidths.wide] };
/** One part more: the press says *the part before*. */
export const OnePartMore: Story = { args: { parts: [part(2), part(3), part(4), { ...part(5), live: true }], more: 1 } };
export const OnePartMoreChinese: Story = { ...OnePartMore, decorators: [chinese, sessionWidths.wide] };
