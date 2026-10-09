import type { Meta, StoryObj } from '@storybook/react-vite';
import { AskHistory } from './AskHistory';
import type { HelpConversationRow } from './history';

// ASKHIST1: Ask Daoris's history, kept on this machine only: its rows, a search, nothing yet, and a list cut short.

const ago = (minutes: number) => new Date(Date.now() - minutes * 60_000).toISOString();

const row = (over: Partial<HelpConversationRow> & { session: string }): HelpConversationRow => ({
  title: 'what is a workspace?', name: null, opening: 'what is a workspace?', about: 'A circle of repositories that share a remote.',
  created: ago(3000), last: ago(2900), pinned: null, live: false, resumable: true, from: null, handed: null, found: null, ...over,
});

const ROWS: HelpConversationRow[] = [
  row({ session: 'h1a2b3c4', title: 'Landing on feature branches', name: 'Landing on feature branches', pinned: ago(60),
    about: 'Repositories → engine → Setup → Line and landing, or `daoris driver landing engine branch feature/{slug}`.' }),
  row({ session: 'h2b3c4d5', title: 'why is engine held?', opening: 'why is engine held?', live: true, last: ago(1),
    about: 'It is held by your press on Repositories → engine; Resume lets its quests start again.' }),
  row({ session: 'h3c4d5e6', title: 'how do I make a plugin that holds quests overnight?', opening: 'how do I make a plugin that holds quests overnight?',
    resumable: false, last: ago(1500), about: 'As an ask at the repository that holds your plugins: it is made with its tests.' }),
  row({ session: 'h4d5e6f7', title: 'and a remote for the team?', opening: 'and a remote for the team?', from: 'h1a2b3c4', handed: 'transcript',
    last: ago(400), about: null }),
];

const meta = {
  title: 'Help/AskHistory',
  component: AskHistory,
  args: {
    rows: ROWS, search: '', onSearch: () => {}, onOpen: () => {}, onRename: () => {}, onPin: () => {}, onStartFrom: () => {},
    onDelete: () => {},
  },
  decorators: [(Story) => <div className="w-[24rem] bg-page p-4"><Story /></div>],
} satisfies Meta<typeof AskHistory>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Pinned first, then the newest; one running, one that starts anew, one started from an earlier one. */
export const Conversations: Story = { args: { shown: 'h2b3c4d5' } };

/** A search found one by what was said in it, and says where. */
export const Found: Story = {
  args: { search: 'remote', rows: [row({ session: 'h1a2b3c4', found: '…A circle of repositories that share a **remote**.' })] },
};

export const FoundNothing: Story = { args: { search: 'nothing like this', rows: [] } };

/** Before the first conversation: what the history is, and that it stays here. */
export const Empty: Story = { args: { rows: [] } };

/** More than the list reads: it says older ones are left out. */
export const Cut: Story = { args: { cut: true } };

/** A pin that could not be kept, said where it was pressed. */
export const Refused: Story = { args: { refusal: 'no conversation here is `h3c4d5e6` any more.' } };
