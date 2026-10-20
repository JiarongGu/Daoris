import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { InTheme } from '../plugins/storyIcons';
import type { UpdateState } from '../update/UpdateBanner';
import { DriverCards, type DriverFacts } from './DriverCards';

// Settings → Driver (D75), drawn from the shell's facts as `DriverDomain` hands them: the home with its door to This
// Daoris, where plugins are looked for, the two dials, the cool-off, this machine's sessions' access and the install's
// update; at the frame's three widths, in English and 中文, light and dark (the UI/UX design R1, R6; UIFIT1). The rows
// are the ones UIFIT1a's shots showed with their values at the far edge; here each sits on the card's track.

const nothing = () => {};

const UPDATE: UpdateState = { state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, last: null };

const FACTS: DriverFacts = {
  home: 'C:/somewhere/Daoris',
  homeAccount: 'same',
  placeAnswers: true,
  pluginsFolder: 'C:/somewhere/Daoris/plugins',
  notify: true,
  strikes: 3,
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
    busy: { access: false, update: false },
    onGo: nothing, onNotify: nothing, onStrikes: nothing, onCoolOff: nothing, onCap: nothing, onAccess: nothing, onSayUpdate: nothing,
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

/**
 * A shell older than This Daoris: the home row keeps its hint and the two notices the start carried, under the value in
 * its column (R1); no cool-off and no access card, since the shell answers neither.
 */
export const OlderShell: Story = {
  args: {
    facts: {
      ...FACTS,
      placeAnswers: false,
      homeAccount: 'overridden',
      homeNotice: 'This start replaced the data folder the account names; a terminal reads the other one.',
      hostNotice: 'The host this window adopted serves another install\'s page.',
      coolOff: undefined,
      machineAccess: undefined,
    },
  },
};

/** Zero strikes: the one value whose consequence is invisible, said under the number. */
export const NeverParks: Story = { args: { facts: { ...FACTS, strikes: 0 } } };
