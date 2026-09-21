import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Quest, Session } from '../api';
import { AttendedSession } from './AttendedSession';

// The assembled region. The CONSOLE is absent in every story below and that is the guarantee
// working, not a gap: a stream arrives over the shell's bridge and Storybook has none, exactly as a
// browser over a keyed remote has none (D47 §4). What a reviewer judges here is the record and the
// observed layer around the hole the stream fills on a desktop.

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const SESSION: Session = {
  id: 's1a2b3c4',
  quest: '7a82cc',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  kind: 'driven',
  harnessVersion: '2.1.4',
  profile: 'owner',
  tree: 'C:/somewhere/.daoris/trees/default/engine/streaming-budget',
  created: at(134),
  updated: at(4),
};

const QUEST: Quest = {
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs a per-frame cap.',
  status: 'Taken',
  filed: at(10_000),
  updated: at(130),
};

const meta: Meta<typeof AttendedSession> = {
  title: 'Work/AttendedSession',
  component: AttendedSession,
  args: { session: SESSION, quest: QUEST },
  decorators: [(Story) => <div className="max-w-3xl p-4"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof AttendedSession>;

export const Attending: Story = {};

/** Nothing chosen. The frame lands here on a first launch, so it is a designed state. */
export const NothingAttended: Story = { args: { session: null, quest: null } };

/** Parked — the whole region reorganised around the thing that is waiting on a person. */
export const Parked: Story = {
  args: {
    session: {
      ...SESSION,
      state: 'awaiting-person',
      created: at(52),
      updated: at(11),
      note: 'Two ways forward.\n\n1. Cap hydration in the scheduler — smaller change, but the budget then lives away from the API that spends it.\n2. Cap it on the chunk API itself — touches more call sites, and the budget ends up where the work is.\n\nI recommend the second: the quest asks for the budget on the chunk API, and option 1 would leave that promise half kept.',
    },
  },
};

/** Finished, reviewable: the lifetime, the quest's close, and what came out of it. */
export const Finished: Story = {
  args: {
    session: {
      ...SESSION,
      state: 'completed',
      created: at(95),
      updated: at(50),
      note: 'gates green; the session closed the quest through its own door.',
      evidence: 'commits landed:\nc0ffee12 cap hydration work per frame\ndeadbee1 expose the budget on the chunk API',
    },
    quest: { ...QUEST, status: 'Done', updated: at(51) },
  },
};
