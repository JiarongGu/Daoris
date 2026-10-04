import type { Meta, StoryObj } from '@storybook/react-vite';
import i18n from '../i18n';
import { paletteCommands } from '../commands';
import { CommandPalette } from './CommandPalette';
import { ATTENDED, IN_A_BROWSER, menuWorld } from './menuFixtures';

// Every state of the palette (SURF9), on the shipped component, from the one table the menu bar reads (UX7a): "what a
// browser sees" and "what a shell sees" are arguments rather than arrangements.

const t = i18n.t.bind(i18n);

const meta: Meta<typeof CommandPalette> = {
  title: 'Chrome/CommandPalette',
  component: CommandPalette,
  args: { open: true, onClose: () => {} },
};
export default meta;

type Story = StoryObj<typeof CommandPalette>;

/** A shell on Quests: every menu's rows that apply, under their menu's name. */
export const InTheShell: Story = { args: { commands: paletteCommands(menuWorld(), t) } };

/** A shell on Sessions with a session attended: its acts join, by its header's owner. */
export const OnSessions: Story = { args: { commands: paletteCommands(menuWorld(ATTENDED), t) } };

/**
 * A browser. Shorter by OMISSION: no machine, no session, no window — because a palette is a promise that what it lists
 * can be done (D47 §4).
 */
export const InABrowser: Story = { args: { commands: paletteCommands(menuWorld(IN_A_BROWSER), t) } };

/** Nothing matches. It says so — an empty panel reads as broken where a sentence reads as an answer. */
export const NothingMatches: Story = { args: { commands: [] } };

/**
 * Where Ask Daoris is here (DOCK1d): the placeholder says anything can be asked, and once something is
 * typed the last row asks it. Type into the story to see the row.
 */
export const WithAsking: Story = { args: { commands: paletteCommands(menuWorld(), t), onAsk: () => {} } };
