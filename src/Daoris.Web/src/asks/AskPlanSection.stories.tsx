import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import type { AskPlan } from '../work/askPlan';
import { AskPlanSection } from './AskPlanSection';

// An ask's plan, as its page's *plan* section draws it (`AskPage.tsx`): the parts with the quest each became, the order between
// them, what the judge found, and a worker's split of its own part one level down; at the main area's three widths, in 中文 and dark.

const nothing = () => {};
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const PLAN: AskPlan = {
  plans: [
    {
      scope: null, revision: 2, depth: 1, why: 'One part per thing a person sees working, the contract written first.',
      parts: [
        { id: 'keel', repository: 'media-api', quest: '3f9a1c', outcome: 'The field name is read from config.', lanes: [], claims: ['src/Config/**'] },
        { id: 'page', repository: 'media-web', quest: '8b20d4', outcome: 'The settings page shows the field name.', lanes: ['web'], claims: ['src/settings/**'] },
        { id: 'docs', repository: 'media-web', quest: null, outcome: 'The guide says where the name is set.', lanes: ['web'], claims: [] },
      ],
      edges: [{ part: 'page', on: 'keel', kind: 'after', why: 'The page reads the contract the keel lands.' }],
      flags: [{ kind: 'claim-record', part: 'docs', text: '`docs` claims `TASKS.md`, a record: the integrator writes it from the part’s intents, so the claim is dropped.' }],
    },
    {
      scope: 'page', revision: 1, depth: 2, why: 'The page is two splits of my own.',
      parts: [
        { id: 'page.form', repository: 'media-web', quest: 'c41e77', outcome: 'The field is editable.', lanes: ['web'], claims: [] },
        { id: 'page.read', repository: 'media-web', quest: 'd9a0b2', outcome: 'The field is listed read-only.', lanes: ['web'], claims: [] },
      ],
      edges: [], flags: [],
    },
  ],
  team: [
    { part: null, role: 'lead', session: 'a1b2c3', revision: 2 },
    { part: 'page', role: 'lead', session: 'd4e5f6', revision: 1 },
  ],
  decisions: [
    { scope: null, revision: 1, decision: 'planned', reason: 'Two repositories, so one part each.', author: 'a1b2c3', act: 'keel #3f9a1c, page #8b20d4' },
    { scope: null, revision: 2, decision: 'revised', reason: 'The docs part is its own, since the guide is read apart.', author: 'a1b2c3', act: 'docs unpublished' },
  ],
};

const meta = {
  title: 'Asks/AskPlanSection',
  component: AskPlanSection,
  args: { plan: PLAN, onOpenQuest: nothing },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof AskPlanSection>;

export default meta;
type Story = StoryObj<typeof meta>;

/** An ask's own plan with an order and a flag, and a worker's split of one part below it. */
export const Plan: Story = {};

/** The same in 中文. */
export const PlanChinese: Story = { decorators: [chinese] };

/** The same in the dark theme. */
export const PlanDark: Story = { decorators: [dark] };

/** At the narrow width the outcome and the claim wrap rather than clip. */
export const PlanNarrow: Story = { decorators: [storyWidths.narrow] };
