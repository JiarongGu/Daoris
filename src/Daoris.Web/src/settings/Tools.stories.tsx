import type { Meta, StoryObj } from '@storybook/react-vite';
import { choiceOf, GitSwitch, ToolCard, ToolLocations } from './Tools';
import { BUILT_IN, GH, GIT, MIRROR, NODE, PWSH } from './toolsFixtures';

// Settings → Tools (TOOLS7, D121 §4.1), in the shape the shell's TOOLS_LIST answers: a tool run as the system's, one
// managed with versions to choose, one about to download, a named file that is gone, one PATH does not find, git's
// switch asked about before it applies, and the resource locations with and without a look.

const handlers = {
  onChoice: () => {}, onUse: () => {}, onDownload: () => {}, onStop: () => {}, onDelete: () => {},
};

const meta = {
  title: 'Settings/Tools',
  component: ToolCard,
  args: { tool: GIT, choice: choiceOf(GIT), ...handlers },
} satisfies Meta<typeof ToolCard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const TheSystems: Story = {};

export const ManagedInUse: Story = { args: { tool: NODE, choice: choiceOf(NODE) } };

export const ManagedANewerOffered: Story = { args: { tool: NODE, choice: { ...choiceOf(NODE), version: '22.12.0' } } };

export const Downloading: Story = {
  args: {
    tool: { ...GH, running: { action: 'download', version: '2.63.0' } },
    choice: { ...choiceOf(GH), way: 'managed' },
    console: <pre className="m-0 font-mono text-meta text-ink-soft">  downloading https://github.com/cli/cli/releases/… (11.8 MB)</pre>,
  },
};

export const ANamedFileThatIsGone: Story = { args: { tool: PWSH, choice: choiceOf(PWSH), onBrowse: () => {} } };

export const NotOnPath: Story = { args: { tool: GH, choice: choiceOf(GH) } };

export const NoVersionNamed: Story = { args: { tool: { ...GH, offered: [] }, choice: { way: 'managed', version: '', file: '' } } };

export const GitNotYetDownloaded: Story = {
  args: { choice: { ...choiceOf(GIT), way: 'managed', version: '2.50.1' }, onAsk: () => {} },
};

export const GitSwitchAsked: Story = {
  args: {
    choice: { ...choiceOf(GIT), way: 'managed', version: '2.51.0' },
    onAsk: () => {},
    asking: {
      use: { action: 'managed', version: '2.51.0' },
      reading: false,
      answer: {
        now: { file: 'C:/somewhere/Git/cmd/git.exe' },
        then: { file: 'C:/somewhere/data/tools/git/2.51.0/package/cmd/git.exe' },
        keys: [{ key: 'core.autocrlf', now: 'true', then: null }, { key: 'core.symlinks', now: 'false', then: 'true' }],
        same: false,
      },
    },
  },
};

export const GitSwitchReading: Story = {
  render: () => <GitSwitch asking={{ use: { action: 'system' }, reading: true }} onConfirm={() => {}} onCancel={() => {}} />,
};

export const GitSwitchTheSame: Story = {
  render: () => (
    <GitSwitch
      asking={{ use: { action: 'system' }, reading: false, answer: { now: {}, then: {}, keys: [], same: true } }}
      onConfirm={() => {}}
      onCancel={() => {}}
    />
  ),
};

export const Locations: Story = {
  render: () => <ToolLocations locations={[MIRROR]} builtIn={BUILT_IN} onLook={() => {}} onAdd={() => {}} onRemove={() => {}} />,
};

export const LocationsAfterALook: Story = {
  render: () => (
    <ToolLocations
      locations={[MIRROR, { address: 'https://gone.example/resources.json', integrity: 'gone.example', exists: false, names: [], notes: [] }]}
      builtIn={BUILT_IN}
      looks={[
        { address: MIRROR.address!, outcome: 'fetched', sentence: 'fetched', added: ['node 22.12.0'], dropped: [] },
        { address: 'https://gone.example/resources.json', outcome: 'failed', sentence: 'https://gone.example/resources.json could not be fetched (could not reach gone.example); it has never been fetched, so it names nothing', added: [], dropped: [] },
      ]}
      onLook={() => {}}
      onAdd={() => {}}
      onRemove={() => {}}
    />
  ),
};

export const OnlyTheListBuiltIn: Story = {
  render: () => <ToolLocations locations={[]} builtIn={BUILT_IN} onLook={() => {}} onAdd={() => {}} onRemove={() => {}} />,
};
