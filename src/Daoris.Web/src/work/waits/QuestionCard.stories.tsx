import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import { chinese } from '../../design/storyLanguage';
import { sessionWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import type { ParkQuestion } from './parkQuestion';
import { QuestionCard } from './QuestionCard';

// The question card (QUESTIONUI1a; the supervisor design §8.1-§8.3): a park's question, each answer a row that reads whole, the
// recommended one marked with the agent's reason, nothing selected at first. Its own stories, apart from the box and the head that
// draw it: no answers, three with one recommended, six long ones, one selected, and listed with no radio; at a session page's three
// widths, in English and 中文, light and dark. The agent's words stay as written in every language.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const THREE: ParkQuestion = {
  id: 'q5t10n00',
  question: 'Which quarterly target should the dashboard tile show? The ticket says "the new quarterly target" without a number.',
  choices: [
    { label: '1,200', does: 'Write 1,200, the planning sheet\'s value, which the ticket links; development already holds it.' },
    { label: '1,350', does: 'Write 1,350, the value in the last stand-up note.' },
    { label: 'Leave it for now', does: 'Ship the tile reading the configuration, and leave the value as it is.' },
  ],
  recommend: 1,
  why: 'The ticket links the planning sheet, and development already holds that value.',
  by: 'before Friday\'s release window',
  at: '2026-10-22T09:05:00Z',
};

const LONG = 'and keep the earlier behaviour behind the setting until the next release, saying so in the changelog and the decision\'s note';

const SIX: ParkQuestion = {
  ...THREE,
  id: 'q6l0ng00',
  question: 'Where should the streaming budget live, given that the chunk API, the scheduler and the cache each spend part of it?',
  choices: [
    { label: 'On the chunk API, where the work is spent', does: `Cap hydration in the chunk API, ${LONG}.` },
    { label: 'In the scheduler, beside the queue', does: `Hold the budget in the scheduler, ${LONG}.` },
    { label: 'In the cache, by entry', does: `Charge each cache entry against the budget, ${LONG}.` },
    { label: 'Split between the API and the scheduler', does: `Give each half the budget, ${LONG}.` },
    { label: 'A setting the owner chooses', does: `Offer the three places as a setting, ${LONG}.` },
    { label: 'Not now', does: 'Leave the budget where it is and write the question into the backlog for the next planning.' },
  ],
  recommend: 1,
  why: 'The quest asks for the cap where the work is spent, and the chunk API is the one place every caller passes through.',
};

const FREE: ParkQuestion = { ...THREE, id: 'q7fr3e00', choices: [], recommend: null, why: null, question: 'Which environment should I test the migration against?' };

/** The card with its selection held as the frame holds it; `listed` draws it with no radio, as the head's card before *Answer…*. */
function Card({ question, initially = null, listed = false }: { question: ParkQuestion; initially?: string | null; listed?: boolean }) {
  const [selected, setSelected] = useState<string | null>(initially);
  return <QuestionCard question={question} selected={selected} onSelect={listed ? undefined : setSelected} />;
}

const meta: Meta<typeof Card> = {
  title: 'Work/Question card',
  component: Card,
  args: { question: THREE },
  decorators: [sessionWidths.half],
};
export default meta;

type Story = StoryObj<typeof Card>;

/** Three answers, the first recommended with its reason, none selected: Tab to the group, arrows to choose. */
export const Three: Story = {};

/** The same in 中文. */
export const ThreeChinese: Story = { decorators: [chinese] };

/** The same in dark. */
export const ThreeDark: Story = { decorators: [dark] };

/** The same in dark and 中文. */
export const ThreeDarkChinese: Story = { decorators: [chinese, dark] };

/** One selected, apart from the recommended: *Clear choice* returns it to none. */
export const Selected: Story = { args: { initially: '1,350' } };

/** The same in dark and 中文. */
export const SelectedDarkChinese: Story = { args: { initially: '1,350' }, decorators: [chinese, dark] };

/** Six long answers at the main area's floor. */
export const SixLongNarrow: Story = { args: { question: SIX }, decorators: [sessionWidths.narrow] };

/** The same in 中文 and dark. */
export const SixLongNarrowChineseDark: Story = { args: { question: SIX }, decorators: [sessionWidths.narrow, chinese, dark] };

/** Six long answers at the wide width. */
export const SixLongWide: Story = { args: { question: SIX }, decorators: [sessionWidths.wide] };

/** No answers offered: the question alone, answered in the person's words. */
export const NoAnswers: Story = { args: { question: FREE } };

/** The same in 中文. */
export const NoAnswersChinese: Story = { args: { question: FREE }, decorators: [chinese] };

/** Listed with no radio, as the head's card lists them until its answer is opened. */
export const Listed: Story = { args: { listed: true } };

/** The same in dark. */
export const ListedDark: Story = { args: { listed: true }, decorators: [dark] };
