import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { LIST } from '../edit/editFixtures';
import { FULL } from '../fixtures';
import { ChangeWorkflow } from './ChangeWorkflow';

// Change… on a repository place's Workflow (AGENTSPLACE1h; the Agents-place design §3): the presets by their composed line, the workflows
// saved here with the places that choose them, and Current, live and derived, at the main area's 80, 39 and 25 rem, in 中文 and in dark.

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const list = {
  ...LIST,
  workflows: [
    ...LIST.workflows,
    {
      id: 'verify-docs', name: 'Verify the docs first', problem: null, versions: [1, 2], schema: 2 as const, digest: 'cd', line: 'Docs are checked before the work lands.',
      pipeline: null, chosen: [{ level: 'workspace' as const, name: 'aurora', role: 'kind' as const, kind: 'docs' }],
    },
  ],
};

const meta: Meta<typeof ChangeWorkflow> = {
  title: 'Workflow/ChangeWorkflow',
  component: ChangeWorkflow,
  args: {
    list, current: FULL, place: { repository: 'engine' }, onUse: async () => ({ done: true, refused: null, message: null, changed: true, reach: [] }),
    onStartFrom: nothing, onClose: nothing,
  },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof ChangeWorkflow>;

export const Lists: Story = {};
export const ListsHalf: Story = { decorators: [storyWidths.half] };
export const ListsNarrow: Story = { decorators: [storyWidths.narrow] };

export const ListsChinese: Story = { decorators: [chinese] };
export const ListsChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const ListsChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

export const ListsDark: Story = { decorators: [dark] };
export const ListsDarkHalf: Story = { decorators: [storyWidths.half, dark] };
export const ListsDarkNarrow: Story = { decorators: [storyWidths.narrow, dark] };

/** The lists on their way, and the read refused. */
export const Reading: Story = { args: { list: null } };
export const ReadRefused: Story = { args: { list: null, error: new Error('the driver is still coming up — try again in a moment.') } };

/** No workflow saved here yet: the presets and Current alone. */
export const NothingSaved: Story = { args: { list: { workflows: [], presets: LIST.presets } } };
