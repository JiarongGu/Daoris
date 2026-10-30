import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import type { RoleCatalogue, RoleRow } from '../../data/wiring';
import { RolesCard } from './Roles';

// The role catalogue on AI features (FOUNDATION1y-i, the foundation design §2.7), in the shapes `ROLES` answers with: the seeded roles,
// Intake removed and waiting, an added role in a workspace with its choices, and the Designer Daoris suggests while no role takes design.

const seed = (id: string, en: string, zh: string, work: string): RoleRow => ({
  id, state: 'seed', seeded: true, nameEn: en, nameZh: zh, takes: [work], scopeKind: 'machine', revision: 0,
});

const CATALOGUE: RoleCatalogue = {
  roles: [
    seed('build', 'Build', '开发', 'build'),
    { id: 'intake', state: 'removed', seeded: true, takes: [], revision: 2 },
    seed('opinion', 'Second opinion', '第二意见', 'review'),
    seed('helper', 'Helper', '助手', 'help'),
    seed('partner', 'Partner', '搭档', 'partner'),
    seed('brainstorm', 'Brainstorm', '头脑风暴', 'brainstorm'),
    seed('ask-daoris', 'Ask Daoris', '问道衍', 'ask-daoris'),
    seed('chat', 'Chat', '聊天', 'chat'),
    {
      id: 'night', state: 'set', seeded: false, nameEn: 'Night shift', nameZh: '夜班', takes: ['build'], scopeKind: 'workspace',
      scopeId: 'studio', revision: 3, choices: [{ agent: 'codex-acp', model: 'gpt-5', effort: 'high' }, { agent: 'claude-code' }],
    },
  ],
  works: ['build', 'design', 'chat', 'ask-daoris', 'intake', 'help', 'partner', 'review', 'brainstorm'],
  kept: true,
  suggested: [{ id: 'designer', nameEn: 'Designer', nameZh: '设计师', takes: ['design'], scopeKind: 'machine', choices: [{ agent: 'claude-code', model: 'opus', effort: 'high' }] }],
};

const meta: Meta<typeof RolesCard> = {
  title: 'Settings/Roles',
  component: RolesCard,
  args: {
    catalogue: CATALOGUE,
    agents: [
      { value: 'claude-code', label: 'Claude Code — claude-code' },
      { value: 'codex-acp', label: 'Codex — codex-acp' },
    ],
    onSet: () => {},
  },
  // The main area at its wide width, as the frame gives it (the UI/UX design R6, UIFIT1).
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof RolesCard>;

/** The seeded roles, Intake removed and waiting, a night shift in one workspace, and the Designer suggested. */
export const Catalogue: Story = {};

/** The same at the half width. */
export const CatalogueHalf: Story = { decorators: [storyWidths.half] };

/** The same at the 400 px floor: each value under its words. */
export const CatalogueNarrow: Story = { decorators: [storyWidths.narrow] };

/** The same in 中文, each role named as it was written in Chinese. */
export const CatalogueChinese: Story = { decorators: [chinese] };

/** The same in 中文 at the floor. */
export const CatalogueChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

/** A home whose driver.json is the file's own: the roles shown, nothing pressable. */
export const NotKept: Story = { args: { catalogue: { ...CATALOGUE, kept: false, suggested: [] } } };
