import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { LANGUAGES } from '../fixtures';
import { WorkspaceSetup, type WorkspaceSetupProps, type WorkspaceWiring } from '../setup/WorkspaceSetup';
import { WorkspaceDetails } from './WorkspacePage';

// The homes of what the workspace's Setup tab holds elsewhere: Remote as a row of Repositories'
// workspace Details; the rules and the machines its remote counts undeclared as Reach among the workspace place's Sessions; the line's
// defaults among its Workflow. At the main area's three widths (80, 39 and 25 rem), in both languages and both themes; a `play` puts the
// focus where a keyboard lands first.

const nothing = () => {};
const REPOSITORIES = ['api', 'billing', 'gateway'];

type Home = 'remote' | 'reach' | 'defaults';
type Args = {
  home: Home;
  /** The remote's state: local only, with the form open, wired, or named whole by the environment. */
  remote?: 'local' | 'open' | 'wired' | 'environment';
  /** Reach with nothing set and every machine declared. */
  empty?: boolean;
  /** The row a door asked for takes the focus. */
  focus?: boolean;
};

const WIRED = { url: 'https://work.example.com', key: 'dk_abcd1234…' };

function Page({ home, remote = 'local', empty = false, focus = false }: Args) {
  if (home === 'remote') {
    const wiring: WorkspaceWiring = {
      remote: remote === 'wired' || remote === 'environment' ? WIRED : null,
      fromEnvironment: remote === 'environment',
      onWire: nothing,
      onUnwire: nothing,
    };
    return <WorkspaceDetails repositories={REPOSITORIES} onOpen={nothing} remote={{ workspace: 'work', wiring, startOpen: remote === 'open', focus }} />;
  }
  const props: WorkspaceSetupProps = home === 'reach'
    ? {
        workspace: 'work',
        rows: 'sessions',
        open: 'reach',
        defaults: {},
        remote: {
          rules: {
            lists: empty
              ? { allow: [], ask: [], deny: [] }
              : { allow: ['Bash(npm run test:*)'], ask: ['Bash(az repos pr create:*)'], deny: ['Bash(rm -rf:*)'] },
            onAdd: nothing,
            onRemove: nothing,
          },
          machines: { undeclared: empty ? [] : ['laptop-2', 'build-agent-old-7f3a'], onRetire: nothing },
        },
      }
    : {
        workspace: 'work',
        rows: 'defaults',
        open: 'defaults',
        defaults: {
          line: { set: 'develop', onSet: nothing },
          language: { table: LANGUAGES, onSet: nothing },
          commits: { onChange: nothing },
          review: { onChange: nothing },
          opinion: { onChange: nothing },
        },
        remote: {},
      };
  return <WorkspaceSetup {...props} />;
}

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof Page> = { title: 'Repositories/WorkspaceHomes', component: Page, args: { home: 'remote' }, decorators: [storyWidths.wide] };
export default meta;

type Story = StoryObj<typeof Page>;

const focusFirst: Story['play'] = async ({ canvasElement }) => {
  canvasElement.querySelector<HTMLElement>('[data-workspace-row], [data-place-row], button')?.focus();
};

/** Details' Remote row, local only: *Wire to a remote…* one press away. */
export const RemoteLocal: Story = {};
export const RemoteLocalHalf: Story = { decorators: [storyWidths.half] };
export const RemoteLocalNarrow: Story = { decorators: [storyWidths.narrow] };
export const RemoteLocalChinese: Story = { decorators: [chinese] };
export const RemoteLocalChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };
export const RemoteLocalDark: Story = { decorators: [dark] };

/** The header's *Wire to a remote…* pressed: the form open in the row, the address and the key. */
export const RemoteWiring: Story = { args: { remote: 'open' } };
export const RemoteWiringHalfChinese: Story = { args: { remote: 'open' }, decorators: [storyWidths.half, chinese] };
export const RemoteWiringNarrowDark: Story = { args: { remote: 'open' }, decorators: [storyWidths.narrow, dark] };

/** Wired: the address, the key's audit prefix and *Unwire*. */
export const RemoteWired: Story = { args: { remote: 'wired' } };
export const RemoteWiredNarrow: Story = { args: { remote: 'wired' }, decorators: [storyWidths.narrow] };
export const RemoteWiredChinese: Story = { args: { remote: 'wired' }, decorators: [chinese] };
export const RemoteWiredDark: Story = { args: { remote: 'wired' }, decorators: [dark] };

/** The environment names the remote whole, and the file is not read: the row says which source decided. */
export const RemoteFromTheEnvironment: Story = { args: { remote: 'environment' } };

/** A door to the row: it takes the focus, whose ring shows. */
export const RemoteFocused: Story = { args: { remote: 'wired', focus: true } };

/** Reach among the place's Sessions: three rules, one in each list, and two machines the remote counts undeclared, each with *Retire*. */
export const Reach: Story = { args: { home: 'reach' } };
export const ReachHalf: Story = { args: { home: 'reach' }, decorators: [storyWidths.half] };
export const ReachNarrow: Story = { args: { home: 'reach' }, decorators: [storyWidths.narrow] };
export const ReachChinese: Story = { args: { home: 'reach' }, decorators: [chinese] };
export const ReachChineseNarrow: Story = { args: { home: 'reach' }, decorators: [storyWidths.narrow, chinese] };
export const ReachDark: Story = { args: { home: 'reach' }, decorators: [dark] };
export const ReachDarkNarrow: Story = { args: { home: 'reach' }, decorators: [storyWidths.narrow, dark] };

/** Reach with no rule of its own and every machine declared. */
export const ReachEmpty: Story = { args: { home: 'reach', empty: true } };

/** Keyboard: the focus rests on the first press of the section, whose ring shows. */
export const ReachFocused: Story = { args: { home: 'reach' }, play: focusFirst };

/** The line's defaults among the place's Workflow: its line, how work lands, its commits, its review and its second opinion. */
export const Defaults: Story = { args: { home: 'defaults' } };
export const DefaultsHalf: Story = { args: { home: 'defaults' }, decorators: [storyWidths.half] };
export const DefaultsNarrow: Story = { args: { home: 'defaults' }, decorators: [storyWidths.narrow] };
export const DefaultsChinese: Story = { args: { home: 'defaults' }, decorators: [chinese] };
export const DefaultsChineseNarrow: Story = { args: { home: 'defaults' }, decorators: [storyWidths.narrow, chinese] };
export const DefaultsDark: Story = { args: { home: 'defaults' }, decorators: [dark] };
export const DefaultsFocused: Story = { args: { home: 'defaults' }, play: focusFirst };
