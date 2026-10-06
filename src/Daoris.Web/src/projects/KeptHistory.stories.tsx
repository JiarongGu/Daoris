import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { ReactNode } from 'react';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import { WORKSPACE_EMPTY, WORKSPACE_KEPT, WORKSPACE_LEFT_OVER, WORKSPACE_PLAN } from '../work/historyFixtures';
import { KeptHistory } from './KeptHistory';

// A workspace's *Kept on this machine* (HIST1e, D153; the history-clearing design §2.4, §6.1), the last section of its
// Details: the reading of what the home keeps of its finished work, what a clear would take, what keeps the rest by reason
// with its door, the conversations only *Delete…* takes and the home's own; then *Clear history…* while anything may go.
// Its first press is `Work/Clear`'s workspace stories. At the main area beside the install's 1546 px window and at 680 px,
// in both languages and both themes.

const nothing = () => {};
const DOORS = { branches: nothing, sync: nothing, quest: nothing, ask: nothing, session: nothing };

function Main({ width, children }: { width: number; children: ReactNode }) {
  return <div className="@container/main max-w-full bg-page p-4" style={{ width }}>{children}</div>;
}

const wide: Decorator = (Story) => <Main width={1180}><Story /></Main>;
const narrow: Decorator = (Story) => <Main width={600}><Story /></Main>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof KeptHistory> = {
  title: 'Repositories/KeptOnThisMachine',
  component: KeptHistory,
  args: { workspace: 'aurora', plan: WORKSPACE_PLAN, doors: DOORS, onClear: nothing },
  decorators: [wide],
};
export default meta;

type Story = StoryObj<typeof KeptHistory>;

/** What the home keeps of `aurora`'s finished work, what a clear would take, and why five stay: *Clear history…* offered. */
export const Reading: Story = {};

/** The same in 中文. */
export const ReadingChinese: Story = { decorators: [chinese] };

/** The same in dark. */
export const ReadingDark: Story = { decorators: [dark] };

/** At 680 px. */
export const ReadingNarrow: Story = { decorators: [narrow] };

/** At 680 px, in 中文. */
export const ReadingNarrowChinese: Story = { decorators: [chinese, narrow] };

/** At 680 px, in dark. */
export const ReadingNarrowDark: Story = { decorators: [dark, narrow] };

/** Every closed unit is kept: the reading says why, and no press is offered. */
export const AllKept: Story = { args: { plan: WORKSPACE_KEPT } };

/** Nothing finished here, and nothing left over: said, with no press. */
export const NothingKept: Story = { args: { plan: WORKSPACE_EMPTY } };

/** Only what records already gone left, and the intake's room: the press takes those. */
export const LeftOverOnly: Story = { args: { plan: WORKSPACE_LEFT_OVER } };

/** The reading on its way. */
export const ReadingOnItsWay: Story = { args: { plan: null, reading: true } };

/** The reading refused, said in its place. */
export const Refused: Story = { args: { plan: null, refusal: 'The service did not answer: is the host running?' } };

/** A clear on its way: the press waits. */
export const Clearing: Story = { args: { busy: true } };
