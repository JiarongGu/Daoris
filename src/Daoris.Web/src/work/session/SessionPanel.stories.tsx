import type { Meta, StoryObj } from '@storybook/react-vite';
import { Button } from '../../design/ui';
import { chinese } from '../../design/storyLanguage';
import { sessionWidths } from '../../design/storyWidths';
import { LiveBand } from '../band/LiveBand';
import { type PanelFacts, SessionPanel } from './SessionPanel';
import { storyOf } from './story';
import { BARE_WAIT, CHANGES, HEAD, LINE, RELEASE_WAIT, TRACK, at } from './session.fixtures';

// The session panel over one line of work, in each state the design names (empty, loading, live, waiting on you, waiting on
// another, failed, finished, stale), at the main area's widths: wide with its Changes rail, half and narrow folded; in English and
// in Chinese. The shots draw each in both themes. The band is the page's own, here with nothing beside the agent's act.

const NOW = Date.parse(at(15, 18));
const STORY = storyOf(LINE);
const LIVE: PanelFacts = {
  id: 's1', head: HEAD, track: TRACK, story: STORY, waits: [], changes: CHANGES, now: NOW, reads: 'next', readMark: Date.parse(at(9, 22)),
  onAnswer: () => {}, onSend: () => {}, onStep: () => {}, onReview: () => {}, onFile: () => {},
};
const ACTS = (
  <>
    <Button variant="ghost">Pause</Button>
    <Button variant="ghost">Stop</Button>
    <Button variant="ghost" aria-label="More">⋯</Button>
  </>
);
const BAND = <LiveBand lifetimes={[]} act={{ title: 'npm run e2e', since: at(15, 14) }} stoppable={new Set()} />;

const meta: Meta<typeof SessionPanel> = {
  title: 'Work/Session/SessionPanel',
  component: SessionPanel,
  args: { facts: LIVE, band: BAND, acts: ACTS },
  decorators: [sessionWidths.wide],
  render: (args) => <div className="h-[44rem]"><SessionPanel {...args} /></div>,
};
export default meta;
type Story = StoryObj<typeof SessionPanel>;

export const Live: Story = {};
export const LiveHalf: Story = { decorators: [sessionWidths.half] };
export const LiveNarrow: Story = { decorators: [sessionWidths.narrow] };
export const LiveInChinese: Story = { decorators: [sessionWidths.wide, chinese] };
export const LiveNarrowInChinese: Story = { decorators: [sessionWidths.narrow, chinese] };

const WAITING: PanelFacts = {
  ...LIVE, head: { ...HEAD, state: 'waiting', since: at(12, 18), doing: null }, waits: [RELEASE_WAIT, BARE_WAIT],
  track: { ...TRACK, chips: TRACK.chips.map((chip) => (chip.node === 'approve'
    ? { ...chip, mark: 'current', wait: { run: 'r1', node: 'approve', why: 'approval' } } : chip.node === 'check' ? { ...chip, mark: 'passed', attempt: null } : chip)), at: 4 },
};
export const WaitingOnYou: Story = { args: { facts: WAITING, band: undefined } };
export const WaitingOnYouNarrow: Story = { args: { facts: WAITING, band: undefined }, decorators: [sessionWidths.narrow] };
export const WaitingOnYouInChinese: Story = { args: { facts: WAITING, band: undefined }, decorators: [sessionWidths.wide, chinese] };

export const WaitingOnAnother: Story = {
  args: { facts: { ...LIVE, head: { ...HEAD, state: 'waitsOn', waitsOn: 'web-app: the limits contract', doing: null } }, band: undefined },
};

const FAILED: PanelFacts = {
  ...LIVE,
  head: { ...HEAD, state: 'failed', since: at(15, 2), doing: 'e2e: 2 of 14 failed' },
  track: { ...TRACK, chips: TRACK.chips.map((chip) => (chip.node === 'check' ? { ...chip, mark: 'failed', attempt: 3 } : chip)) },
  story: [...STORY, {
    kind: 'step', key: 'step:r1/check/end/3', at: Date.parse(at(15, 2)), node: 'check', edge: 'end', mark: 'failed', attempt: 3,
  }],
  waits: [{
    id: 'w3', title: 'The check failed three times: try again on another model?', kind: 'failed', since: at(15, 2),
    what: 'Two e2e tests time out on the burst path; a second agent read the logs and found no flake.',
    yes: { label: 'Try again with Claude · opus-4.1', effect: 'Runs the check again on the stronger model.' },
    no: { label: 'Drop this step', effect: 'The run goes on without the check; the release waits for you.' },
    look: [{ label: 'run #421', href: '#run' }], known: ['3 attempts', 'logs read by Codex · gpt-5.1'],
    from: { role: 'checker', agent: 'Claude', model: 'sonnet-4.5', at: at(15, 2) },
  }],
};
export const Failed: Story = { args: { facts: FAILED, band: undefined } };
export const FailedNarrow: Story = { args: { facts: FAILED, band: undefined }, decorators: [sessionWidths.narrow] };

const FINISHED: PanelFacts = {
  ...LIVE, head: { ...HEAD, state: 'finished', since: at(16, 2), doing: 'Landed on main 16:02' }, reads: 'finished',
  track: { ...TRACK, chips: TRACK.chips.map((chip) => ({ ...chip, mark: 'passed', attempt: null })), at: 5 },
  changes: { ...CHANGES, pullRequest: { name: '#212' } },
};
export const Finished: Story = { args: { facts: FINISHED, band: undefined } };
export const FinishedInChinese: Story = { args: { facts: FINISHED, band: undefined }, decorators: [sessionWidths.wide, chinese] };

export const Stale: Story = {
  args: { facts: { ...LIVE, head: { ...HEAD, state: 'stale', since: at(15, 31), doing: null } }, band: undefined },
};
export const Loading: Story = { args: { facts: { ...LIVE, head: { ...HEAD, state: 'loading' }, story: [] }, loading: true, band: undefined } };
export const Empty: Story = { args: { facts: null } };
export const EmptyInChinese: Story = { args: { facts: null }, decorators: [sessionWidths.wide, chinese] };
