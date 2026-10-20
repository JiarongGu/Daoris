import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { InTheme } from '../../plugins/storyIcons';
import type { Answered } from '../../work/InlineConfirm';
import { FullAccessAsk } from './HelpModePicker';

// Ask Daoris's full access asked once (ACCESSASK1, the UI/UX design §3.4): the one `InlineConfirm` in its warning tone, naming the
// servers it would add and when it takes effect; waiting for the driver's answer with its presses kept; and a refusal said inside
// it. In the side bar at its 300 px floor and at the 45% a wide window gives it, in both themes and in 中文.

const meta = {
  title: 'Help/FullAccessAsk',
  component: FullAccessAsk,
  args: { adds: ['browser', 'git-tools'], onEnable: (_answered: Answered) => {}, onClose: () => {} },
} satisfies Meta<typeof FullAccessAsk>;

export default meta;
type Story = StoryObj<typeof meta>;

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;
/** The side bar's floor, at every window width. */
const sideFloor: Decorator = (Story) => <div className="w-[300px]"><Story /></div>;
/** The side bar at 45% of a 1546 px window. */
const sideWide: Decorator = (Story) => <div className="w-[696px] max-w-full"><Story /></div>;

/** In the side bar at its floor: the servers it adds, and from when. */
export const SideBar: Story = { decorators: [sideFloor] };

/** The same in dark. */
export const SideBarDark: Story = { decorators: [sideFloor, dark] };

/** The same in 中文. */
export const SideBarChinese: Story = { decorators: [sideFloor, chinese] };

/** At the side bar's 45%. */
export const SideBarWide: Story = { decorators: [sideWide] };

/** No server is withheld now: full access adds running commands and editing files. */
export const AddsNone: Story = { args: { adds: [] }, decorators: [sideFloor] };

/** The servers still being read. */
export const AddsReading: Story = { args: { adds: null }, decorators: [sideFloor] };

/** Pressed, waiting for the driver: *Waiting…* beside the presses, which wait with it. */
export const Waiting: Story = {
  args: { onEnable: () => {} },
  decorators: [sideFloor],
  play: async ({ canvasElement }) => {
    canvasElement.querySelector<HTMLButtonElement>('button')?.click();
  },
};

/** Refused: the driver's sentence inside the ask, whole, and the press offered again. */
export const Refused: Story = {
  args: { onEnable: (answered) => answered.refused('The driver could not write driver.json: it is in use by another process.') },
  decorators: [sideFloor],
  play: Waiting.play,
};

/** Refused, in dark. */
export const RefusedDark: Story = { ...Refused, decorators: [sideFloor, dark] };
