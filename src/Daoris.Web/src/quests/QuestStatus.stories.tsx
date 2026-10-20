import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import type { Quest } from '../data/api';
import { DECLINED, DONE, OPEN, REOPENED, SEALED, SUBMITTED, TAKEN } from './fixtures';
import { QuestStatus } from './QuestStatus';

// A quest's closure states (FABRIC-CLOSE1d2) where a list or a line draws its status off its page: the chain, the map's detail,
// an ask's work, a session's relations, Overview. On its own (STORYGAP1, the UI/UX design §1.2 item 6): *submitted* changes what
// a list reads as done, and *source reopened* stands beside it. Drawn as a line of an ask's work draws it, title then status,
// against every status a quest no closure holds reads; at the main area's three widths, in 中文 and dark.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/** Each quest as a line draws it: its title, then its status. */
function Lines({ quests }: { quests: readonly Quest[] }) {
  return (
    <ul className="m-0 grid list-none gap-1.5 p-0">
      {quests.map((quest) => (
        <li key={quest.id} className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-body">
          <span className="min-w-0 wrap-anywhere text-ink">{quest.title}</span>
          <QuestStatus quest={quest} />
        </li>
      ))}
    </ul>
  );
}

/** In a closure graph: submitted and held for the person's yes, submitted and sealed, and a step whose source was reopened. */
const CLOSURE: readonly Quest[] = [SUBMITTED, SEALED, REOPENED];
/** And as a quest no closure holds reads, beside them. */
const EVERY: readonly Quest[] = [OPEN, TAKEN, ...CLOSURE, DONE, DECLINED];

const meta = {
  title: 'Quests/QuestStatus',
  component: Lines,
  args: { quests: EVERY },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof Lines>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Every status a line draws: open, taken, the three closure states, done and declined. */
export const Every: Story = {};

/** *Submitted*, its pill keeping the status's tone, the hold beside it. */
export const Submitted: Story = { args: { quests: [SUBMITTED, SEALED] } };

/** *Source reopened* beside a step whose source was reopened: keyed on the hold, not the status. */
export const SourceReopened: Story = { args: { quests: [REOPENED, { ...REOPENED, id: 'ro1ro1', status: 'Open', title: 'A withdrawn step, open again' }] } };

/** Half: a 680 px window with the list a strip beside it. */
export const Half: Story = { decorators: [storyWidths.half] };

/** The main area's 400 px floor: a title wraps, its pills under it. */
export const Narrow: Story = { decorators: [storyWidths.narrow] };

/** In 中文, wide. */
export const Chinese: Story = { decorators: [chinese] };

/** In 中文, at the floor. */
export const ChineseNarrow: Story = { decorators: [chinese, storyWidths.narrow] };

/** In dark. */
export const Dark: Story = { decorators: [dark] };

/** In 中文 and dark, half. */
export const ChineseDarkHalf: Story = { decorators: [chinese, dark, storyWidths.half] };
