import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import type { AskLine } from '../work/entries';
import { AskLines } from './AskLines';

// An ask's lines of work (SESSONE1c, D167 point 5), as its page's *sessions* section draws them (`AskPage.tsx`), on their own
// (STORYGAP1, the UI/UX design §1.2 item 6: no `AskPage` story hands it lines): one line per repository's line of work, its
// parts counted and its leading part's word, each a door into Sessions where Sessions is here; a word the page has none for,
// as the driver wrote it; at the main area's three widths, in 中文 and dark.

const nothing = () => {};
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const LINES: AskLine[] = [
  { entry: 'ask:7c1e9a/engine', repository: 'engine', parts: 3, leading: 's-engine-3', shown: 'working' },
  { entry: 'ask:7c1e9a/game', repository: 'game', parts: 1, leading: 's-game-1', shown: 'awaiting-person' },
  { entry: 'ask:7c1e9a/lantern', repository: 'lantern', parts: 2, leading: 's-lantern-2', shown: 'completed' },
];

/** A repository's name long enough to wrap at the floor, and a word this page has none for. */
const LONG: AskLine[] = [
  ...LINES,
  { entry: 'ask:7c1e9a/telemetry', repository: 'telemetry-ingest-and-retention-service', parts: 12, leading: 's-tel-12', shown: 'rehearsing' },
];

const meta = {
  title: 'Asks/AskLines',
  component: AskLines,
  args: { lines: LINES, onAttend: nothing },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof AskLines>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Three repositories' lines of work, each a door into Sessions at its leading part. */
export const Lines: Story = {};

/** Where Sessions is not here (a browser): each named, and no door. */
export const NoDoor: Story = { args: { onAttend: undefined } };

/** A long name, many parts, and a word the page has none for, said as the driver wrote it, quiet. */
export const LongAndUnknown: Story = { args: { lines: LONG } };

/** Half: a 680 px window with the list a strip beside it. */
export const Half: Story = { args: { lines: LONG }, decorators: [storyWidths.half] };

/** The main area's 400 px floor: the long name wraps, its count and word under it. */
export const Narrow: Story = { args: { lines: LONG }, decorators: [storyWidths.narrow] };

/** In 中文, wide. */
export const Chinese: Story = { args: { lines: LONG }, decorators: [chinese] };

/** In 中文, at the floor. */
export const ChineseNarrow: Story = { args: { lines: LONG }, decorators: [chinese, storyWidths.narrow] };

/** In dark. */
export const Dark: Story = { args: { lines: LONG }, decorators: [dark] };

/** In 中文 and dark, half. */
export const ChineseDarkHalf: Story = { args: { lines: LONG }, decorators: [chinese, dark, storyWidths.half] };
