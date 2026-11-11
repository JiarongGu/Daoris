import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { InTheme } from '../../plugins/storyIcons';
import { FocusDivider } from './FocusDivider';

// The transcript's line above the message that carried a change of focus (`docs/2026-11-11-ask-scope-ux-design.md` §5), at the
// panel's narrow, default and wide widths (300, 384 and 480 px), in both languages and both themes.

const at = '2026-11-11T14:02:00';
const games = { kind: 'workspace', workspace: 'Games' } as const;
const engine = { kind: 'repository', workspace: 'Games', repository: 'engine' } as const;

const meta = {
  title: 'Help/FocusDivider',
  component: FocusDivider,
  decorators: [(Story) => (
    <div className="flex flex-wrap gap-6 bg-page py-3">
      {[300, 384, 480].map((width) => <div key={width} style={{ width }} className="@container px-3"><Story /></div>)}
    </div>
  )],
  args: { mark: { word: 'w2', at, from: games, to: engine, widened: false } },
} satisfies Meta<typeof FocusDivider>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Changed: Story = {};

export const ChangedChinese: Story = { decorators: [chinese] };

/** A widening the driver made for a place no longer on this machine. */
export const Widened: Story = { args: { mark: { word: 'w3', at, from: engine, to: games, widened: true } } };

export const ToTheMachine: Story = { args: { mark: { word: 'w4', at, from: engine, to: { kind: 'machine' }, widened: false } } };

export const Dark: Story = { decorators: [(Story) => <InTheme theme="dark" className="p-0"><Story /></InTheme>] };
