import type { Ask } from '../api';

// Asks in each state a real machine reaches, for the stories and the molecule tests alike — one set, so
// what a reviewer designs against is what the tests assert against.

const hoursAgo = (count: number) => new Date(Date.now() - count * 3_600_000).toISOString();

export const PROPOSED: Ask = {
  id: '7c1e9a04b2d5', workspace: 'aurora', state: 'Proposed', tier: 'declarations',
  sentence: 'The chunk streamer stalls on a cold cache — cap its hydration per frame.\n\n'
    + 'Seen on the test rig after a fresh install; the trace is attached.',
  asked: hoursAgo(3), updated: hoursAgo(3),
  links: ['https://tickets.example/T-42'],
  attachments: [{ name: 'trace.log', sha256: 'a'.repeat(64), bytes: 2_300, path: 'C:/somewhere/private/asks/trace.log' }],
  proposal: [
    { repository: 'engine', score: 5, matched: ['chunk', 'stream', 'frame'] },
    { repository: 'game', score: 2, matched: ['cache'] },
  ],
  quests: [],
};

/** Nothing in the circle's declarations shares its words — the tier says so, and proposes nobody. */
export const UNMATCHED: Ask = {
  ...PROPOSED, id: '0b9f3c21aa77', sentence: 'Tidy the release notes.', proposal: [], links: [], attachments: [],
};

/** Published by a person accepting a proposal, and then to a second repository. */
export const PUBLISHED: Ask = {
  ...PROPOSED, id: '5d2e8f13c0a1', state: 'Published', quests: ['9a8b7c6d5e4f', '1f2e3d4c5b6a'], updated: hoursAgo(1),
};

/** The asker named the receiver, which published at once — no tier had to decide. */
export const NAMED: Ask = { ...PUBLISHED, id: 'e4d3c2b1a0f9', tier: 'named', quests: ['9a8b7c6d5e4f'] };

/** A named receiver that could not be asked: kept, with the service's sentence and its proposal. */
export const REFUSED: Ask = {
  ...PROPOSED, id: 'c3b2a1908f7e',
  note: '`newcomer` is not an adopted repository in `aurora` — only an adopter can be asked.',
};

export const CLOSED: Ask = {
  ...PROPOSED, id: '2a3b4c5d6e7f', state: 'Closed', note: 'Answered in the design review instead.', updated: hoursAgo(1),
};

/** A tier this page has no word for (INT4b's session, or anything later) — shown as the service wrote it. */
export const UNKNOWN_TIER: Ask = { ...PUBLISHED, id: 'f0e1d2c3b4a5', tier: 'intake-session' };

/** A CJK sentence, long, so the card's one line and the record's title are both tried. */
export const LONG_CJK: Ask = {
  ...PROPOSED, id: '8e7d6c5b4a39', workspace: '工作区',
  sentence: '流式加载在冷缓存时卡顿，需要按帧限制加载量，并在测试机上复现后附上完整的跟踪日志与截图，以便引擎仓库的代理判断是否要改动分块接口。',
};
