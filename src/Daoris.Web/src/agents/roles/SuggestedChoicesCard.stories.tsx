import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import type { ActorRow, RoleCatalogue, RoleRow } from '../../data/wiring';
import { RolesCard } from './Roles';

// MODELROLE1g: the head of Working roles at first run, with the choices Daoris suggests for the roles and the words that say using them
// can spend more. Each story is one width and one language; the theme is the toolbar's.

const seed = (id: string, en: string, zh: string, work: string): RoleRow => ({
  id, state: 'seed', seeded: true, name: { en, zh }, takes: [work], scopeKind: 'machine', revision: 0,
});

const OPUS = { agent: 'claude-code', model: 'opus', effort: 'high' };

const CATALOGUE: RoleCatalogue = {
  roles: [
    seed('build', 'Build', '开发', 'build'), seed('intake', 'Intake', '受理', 'intake'), seed('opinion', 'Second opinion', '第二意见', 'review'),
    seed('helper', 'Helper', '助手', 'help'), seed('partner', 'Partner', '搭档', 'partner'), seed('brainstorm', 'Brainstorm', '头脑风暴', 'brainstorm'),
  ],
  works: ['build', 'design', 'chat', 'ask-daoris', 'intake', 'help', 'partner', 'review', 'brainstorm'],
  kept: true,
  suggestedChoices: [
    { role: 'build', choices: [{ agent: 'claude-code', model: 'sonnet', effort: 'high' }, OPUS] },
    { role: 'opinion', choices: [OPUS] }, { role: 'helper', choices: [OPUS] }, { role: 'partner', choices: [OPUS] }, { role: 'brainstorm', choices: [OPUS] },
  ],
  suggestedChoicesDated: '2026-10-31',
};

const before = (id: string): ActorRow => ({ id, state: 'before', choices: [{ agent: 'claude-code', from: 'before' }] });

const meta: Meta<typeof RolesCard> = {
  title: 'Agents/Roles/Suggested choices',
  component: RolesCard,
  args: {
    catalogue: CATALOGUE,
    staffing: ['build', 'intake', 'opinion', 'helper', 'partner', 'brainstorm'].map(before),
    agents: [{ value: 'claude-code', label: 'Claude Code — claude-code' }],
    onSet: () => {},
    onSetAll: () => {},
  },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof RolesCard>;

/** First run, wide: the card above the rows. */
export const FirstRun: Story = {};

export const FirstRunHalf: Story = { decorators: [storyWidths.half] };

export const FirstRunNarrow: Story = { decorators: [storyWidths.narrow] };

export const FirstRunChinese: Story = { decorators: [chinese] };

export const FirstRunChineseHalf: Story = { decorators: [storyWidths.half, chinese] };

export const FirstRunChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

/** An agent the machine has not set up is left out, and the card says so. */
export const LeftOut: Story = {
  args: { catalogue: { ...CATALOGUE, suggestedChoices: [{ role: 'build', choices: [OPUS, { agent: 'codex-acp' }] }] } },
};
