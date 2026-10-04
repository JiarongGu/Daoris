import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Quest, Session } from '../api';
import { SessionTimeline, TimelineEntry } from './SessionTimeline';

// Each kind of observed event, then the assembled layer. What is NOT here is the point: there is no
// story for "step 3 of 7", because the record has no such thing and reading one out of the stream
// was rejected by name (D52).

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const SESSION: Session = {
  id: 's1a2b3c4',
  quest: '7a82cc',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'completed',
  kind: 'driven',
  created: at(95),
  updated: at(50),
  evidence: 'commits landed:\nc0ffee12 cap hydration work per frame\ndeadbee1 expose the budget on the chunk API\n1a2b3c4d test: a frame that would have hydrated four chunks',
};

const QUEST: Quest = {
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs a per-frame cap.',
  status: 'Done',
  filed: at(10_000),
  updated: at(55),
};

const meta: Meta<typeof SessionTimeline> = {
  title: 'Work/SessionTimeline',
  component: SessionTimeline,
  args: { session: SESSION, quest: QUEST },
  decorators: [(Story) => <div className="max-w-xl"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof SessionTimeline>;

/** A session that ran, closed its quest, and landed three commits. */
export const Whole: Story = {};

/**
 * A session nothing has happened to yet. One entry is the honest answer — the alternative is
 * inventing the steps nobody observed.
 */
export const JustQueued: Story = {
  args: {
    session: { ...SESSION, state: 'queued', created: at(2), updated: at(2), evidence: undefined },
    quest: null,
  },
};

/** Parked: the observation that explains why is on the entry, verbatim. */
export const Parked: Story = {
  args: {
    session: {
      ...SESSION,
      state: 'awaiting-person',
      created: at(52),
      updated: at(11),
      note: 'Two ways forward; I recommend capping on the chunk API, which is what the quest asks for.',
      evidence: undefined,
    },
    quest: { ...QUEST, status: 'Taken', updated: at(51) },
  },
};

/**
 * A long note folds to four lines, with *Show all* (UX6b, design §2.5): the install's side bar showed an ended session's
 * whole verify report, in italics, past the window's foot. Drawn at the side bar's usual width.
 */
export const ALongNoteFolds: Story = {
  args: {
    session: {
      ...SESSION,
      note: [
        'verify passed: typecheck clean across the CLI, the web and the desktop modules.',
        'cli: 1214 tests in 96 files, all passed, in 41 seconds.',
        'web: 3798 tests in 225 files, all passed, in 268 seconds.',
        'daoris check: the doctrine is current and the index is in step with the files.',
        'doc-budgets: two documents over their ceiling, reported and not enforced.',
        'doc-duplicates: nothing held twice. release-prep --check: every version reference agrees.',
        'devkit: the five universal gates are green.',
      ].join('\n'),
      evidence: undefined,
    },
  },
  decorators: [(Story) => <div className="w-[400px] p-3"><Story /></div>],
};

/** The same fold across a note's blocks: Daoris's line worded in the reader's language, then the agent's words set apart. */
export const ALongNoteOfSeveralBlocksFolds: Story = {
  args: {
    session: {
      ...SESSION,
      note: 'The quest reached done.',
      noteParts: [
        { code: 'ended.done', values: {}, text: 'The quest reached done.' },
        {
          words: [
            'Capped hydration at four chunks a frame and exposed the budget on the chunk API.',
            'The playtest streamed the whole world with no seams; the slowest frame took 14 ms.',
            'Two follow-ups for the game: read the budget from the level file, and show it in the debug overlay.',
            'Nothing else was touched.',
          ].join('\n'),
          by: 'agent',
        },
      ],
      evidence: undefined,
    },
  },
  decorators: [(Story) => <div className="w-[400px] p-3"><Story /></div>],
};

/** Work that produced nothing. The driver's sentence says so and the timeline does not soften it. */
export const NothingLanded: Story = {
  args: { session: { ...SESSION, evidence: 'no commits landed' }, quest: null },
};

/** Each kind on its own, which is where the marks and the wording are actually reviewed. */
export const EveryKind: StoryObj = {
  render: () => (
    <ol className="m-0 max-w-xl list-none p-0">
      <TimelineEntry event={{ kind: 'opened', at: at(95) }} />
      <TimelineEntry event={{ kind: 'quest', at: at(90), status: 'Taken' }} />
      <TimelineEntry
        event={{ kind: 'state', at: at(60), state: 'awaiting-person', note: 'two ways forward; I recommend the second.' }}
      />
      <TimelineEntry event={{ kind: 'quest', at: at(55), status: 'Declined', note: 'the chunk API is being replaced next quarter.' }} />
      <TimelineEntry event={{ kind: 'state', at: at(52), state: 'failed', note: 'the process exited 1 with the quest unexplained.' }} />
      <TimelineEntry
        event={{
          kind: 'evidence',
          at: at(50),
          text: 'commits landed:',
          commits: [
            { sha: 'c0ffee12', subject: 'cap hydration work per frame' },
            { sha: 'deadbee1', subject: '让区块 API 暴露流式预算，供上层调度器读取，并在超出时回退到上一帧的结果' },
          ],
        }}
      />
      <TimelineEntry event={{ kind: 'evidence', at: at(50), text: 'no commits readable', commits: [] }} />
    </ol>
  ),
};
