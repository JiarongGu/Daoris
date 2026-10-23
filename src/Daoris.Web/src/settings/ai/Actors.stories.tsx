import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import type { ActorRow } from '../../data/wiring';
import type { StartWiring } from '../../map/wiring';
import { ActorsCard } from './Actors';
import { AskDaorisAbout, IntakeAbout } from './ActorAbout';

// The actors on AI features (MODELROLE1c, D205 §7): each kind of work Daoris starts, its order folded to one line, in the shapes
// `ACTORS` answers with; Build set, the rest as before or off.

const row = (id: string, state: ActorRow['state'], choices: ActorRow['choices'] = [], error?: string): ActorRow => ({ id, state, choices, error });

const ROWS: ActorRow[] = [
  row('build', 'set', [
    { agent: 'claude-code', model: 'opus', effort: 'high', from: '1' },
    { agent: 'codex-acp', from: '2' },
  ]),
  row('intake', 'off'),
  row('opinion', 'before'),
  row('helper', 'before'),
  row('partner', 'before'),
  row('brainstorm', 'set', [{ agent: 'claude-code', from: '1' }]),
  row('ask-daoris', 'before', [{ agent: 'claude-code-acp', from: 'before' }]),
  row('chat', 'set', [{ agent: 'claude-code', from: '1' }]),
];

const meta: Meta<typeof ActorsCard> = {
  title: 'Settings/Actors',
  component: ActorsCard,
  args: {
    rows: ROWS,
    agents: [
      { value: 'claude-code', label: 'Claude Code — claude-code' },
      { value: 'claude-code-acp', label: 'Claude Code — claude-code-acp' },
      { value: 'codex-acp', label: 'Codex — codex-acp' },
    ],
    onSave: () => {},
  },
  // The main area at its wide width, as the frame gives it (the UI/UX design R6, UIFIT1).
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof ActorsCard>;

/** Build set to two choices after a change of its agent, Chat and Brainstorm written for the agent they ran. */
export const BuildSet: Story = {};

/** The same at the half width. */
export const BuildSetHalf: Story = { decorators: [storyWidths.half] };

/** The same at the 400 px floor: each value under its words. */
export const BuildSetNarrow: Story = { decorators: [storyWidths.narrow] };

/** The same in 中文, wide. */
export const BuildSetChinese: Story = { decorators: [chinese] };

/** The same in 中文 at the floor. */
export const BuildSetChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

/** Build's entry in driver.json does not read: the driver's sentence, and nothing starts. */
export const Unreadable: Story = {
  args: { rows: [row('build', 'unreadable', [], 'choice 1 names no agent'), ...ROWS.slice(1)] },
};

const intakeStart = (extra: Partial<StartWiring>): StartWiring => ({
  job: 'intake', workspace: 'default', adapter: 'claude-code-acp', owner: 'claude-code', product: 'Claude Code',
  profile: 'account-1', profileFrom: 'machine', version: '0.9.1', versionFrom: 'unset',
  commanded: false, refusal: null, ...extra,
});

const NAMES: Record<string, string> = { 'account-1': 'someone@example.invalid', 'account-2': 'API key …wxyz' };

/**
 * MODELROLE1d (D205 §7): the Intake and Ask Daoris cards folded into their rows. Intake set, in two circles, one with its own
 * account and one the driver would hold, in its words; Ask Daoris off, its own words for off.
 */
const FOLDED = {
  rows: [ROWS[0]!, row('intake', 'set', [{ agent: 'claude-code-acp', from: '1' }]), ...ROWS.slice(2, 6), row('ask-daoris', 'off'), ROWS[7]!],
  about: {
    intake: {
      why: 'An ask names no repository. The declarations answer it first.',
      below: (
        <IntakeAbout
          agent="claude-code-acp"
          starts={[
            intakeStart({ workspace: 'aurora', profile: 'account-2', profileFrom: 'workspace' }),
            intakeStart({
              refusal: '`claude-code` is pinned to 9.9.9 on this machine, and nothing is installed at that version — `daoris agent pin claude-code 9.9.9` installs it, and `daoris agent unpin claude-code` goes back to PATH.',
            }),
          ]}
          nameOf={(_owner, profile) => (profile ? NAMES[profile] ?? profile : "this machine's own")}
        />
      ),
    },
    'ask-daoris': { below: <AskDaorisAbout agent={null} /> },
  },
};

export const IntakeAndAskDaorisFolded: Story = { args: FOLDED };

/** The same at the half width. */
export const IntakeAndAskDaorisFoldedHalf: Story = { args: FOLDED, decorators: [storyWidths.half] };

/** The same at the 400 px floor. */
export const IntakeAndAskDaorisFoldedNarrow: Story = { args: FOLDED, decorators: [storyWidths.narrow] };

/** The same in 中文, wide. */
export const IntakeAndAskDaorisFoldedChinese: Story = { args: FOLDED, decorators: [chinese] };

/** The same in 中文 at the floor. */
export const IntakeAndAskDaorisFoldedChineseNarrow: Story = { args: FOLDED, decorators: [storyWidths.narrow, chinese] };
