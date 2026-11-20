import type { Turn } from '../conversation/conversationTypes';
import type { ExchangeCard } from '../conversation/exchanges';
import type { ChannelItem } from '../channel';
import type { AtWorkExchange } from '../atWork/types';
import { lineRowsOf } from '../exchanges/rows';
import type { StepRow, Track } from '../track/passage';
import type { StoryLine } from './story';
import type { ChangesFacts } from './ChangesRail';
import type { HeadFacts } from './PanelHead';
import type { WaitCardFacts } from './WaitCard';

// One line of work as the session panel reads it: a rate limit built, reviewed and checked, on a public stand-in repository.
// Its stories and tests share it, so each part is drawn over the same facts the order test reads.

export const at = (hour: number, minute: number, second = 0) =>
  `2026-11-20T${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}:${String(second).padStart(2, '0')}Z`;
const ms = (iso: string) => Date.parse(iso);

const TURNS: Turn[] = [
  {
    key: 't1',
    ask: { key: 't1', text: 'Rate-limit /v1 per key: 100 a minute, burst 20; 429 with Retry-After.', origin: 'person', at: at(9, 12, 5) },
    ended: at(9, 16),
    items: [
      { key: 'b1', kind: 'tool', at: at(9, 13), title: 'Read src/api/middleware.ts', toolKind: 'read', status: 'completed' },
      { key: 'b2', kind: 'tool', at: at(9, 13, 30), title: 'Read src/infra/config.ts', toolKind: 'read', status: 'completed' },
      { key: 'b3', kind: 'tool', at: at(9, 14), title: 'Edit src/limits/bucket.ts', toolKind: 'edit', status: 'completed', locations: ['src/limits/bucket.ts'] },
      { key: 'b4', kind: 'tool', at: at(9, 15), title: 'npm test', toolKind: 'execute', status: 'failed' },
      { key: 'b5', kind: 'tool', at: at(9, 15, 30), title: 'helper_ask', toolKind: 'other', status: 'completed' },
    ],
  },
  {
    key: 't2',
    ask: { key: 't2', text: 'src/infra/redis.ts, pooled; reuse it', origin: 'target', at: at(9, 18), from: 'helper', helper: 'h1' },
    ended: at(9, 21),
    items: [
      { key: 'b6', kind: 'message', at: at(9, 20), text: 'Token bucket in Redis, refilled atomically; middleware on /v1.' },
    ],
  },
  {
    key: 't3',
    ask: { key: 't3', text: 'Agreed on a burst of 20; the refill must stay atomic.', origin: 'target', at: at(9, 40), opinion: 'op1' },
    ended: at(9, 42),
    items: [
      { key: 'b7', kind: 'message', at: at(9, 41), text: 'Kept the burst at 20; the refill runs in one Lua script.' },
    ],
  },
];

/**
 * A turn that ran work beside itself: a thought and a plan, a call that started a subagent, a read, a call that started a background
 * task, and that task's end, each leading to the other.
 */
export const BESIDE_TURN: Turn = {
  key: 't9',
  ask: { key: 't9', text: 'Check the limits across the services.', origin: 'person', at: at(10, 0) },
  items: [
    { key: 'c1', kind: 'thought', at: at(10, 0, 10), text: 'Each service keeps its own limiter.' },
    { key: 'c1p', kind: 'plan', at: at(10, 0, 15), entries: [{ content: 'Survey the services', status: 'in_progress' }] },
    {
      key: 'c2', kind: 'tool', at: at(10, 1), title: 'Task', toolKind: 'other', status: 'completed',
      beside: { kind: 'child', id: 'sub-1', name: 'limit survey', task: 'List every limiter', started: at(10, 1), ended: at(10, 4), end: 'ok' },
    },
    { key: 'c3', kind: 'tool', at: at(10, 5), title: 'Read src/limits/bucket.ts', toolKind: 'read', status: 'completed' },
    {
      key: 'c4', kind: 'tool', at: at(10, 6), title: 'npm run soak', toolKind: 'execute', status: 'completed',
      beside: { kind: 'task', id: 'task-1', name: 'soak test', started: at(10, 6), endKey: 'c5' },
    },
    {
      key: 'c5', kind: 'beside', at: at(10, 9),
      beside: { kind: 'task', id: 'task-1', name: 'soak test', started: at(10, 6), ended: at(10, 9), end: 'ok', startKey: 'c4' },
    },
  ],
};

const STEPS: StepRow[] = [
  { key: 'r1/build/start/1', run: 'r1', node: 'build', edge: 'start', attempt: 1, mark: 'current', at: ms(at(9, 12)), order: 0 },
  { key: 'r1/build/end/1', run: 'r1', node: 'build', edge: 'end', attempt: 1, mark: 'passed', at: ms(at(9, 30)), order: 0 },
  { key: 'r1/review/start/1', run: 'r1', node: 'review', edge: 'start', attempt: 1, mark: 'current', at: ms(at(9, 30)), order: 1 },
];

const EXCHANGES: ExchangeCard[] = [
  {
    id: 'helper:h1', ask: 'h1', call: 'b5', question: 'Where is the Redis client?', agent: 'knowledge helper', state: 'answered', at: at(9, 15, 30),
    helper: 'h1s', answer: { key: 't2', text: 'src/infra/redis.ts, pooled; reuse it', at: at(9, 18) },
  },
];

const CHANNEL: ChannelItem[] = [
  { kind: 'message', id: 'm1', at: at(9, 25), repository: 'web-app', agent: 'claude-code', text: 'Can the limits header carry the remaining quota?' },
];

/** A piece of the line's work asked of a partner during the review, not yet taken up. */
export const PIECE: AtWorkExchange = {
  id: 'piece:p1', part: 's1', expects: 'piece', to: 'gemini', delivery: 'kept', outcome: 'asked', holds: true, at: at(9, 32),
};

/**
 * The line the order test reads: two turns and a helper's answer, a step's start, end and the next one's start, a channel message and
 * a piece asked of a partner, each of the last two a row of its line.
 */
export const LINE: StoryLine = {
  turns: TURNS, steps: STEPS, exchanges: EXCHANGES, rows: lineRowsOf({ messages: CHANNEL, exchanges: [PIECE] }),
  opinionOf: () => ({ reviewer: 'Codex', model: 'gpt-5.1' }),
};

export const TRACK: Track = {
  run: 'r1', workflow: 'build-review-check', at: 3, of: 5,
  chips: [
    { node: 'build', mark: 'passed', role: 'builder', attempt: null, wait: null },
    { node: 'review', mark: 'passed', role: 'reviewer', attempt: null, wait: null },
    { node: 'check', mark: 'current', role: 'checker', attempt: 2, wait: null },
    { node: 'approve', mark: 'next', role: 'release manager', attempt: null, wait: null },
    { node: 'release', mark: 'next', role: null, attempt: null, wait: null },
  ],
};

export const HEAD: HeadFacts = {
  state: 'live', since: at(9, 12), doing: 'running the e2e suite',
  title: 'token-bucket limits', ask: 'Rate-limit the public API',
  repository: 'api-server', branch: 'daoris/q41', role: 'Builder', maker: 'Claude', model: 'opus-4.1', asked: null, account: 'home',
};

export const CHANGES: ChangesFacts = {
  files: [
    { path: 'src/limits/bucket.ts', change: 'M', added: 121, removed: 0 },
    { path: 'src/limits/refill.lua', change: 'A', added: 38, removed: 0 },
    { path: 'src/api/middleware.ts', change: 'M', added: 44, removed: 29 },
    { path: 'test/limits.e2e.ts', change: 'A', added: 45, removed: 0 },
    { path: 'docs/limits.md', change: 'M', added: 0, removed: 8 },
  ],
  commits: 3, pullRequest: null, checks: { passed: 46, of: 46, at: at(13, 40) },
};

export const RELEASE_WAIT: WaitCardFacts = {
  id: 'w1', title: 'Release 2.4 to beta?', kind: 'release', since: at(12, 18), holds: 2,
  what: 'Ships the limits and the remaining-quota header to beta. Both passed their checks together.',
  yes: { label: 'Approve release to beta', effect: 'Deploys to beta now; one press rolls it back for 24 hours.' },
  no: { label: 'Hold', effect: 'Stays on stage; the rest goes on.' },
  look: [{ label: 'release notes', href: '#notes' }, { label: 'run #418', href: '#run' }],
  known: ['checks 46/46 at 13:40', 'reviewed by Codex · gpt-5.1'],
  from: { role: 'release manager', agent: 'Claude', model: 'sonnet-4.5', at: at(12, 18) },
  meanwhile: 'infra waits; the docs go on',
};

/** A wait whose asker said neither what no does nor how it is known: the card says so rather than leave a blank. */
export const BARE_WAIT: WaitCardFacts = {
  id: 'w2', title: 'Which header name?', kind: 'question', since: at(10, 2),
  what: 'The draft names it RateLimit-Remaining; the old clients read X-RateLimit-Remaining.',
  options: [
    { label: 'RateLimit-Remaining', effect: 'Follows the draft; old clients read nothing.' },
    { label: 'Both', effect: 'Sends both headers for one release, then drops the old one.' },
  ],
  look: [], known: [],
  from: { role: 'builder', agent: 'Claude', model: 'opus-4.1', at: at(10, 2) },
};
