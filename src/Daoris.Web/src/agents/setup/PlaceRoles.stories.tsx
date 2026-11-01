import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import type { RoleCatalogue, RoleStructure } from '../../data/wiring';
import { InTheme } from '../../plugins/storyIcons';
import type { Answered } from '../../work/InlineConfirm';
import { PlaceRolesView } from './PlaceRoles';
import { AGENTS, CATALOGUE, CHAIN } from './setupFixtures';

// A place's Roles (SETUPUX1l, the setup-UX design §2.4): only the works that differ from the parent as rows, the rest folded to one line
// naming them; the role editor opened in place from *Give … its own…* or *Edit*; a role shared by two works edited once with both named;
// the driver's refusal under the editor. At the main area's three widths (80, 39 and 25 rem), in both languages and both themes; a
// `play` presses what a person would, by keyboard-reachable controls.

const nothing = () => {};
const ENGINE = { workspace: 'aurora', repository: 'engine' };

const press = (canvasElement: HTMLElement, name: RegExp) => {
  const button = [...canvasElement.querySelectorAll<HTMLButtonElement>('button')].find((each) => name.test(each.getAttribute('aria-label') ?? each.textContent ?? ''));
  button?.click();
  return button;
};
const settle = () => new Promise((resolve) => setTimeout(resolve, 80));

/** Engine's design and building taken by one role of its own: one edit covers both. */
const SHARED_CHAIN = (): RoleStructure => {
  const answer = CHAIN(ENGINE);
  const mine = answer.places[0]!;
  const role = mine.works.find((each) => each.work === 'build')!.role!;
  return { ...answer, places: [{ ...mine, works: mine.works.map((each) => (each.work === 'design' ? { ...each, role } : each)) }, ...answer.places.slice(1)] };
};
const SHARED: RoleCatalogue = {
  ...CATALOGUE, roles: CATALOGUE.roles.map((each) => (each.id === 'engine-build' ? { ...each, takes: ['build', 'design'], choices: [{ agent: 'claude-code', model: 'sonnet', effort: 'high' }] } : each)),
};
const OWN: RoleCatalogue = {
  ...CATALOGUE, roles: CATALOGUE.roles.map((each) => (each.id === 'engine-build' ? { ...each, choices: [{ agent: 'claude-code', model: 'sonnet', effort: 'high' }] } : each)),
};

type Args = { shared?: boolean; refuse?: string };

function Roles({ shared = false, refuse }: Args) {
  const catalogue = shared ? SHARED : OWN;
  const editor = {
    agents: AGENTS, offers: { 'claude-code': { models: ['opus', 'sonnet', 'haiku'], efforts: ['low', 'medium', 'high'] } }, staffing: [], catalogue, read: 'ok' as const, busy: false,
    onSet: refuse ? (_press: unknown, answered: Answered) => answered.refused(refuse) : nothing,
    onSetActor: nothing,
  };
  return <PlaceRolesView place={ENGINE} read="ok" answer={shared ? SHARED_CHAIN() : CHAIN(ENGINE)} editor={editor} appointed={null} onSource={nothing} />;
}

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof Roles> = { title: 'Agents/PlaceRoles', component: Roles, decorators: [storyWidths.wide] };
export default meta;

type Story = StoryObj<typeof Roles>;

/** Engine's Roles: building (set here) and the second opinion (cooling) are rows; seven works fold to one line naming them. */
export const Folded: Story = {};
export const FoldedHalf: Story = { decorators: [storyWidths.half] };
export const FoldedNarrow: Story = { decorators: [storyWidths.narrow] };
export const FoldedChinese: Story = { decorators: [chinese] };
export const FoldedChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const FoldedChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };
export const FoldedDark: Story = { decorators: [dark] };
export const FoldedDarkNarrow: Story = { decorators: [storyWidths.narrow, dark] };

const unfold: Story['play'] = async ({ canvasElement }) => {
  press(canvasElement, /more, as |另有/);
  await settle();
};

/** The fold pressed: the works that take aurora's role, each with *Give engine its own…*. */
export const Unfolded: Story = { play: unfold };
export const UnfoldedNarrowChinese: Story = { play: unfold, decorators: [storyWidths.narrow, chinese] };

const give: Story['play'] = async ({ canvasElement }) => {
  press(canvasElement, /more, as |另有/);
  await settle();
  press(canvasElement, /Give engine its own… for design|为engine设置专属角色…，设计与决策/);
  await settle();
};

/** *Give engine its own…* pressed: the role editor in place, its choices first, *Edit role* folded and open for a new role, the consequence before Save. */
export const GivingItsOwn: Story = { play: give };
export const GivingItsOwnHalf: Story = { play: give, decorators: [storyWidths.half] };
export const GivingItsOwnNarrow: Story = { play: give, decorators: [storyWidths.narrow] };
export const GivingItsOwnChinese: Story = { play: give, decorators: [chinese] };
export const GivingItsOwnChineseNarrow: Story = { play: give, decorators: [storyWidths.narrow, chinese] };
export const GivingItsOwnDark: Story = { play: give, decorators: [dark] };

const edit: Story['play'] = async ({ canvasElement }) => {
  press(canvasElement, /Edit, the role taking building|编辑，承担开发的角色/);
  await settle();
};

/** *Edit* pressed on the role set here, the typed model kept beside the agent's offers. */
export const EditingItsRole: Story = { play: edit };
export const EditingItsRoleNarrow: Story = { play: edit, decorators: [storyWidths.narrow] };
export const EditingItsRoleChineseDark: Story = { play: edit, decorators: [chinese, dark] };

/** Keyboard: focus rests on *Edit*, whose ring shows. */
export const EditFocused: Story = {
  play: async ({ canvasElement }) => {
    [...canvasElement.querySelectorAll<HTMLButtonElement>('button')].find((each) => /Edit, the role taking building|编辑，承担开发的角色/.test(each.getAttribute('aria-label') ?? ''))?.focus();
  },
};

/** One role taking building and design: both are rows, each saying it is shared, and one edit covers both. */
export const SharedRole: Story = { args: { shared: true } };
export const SharedRoleNarrowChinese: Story = { args: { shared: true }, decorators: [storyWidths.narrow, chinese] };

const sharedEdit: Story['play'] = async ({ canvasElement }) => {
  press(canvasElement, /Edit, the role taking building|编辑，承担开发的角色/);
  await settle();
};

/** The shared role's editor names the works it takes and offers no single work to change. */
export const SharedRoleEditor: Story = { args: { shared: true }, play: sharedEdit };
export const SharedRoleEditorNarrow: Story = { args: { shared: true }, play: sharedEdit, decorators: [storyWidths.narrow] };

const refused: Story['play'] = async ({ canvasElement }) => {
  press(canvasElement, /Edit, the role taking building|编辑，承担开发的角色/);
  await settle();
  const form = canvasElement.querySelector('form');
  const save = [...(form?.querySelectorAll<HTMLButtonElement>('button') ?? [])].find((each) => each.type === 'submit');
  save?.click();
  await settle();
};
const REFUSAL = '`engine-review` already takes `review` in that scope: at most one role takes a work in one scope.';

/** The driver's two-roles-one-work refusal, said under the editor, which stays open. */
export const TwoRolesOneWork: Story = { args: { refuse: REFUSAL }, play: refused };
export const TwoRolesOneWorkNarrowDark: Story = { args: { refuse: REFUSAL }, play: refused, decorators: [storyWidths.narrow, dark] };
