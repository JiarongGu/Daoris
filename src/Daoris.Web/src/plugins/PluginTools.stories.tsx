import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import type { ToolShown } from './catalog';
import { PluginTools } from './PluginTools';
import { InTheme } from './storyIcons';

// A plugin's tools on its page (ADOSIGNIN1, D150 point 7), the Azure DevOps pull-request plugin's `az`: as its manifest says it
// before anyone has asked, after the person's Check with two unmet checks, a tool not found, a console open, and a press the
// driver turned down; at the page's width (80rem), a window's main area (39rem) and the 400 px floor (25rem), in 中文 and in dark.

const AZ: ToolShown = {
  id: 'az', name: 'Azure CLI', for: 'Opens the pull request, and reads its status before pushing to it again.', versions: '>=2.0.79',
  install: 'winget install -e --id Microsoft.AzureCLI',
  checks: [
    { says: 'Its devops extension is added', fix: 'az extension add --name azure-devops', signIn: false, press: true },
    { says: 'Signed in', fix: 'az login', signIn: true, press: true },
  ],
};

const UNMET: ToolShown = {
  ...AZ, found: true, ok: true, version: '2.70.0', sentence: 'Azure CLI 2.70.0, the system\'s, in its range (2.0.79 or newer).',
  checks: [
    { ...AZ.checks[0]!, ready: false, sentence: 'not ready: Its devops extension is added.' },
    { ...AZ.checks[1]!, ready: false, sentence: 'not ready: Signed in.' },
  ],
};

const nothing = () => {};

const meta: Meta<typeof PluginTools> = {
  title: 'Plugins/PluginTools',
  component: PluginTools,
  args: { plugin: 'azure-devops-pull-request', tools: [AZ], onCheck: nothing, onFix: nothing },
  decorators: [(Story) => <div className="w-[39rem] max-w-full border border-line bg-page p-4"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof PluginTools>;

const wide: Decorator = (Story) => <div className="w-[80rem] max-w-full"><Story /></div>;
const floor: Decorator = (Story) => <div className="w-[25rem] max-w-full"><Story /></div>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;
const light: Decorator = (Story) => <InTheme theme="light"><Story /></InTheme>;

/** As the manifest says it: nothing has been asked of the machine, so there is only *Check*, and no press. */
export const Declared: Story = { decorators: [light] };

/** Found, with the devops extension and the sign-in both unmet: *Set up* and *Sign in*, each naming the command it runs. */
export const Unmet: Story = { args: { tools: [UNMET] }, decorators: [light] };

/** The same at the page's width, in light. */
export const UnmetWide: Story = { args: { tools: [UNMET] }, decorators: [wide, light] };

/** The same at the page's width, in 中文 and in dark. */
export const UnmetWideChineseDark: Story = { args: { tools: [UNMET] }, decorators: [wide, chinese, dark] };

/** The same at a window's main area, in dark. */
export const UnmetDark: Story = { args: { tools: [UNMET] }, decorators: [dark] };

/** The same at a window's main area, in 中文 and in light. */
export const UnmetChinese: Story = { args: { tools: [UNMET] }, decorators: [chinese, light] };

/** The same at the 400 px floor, in light. */
export const UnmetFloor: Story = { args: { tools: [UNMET] }, decorators: [floor, light] };

/** The same at the 400 px floor, in 中文 and in dark. */
export const UnmetFloorChineseDark: Story = { args: { tools: [UNMET] }, decorators: [floor, chinese, dark] };

/** The same at the 400 px floor, in 中文 and in light. */
export const UnmetFloorChinese: Story = { args: { tools: [UNMET] }, decorators: [floor, chinese, light] };

/** The same at the 400 px floor, in dark. */
export const UnmetFloorDark: Story = { args: { tools: [UNMET] }, decorators: [floor, dark] };

/** Ready: the devops extension added and signed in, so no press; the standing is what the checks said. */
export const Ready: Story = {
  args: {
    tools: [{
      ...UNMET,
      checks: [
        { ...AZ.checks[0]!, ready: true, sentence: 'ready: Its devops extension is added.' },
        { ...AZ.checks[1]!, ready: true, sentence: 'ready: Signed in.' },
      ],
    }],
  },
  decorators: [light],
};

/** Not found: the plugin's own words for getting it, and no press, since a fix would have no tool to run. */
export const NotFound: Story = {
  args: { tools: [{ ...AZ, found: false, ok: false, sentence: '`az` is not on this machine\'s PATH.' }] },
  decorators: [light],
};

/** Not found, and the plugin names no way to get it. */
export const NotFoundNoHint: Story = {
  args: { tools: [{ ...AZ, install: null, found: false, ok: false }] },
  decorators: [floor, dark],
};

/** A console is open for the sign-in: every press waits, and the page says where the person acts. */
export const ConsoleOpen: Story = { args: { tools: [UNMET], fixing: { tool: 'az', check: 2 } }, decorators: [light] };

/** The same at the 400 px floor, in 中文 and in dark. */
export const ConsoleOpenFloorChineseDark: Story = {
  args: { tools: [UNMET], fixing: { tool: 'az', check: 2 } },
  decorators: [floor, chinese, dark],
};

/** A check being read: *Check* holds. */
export const Checking: Story = { args: { checking: true }, decorators: [light] };

/** A fix naming a command the plugin does not declare: said, and no press is drawn for it. */
export const FixNotDeclared: Story = {
  args: {
    tools: [{
      ...UNMET,
      checks: [{
        says: 'Signed in', fix: 'powershell -c whoami', signIn: true, press: false, ready: false, sentence: 'not ready: Signed in.',
        refusal: '`powershell` is not a tool this plugin declares in `tools`, so its `fix` is not run: Daoris runs only the commands of a plugin\'s own tools.',
      }],
    }],
  },
  decorators: [light],
};

/** A press the driver turned down after the check (the tool vanished between): its sentence, beside the check. */
export const PressRefused: Story = {
  args: {
    tools: [UNMET],
    refused: { tool: 'az', check: 2, why: 'not_found', message: '`az` is not on this machine\'s PATH.' },
  },
  decorators: [light],
};
