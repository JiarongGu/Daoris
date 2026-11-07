import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Session } from '../../data/api';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import type { CourseRead, LineSupervision } from './lineSupervision';
import { SessionHead } from './SessionHead';
import { SupervisorCourseCard } from './SupervisorCourseCard';

// The supervisor's decision on a point a person's `refer` rule held a line at. The card
// stands while the decision is held, with *Overrule*; the record's *Details*, opened, says it whole in every state. Held, gone on,
// overruled, one that did not go on, and one left to the person; at the frame's widths, in English, 中文 and dark.

const MINUTE = 60_000;

const course = (state: string, over: Partial<CourseRead> = {}): CourseRead => ({
  id: 'd1', point: 'rule-1', count: 'answers-alone', n: 2, rule: 'aa', place: 'repository:engine', do: 'go-on', state, why: null,
  holder: { kind: 'terminal' }, keptAt: new Date(Date.now() - 2 * MINUTE).toISOString(), goesOnAt: new Date(Date.now() + 3 * MINUTE).toISOString(),
  stateAt: new Date(Date.now() - 2 * MINUTE).toISOString(), earlier: false, ...over,
});

const SESSION: Session = {
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', door: 'fake', state: 'awaiting-person', kind: 'driven',
  created: '2026-11-07T09:00:00Z', updated: '2026-11-07T09:12:00Z',
};

/** The head over a line the supervisor decided, its card and its *Details* opened. */
function Decided({ state, over }: { state: string; over?: Partial<CourseRead> }) {
  const supervision: LineSupervision = { counts: [], acts: [], courses: [course(state, over)] };
  return (
    <SessionHead
      session={SESSION} headed supervision={supervision}
      supervisorCourse={<SupervisorCourseCard course={supervision.courses[0]} onOverrule={() => {}} />}
    />
  );
}

const meta: Meta<typeof Decided> = {
  title: 'Work/SupervisorCourse',
  component: Decided,
  args: { state: 'held' },
  decorators: [storyWidths.wide],
  play: async ({ canvasElement }) => {
    [...canvasElement.querySelectorAll<HTMLButtonElement>('button')]
      .find((button) => /^(Details|详情)/.test(button.textContent ?? ''))?.click();
  },
};
export default meta;

type Story = StoryObj<typeof Decided>;

/** Held for its window: the card says what the supervisor chose and when it goes on, with *Overrule*; *Details* says the same. */
export const GoOnHeld: Story = {};
export const GoOnHeldHalf: Story = { decorators: [storyWidths.half] };
export const GoOnHeldNarrow: Story = { decorators: [storyWidths.narrow] };
export const GoOnHeldChinese: Story = { decorators: [storyWidths.half, chinese] };
export const GoOnHeldNarrowChinese: Story = { decorators: [storyWidths.narrow, chinese] };
export const GoOnHeldDark: Story = { decorators: [storyWidths.narrow, (Story) => <InTheme theme="dark"><Story /></InTheme>] };

/** It went on: no card, *Details* says it. */
export const WentOn: Story = { args: { state: 'went-on' } };
export const WentOnChinese: Story = { args: { state: 'went-on' }, decorators: [storyWidths.half, chinese] };

/** The person overruled it: no card, *Details* says so. */
export const Overruled: Story = { args: { state: 'overruled' } };

/** Its time came and the rule held the line again: *Details* says why it did not go on. */
export const DidNotGoOn: Story = { args: { state: 'stale', over: { why: 'rule-holds' } } };
export const DidNotGoOnChinese: Story = { args: { state: 'stale', over: { why: 'rule-holds' } }, decorators: [storyWidths.half, chinese] };

/** The supervisor left the point to the person: *Details* says it waits for them. */
export const LeftToYou: Story = { args: { state: 'left', over: { do: 'hold' } } };
export const LeftToYouChinese: Story = { args: { state: 'left', over: { do: 'hold' } }, decorators: [storyWidths.half, chinese] };
