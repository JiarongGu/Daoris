import type { Convergence, Entry, Hit } from '../api';

// Search's and Convergence's fixtures (FRAME1f): hits, entries and findings shaped as the service answers them, for the
// stories and the molecules' suites. Every name is the example family's or invented; none is a person's repository.

export const HITS: Hit[] = [
  {
    id: 'game:.claude/knowledge/world-streaming.md', repository: 'game', kind: 'Knowledge', path: '.claude/knowledge/world-streaming.md',
    title: 'world-streaming', score: 3.1,
    excerpt: "The example game's own knowledge: chunk hydration runs neighbours-first, so a player turning the camera never sees an unhydrated seam.",
  },
  {
    id: 'engine:docs/FIX-LOG.md#A console that is not UTF-8', repository: 'engine', kind: 'Fix', path: 'docs/FIX-LOG.md',
    title: 'A console that is not UTF-8', score: 2.4,
    excerpt: 'Writing a file through the console mangled every non-ASCII character; set the encoding first, then write the file directly.',
  },
  {
    id: 'engine:docs/DECISIONS.md#D12', repository: 'engine', kind: 'Decision', path: 'docs/DECISIONS.md',
    title: 'D12 — streaming budget per frame', score: 1.2,
    excerpt: 'Streaming takes at most two milliseconds a frame, so hydration never costs a frame the player sees.',
  },
];

/** A hit in 中文: its title and excerpt are content, shown as they are. */
export const CJK_HIT: Hit = {
  id: '渲染管线:docs/决策.md#场景加载', repository: '渲染管线', kind: 'Decision', path: 'docs/决策.md',
  title: '场景加载的顺序', score: 2.2, excerpt: '场景加载先读邻近区块，再读远处区块，玩家转动视角时不会看到空缺。',
};

export const ENTRY: Entry = {
  id: 'game:.claude/knowledge/world-streaming.md', repository: 'game', kind: 'Knowledge', provenance: 'Local',
  title: 'world-streaming', path: '.claude/knowledge/world-streaming.md',
  body: [
    '---',
    'name: world-streaming',
    'applies_when: loading or unloading world chunks in the example game',
    'enforces: hydrate a chunk before its neighbours are visible; never evict the player\'s anchor chunk',
    '---',
    '',
    '# World streaming — chunk hydration order',
    '',
    "The example game's own knowledge: **chunk hydration** runs neighbours-first, so a player turning the",
    'camera never sees an unhydrated seam — and the anchor chunk, the one the player stands in, is never',
    'evicted, whatever the memory pressure says.',
  ].join('\n'),
};

/** A canonical rule, as every adopter holds it. */
export const RULE: Entry = {
  id: 'engine:AGENTS.md#reaching-in', repository: 'engine', kind: 'Rule', provenance: 'Canonical',
  title: 'reaching-in', path: 'AGENTS.md',
  body: '# Never write into another repository\n\n**Do not touch another repository — not its code, not its files, not its backlog.** Publish the request and let whoever works there take it.',
};

/** An entry whose source lines run past a hundred characters, as a decisions record's do. */
export const LONG_LINES: Entry = {
  ...ENTRY,
  id: 'engine:docs/DECISIONS.md#D12', repository: 'engine', kind: 'Decision', title: 'D12 — streaming budget per frame', path: 'docs/DECISIONS.md',
  body: '## D12 — streaming budget per frame\n\nStreaming takes at most two milliseconds a frame, measured on the slowest machine the game supports, so hydration never costs a frame the player sees; a chunk that would take longer is split across frames instead.',
};

/** An entry in 中文. */
export const CJK_ENTRY: Entry = {
  id: CJK_HIT.id, repository: CJK_HIT.repository, kind: 'Decision', provenance: 'Local', title: CJK_HIT.title, path: CJK_HIT.path,
  body: '## 场景加载的顺序\n\n场景加载先读邻近区块，再读远处区块，玩家转动视角时不会看到空缺。',
};

const entryOf = (entry: Entry) => ({ id: entry.id, repository: entry.repository, kind: entry.kind, path: entry.path, title: entry.title });

export const RESTATEMENT: Convergence = {
  method: 'Restatement', similarity: 0.912, repositories: ['engine', 'game'],
  suggestion: 'A copy that has drifted. Read both, and promote the one that is right with `daoris upstream <file>` where it lives.',
  entries: [
    { id: 'engine:.claude/rules/engine-mechanics.md', repository: 'engine', kind: 'Rule', path: '.claude/rules/engine-mechanics.md', title: 'engine-mechanics' },
    { id: 'game:.claude/rules/engine-mechanics.md', repository: 'game', kind: 'Rule', path: '.claude/rules/engine-mechanics.md', title: 'engine-mechanics' },
  ],
};

export const CONVERGENT: Convergence = {
  method: 'Convergent', similarity: 0.781, repositories: ['engine', 'game'],
  suggestion: 'Two repositories learned this separately. Read both; if they say one thing, promote it to the canon with `daoris upstream <file>`.',
  entries: [entryOf(LONG_LINES), entryOf(ENTRY)],
};

export const IDENTICAL: Convergence = {
  method: 'Identical', similarity: 1, repositories: ['engine', 'game', 'studio-tools'],
  suggestion: 'The same document in three places. Keep one, and have the others name it.',
  entries: [
    { id: 'engine:docs/streaming.md', repository: 'engine', kind: 'Knowledge', path: 'docs/streaming.md', title: 'streaming' },
    { id: 'game:docs/streaming.md', repository: 'game', kind: 'Knowledge', path: 'docs/streaming.md', title: 'streaming' },
    { id: 'studio-tools:docs/streaming.md', repository: 'studio-tools', kind: 'Knowledge', path: 'docs/streaming.md', title: 'streaming' },
  ],
};

export const CJK_FINDING: Convergence = {
  method: 'Convergent', similarity: 0.802, repositories: ['渲染管线', 'game'],
  suggestion: 'Two repositories learned this separately. Read both; if they say one thing, promote it to the canon with `daoris upstream <file>`.',
  entries: [entryOf(CJK_ENTRY), entryOf(ENTRY)],
};

export const FINDINGS: Convergence[] = [CONVERGENT, IDENTICAL, RESTATEMENT];

/** The bodies a finding's entries read whole, by id. */
export const BODIES: Record<string, Entry> = Object.fromEntries([
  ENTRY, LONG_LINES, CJK_ENTRY,
  { ...RULE, id: RESTATEMENT.entries[0]!.id, repository: 'engine', title: 'engine-mechanics', path: '.claude/rules/engine-mechanics.md', provenance: 'Local' },
  { ...RULE, id: RESTATEMENT.entries[1]!.id, repository: 'game', title: 'engine-mechanics', path: '.claude/rules/engine-mechanics.md', provenance: 'Local', body: `${RULE.body}\n\nThe game adds: a request names the evidence.` },
].map((entry) => [entry.id, entry]));
