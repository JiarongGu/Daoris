import type { Ask, Quest, Session } from '../data/api';
import type { SessionGrouping } from './groups';
import type { EntryPart, SessionEntry } from './entries';

// The install's evidence of 2026-10-14 as the page now reads it (D167, SESSONE1c), with neutral names: two asks, one of
// them over an app and its database, which the list showed as eleven records and which are three lines of work. Shared by
// the rail's tests, the strip's, the ask page's and the stories.

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const record = (id: string, repository: string, state: Session['state'], over: Partial<Session> = {}): Session => ({
  id, repository, state, adapter: 'claude-code', kind: 'driven', quest: null, created: at(300), updated: at(10), ...over,
});

/** The ask `c98a7e` (an app and its database repository) and the ask `841c38` (the app alone): eleven records. */
export const EVIDENCE_SESSIONS: Session[] = [
  // The intakes answered and ended: their asks became quests, so they are the asks' pages', not the list's.
  record('1a000001', 'daoris', 'completed', { kind: 'chat', ask: 'c98a7e', updated: at(280) }),
  record('1a000002', 'daoris', 'completed', { kind: 'chat', ask: '841c38', updated: at(200) }),
  // c98a7e in the app: a failed start, its retry, and a request the database repository sent back.
  record('ap000001', 'app', 'failed', { quest: 'q0000a', created: at(270), updated: at(260), tree: 'C:/work/trees/app/t-one' }),
  record('ap000002', 'app', 'completed', { quest: 'q0000a', created: at(255), updated: at(180), tree: 'C:/work/trees/app/t-one' }),
  record('ap000003', 'app', 'working', { quest: 'q0000c', created: at(60), updated: at(2), tree: 'C:/work/trees/app/t-two' }),
  // c98a7e in the database repository: its work, and the app's request.
  record('db000001', 'db', 'completed', { quest: 'q0000b', created: at(250), updated: at(120), tree: 'C:/work/trees/db/t-one' }),
  record('db000002', 'db', 'completed', { quest: 'q0000d', created: at(110), updated: at(40) }),
  record('db000003', 'db', 'working', { quest: 'q0000d', created: at(35), updated: at(5) }),
  // 841c38 in the app: one line of work in three records.
  record('ap000011', 'app', 'stopped', { quest: 'q0000e', created: at(190), updated: at(170) }),
  record('ap000012', 'app', 'failed', { quest: 'q0000e', created: at(165), updated: at(150) }),
  record('ap000013', 'app', 'completed', { quest: 'q0000e', created: at(140), updated: at(100) }),
];

const part = (session: string, quest: string | null, shown: string, group: EntryPart['group'], began: string, over: Partial<EntryPart> = {}): EntryPart => ({
  session, quest, shown, group, began, after: null, askedBy: null, ...over,
});

/** The driver's entries over `EVIDENCE_SESSIONS`, as `SESSION_GROUPS` answers them, in the reader's group order. */
export const EVIDENCE_ENTRIES: SessionEntry[] = [
  {
    entry: 'ask:c98a7e/app', origin: 'ask:c98a7e', repository: 'app', group: 'working', leading: 'ap000003', head: 'ap000002', opens: 'ap000003',
    parts: [
      part('ap000001', 'q0000a', 'failed', 'ended', 'opened'),
      part('ap000002', 'q0000a', 'completed', 'ended', 'retried', { after: 'ap000001' }),
      part('ap000003', 'q0000c', 'working', 'working', 'asked', { askedBy: 'db' }),
    ],
  },
  {
    entry: 'ask:c98a7e/db', origin: 'ask:c98a7e', repository: 'db', group: 'working', leading: 'db000003', head: 'db000003', opens: 'db000003',
    parts: [
      part('db000001', 'q0000b', 'completed', 'ended', 'opened'),
      part('db000002', 'q0000d', 'completed', 'ended', 'asked', { askedBy: 'app' }),
      part('db000003', 'q0000d', 'working', 'working', 'resumed', { after: 'db000002' }),
    ],
  },
  {
    entry: 'ask:841c38/app', origin: 'ask:841c38', repository: 'app', group: 'ended', leading: 'ap000013', head: 'ap000013', opens: 'ap000013',
    parts: [
      part('ap000011', 'q0000e', 'stopped', 'ended', 'opened'),
      part('ap000012', 'q0000e', 'failed', 'ended', 'carried-on', { after: 'ap000011' }),
      part('ap000013', 'q0000e', 'completed', 'ended', 'retried', { after: 'ap000012' }),
    ],
  },
];

const row = (session: string, group: SessionGrouping['group'], shown: string): SessionGrouping => ({
  session, group, shown, archived: false, teammate: false,
});

/** The rows `SESSION_GROUPS` answers beside the entries: every record, in the reader's order. */
export const EVIDENCE_GROUPINGS: SessionGrouping[] = [
  row('ap000003', 'working', 'working'), row('db000003', 'working', 'working'),
  row('1a000002', 'ended', 'completed'), row('1a000001', 'ended', 'completed'), row('ap000013', 'ended', 'completed'),
  row('ap000012', 'ended', 'failed'), row('ap000011', 'ended', 'stopped'), row('ap000002', 'ended', 'completed'),
  row('ap000001', 'ended', 'failed'), row('db000002', 'ended', 'completed'), row('db000001', 'ended', 'completed'),
];

/** The asks' names, by id, as the page's `askName` gives them. */
export const EVIDENCE_ASKS: Record<string, string> = {
  c98a7e: 'Add the export feed to the app, backed by the database',
  '841c38': 'Tidy the settings page',
};

const quest = (id: string, from: string, to: string, title: string): Quest => ({
  id, from, to, title, body: title, status: 'Taken', filed: at(300), updated: at(20),
});

/** The quests the evidence's parts served, so a row's name is the quest's where no ask names it. */
export const EVIDENCE_QUESTS: Quest[] = [
  quest('q0000a', 'daoris', 'app', 'Build the export feed screen'),
  quest('q0000b', 'daoris', 'db', 'Add the export table'),
  quest('q0000c', 'db', 'app', 'Read the export table from the app'),
  quest('q0000d', 'app', 'db', 'Index the export table by date'),
  quest('q0000e', 'daoris', 'app', 'Fix the settings page layout'),
];

/** The asks as the service lists them, naming what `EVIDENCE_ASKS` says. */
export const EVIDENCE_ASK_RECORDS: Ask[] = [
  {
    id: 'c98a7e', workspace: 'default', sentence: EVIDENCE_ASKS.c98a7e!, state: 'Published', tier: 'declarations',
    asked: at(290), updated: at(20), links: [], attachments: [], proposal: [], quests: ['q0000a', 'q0000b'],
  },
  {
    id: '841c38', workspace: 'default', sentence: EVIDENCE_ASKS['841c38']!, state: 'Published', tier: 'declarations',
    asked: at(210), updated: at(100), links: [], attachments: [], proposal: [], quests: ['q0000e'],
  },
];
