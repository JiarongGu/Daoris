import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { AskReview } from './AskReview';
import { CLOSED, PROPOSED } from './fixtures';

// An ask's review choice (REVIEWENV1g, D154 point 3), as its page's *review* section draws it (`AskPage.tsx`), on its own
// (STORYGAP1, the UI/UX design §1.2 item 6): nothing chosen, a choice with the person's words, an intake's proposal beside it
// with *Apply*, a press on its way, a closed ask; at the main area's three widths (`storyWidths`), in 中文 and dark.

const nothing = () => {};
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const CHOSEN = {
  ...PROPOSED,
  reviewChoices: [{ choice: 'local', at: '2026-10-09T09:00:00Z', words: 'run it locally against dev data' }],
  reviewProposals: [{ choice: 'off', reason: 'Only the README changes, which has nothing to show.', at: '2026-10-09T08:55:00Z' }],
};

const meta = {
  title: 'Asks/AskReview',
  component: AskReview,
  args: { ask: PROPOSED, environments: ['dev', 'local'], onChoose: nothing },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof AskReview>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Nothing chosen: each repository's rule decides, and the choice to set. */
export const ByRule: Story = {};

/** The person's choice standing with their words, and an intake's proposal beside it with *Apply*. */
export const ChosenWithProposal: Story = { args: { ask: CHOSEN } };

/** A press on its way: *Apply* and *Set* wait. */
export const Busy: Story = { args: { ask: CHOSEN, busy: true } };

/** Closed: the choice as it stood, and nothing to press. */
export const Closed: Story = { args: { ask: { ...CHOSEN, state: CLOSED.state, note: CLOSED.note } } };

/** Where nothing can set it (a browser): the standing alone. */
export const ReadOnly: Story = { args: { ask: CHOSEN, onChoose: undefined } };

/** Half: a 680 px window with the list a strip beside it. */
export const Half: Story = { args: { ask: CHOSEN }, decorators: [storyWidths.half] };

/** The main area's 400 px floor: the proposal's *Apply* wraps under its reason, the select over the words. */
export const Narrow: Story = { args: { ask: CHOSEN }, decorators: [storyWidths.narrow] };

/** In 中文, wide. */
export const Chinese: Story = { args: { ask: CHOSEN }, decorators: [chinese] };

/** In 中文, at the floor. */
export const ChineseNarrow: Story = { args: { ask: CHOSEN }, decorators: [chinese, storyWidths.narrow] };

/** In dark. */
export const Dark: Story = { args: { ask: CHOSEN }, decorators: [dark] };

/** In 中文 and dark, half. */
export const ChineseDarkHalf: Story = { args: { ask: CHOSEN }, decorators: [chinese, dark, storyWidths.half] };
