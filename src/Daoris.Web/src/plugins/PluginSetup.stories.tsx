import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import { interfacesOf } from './interfaces';
import { PluginSetup } from './PluginSetup';
import { InTheme } from './storyIcons';

// A plugin's Set up tab: its sign-in commands with Run, an entry refused alone, its storage and its sign-out at removal, a view it
// declares, a part this build does not know, and Where it is on with a word at a workspace; then switched off at the place in force.
// In both themes and both languages.

const INTERFACES = interfacesOf({
  commands: [
    { name: 'login', title: { en: 'Sign in', zh: '登录' }, about: { en: 'Signs in by a device code, or keeps a personal access token.', zh: '用设备代码登录，或保存个人访问令牌。' }, interactive: true },
    { name: 'status', title: { en: 'Whether it is signed in', zh: '是否已登录' }, about: { en: 'Says who the token signs in as.', zh: '说出令牌以谁的身份登录。' }, tool: true },
    { name: 'logout', title: { en: 'Sign out', zh: '退出登录' }, about: { en: 'Forgets the token.', zh: '忘掉令牌。' }, writes: true },
  ],
  storage: { for: { en: 'The token its sign-in keeps, and when the service last refused it.', zh: '登录保存的令牌，以及服务上次拒绝它的时间。' }, forget: 'logout' },
  views: [{ id: 'pull-requests', title: { en: 'Pull requests', zh: '拉取请求' }, where: ['side', 'page'], places: ['repository'], context: ['session'] }],
  refused: [{ interface: 'commands', entry: 'deploy', problem: '`deploy`: a command for agents never asks the person.' }],
  unknown: ['status-items'],
});

const PLACES = [
  { key: 'workspace:home' }, { key: 'repository:engine', workspace: 'home' }, { key: 'repository:site', workspace: 'home' },
];

const meta: Meta<typeof PluginSetup> = {
  title: 'Plugins/PluginSetup',
  component: PluginSetup,
  args: {
    id: 'acme-boards', enabled: true, refused: false, interfaces: INTERFACES, points: ['work/perform', 'work/state'],
    switches: [{ what: 'tools', place: 'workspace:home', on: false }], place: 'machine', places: PLACES,
    onRun: () => {}, onSwitch: () => {},
  },
  decorators: [(Story) => <div className="w-[46rem] max-w-full"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof PluginSetup>;

export const Light: Story = { render: (args) => <InTheme theme="light"><PluginSetup {...args} /></InTheme> };
export const Dark: Story = { render: (args) => <InTheme theme="dark"><PluginSetup {...args} /></InTheme> };
export const Chinese: Story = { decorators: [chinese] };
export const ChineseDark: Story = { decorators: [chinese], render: (args) => <InTheme theme="dark"><PluginSetup {...args} /></InTheme> };

/** Its commands switched off at the workspace in force: every Run drawn disabled, with why. */
export const SwitchedOffHere: Story = {
  args: { place: 'workspace:home', switches: [{ what: 'commands', place: 'workspace:home', on: false }] },
};

/** At the side bar's narrow width: the table scrolls inside its own frame. */
export const Narrow: Story = { decorators: [(Story) => <div className="w-[20rem]"><Story /></div>] };
