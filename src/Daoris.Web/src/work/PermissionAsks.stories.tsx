import type { Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { PermissionAsks, type PermissionWait } from './PermissionAsks';

// A tool call a session holds for the person's answer under asking (AUTONOMY1b3c, D184 point 3): the call as the agent named it,
// its kind, how long it has waited and the bound, and a press per choice the agent offered, never an allow it did not offer. In
// English, 中文 and dark.

const minutesAgo = (minutes: number) => new Date(Date.now() - minutes * 60_000).toISOString();

const held = (over: Partial<PermissionWait> & Pick<PermissionWait, 'request'>): PermissionWait => ({
  session: 's1a2b3c4', call: null, title: null, kind: null, since: minutesAgo(2),
  choices: [{ id: 'allow', name: 'Allow', kind: 'allow_once' }, { id: 'reject', name: 'Reject', kind: 'reject_once' }],
  ...over,
});

const WAITS: PermissionWait[] = [
  held({ request: '1', title: 'npm test --workspace engine -- --runInBand', kind: 'execute', since: minutesAgo(3) }),
  held({ request: '2', title: 'Edit src/streaming/budget.ts', kind: 'edit', since: minutesAgo(1) }),
  // No allow offered: only a refusal is drawn.
  held({ request: '3', kind: 'fetch', choices: [{ id: 'reject', name: 'Reject', kind: 'reject_once' }, { id: 'never', name: 'Reject always', kind: 'reject_always' }] }),
];

const meta: Meta<typeof PermissionAsks> = {
  title: 'Work/PermissionAsks',
  component: PermissionAsks,
  args: { waits: WAITS, onAnswer: () => {} },
  decorators: [(Story) => <div className="w-[36rem] max-w-full bg-page p-4"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof PermissionAsks>;

/** A command and an edit with *Allow once* and *Refuse*, and an untitled fetch where no allow was offered. */
export const Held: Story = {};

/** The same in 中文. */
export const HeldChinese: Story = { decorators: [chinese] };

/** The same in dark. */
export const HeldDark: Story = { decorators: [(Story) => <InTheme theme="dark"><Story /></InTheme>] };

/** Read only, as a browser shows them: no press. */
export const NothingToPress: Story = { args: { onAnswer: undefined } };
