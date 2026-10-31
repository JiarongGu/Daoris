import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import type { ActorRow, RoleCatalogue, RoleRow } from '../../data/wiring';
import type { StartWiring } from '../../map/wiring';
import { AskDaorisAbout, IntakeAbout } from '../../settings/ai/ActorAbout';
import { RolesCard } from './Roles';

// The roles on AI features (FOUNDATION1y-i, MODELROLE1c, COMPANYVIEW1a), one row for each, in the shapes `ROLES` and `ACTORS` answer
// with: the seeded roles with their staffing, Chat removed and waiting, an added role in a workspace with its choices, and the Designer
// Daoris suggests while no role takes design.

const seed = (id: string, en: string, zh: string, work: string): RoleRow => ({
  id, state: 'seed', seeded: true, nameEn: en, nameZh: zh, takes: [work], scopeKind: 'machine', revision: 0,
});

const CATALOGUE: RoleCatalogue = {
  roles: [
    seed('build', 'Build', '开发', 'build'),
    seed('intake', 'Intake', '受理', 'intake'),
    seed('opinion', 'Second opinion', '第二意见', 'review'),
    seed('helper', 'Helper', '助手', 'help'),
    seed('partner', 'Partner', '搭档', 'partner'),
    seed('brainstorm', 'Brainstorm', '头脑风暴', 'brainstorm'),
    seed('ask-daoris', 'Ask Daoris', '问道衍', 'ask-daoris'),
    { id: 'chat', state: 'removed', seeded: true, takes: [], revision: 2 },
    {
      id: 'night', state: 'set', seeded: false, nameEn: 'Night shift', nameZh: '夜班', takes: ['build'], scopeKind: 'workspace',
      scopeId: 'studio', revision: 3, choices: [{ agent: 'codex-acp', model: 'gpt-5', effort: 'high' }, { agent: 'claude-code' }],
    },
  ],
  works: ['build', 'design', 'chat', 'ask-daoris', 'intake', 'help', 'partner', 'review', 'brainstorm'],
  kept: true,
  suggested: [{ id: 'designer', nameEn: 'Designer', nameZh: '设计师', takes: ['design'], scopeKind: 'machine', choices: [{ agent: 'claude-code', model: 'opus', effort: 'high' }] }],
};

const row = (id: string, state: ActorRow['state'], choices: ActorRow['choices'] = [], error?: string): ActorRow => ({ id, state, choices, error });

const STAFFING: ActorRow[] = [
  row('build', 'set', [{ agent: 'claude-code', model: 'opus', effort: 'high', from: '1' }, { agent: 'codex-acp', from: '2' }]),
  row('intake', 'off'),
  row('opinion', 'before'),
  row('helper', 'before'),
  row('partner', 'before'),
  row('brainstorm', 'set', [{ agent: 'claude-code', from: '1' }]),
  row('ask-daoris', 'before', [{ agent: 'claude-code-acp', from: 'before' }]),
  row('chat', 'set', [{ agent: 'claude-code', from: '1' }]),
];

const meta: Meta<typeof RolesCard> = {
  title: 'Agents/Roles',
  component: RolesCard,
  args: {
    catalogue: CATALOGUE,
    staffing: STAFFING,
    agents: [
      { value: 'claude-code', label: 'Claude Code — claude-code' },
      { value: 'claude-code-acp', label: 'Claude Code — claude-code-acp' },
      { value: 'codex-acp', label: 'Codex — codex-acp' },
    ],
    onSet: () => {},
    onSetActor: () => {},
  },
  // The main area at its wide width, as the frame gives it (the UI/UX design R6, UIFIT1).
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof RolesCard>;

/** The seeded roles with their staffing, Chat removed and waiting, a night shift in one workspace, and the Designer suggested. */
export const Catalogue: Story = {};

/** The same at the half width. */
export const CatalogueHalf: Story = { decorators: [storyWidths.half] };

/** The same at the 400 px floor: each value under its words. */
export const CatalogueNarrow: Story = { decorators: [storyWidths.narrow] };

/** The same in 中文, each role named as it was written in Chinese. */
export const CatalogueChinese: Story = { decorators: [chinese] };

/** The same in 中文 at the floor. */
export const CatalogueChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

/** A home whose driver.json is the file's own: each seeded row keeps its order editor, and Add a role and Remove are held. */
export const NotKept: Story = { args: { catalogue: { ...CATALOGUE, kept: false, suggested: [] } } };

/** The same at the floor, in 中文. */
export const NotKeptChineseNarrow: Story = { args: { catalogue: { ...CATALOGUE, kept: false, suggested: [] } }, decorators: [storyWidths.narrow, chinese] };

/** A shell that answers the staffing and no roles: one order editor per role, and the card says the catalogue is not offered. */
export const OlderShell: Story = { args: { catalogue: null, read: 'notOffered' } };

/** The same at the half width. */
export const OlderShellHalf: Story = { args: { catalogue: null, read: 'notOffered' }, decorators: [storyWidths.half] };

/** The same at the floor, in 中文. */
export const OlderShellChineseNarrow: Story = { args: { catalogue: null, read: 'notOffered' }, decorators: [storyWidths.narrow, chinese] };

/** A read that failed, told apart from a shell that does not offer it. */
export const ReadFailed: Story = { args: { catalogue: null, read: 'failed' } };

/** An empty catalogue. */
export const Empty: Story = { args: { catalogue: { ...CATALOGUE, roles: [], suggested: [] }, staffing: [] } };

/** Build's entry in driver.json does not read: the driver's sentence, and nothing starts. */
export const Unreadable: Story = { args: { staffing: [row('build', 'unreadable', [], 'choice 1 names no agent'), ...STAFFING.slice(1)] } };

/** FOUNDATION1w-s: an older Daoris moved Build and turned Intake off; the turn-off applied at once, Build's change waiting. */
const FOUND = [{
  id: 3,
  at: '2026-10-30T10:00:00Z',
  changes: [
    { role: 'build', was: row('build', 'before', [{ agent: 'claude-code-acp', from: 'before' }]), now: row('build', 'set', [{ agent: 'codex-acp', from: '1' }]), applied: false },
    { role: 'intake', was: row('intake', 'before', [{ agent: 'codex-acp', from: 'before' }]), now: row('intake', 'off'), applied: true },
  ],
}];

/** Changed outside Daoris: what each change did, with *Take it* and *Dismiss*. */
export const Found: Story = { args: { found: FOUND } };

/** The same in 中文. */
export const FoundChinese: Story = { args: { found: FOUND }, decorators: [chinese] };

const intakeStart = (extra: Partial<StartWiring>): StartWiring => ({
  job: 'intake', workspace: 'default', adapter: 'claude-code-acp', owner: 'claude-code', product: 'Claude Code',
  profile: 'account-1', profileFrom: 'machine', version: '0.9.1', versionFrom: 'unset',
  commanded: false, refusal: null, ...extra,
});

const NAMES: Record<string, string> = { 'account-1': 'someone@example.invalid', 'account-2': 'API key …wxyz' };

/**
 * MODELROLE1d (D205 §7): the Intake and Ask Daoris explanations on their rows. Intake set, in two circles, one with its own
 * account and one the driver would hold, in its words; Ask Daoris off, its own words for off.
 */
const FOLDED = {
  staffing: [STAFFING[0]!, row('intake', 'set', [{ agent: 'claude-code-acp', from: '1' }]), ...STAFFING.slice(2, 6), row('ask-daoris', 'off'), STAFFING[7]!],
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
