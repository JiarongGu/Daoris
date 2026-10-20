import type { Meta, StoryObj } from '@storybook/react-vite';
import { type SignInActs, SignInSteps } from './SignInSteps';

// A sign-in's steps (SIGNIN1, the UI/UX design §3.2): a story per state of its table, from the driver's phases and the tool's
// lines as they arrive. The times run from when the story was opened.

const AT = Date.now();
const nothing = () => {};
const ACTS: SignInActs = {
  copied: null, onCopy: nothing, code: '', onCode: nothing, onSend: (event) => event.preventDefault(),
  sending: false, sent: false, onRetry: nothing, onClose: nothing,
};

const STARTED = [{ phase: 'start', at: AT - 2_000 }];
const UP = [{ phase: 'start', at: AT - 72_000 }, { phase: 'browser', at: AT - 70_000 }];

const CLAUDE = ["$ claude auth login", 'Opening browser to sign in…'];
const CLAUDE_LINK = [...CLAUDE, "If the browser didn't open, visit: https://claude.ai/oauth/authorize?code=true&client_id=00000000"];
const CODEX = [
  '$ codex login --device-auth', '', 'Follow these steps to sign in with ChatGPT using device code authorization:', '',
  '1. Open this link in your browser and sign in to your account', '   https://auth.openai.com/codex/device', '',
  '2. Enter this one-time code (expires in 15 minutes)', '   ABCD-12345', '',
];

const meta: Meta<typeof SignInSteps> = {
  title: 'Agents/SignInSteps',
  component: SignInSteps,
  args: { title: 'Signing in to account-2', tool: 'Claude Code', action: 'login', phases: UP, texts: CLAUDE_LINK, acts: ACTS },
  decorators: [(Story) => <div className="w-[40rem] max-w-full rounded-card border border-line bg-raised p-3"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof SignInSteps>;

/** Starting: the first phase live, the rest to come. */
export const Starting: Story = { args: { phases: STARTED, texts: [] } };

/** Waiting, no link yet: the tool is up and has printed nothing to open. */
export const WaitingNoLink: Story = { args: { texts: ['$ claude auth login'] } };

/** Waiting, link shown: the tool opened the browser, and the link is here to open or copy. */
export const WaitingLink: Story = {};

/** Waiting, device code: Codex's code to enter on its page, with its own bound and the link. */
export const WaitingDeviceCode: Story = {
  args: { title: 'Signing in to another Codex account', tool: 'Codex', action: 'login-new', texts: CODEX },
};

/** Code asked: the tool waits on a code the page showed, and the box takes it. */
export const CodeAsked: Story = { args: { texts: [...CLAUDE_LINK, 'Paste code here if prompted >'] } };

/** Code sent: the box has gone, and the tool is finishing. */
export const CodeSent: Story = { args: { texts: [...CLAUDE_LINK, 'Paste code here if prompted >'], acts: { ...ACTS, sent: true } } };

/** The driver's own phases after the tool's: who signed in, then the lists. */
export const CheckingWho: Story = { args: { phases: [...UP, { phase: 'who', at: AT - 1_000 }] } };

/** Refused at the start: the first phase failed with the driver's sentence whole, *Try again* and *Close*. */
export const RefusedAtTheStart: Story = {
  args: {
    title: 'Signing in to another Codex account', tool: 'Codex', action: 'login-new', phases: [], texts: [],
    refused: '`codex` is pinned to 0.160.0 on this machine, and nothing is installed at that version, so nothing was signed in.',
  },
};

/** Failed at a phase: the tool exited before anyone signed in, the earlier phase done. */
export const FailedAtAPhase: Story = {
  args: { failed: 'Sign-in did not finish (the tool exited 1). Its output says why.', failedAt: AT - 1_000 },
};

/** The line alone, as a row of *What needs you* says it: the phase now and what it says. */
export const Line: Story = { args: { shown: 'line', title: 'Signing in to another Codex account', tool: 'Codex', texts: CODEX } };

/** From an older host that writes no phases: the steps as they were. */
export const OlderHost: Story = { args: { phases: [] } };
