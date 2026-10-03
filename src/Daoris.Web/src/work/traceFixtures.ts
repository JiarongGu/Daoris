import { CLAIMED } from './handedFixtures';
import type { TraceAnswer, TraceAskLink, TraceChain, TraceQuestLink, TraceSessionLink } from './trace';

// How a session or a quest came to be, as the driver's `TRACE` route answers it (TRACE1b): the shapes its route test holds,
// shared by the molecule's stories and its tests. Ids, repositories, branches and the person's words are as a record keeps
// them; every line Daoris says is a code.

/** The ask: the person's words, each with how and when it was given, and a go-ahead approved and one still waiting. */
export const ASK: TraceAskLink = {
  id: 'a1', source: 'asks', namedBy: { kind: 'quest', id: 'q1' }, workspace: 'work', state: 'Published', tier: 'intake',
  intake: { session: 'i1', state: { state: 'completed' }, agent: { adapter: 'claude-code', harness: '2.1.4', account: 'work' } },
  quests: ['q1'],
  words: [
    { kind: 'asked', text: 'The dashboard figure reads zero. Use the v3 bridge, as common-report does.', at: '2026-10-03T09:00:00Z' },
    { kind: 'answered', text: 'common-report, not a report type of its own', at: '2026-10-03T09:20:00Z', session: 's1', quest: 'q1' },
    { kind: 'added', text: 'also the tile on the second page', at: '2026-10-03T09:45:00Z', session: 's2', quest: 'q1' },
  ],
  goAheads: [
    {
      number: 1, kind: 'write', on: 'production', act: 'dashboard configuration',
      answer: { approved: true, words: 'run the put', at: '2026-10-03T09:30:00Z' }, firstAskedBy: 's1', firstAskedAt: '2026-10-03T09:10:00Z',
    },
    { number: 2, kind: 'push', on: 'origin', act: 'main', firstAskedBy: 's2', firstAskedAt: '2026-10-03T09:50:00Z' },
  ],
};

/** The quest: its requirement in the person's words with its check, met; published by the intake. */
export const QUEST: TraceQuestLink = {
  id: 'q1', source: 'quests', namedBy: { kind: 'session', id: 's1' }, address: 'dashboards', title: 'Fix the dashboard figure',
  status: 'Done', filed: '2026-10-03T09:02:00Z', moved: '2026-10-03T10:00:00Z', ask: 'a1', from: 'ask #a1', publishedBy: 'i1',
  note: 'the figure reads through v3',
  requirements: [
    { number: 1, quote: 'use the v3 bridge', check: 'the report reads through bridge v3', answer: 'met', met: 'the tile reads through v3 now' },
    { number: 2, quote: 'as common-report does', check: 'the same module', answer: 'departed', departed: 'the bridge has no tile feed', on: 'as common-report does' },
  ],
  then: [],
  records: [{ id: 's1', state: 'failed' }, { id: 's2', state: 'completed' }],
};

/** The first session: cut off by its account's limit, before the standing answer was set and before the go-ahead's answer. */
export const FIRST: TraceSessionLink = {
  id: 's1', source: 'sessions', kind: 'driven', state: { state: 'failed', limit: true },
  opened: '2026-10-03T09:05:00Z', moved: '2026-10-03T09:15:00Z',
  agent: { adapter: 'claude-code', harness: '2.1.3', account: 'personal' },
  tree: 'dashboards-q1', baseCommit: 'abc1234def567890', quest: 'q1', before: { first: true },
  evidence: { said: 'no commits landed', lines: [], commits: [] },
  events: {
    source: 'events', starts: ['opened on `personal`: the list begins there.'],
    instructions: [{ seq: 2, chars: 1234 }], accepted: [],
  },
  rules: { source: 'rules', missing: 'gone-with-run', allowed: 0, asked: 0, denied: 0, hard: 0, guarded: false },
  landing: { source: 'landings', missing: 'none', branches: [] },
  stood: {
    standing: { repository: 'dashboards', state: 'after', source: 'config', at: '2026-10-03T09:25:00Z' },
    goAheads: [{ number: 1, state: 'asked-after', mine: true }, { number: 2, state: 'asked-after' }],
    words: { ask: 'a1', before: 1, of: 3 },
  },
};

/** The carry-on: another account in the same tree, which closed it done; its work landed on a branch a plugin pushed. */
export const SECOND: TraceSessionLink = {
  id: 's2', source: 'sessions', kind: 'driven', state: { state: 'completed' },
  opened: '2026-10-03T09:40:00Z', moved: '2026-10-03T10:00:00Z',
  agent: { adapter: 'claude-code', harness: '2.1.4', account: 'work' },
  tree: 'dashboards-q1', baseCommit: 'abc1234def567890', quest: 'q1',
  before: { first: false, session: 's1', state: 'failed', ended: true, at: '2026-10-03T09:15:00Z', tree: 'same' },
  took: true,
  evidence: {
    said: 'commits landed:', lines: ['1a2b3c4 Read the figure through v3', '9f8e7d6 Test the tile'],
    commits: [{ sha: '1a2b3c4', line: '1a2b3c4 Read the figure through v3' }, { sha: '9f8e7d6', line: '9f8e7d6 Test the tile' }],
  },
  events: {
    source: 'events',
    starts: ['carried on from session `s1` on `work`; `personal` is cooling.'],
    instructions: [{ seq: 2, chars: 6786, account: CLAIMED }],
    accepted: [],
  },
  rules: { source: 'rules', missing: 'gone-with-run', allowed: 0, asked: 0, denied: 0, hard: 0, guarded: false },
  landing: {
    source: 'landings',
    branches: [{
      branch: 'feature/q1-fix-the-dashboard-figure', tip: '1a2b3c4d5e6f708192a3b4c5d6e7f80910111213', at: '2026-10-03T10:05:00Z',
      line: 'main', from: 'abc1234def567890', plugin: 'github', pushed: true, pullRequest: 'https://example.test/pull/7',
    }],
  },
  stood: {
    standing: { repository: 'dashboards', state: 'before', source: 'config', at: '2026-10-03T09:25:00Z', says: 'test on dev first, never production' },
    goAheads: [{ number: 1, state: 'approved-before', at: '2026-10-03T09:30:00Z' }, { number: 2, state: 'asked-after', mine: true }],
    words: { ask: 'a1', before: 2, of: 3 },
  },
};

/** From a quest: its ask, the quest, both its sessions oldest first. */
export const QUEST_CHAIN: TraceChain = {
  kind: 'quest', id: 'q1', unread: [],
  links: [
    { kind: 'ask', ask: ASK },
    { kind: 'quest', quest: QUEST },
    { kind: 'session', session: FIRST },
    { kind: 'session', session: SECOND },
  ],
};

/** From a session: its ask, its quest, and that session alone. */
export const SESSION_CHAIN: TraceChain = {
  kind: 'session', id: 's2', unread: [],
  links: [{ kind: 'ask', ask: ASK }, { kind: 'quest', quest: QUEST }, { kind: 'session', session: SECOND }],
};

/**
 * Links nothing keeps, each said where it would have been: the ask gone from the service, no record of the session on this
 * machine, its rules gone with its run, no landing it could say, no harness or account on its record, no tree, and no moment
 * it opened.
 */
export const MISSING_CHAIN: TraceChain = {
  kind: 'session', id: 's3', unread: [],
  links: [
    { kind: 'ask', ask: { id: 'a9', source: 'asks', missing: 'not-found', namedBy: { kind: 'quest', id: 'q3' }, quests: [] } },
    {
      kind: 'quest',
      quest: {
        id: 'q3', source: 'quests', namedBy: { kind: 'session', id: 's3' }, address: 'dashboards', title: 'Tidy the legend',
        status: 'Taken', filed: '2026-10-03T08:00:00Z', moved: '2026-10-03T08:10:00Z', ask: 'a9', from: 'ask #a9',
        requirements: [{ number: 1, quote: 'keep the colours', check: 'the palette is unchanged', answer: 'not-yet' }],
        then: [], records: [{ id: 's3', state: 'stopped' }],
      },
    },
    {
      kind: 'session',
      session: {
        id: 's3', source: 'sessions', kind: 'driven', state: { state: 'stopped', interrupted: true }, moved: '2026-10-03T08:30:00Z',
        agent: { adapter: 'claude-code' }, quest: 'q3',
        events: { source: 'events', missing: 'none-here', starts: [], instructions: [], accepted: [] },
        rules: { source: 'rules', missing: 'gone-with-run', allowed: 0, asked: 0, denied: 0, hard: 0, guarded: false },
        landing: { source: 'landings', missing: 'unknown', branches: [] },
        stood: { missing: 'no-moment', goAheads: [] },
      },
    },
  ],
};

/** A quest no ask asked, a running session's rules read from the file it was handed, and its work not landed yet. */
export const NO_ASK_CHAIN: TraceChain = {
  kind: 'quest', id: 'q5', unread: [],
  links: [
    {
      kind: 'quest',
      quest: {
        id: 'q5', source: 'quests', namedBy: { kind: 'session', id: 's5' }, address: 'reports:api', title: 'Expose the figure',
        status: 'Taken', filed: '2026-10-03T11:00:00Z', moved: '2026-10-03T11:05:00Z', from: 'dashboards', awaits: 'q6',
        requirements: [], then: [{ to: 'docs', title: 'Say how the figure is read' }], records: [{ id: 's5', state: 'working' }],
      },
    },
    {
      kind: 'session',
      session: {
        id: 's5', source: 'sessions', kind: 'driven', state: { state: 'working' }, opened: '2026-10-03T11:05:00Z',
        moved: '2026-10-03T11:06:00Z', agent: { adapter: 'codex', harness: '0.48.0' }, tree: 'reports-q5', baseCommit: 'feedface',
        quest: 'q5', before: { first: true },
        events: { source: 'events', starts: [], instructions: [{ seq: 1, chars: 70000, kept: 65536, account: null }], accepted: [] },
        rules: { source: 'rules', allowed: 14, asked: 0, denied: 4, hard: 1, guarded: true },
        landing: { source: 'landings', missing: 'unknown', branches: [] },
        stood: { standing: { repository: 'reports', state: 'none', source: 'config' }, goAheads: [] },
      },
    },
  ],
};

/** An ask from a host that kept no words, a merge into the line kept only as the person's acceptance, and a teammate's record. */
export const OLD_CHAIN: TraceChain = {
  kind: 'quest', id: 'q7', unread: [],
  links: [
    {
      kind: 'ask',
      ask: {
        id: 'a7', source: 'asks', namedBy: { kind: 'quest', id: 'q7' }, workspace: 'work', state: 'Closed', tier: 'named', quests: ['q7'],
        sentence: 'Rename the export button to "Download".', words: null, goAheads: null, note: 'done in one quest',
      },
    },
    {
      kind: 'quest',
      quest: {
        id: 'q7', source: 'quests', namedBy: { kind: 'session', id: 's7' }, address: 'web', title: 'Rename the export button',
        status: 'Done', filed: '2026-10-02T09:00:00Z', moved: '2026-10-02T10:00:00Z', ask: 'a7', from: 'ask #a7',
        requirements: [], then: [], records: [{ id: 's7', state: 'completed' }, { id: 'laptop/s8', state: 'completed' }],
      },
    },
    {
      kind: 'session',
      session: {
        id: 's7', source: 'sessions', kind: 'driven', state: { state: 'completed' }, opened: '2026-10-02T09:10:00Z',
        moved: '2026-10-02T09:40:00Z', agent: { adapter: 'claude-code', harness: '2.1.0' }, tree: 'web-q7', baseCommit: '0badc0de',
        quest: 'q7', before: { first: true },
        evidence: { said: 'commits landed:', lines: ['7c7c7c7 Rename the button'], commits: [{ sha: '7c7c7c7', line: '7c7c7c7 Rename the button' }] },
        events: {
          source: 'events', starts: [], instructions: [{ seq: 1, chars: 900, account: null }],
          accepted: [{ at: '2026-10-02T09:50:00Z', said: 'the person accepted this work: merged `daoris/s7` into `main` — 1 commit(s).' }],
        },
        rules: { source: 'rules', missing: 'gone-with-run', allowed: 0, asked: 0, denied: 0, hard: 0, guarded: false },
        landing: { source: 'landings', missing: 'merge-accepted', branches: [] },
        stood: { standing: { repository: 'web', state: 'moment-unknown', source: 'config' }, goAheads: [] },
      },
    },
    {
      kind: 'session',
      session: {
        id: 'laptop/s8', source: 'sessions', kind: 'driven', state: { state: 'completed' }, opened: '2026-10-02T09:45:00Z',
        moved: '2026-10-02T09:55:00Z', agent: { adapter: 'claude-code', harness: '2.1.0', teammate: true }, teammate: true,
        quest: 'q7', before: { first: false, session: 's7', state: 'completed', ended: true, at: '2026-10-02T09:40:00Z', tree: 'unknown' },
        stood: { goAheads: [] },
      },
    },
  ],
};

/** A store that did not answer: the session records, so the quest names none and no session is read. */
export const UNREAD_ANSWER: TraceAnswer = {
  chain: {
    kind: 'quest', id: 'q1', unread: [{ store: 'sessions' }],
    links: [{ kind: 'ask', ask: ASK }, { kind: 'quest', quest: { ...QUEST, records: null } }],
  },
  unread: [{ store: 'sessions' }],
};

export const answer = (chain: TraceChain): TraceAnswer => ({ chain, unread: chain.unread });

/** Nothing on the record names it, and every store answered. */
export const NOTHING: TraceAnswer = { chain: null, unread: [] };
