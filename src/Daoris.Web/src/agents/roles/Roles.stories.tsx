import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { InTheme } from '../../plugins/storyIcons';
import { storyWidths } from '../../design/storyWidths';
import type { ActorRow, RoleCatalogue, RoleRow } from '../../data/wiring';
import type { StartWiring } from '../../map/wiring';
import { AskDaorisAbout, IntakeAbout } from '../../settings/ai/ActorAbout';
import { type Editing, RolesCard } from './Roles';

// The roles on AI features (FOUNDATION1y-i, MODELROLE1c, COMPANYVIEW1a), one row for each, in the shapes `ROLES` answers
// with: the seeded roles with their staffing, Chat removed and waiting, an added role in a workspace with its choices, and the Designer
// Daoris suggests while no role takes design.

const seed = (id: string, en: string, zh: string, work: string): RoleRow => ({
  id, state: 'seed', seeded: true, name: { en, zh }, takes: [work], scopeKind: 'machine', revision: 0,
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
      id: 'night', state: 'set', seeded: false, name: { en: 'Night shift', zh: '夜班' }, takes: ['build'], scopeKind: 'workspace',
      scopeId: 'studio', revision: 3, choices: [{ agent: 'codex-acp', model: 'gpt-5', effort: 'high' }, { agent: 'claude-code' }],
    },
  ],
  works: ['build', 'design', 'chat', 'ask-daoris', 'intake', 'help', 'partner', 'review', 'brainstorm'],
  kept: true,
  suggested: [{ id: 'designer', name: { en: 'Designer', zh: '设计师' }, takes: ['design'], scopeKind: 'machine', choices: [{ agent: 'claude-code', model: 'opus', effort: 'high' }] }],
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

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

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

// MODELROLE1f: each role's last seven days under its row; Build fell to later choices, Intake kept to its first, the rest have no line.
const WEEK: RoleCatalogue = {
  ...CATALOGUE,
  use: {
    days: 7,
    roles: [
      { role: 'build', starts: 12, choices: [{ choice: '1', starts: 9 }, { choice: '2', starts: 3 }], fell: 3, why: [{ code: 'cooling', starts: 2 }, { code: 'signed-out', starts: 1 }] },
      { role: 'ask-daoris', starts: 1, choices: [{ choice: 'before', starts: 1 }], fell: 0, why: [] },
    ],
  },
};

/** The week's line under Build and Ask Daoris, at the wide width. */
export const WeekShown: Story = { args: { catalogue: WEEK } };

/** The same at the half width. */
export const WeekShownHalf: Story = { args: { catalogue: WEEK }, decorators: [storyWidths.half] };

/** The same at the 400 px floor. */
export const WeekShownNarrow: Story = { args: { catalogue: WEEK }, decorators: [storyWidths.narrow] };

/** The same in 中文. */
export const WeekShownChinese: Story = { args: { catalogue: WEEK }, decorators: [chinese] };

/** The same in 中文 at the half width. */
export const WeekShownChineseHalf: Story = { args: { catalogue: WEEK }, decorators: [storyWidths.half, chinese] };

/** The same in 中文 at the floor. */
export const WeekShownChineseNarrow: Story = { args: { catalogue: WEEK }, decorators: [storyWidths.narrow, chinese] };

/** A log of three days says its span instead of seven. */
export const WeekOfAYoungLog: Story = { args: { catalogue: { ...WEEK, use: { ...WEEK.use!, days: 3 } } }, decorators: [storyWidths.narrow] };

// AGENTSPLACE1g: All roles with *Open at <place>* on the role placed elsewhere, and the editor open on the added role of a workspace with
// the agent's own offers (a model picked, an effort typed after other…); it asks for no id and its place is fixed.
const OFFERS = { 'claude-code': { models: ['opus', 'sonnet', 'haiku'], efforts: ['low', 'medium', 'high'] }, 'codex-acp': { models: ['gpt-5'], efforts: ['low', 'high'] } };

const EDITING: Editing = {
  id: 'night', isNew: false, revision: 3, name: 'Night shift', nameIn: [['zh', '夜班']], takes: ['build'], work: 'build',
  place: 'workspace', placeId: 'studio',
  drafts: [{ key: 1, agent: 'codex-acp', model: 'gpt-5', effort: 'high' }, { key: 2, agent: 'claude-code', model: 'o-4', effort: '' }],
};

const ALL = { offers: OFFERS, onOpenPlace: () => {} };
const EDIT = { offers: OFFERS, editor: EDITING, onEditor: () => {}, only: 'night' };

/** All roles: the machine-wide roles apart from those placed elsewhere, which are read and opened at their place. */
export const AllRoles: Story = { args: ALL };

/** The same at the half width. */
export const AllRolesHalf: Story = { args: ALL, decorators: [storyWidths.half] };

/** The same at the 400 px floor. */
export const AllRolesNarrow: Story = { args: ALL, decorators: [storyWidths.narrow] };

/** The same in 中文. */
export const AllRolesChinese: Story = { args: ALL, decorators: [chinese] };

/** The same in 中文 at the floor. */
export const AllRolesChineseNarrow: Story = { args: ALL, decorators: [storyWidths.narrow, chinese] };

/** The role editor with the agent's own offers; *Edit role* folds the name, work and place. */
export const RoleEditor: Story = { args: EDIT };

/** The same at the half width. */
export const RoleEditorHalf: Story = { args: EDIT, decorators: [storyWidths.half] };

/** The same at the 400 px floor. */
export const RoleEditorNarrow: Story = { args: EDIT, decorators: [storyWidths.narrow] };

/** The same in 中文. */
export const RoleEditorChinese: Story = { args: EDIT, decorators: [chinese] };

/** The same in 中文 at the floor. */
export const RoleEditorChineseNarrow: Story = { args: EDIT, decorators: [storyWidths.narrow, chinese] };

/** The same in dark, and in 中文 in dark at the floor (SETUPUX1l: one editor in both themes). */
export const RoleEditorDark: Story = { args: EDIT, decorators: [dark] };
export const RoleEditorChineseDarkNarrow: Story = { args: EDIT, decorators: [storyWidths.narrow, chinese, dark] };

/** A home whose driver.json is the file's own: each seeded row keeps its order editor, and Add a role and Remove are held. */
export const NotKept: Story = { args: { catalogue: { ...CATALOGUE, kept: false, suggested: [] } } };

/** The same at the floor, in 中文. */
export const NotKeptChineseNarrow: Story = { args: { catalogue: { ...CATALOGUE, kept: false, suggested: [] } }, decorators: [storyWidths.narrow, chinese] };

/** A read that failed, told apart from a shell that does not offer it. */
export const ReadFailed: Story = { args: { catalogue: null, read: 'failed' } };

/** An empty catalogue. */
export const Empty: Story = { args: { catalogue: { ...CATALOGUE, roles: [], suggested: [] }, staffing: [] } };

/** Build's entry in driver.json does not read: the driver's sentence, and nothing starts. */
export const Unreadable: Story = { args: { staffing: [row('build', 'unreadable', [], 'choice 1 names no agent'), ...STAFFING.slice(1)] } };

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
