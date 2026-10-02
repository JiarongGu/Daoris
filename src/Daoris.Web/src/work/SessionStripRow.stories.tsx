import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Session } from '../api';
import { SessionStripRow } from './SessionRow';

// A session in the rail's 56px strip (FRAME6): its repository's initial and its mark, with the title,
// the repository and the state as its name and tip — the strip has no room for the words.

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const SESSION: Session = {
  id: 's1a2b3c4', quest: null, repository: 'engine', adapter: 'claude-code',
  state: 'working', kind: 'chat', created: at(20), updated: at(1),
};

const meta: Meta<typeof SessionStripRow> = {
  title: 'Work/SessionStripRow',
  component: SessionStripRow,
  args: { session: SESSION },
  // The strip's real width, and the list a row sits in.
  decorators: [(Story) => (
    <ul className="m-0 grid w-14 list-none justify-items-center gap-1 border border-line bg-raised px-0 py-1.5">
      <Story />
    </ul>
  )],
};
export default meta;

type Story = StoryObj<typeof SessionStripRow>;

/** Working: the live mark. */
export const Working: Story = {};

/** The one being attended, with the accent rail the activity bar gives its current place. */
export const Attended: Story = { args: { selected: true } };

/** Waiting on the person: the attention mark outranks being busy. */
export const AwaitingPerson: Story = { args: { session: { ...SESSION, state: 'awaiting-person' } } };

/** A parked quest's last session (SESSUX1c): the waiting mark, and *parked* in its name and tip. */
export const Parked: Story = {
  args: {
    session: { ...SESSION, state: 'failed', kind: 'driven', quest: '7a82cc' },
    grouping: { session: SESSION.id, group: 'you', shown: 'parked', archived: false, teammate: false, strikes: 3 },
  },
};

/** A repository named in 中文: its first character, whole. */
export const ChineseName: Story = { args: { session: { ...SESSION, repository: '引擎' } } };
