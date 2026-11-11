import type { ReactNode } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { InTheme } from '../../plugins/storyIcons';
import { MACHINE, type Place } from '../focus';
import { FocusCrumb } from './FocusCrumb';
import { FocusPicker } from './FocusPicker';
import type { PlaceRepository } from './focusPlaces';

// Ask Daoris's focus chip, its picker and the head's breadcrumb (`docs/2026-11-11-ask-scope-ux-design.md` §1-2), in the panel's
// composer bar at its narrow, default and wide widths (300, 384 and 480 px), in both languages and both themes. The picker opens
// upward from the chip at the panel's foot; nothing it chooses goes anywhere.

const REPOSITORIES: PlaceRepository[] = [
  { name: 'engine', workspace: 'Games', checkout: true },
  { name: 'tools', workspace: 'Games', checkout: true },
  { name: 'api', workspace: 'Work', checkout: true },
  { name: 'site', workspace: 'Work', checkout: false },
];
const games: Place = { kind: 'workspace', workspace: 'Games' };
const work: Place = { kind: 'workspace', workspace: 'Work' };
const engine: Place = { kind: 'repository', workspace: 'Games', repository: 'engine' };
const api: Place = { kind: 'repository', workspace: 'Work', repository: 'api' };

/** The panel's foot at each width: the chip at the bottom of a column tall enough for the picker above it. */
const atTheFoot = (Story: () => ReactNode) => (
  <div className="flex flex-wrap items-end gap-6 bg-page">
    {[300, 384, 480].map((width) => (
      <div key={width} style={{ width }} className="@container flex h-[30rem] flex-col justify-end border-x border-line bg-page">
        <div className="flex flex-wrap items-start gap-x-2 gap-y-1 border-t border-line px-3 py-1.5"><Story /></div>
      </div>
    ))}
  </div>
);
const dark = (Story: () => ReactNode) => <InTheme theme="dark" className="p-0"><Story /></InTheme>;

const meta = {
  title: 'Help/FocusPicker',
  component: FocusPicker,
  decorators: [atTheFoot],
  args: { focus: games, next: null, window: 'Games', repositories: REPOSITORIES, recent: [engine, work, api], onChoose: () => {} },
} satisfies Meta<typeof FocusPicker>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Every state the chip has: the window's workspace, a choice, this machine, a change waiting, gone, gone and widening, reading. */
const states: Story['render'] = (args) => (
  <div className="grid gap-1.5">
    <FocusPicker {...args} />
    <FocusPicker {...args} focus={engine} />
    <FocusPicker {...args} focus={MACHINE} />
    <FocusPicker {...args} next={engine} />
    <FocusPicker {...args} next={engine} narrow />
    <FocusPicker {...args} focus={{ kind: 'repository', workspace: 'Games', repository: 'legacy' }} gone />
    <FocusPicker {...args} focus={{ kind: 'repository', workspace: 'Games', repository: 'legacy' }} gone wider={games} />
    <FocusPicker {...args} repositories={undefined} />
  </div>
);

export const States: Story = { render: states };
export const StatesDark: Story = { render: states, decorators: [atTheFoot, dark] };
export const StatesChinese: Story = { render: states, decorators: [atTheFoot, chinese] };

/** The picker open on a Games window: the window first, Recent, this machine, Work unfolding onto its repositories. */
export const Open: Story = { args: { defaultOpen: true } };
export const OpenDark: Story = { args: { defaultOpen: true }, decorators: [atTheFoot, dark] };
export const OpenChinese: Story = { args: { defaultOpen: true }, decorators: [atTheFoot, chinese] };

/** A change waiting: its line, and the way to keep the focus as it is, above the list. */
export const OpenPending: Story = { args: { defaultOpen: true, next: engine } };

/** On a machine whose only workspace is `default`: this machine, with no workspace level. */
export const OneWorkspace: Story = {
  args: { defaultOpen: true, focus: MACHINE, window: 'default', recent: [], repositories: [{ name: 'engine', workspace: 'default', checkout: true }] },
};

/** The head's breadcrumb: Games' engine in a Work window, started in Games, a change waiting; and gone. */
const heads: Story['render'] = () => (
  <div className="grid w-full gap-2">
    <FocusCrumb focus={engine} next={null} window="Games" home="Games" onOpen={() => {}} />
    <FocusCrumb focus={engine} next={games} window="Work" home="Games" onOpen={() => {}} />
    <FocusCrumb focus={engine} next={null} gone window="Games" home="Games" onOpen={() => {}} />
  </div>
);

export const Head: Story = { render: heads };
export const HeadDark: Story = { render: heads, decorators: [atTheFoot, dark] };
export const HeadChinese: Story = { render: heads, decorators: [atTheFoot, chinese] };
