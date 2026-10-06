import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import { HandedAccount } from './HandedAccount';
import { BARE, CLAIMED, CUT, NEWER, UNREAD } from './handedFixtures';

// What a driven session was handed, section by section (CONTEXT1, D143 point 1), as it sits beneath the target on the
// session's page: folded to its size; a claim on an ask, open, with the rules handed beside it and what was not handed; a
// carry-on whose every bound left something out; words that could not be read; a quest that carries nothing on an agent
// that takes no rules; a target from before the account was kept; a newer driver's codes; and each in 中文 and in dark.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/** The target's own card, which the account sits at the foot of on the page (`ConversationView`'s ask). */
function Card({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <div className="rounded-card border border-line border-l-[3px] border-l-accent bg-raised px-3 py-2">
      <span className="text-meta text-ink-faint">{t('work.conversation.target')}</span>
      <p className="m-0 mt-0.5 line-clamp-2 whitespace-pre-wrap text-body text-ink">
        {'You are the agent for `reports`, working inside its own repository and nowhere else.\n\nYour target is quest `#abc123`, asked by `ask #a1b2c3`:'}
      </p>
      {children}
    </div>
  );
}

const meta: Meta<typeof HandedAccount> = {
  title: 'Work/HandedAccount',
  component: HandedAccount,
  args: { account: CLAIMED },
  // Drawn inside the card rather than decorated with it, so a story's language reaches the card's label too.
  render: (args) => <Card><HandedAccount {...args} /></Card>,
};
export default meta;

type Story = StoryObj<typeof HandedAccount>;

/** Folded, as the page first shows it: its size, its sections. */
export const Folded: Story = {};

/** A claim on an ask, open: each section's size and source, the rules handed beside it, and what was not handed. */
export const Claimed: Story = { args: { defaultOpen: true } };

/** The same in 中文: the names, sources and absences worded; ids, names and files as written. */
export const ClaimedChinese: Story = { ...Claimed, decorators: [chinese] };

/** The same in dark. */
export const ClaimedDark: Story = { ...Claimed, decorators: [dark] };

/** A carry-on whose every bound left something out, each said with its bound, and the sections it cut counted. */
export const EveryBoundCut: Story = { args: { account: CUT, defaultOpen: true } };

/** Every bound's cut in 中文, its moments written the way 中文 writes them. */
export const EveryBoundCutChinese: Story = { ...EveryBoundCut, decorators: [chinese] };

/** Every bound's cut in dark. */
export const EveryBoundCutDark: Story = { ...EveryBoundCut, decorators: [dark] };

/** The person's words could not be read for this start: their sentence handed, and said unread. */
export const WordsUnread: Story = { args: { account: UNREAD, defaultOpen: true } };

/** A repository's quest that carries nothing, on an agent that takes no rules from Daoris: most of it the driver's own. */
export const CarriesNothing: Story = { args: { account: BARE, defaultOpen: true } };

/** A target handed before the account was kept says so, rather than showing nothing (D143 point 3). */
export const NotKept: Story = { args: { account: null } };

/** Not kept, in 中文. */
export const NotKeptChinese: Story = { ...NotKept, decorators: [chinese] };

/** A newer driver's section, cut and absence: its own English, marked, beside what this page words. */
export const NewerDriver: Story = { args: { account: NEWER, defaultOpen: true }, decorators: [chinese] };

/** A narrow pane: the source column wraps, and the sizes stay on their line. */
export const Narrow: Story = {
  args: { account: CUT, defaultOpen: true },
  decorators: [chinese, (Story) => <div className="w-80"><Story /></div>],
};
