import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { AiJobs, type IntakeJob } from './AiJobs';
import type { StartWiring } from '../map/wiring';

// Daoris's own AI (AGT6): each job it may use a model for, the tier that answers it now, and how to
// change it — in the shapes the service's status and the driver's STARTS answer with.

const LEXICAL = {
  tier: 'lexical only', semantic: false,
  note: 'Set DAORIS_EMBED_MODEL to enable semantic recall — it is what finds two repositories that reached the same conclusion in different words.',
};

const intakeStart = (extra: Partial<StartWiring>): StartWiring => ({
  job: 'intake', workspace: 'default', adapter: 'claude-code-acp', owner: 'claude-code', product: 'Claude Code',
  profile: 'account-1', profileFrom: 'machine', version: '0.9.1', versionFrom: 'unset',
  commanded: false, refusal: null, ...extra,
});

const NAMES: Record<string, string> = { 'account-1': 'someone@example.invalid', 'account-2': 'API key …wxyz' };

const intake = (extra: Partial<IntakeJob>): IntakeJob => ({
  adapter: null,
  agents: [
    { value: 'claude-code', label: 'Claude Code — claude-code' },
    { value: 'claude-code-acp', label: 'Claude Code — claude-code-acp' },
    { value: 'codex-acp', label: 'Codex — codex-acp' },
  ],
  starts: [],
  nameOf: (_owner, profile) => (profile ? NAMES[profile] ?? profile : "this machine's own"),
  onChange: () => {},
  ...extra,
});

const meta: Meta<typeof AiJobs> = {
  title: 'Settings/AiJobs',
  component: AiJobs,
  args: { search: LEXICAL, intake: intake({}) },
  // The main area at its wide width, as the frame gives it (the UI/UX design R6, UIFIT1).
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof AiJobs>;

/** A machine as it arrives: no embedding model, and asks answered by declarations only. */
export const NothingNamed: Story = {};

/** The same at the half width, 630 px. */
export const NothingNamedHalf: Story = { decorators: [storyWidths.half] };

/** The same at the 400 px floor: each value under its words. */
export const NothingNamedNarrow: Story = { decorators: [storyWidths.narrow] };

/** The same in 中文, wide. */
export const NothingNamedChinese: Story = { decorators: [chinese] };

/** The same in 中文 at the floor. */
export const NothingNamedChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

/** A model for search, and an agent for the intake — running as the machine's default account. */
export const BothJobsOnAModel: Story = {
  args: {
    search: { tier: 'lexical + semantic', semantic: true, note: null },
    intake: intake({ adapter: 'claude-code-acp', starts: [intakeStart({})] }),
  },
};

/** Two circles, one with its own account, and one where the intake would be held — the driver's words. */
export const TwoCirclesOneHeld: Story = {
  args: {
    intake: intake({
      adapter: 'claude-code-acp',
      starts: [
        intakeStart({ workspace: 'aurora', profile: 'account-2', profileFrom: 'workspace' }),
        intakeStart({
          refusal: '`claude-code` is pinned to 9.9.9 on this machine, and nothing is installed at that version — `daoris agent pin claude-code 9.9.9` installs it, and `daoris agent unpin claude-code` goes back to PATH.',
        }),
      ],
    }),
  },
};

/** A browser: the service's tier is all it may be told — the intake is this machine's, and absent. */
export const InABrowser: Story = {
  args: { intake: undefined },
};

/** Before the service has answered: the tier is not claimed. */
export const TierNotYetKnown: Story = {
  args: { search: undefined, intake: undefined },
};

/**
 * HELPMODE1c1 (the modes design §5's sketch): Ask Daoris on Claude Code, restricted, with its configured access under the access
 * row, housekeeping's inspection chosen and the browser waiting for approval.
 */
export const RestrictedWithConfiguredAccess: Story = {
  args: {
    intake: undefined,
    helper: {
      adapter: 'claude-code-acp',
      agents: [{ value: 'claude-code-acp', label: 'Claude Code — claude-code-acp' }, { value: 'codex-acp', label: 'Codex — codex-acp' }],
      onChange: () => {},
    },
    access: { mode: 'restricted', confirming: false, adds: [], onPick: () => {}, onEnable: () => {}, onCancel: () => {} },
    grants: {
      agent: { name: 'Claude Code', rules: true },
      grants: [
        { id: 'housekeeping', chosen: ['inspect'] },
        { id: 'browser', chosen: [], plugin: 'in-app-browser' },
        { id: 'checkouts', chosen: ['engine'] },
        { id: 'settings', chosen: ['notify'] },
      ],
      built: {
        housekeeping: ['inspect', 'clean-up', 'discard-branch'], settings: ['notify', 'language', 'timeout', 'cap', 'cooloff'],
        checkouts: ['engine', 'reports-db'],
      },
      onChoose: () => {},
      onManage: () => {},
    },
  },
};
