import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { Card, CardStack, SettingList } from '../../design/ui';
import { PlaceRiskActs, type RiskActsView } from './PlaceRiskActs';

// A place's act table: per kind of act its class, where the rule comes from, who answers by the cast, the last seven days, `policy.change` locked, and
// a picked change with the waits it opens or releases; at the main area's 80, 39 and 25 rem, and in 中文.

const nothing = () => {};

const view: RiskActsView = {
  place: 'repository/engine', mode: 'restricted', revision: 'a1b2c3d4e5f6', unknown: [],
  rows: [
    { kind: 'work.done', class: 'none', source: 'seed', reason: 'no risk declared', passes: 14, gates: 0, locked: false },
    { kind: 'line.land', class: 'none', source: 'seed', reason: 'no risk declared', passes: 6, gates: 0, locked: false },
    { kind: 'spend.money', class: 'owner', source: 'repository/engine', reason: 'set here', passes: 0, gates: 2, locked: false },
    { kind: 'reach.widen', class: 'role', by: 'approver', source: 'seed', reason: 'seed', answers: { role: 'approver', holder: 'agent:codex' }, passes: 1, gates: 3, locked: false },
    { kind: 'history.rewrite', class: 'owner', source: 'seed', reason: 'seed', passes: 0, gates: 0, locked: false },
    { kind: 'policy.change', class: 'owner', source: 'fixed', reason: 'fixed', passes: 0, gates: 0, locked: true },
  ],
  shifts: [],
};

const opening: RiskActsView = {
  ...view,
  shifts: [
    { place: 'repository/engine', act: 'line.land', mode: 'restricted', from: 'none', to: 'owner', effect: 'opens' },
    { place: 'repository/engine', act: 'line.land', mode: 'full', from: 'none', to: 'owner', effect: 'opens' },
  ],
};

const meta: Meta<typeof PlaceRiskActs> = {
  title: 'Settings/PlaceRiskActs',
  component: PlaceRiskActs,
  args: { name: 'engine', twin: '`daoris settings risk set repository/engine <act> none|role|owner`', view, onPick: nothing, onSave: nothing, onCancel: nothing },
  decorators: [storyWidths.wide, (Story) => <CardStack><Card><SettingList><Story /></SettingList></Card></CardStack>],
};
export default meta;

type Story = StoryObj<typeof PlaceRiskActs>;

export const Table: Story = {};
export const TableHalf: Story = { decorators: [storyWidths.half] };
export const TableNarrow: Story = { decorators: [storyWidths.narrow] };

export const Pending: Story = { args: { view: opening, pending: { kind: 'line.land', class: 'owner' } } };
export const PendingNothing: Story = { args: { pending: { kind: 'work.done', class: 'none' } } };
export const Unreadable: Story = { args: { view: { ...view, fault: '`risk` is not an object of places' } } };

export const TableChinese: Story = { decorators: [chinese] };
export const TableChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };
export const PendingChinese: Story = { args: { view: opening, pending: { kind: 'line.land', class: 'owner' } }, decorators: [chinese] };
