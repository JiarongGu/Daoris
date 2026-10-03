import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { I18nextProvider } from 'react-i18next';
import i18n from '../i18n';
import type { NotePart } from '../api';
import { Note } from './Note';

// A session's note in every shape a record brings it (LANG1b, D142; the language design §2, §5, §6): Daoris's lines worded
// in the reader's language, in English and in 中文; a split note, the lead-in worded and the agent's question beneath it
// as written; a code this page does not know; a record from before parts; a program's words; and one run for a row.

/** A reader of 中文, whatever the window's language, sharing the catalogues. */
const zh = i18n.cloneInstance({ lng: 'zh' });

/** The story in 中文. */
const chinese: Decorator = (Story) => <I18nextProvider i18n={zh}><Story /></I18nextProvider>;

/** A driven session that went on with its answer and ended with its quest still taken, on an account now cooling. */
const ENDED: NotePart[] = [
  {
    code: 'ended.answered-unfinished',
    values: { awaits: '9c41de', exit: 1 },
    text: 'It went on with #9c41de answered, and ended with the quest still taken (exit 1).',
  },
  {
    code: 'account.cooling',
    values: { until: '2026-10-03T16:00:00Z', why: 'stated' },
    text: 'The account it ran on is cooling until 2026-10-03 16:00 UTC (the agent said so); nothing starts on it until then.',
  },
];
const ENDED_NOTE = ENDED.map((part) => ('text' in part ? part.text : '')).join(' ');

/** The language design §3's example: Daoris's lead-in, the agent's question, the ledger's *Answered:* and the person's word. */
const ASKED: NotePart[] = [
  { code: 'ended.parked-asked', values: {}, text: 'It stopped with its quest still taken, to ask you:' },
  {
    words: 'Which branch should the release land on?\n\n1. `main`, as every release has so far.\n2. `release/1.0`, which keeps `main` open for the next feature.\n\nI recommend the second.',
    by: 'agent',
  },
  { code: 'ledger.answered', values: {}, text: 'Answered:' },
  { words: 'The second.', by: 'person' },
];

const meta: Meta<typeof Note> = {
  title: 'Work/Note',
  component: Note,
  args: { note: ENDED_NOTE, parts: ENDED, className: 'text-body leading-relaxed text-ink-soft' },
  decorators: [(Story) => <div className="max-w-3xl"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof Note>;

/** Daoris's lines, worded by code: an exit and a cool-off's moment and reason, in English. */
export const Coded: Story = {};

/** The same record in 中文: the lines from the catalogue, the moment as 中文 writes it. Nothing reads the English. */
export const CodedChinese: Story = { decorators: [chinese] };

/** A split note: the lead-in worded, the agent's question beneath it as written, its own formatting kept. */
export const AgentQuestion: Story = { args: { note: 'It stopped with its quest still taken, to ask you: …', parts: ASKED } };

/** The split note in 中文: Daoris's lines change, the agent's and the person's words do not. */
export const AgentQuestionChinese: Story = { ...AgentQuestion, decorators: [chinese] };

/** A code this page does not know (a newer driver's): its own English, marked *shown as recorded*, beside a worded line. */
export const UnknownCode: Story = {
  args: {
    note: 'The quest reached done. It handed its tree to the next step.',
    parts: [
      { code: 'ended.done', values: {}, text: 'The quest reached done.' },
      { code: 'ended.handed-on', values: { step: '2' }, text: 'It handed its tree to the next step.' },
    ],
  },
  decorators: [chinese],
};

/** A record from before parts, every record on the install before LANG1a: its note as kept, marked. */
export const OldNote: Story = {
  args: {
    note: 'It stopped with its quest still taken, to ask you: should the budget live on the chunk API or in the scheduler?',
    parts: null,
  },
  decorators: [chinese],
};

/** The old note in English: the same mark, since the page did not word it here either. */
export const OldNoteEnglish: Story = { ...OldNote, decorators: [] };

/** A program's words passed through as written, beneath the line that leads into them. */
export const ProgramWords: Story = {
  args: {
    note: 'Daoris could not record where its branch started, …: fatal: not a valid object name',
    parts: [
      {
        code: 'started.tree-unrecorded',
        values: {},
        text: 'Daoris could not record where its branch started, so bringing it up to date will cut where its work first differs from the line:',
      },
      { words: "fatal: Not a valid object name: 'origin/main'.", by: 'program' },
    ],
  },
};

/** One run, for a row too narrow for blocks: the band's two lines, the question inline. */
export const Compact: Story = {
  args: { note: null, parts: ASKED, compact: true, clamp: 2, className: 'text-small text-ink-soft' },
  decorators: [chinese, (Story) => <div className="w-80"><Story /></div>],
};
