import type { Meta, StoryObj } from '@storybook/react-vite';
import { PhaseList, type PhaseRow } from './phases';

// R2 (the UI/UX design, SIGNIN1): a step that waits says its phase and how long. Each state as the list standing open,
// where the process is its own surface, and as the line alone, where a row or a head has no room for a list.

const AT = Date.now();
const done = (key: string, label: string, from: number, to: number): PhaseRow => ({ key, label, state: 'done', from, to });
const later = (key: string, label: string): PhaseRow => ({ key, label, state: 'to-come' });

const STARTING: PhaseRow[] = [
  { key: 'start', label: 'Starting Claude Code', state: 'now', from: AT - 3_000 },
  later('browser', 'Waiting on your browser'),
  later('who', 'Checking who signed in'),
  later('lists', 'Joining the lists'),
];

const WAITING: PhaseRow[] = [
  done('start', 'Starting Claude Code', AT - 72_000, AT - 70_000),
  {
    key: 'browser', label: 'Waiting on your browser', state: 'waits', from: AT - 70_000,
    body: <p className="m-0 text-small text-ink-soft">The tool opened your browser — finish signing in there.</p>,
  },
  later('who', 'Checking who signed in'),
  later('lists', 'Joining the lists'),
];

const CHECKING: PhaseRow[] = [
  done('start', 'Starting Codex', AT - 50_000, AT - 49_000),
  done('browser', 'Waiting on your browser', AT - 49_000, AT - 2_000),
  { key: 'who', label: 'Checking who signed in', state: 'now', from: AT - 2_000 },
  later('lists', 'Joining the lists'),
];

const FAILED: PhaseRow[] = [
  done('start', 'Starting Codex', AT - 50_000, AT - 49_000),
  {
    key: 'browser', label: 'Waiting on your browser', state: 'failed', from: AT - 49_000, to: AT - 1_000,
    sentence: 'Sign-in did not finish (the tool exited 1), so nothing was kept. Its output says why.',
  },
  later('who', 'Checking who signed in'),
  later('lists', 'Joining the lists'),
];

const REFUSED: PhaseRow[] = [
  {
    key: 'start', label: 'Starting Codex', state: 'failed',
    sentence: '`codex` is pinned to 0.160.0 on this machine, and nothing is installed at that version, so nothing was signed in — `daoris agent pin codex 0.160.0` installs it.',
  },
  later('browser', 'Waiting on your browser'),
  later('who', 'Checking who signed in'),
  later('lists', 'Joining the lists'),
];

const meta: Meta<typeof PhaseList> = {
  title: 'Design/PhaseList',
  component: PhaseList,
  args: { label: 'Signing in to account-2', phases: WAITING },
  decorators: [(Story) => <div className="w-[32rem] max-w-full rounded-card border border-line bg-raised p-3"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof PhaseList>;

/** Starting: the first phase live, the rest to come. */
export const Starting: Story = { args: { phases: STARTING } };

/** Waiting on the person: open's hue, the time running, and what the phase says under it. */
export const Waiting: Story = {};

/** The driver's own phase after the tool's: the live mark, the earlier ones done with the time each took. */
export const Checking: Story = { args: { phases: CHECKING } };

/** Failed at a phase: its sentence whole in the danger ink, the earlier ones done. */
export const Failed: Story = { args: { phases: FAILED } };

/** Refused at the start: the first phase failed with the driver's sentence, wrapping anywhere a long id needs. */
export const Refused: Story = { args: { phases: REFUSED } };

/** The line alone, as a row of *What needs you* says it: the phase now, its time and the whole's, and what it says. */
export const WaitingLine: Story = { args: { shown: 'line' } };

/** The line alone, failed. */
export const FailedLine: Story = { args: { phases: FAILED, shown: 'line' } };

/** In 中文: the words its holder gives it, the times and the states in the window's language. */
export const Chinese: Story = {
  args: {
    label: '正在登录 account-2',
    phases: [
      done('start', '正在启动 Codex', AT - 50_000, AT - 49_000),
      { key: 'browser', label: '等你在浏览器中完成', state: 'waits', from: AT - 49_000 },
      later('who', '正在确认登录的是谁'),
      later('lists', '正在加入列表'),
    ],
  },
};
