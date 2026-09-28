import type { Meta, StoryObj } from '@storybook/react-vite';
import { ConversationFind } from './ConversationFind';

// The way through a long run (SESS1 S9): the jumps, and a search in each state it answers in.

const meta = {
  title: 'Work/ConversationFind',
  component: ConversationFind,
  args: {
    hasFailure: true, onFirstFailure: () => {}, hasWords: true, onLastWords: () => {},
    query: '', onQuery: () => {}, onStep: () => {},
  },
} satisfies Meta<typeof ConversationFind>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Idle: Story = {};

export const Found: Story = { args: { query: 'gates', hits: 12, at: 2 } };

export const FoundMoreThanHeld: Story = { args: { query: 'test', hits: 100, at: 0, cut: true } };

export const NothingFound: Story = { args: { query: 'nowhere', hits: 0 } };

export const NoFailure: Story = { args: { hasFailure: false } };
