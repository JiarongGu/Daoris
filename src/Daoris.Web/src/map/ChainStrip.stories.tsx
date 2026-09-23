import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Quest, Session } from '../api';
import { ChainStrip } from './ChainStrip';
import { buildChain } from './chain';

// Chains as the service makes them (D65 §4): an ask becomes a quest, closing it publishes the next
// step with `parent` set, and what is still to come rides on the newest step. Built through
// `buildChain` rather than by hand, so a story cannot show a shape the model would never produce.

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const DEVELOP: Quest = {
  id: '7a82cc', from: 'ask #c7c4de', to: 'engine', title: 'Develop the media config reader',
  body: '', status: 'Done', filed: at(300), updated: at(120),
};

const VERIFY: Quest = {
  id: '91be07', from: 'engine', to: 'game', title: 'Verify #7a82cc in the browser',
  body: '', status: 'Taken', filed: at(120), updated: at(20), parent: '7a82cc',
  then: [{ to: 'engine', title: 'Report what the browser showed', body: '' }],
};

const session = (id: string, quest: string, minutesAgo: number, extra: Partial<Session> = {}): Session => ({
  id, quest, repository: 'engine', adapter: 'claude-code', harnessVersion: '2.1.270', profile: 'account-2',
  state: 'completed', kind: 'driven', created: at(minutesAgo), updated: at(minutesAgo - 5), ...extra,
});

const SESSIONS: Session[] = [
  session('s-first', '7a82cc', 290, { state: 'failed' }),
  session('s-second', '7a82cc', 200),
  session('s-verify', '91be07', 25, { state: 'working', repository: 'game', adapter: 'claude-code-acp' }),
];

const meta: Meta<typeof ChainStrip> = {
  title: 'Map/ChainStrip',
  component: ChainStrip,
  args: { chain: buildChain('91be07', [DEVELOP, VERIFY], SESSIONS) },
  decorators: [(Story) => <div className="max-w-xl"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof ChainStrip>;

/** The middle of a chain: the ask, a step done after two attempts, this one running, one to come. */
export const MidChain: Story = {};

/** Read from the first step: what came after it is listed with its own sessions. */
export const FromTheStart: Story = {
  args: { chain: buildChain('7a82cc', [DEVELOP, VERIFY], SESSIONS) },
};

/** A step whose parent this page does not hold — named, not invented and not dropped. */
export const ParentOutOfView: Story = {
  args: { chain: buildChain('91be07', [VERIFY], SESSIONS) },
};

/** Nothing has run yet: every step says so rather than looking empty. */
export const NothingRan: Story = {
  args: { chain: buildChain('91be07', [DEVELOP, VERIFY], []) },
};
