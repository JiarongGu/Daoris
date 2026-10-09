import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { ReactNode } from 'react';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import { OpinionGate, type OpinionGateActs } from './OpinionGate';
import { DETAIL, GATES, NOTHING } from './opinionFixtures';

// *Second opinion* (XAGENT1g; the second-agent design §8.5, §9), beside the review's gate where *Accept…* would be: each state
// of §8.5's table, with the findings beside their answers. At the main area's width beside a 1546 px window and at 680 px, in
// both languages and both themes.

const nothing = () => {};
const ACTS: OpinionGateActs = {
  ask: nothing, sameAgent: nothing, stop: nothing, anyway: nothing, myself: nothing, sendBack: nothing, openSession: nothing,
};

function Main({ width, children }: { width: number; children: ReactNode }) {
  return <div className="@container/main max-w-full bg-page p-4" style={{ width }}>{children}</div>;
}

const wide: Decorator = (Story) => <Main width={760}><Story /></Main>;
const narrow: Decorator = (Story) => <Main width={400}><Story /></Main>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof OpinionGate> = {
  title: 'Work/Opinion gate',
  component: OpinionGate,
  args: { gate: GATES.disputed, detail: DETAIL, acts: ACTS, working: 's42', pressComing: true },
  decorators: [wide],
};
export default meta;

type Story = StoryObj<typeof OpinionGate>;

/** A later step of the chain still works here: one opinion reads the chain's whole work, or *Ask now*, or *Go on anyway…*. */
export const WaitsForTheChain: Story = { args: { gate: GATES.waitsChain, detail: null } };

/** Not asked yet: the driver's next look asks it, or *Ask now*. */
export const NotAsked: Story = { args: { gate: GATES.notAsked, detail: null } };

/** Being read by another maker's agent, for at most its minutes: *Stop…* and the door to its session. */
export const BeingRead: Story = { args: { gate: GATES.reading, detail: { tier: 'agent', first: { ...DETAIL.first!, state: 'reading', findings: null } } } };

/** The same in 中文. */
export const BeingReadChinese: Story = { ...BeingRead, decorators: [chinese] };

/** Its findings with the working session, which answers them as its next turn. */
export const WithTheSession: Story = { args: { gate: GATES.withSession } };

/** Read again: the commits made in answer. */
export const ReadAgain: Story = { args: { gate: GATES.readAgain } };

/** Disputed, *Accept…* coming: its findings open, the must beside its rejection; *Send back…* and *Ask again*. */
export const Disputed: Story = {};

/** The same in 中文. */
export const DisputedChinese: Story = { decorators: [chinese] };

/** The same in dark. */
export const DisputedDark: Story = { decorators: [dark] };

/** The same in dark and 中文. */
export const DisputedDarkChinese: Story = { decorators: [chinese, dark] };

/** The same at 680 px, the main area's 400 px floor. */
export const DisputedNarrow: Story = { decorators: [narrow] };

/** Disputed where the work lands by itself: no press is coming, so *Go on anyway…* answers it here. */
export const DisputedNoPressComing: Story = { args: { pressComing: false } };

/** None to be had, and the rule requires one: a cool-off until its reset; *Try again*, the same agent, *I looked myself…*, *Go on anyway…*. */
export const UnavailableRequired: Story = { args: { gate: GATES.unavailableRequired, detail: { tier: 'none' } } };

/** The same in 中文. */
export const UnavailableRequiredChinese: Story = { ...UnavailableRequired, decorators: [chinese] };

/** None to be had, not required: said beside *Accept…*, and nothing waits. */
export const Unavailable: Story = { args: { gate: GATES.unavailable, detail: { tier: 'none' } } };

/** Commits since were not read by another agent: *Accept…* answers them, or *Ask again*. */
export const CommitsSince: Story = { args: { gate: GATES.commitsSince } };

/** Settled: its findings answered, none disputed. */
export const Settled: Story = { args: { gate: GATES.settled, detail: { ...DETAIL, first: { ...DETAIL.first!, findings: [{ ...DETAIL.first!.findings![1]! }] } } } };

/** Settled by a pass that raised nothing: what it read, never *no issues*. */
export const SettledRaisedNothing: Story = { args: { gate: GATES.settledNothing, detail: NOTHING } };

/** The person went on anyway, with their words. */
export const GoneOnAnyway: Story = {
  args: { gate: GATES.anyway, detail: { ...DETAIL, person: { said: 'anyway', at: '2026-10-09T10:00:00Z', words: 'The bound is right; I read it.' } } },
};

/** Whether it waits could not be read: the driver's sentence beneath. */
export const Unread: Story = { args: { gate: GATES.unread, detail: null } };
