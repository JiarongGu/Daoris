import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import type { CommandShown, PluginCommandRound } from './interfaces';
import { RoundCard } from './RoundCard';
import { InTheme } from './storyIcons';

// The round card in each state a sign-in passes through: a device code with its page, a hidden token, the end with what the plugin
// handed back, an ending other than 0, and a command that asks its arguments and its confirm first; in both themes and languages.

const RUN = '0123456789abcdef0123456789abcdef';

const SIGN_IN: CommandShown = {
  name: 'login', title: { en: 'Sign in', zh: '登录' }, about: '', interactive: true, tool: false, writes: false, confirm: null,
  on: [], args: [], adopts: null,
};
const START: CommandShown = {
  ...SIGN_IN, name: 'start-stage', title: { en: 'Start a release stage', zh: '启动发布阶段' }, interactive: false, writes: true,
  confirm: { en: 'Start this stage now? Approvals still decide.', zh: '现在启动此阶段？仍由审批决定。' },
  args: [
    { name: 'stage', type: 'text', title: { en: 'Stage', zh: '阶段' }, required: true, choices: [] },
    { name: 'wait', type: 'boolean', title: { en: 'Wait for it to end', zh: '等待它结束' }, required: false, choices: [] },
  ],
};

const round = (over: Partial<PluginCommandRound>): PluginCommandRound => ({
  run: RUN, said: [], ask: null, open: null, code: null, then: null, data: null, exit: 0, ...over,
});

/** A stand-in plugin that answers each round from a script and keeps the last one waiting. */
const scripted = (rounds: PluginCommandRound[]) => async () => (rounds.length > 1 ? rounds.shift()! : rounds[0]!);

const DEVICE = [round({
  said: ['To sign in, open the page and enter the code.', 'Press Go on once the page says you are signed in.'],
  open: 'https://sign-in.example/device', code: 'K7QX-M2PD', ask: { name: 'signed-in', prompt: 'Signed in at the page?', masked: false },
})];
const TOKEN = [round({
  said: ['Paste a personal access token with Code read and write.'], ask: { name: 'token', prompt: 'Personal access token', masked: true },
})];
const DONE = [round({ said: ['Signed in as builder@example.org.', 'The token expires on 2027-02-01.'], data: { signedIn: true, expires: '2027-02-01' } })];
const REFUSED = [round({ said: ['The service refused the token: it lacks Code (read and write).'], exit: 3 })];

const meta: Meta<typeof RoundCard> = {
  title: 'Plugins/RoundCard',
  component: RoundCard,
  args: { plugin: 'acme-boards', pluginName: 'Acme boards', command: SIGN_IN, place: 'machine', onEnded: () => {}, onClose: () => {} },
  decorators: [(Story) => <div className="w-[40rem] max-w-full"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof RoundCard>;

export const DeviceCode: Story = { args: { send: scripted([...DEVICE]) }, render: (args) => <InTheme theme="light"><RoundCard {...args} /></InTheme> };
export const DeviceCodeDark: Story = { args: { send: scripted([...DEVICE]) }, render: (args) => <InTheme theme="dark"><RoundCard {...args} /></InTheme> };
export const DeviceCodeChinese: Story = { args: { send: scripted([...DEVICE]) }, decorators: [chinese] };
export const HiddenToken: Story = { args: { send: scripted([...TOKEN]) }, render: (args) => <InTheme theme="light"><RoundCard {...args} /></InTheme> };
export const HiddenTokenDark: Story = { args: { send: scripted([...TOKEN]) }, render: (args) => <InTheme theme="dark"><RoundCard {...args} /></InTheme> };
export const HiddenTokenChinese: Story = { args: { send: scripted([...TOKEN]) }, decorators: [chinese] };
export const Done: Story = { args: { send: scripted([...DONE]) } };
export const DoneDark: Story = { args: { send: scripted([...DONE]) }, render: (args) => <InTheme theme="dark"><RoundCard {...args} /></InTheme> };
export const Refused: Story = { args: { send: scripted([...REFUSED]) } };
export const RefusedChinese: Story = { args: { send: scripted([...REFUSED]) }, decorators: [chinese] };
export const AsksFirst: Story = { args: { command: START, send: scripted([round({ said: ['Started.'] })]) } };
export const AsksFirstDark: Story = {
  args: { command: START, send: scripted([round({ said: ['Started.'] })]) },
  render: (args) => <InTheme theme="dark"><RoundCard {...args} /></InTheme>,
};
export const AsksFirstChinese: Story = { args: { command: START, send: scripted([round({ said: ['已启动。'] })]) }, decorators: [chinese] };
