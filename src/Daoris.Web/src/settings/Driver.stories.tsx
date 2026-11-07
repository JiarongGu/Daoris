import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { InTheme } from '../plugins/storyIcons';
import type { UpdateState } from '../update/UpdateBanner';
import { SETTINGS } from '../bridge/generated/routes';
import { DriverCards, type DriverFacts } from './DriverCards';

// Settings → Driver, drawn from the shell's facts as `DriverDomain` hands them: the home with its door to This
// Daoris, where plugins are looked for, the two dials, the cool-off, this machine's sessions' access and the install's
// update; at the frame's three widths, in English and 中文, light and dark (the UI/UX design R1, R6; UIFIT1). The rows
// are the ones UIFIT1a's shots showed with their values at the far edge; here each sits on the card's track.

const nothing = () => {};

const UPDATE: UpdateState = { state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, last: null };

const FACTS: DriverFacts = {
  home: 'C:/somewhere/Daoris',
  pluginsFolder: 'C:/somewhere/Daoris/plugins',
  notify: true,
  coolOff: 60,
  cap: 2,
  machineAccess: { full: false, pauses: [{ pause: 'goAheads', value: 'never' }] },
  update: UPDATE,
};

const meta: Meta<typeof DriverCards> = {
  title: 'Settings/Driver',
  component: DriverCards,
  args: {
    facts: FACTS,
    minutes: { cooloff: SETTINGS.find((each) => each.key === 'cooloff')!, quiet: SETTINGS.find((each) => each.key === 'quiet')! },
    busy: { access: false, update: false },
    onGo: nothing, onNotify: nothing, onCoolOff: nothing, onCap: nothing, onAccess: nothing, onSayUpdate: nothing,
  },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof DriverCards>;

/** The domain at the wide main area: one label track a card, each value beside its words, the cards 16 px apart. */
export const Driver: Story = {};

/** At the half width, 630 px: the track still beside. */
export const DriverHalf: Story = { decorators: [storyWidths.half] };

/** At the 400 px floor: the track goes, each value under its words. */
export const DriverNarrow: Story = { decorators: [storyWidths.narrow] };

/** In 中文, wide: the shorter labels keep the track at its floor. */
export const DriverChinese: Story = { decorators: [chinese] };

/** In 中文 at the half width. */
export const DriverChineseHalf: Story = { decorators: [storyWidths.half, chinese] };

/** In 中文 at the floor. */
export const DriverChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

/** In dark, wide. */
export const DriverDark: Story = { decorators: [(Story) => <InTheme theme="dark"><Story /></InTheme>] };

/** In dark at the floor. */
export const DriverDarkNarrow: Story = { decorators: [storyWidths.narrow, (Story) => <InTheme theme="dark"><Story /></InTheme>] };
