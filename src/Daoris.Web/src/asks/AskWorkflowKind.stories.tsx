import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { AskWorkflowKind } from './AskWorkflowKind';
import { CLOSED, PROPOSED } from './fixtures';

// An ask's kind of work (WORKFLOW1i-c, D157), as its page's *kind* section draws it (`AskPage.tsx`), on its own (STORYGAP1, the
// UI/UX design §1.2 item 6, where it was drawn only inside `AskPage`'s stories at one width): nothing chosen, a kind with its
// workflow and a proposal with *Apply*, two proposals, a press on its way, a closed ask; at the main area's three widths, in
// 中文 and dark.

const nothing = () => {};
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const PROPOSAL = { kind: 'docs', reason: 'Only the README changes.', at: '2026-10-09T09:05:00Z' };
const CHOSEN = {
  ...PROPOSED,
  workflowChoices: [{ kind: 'bug', workflow: 'fix-flow', at: '2026-10-09T09:00:00Z' }],
  workflowProposals: [PROPOSAL],
};

const meta = {
  title: 'Asks/AskWorkflowKind',
  component: AskWorkflowKind,
  args: { ask: CHOSEN, onChoose: nothing },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof AskWorkflowKind>;

export default meta;
type Story = StoryObj<typeof meta>;

/** The person's kind standing with its workflow, and an intake's proposal beside it with *Apply*. */
export const ChosenWithProposal: Story = {};

/** Nothing chosen, and two proposals its intake kept, each with its reason. */
export const NoneChosen: Story = {
  args: {
    ask: {
      ...PROPOSED,
      workflowProposals: [
        { kind: 'bug', reason: 'The streamer stalls: something that worked stopped working.', at: '2026-10-09T08:50:00Z' },
        PROPOSAL,
      ],
    },
  },
};

/** A kind chosen with no workflow named, and no proposal left: the choice said alone. */
export const ChosenAlone: Story = { args: { ask: { ...PROPOSED, workflowChoices: [{ kind: 'feature', workflow: null, at: '2026-10-09T09:00:00Z' }] } } };

/** A press on its way: *Apply* waits. */
export const Busy: Story = { args: { busy: true } };

/** Closed: the proposal stays said, with no *Apply*. */
export const Closed: Story = { args: { ask: { ...CHOSEN, state: CLOSED.state, note: CLOSED.note } } };

/** Half: a 680 px window with the list a strip beside it. */
export const Half: Story = { decorators: [storyWidths.half] };

/** The main area's 400 px floor: *Apply* wraps under its reason. */
export const Narrow: Story = { decorators: [storyWidths.narrow] };

/** In 中文, wide. */
export const Chinese: Story = { decorators: [chinese] };

/** In 中文, at the floor. */
export const ChineseNarrow: Story = { decorators: [chinese, storyWidths.narrow] };

/** In dark. */
export const Dark: Story = { decorators: [dark] };

/** In 中文 and dark, half. */
export const ChineseDarkHalf: Story = { decorators: [chinese, dark, storyWidths.half] };
