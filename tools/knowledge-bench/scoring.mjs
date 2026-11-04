/** The corpus document a path names, in either layout; null for anything else. */
export function docNameOf(path) {
  const p = String(path ?? '').replace(/\\/g, '/');
  return /(?:^|\/)knowledge\/([^/]+)\.md$/i.exec(p)?.[1]
    ?? /(?:^|\/)\.claude\/skills\/([^/]+)\/SKILL\.md$/i.exec(p)?.[1]
    ?? null;
}

const INDEX_FILES = /(?:^|\/)(AGENTS|CLAUDE|INDEX)\.md$/i;

/** The top-level tool calls, in order; a subagent's (with a parent) are not the session's own. */
export function toolCalls(events) {
  const calls = [];
  for (const e of events) {
    if (e.type !== 'assistant' || e.parent_tool_use_id) continue;
    for (const block of e.message?.content ?? []) {
      if (block.type === 'tool_use') calls.push({ n: calls.length + 1, name: block.name, input: block.input ?? {} });
    }
  }
  return calls;
}

/**
 * The corpus document a call aims at: what it reads, or the one file or skill folder a search is confined to. A
 * session that searches inside the right document has already chosen it, which is the discovery being measured.
 */
export function targetOf(call) {
  if (call.name === 'Skill') return call.input.skill ?? call.input.name ?? null;
  if (call.name === 'Glob') {
    // A pattern that spells one document's file or skill folder out, with no wildcard in the name.
    const p = String(call.input.pattern ?? '').replace(/\\/g, '/');
    return /(?:^|\/)knowledge\/([^/*?{}[\]]+)\.md$/.exec(p)?.[1] ?? /(?:^|\/)\.claude\/skills\/([^/*?{}[\]]+)(?:\/|$)/.exec(p)?.[1] ?? null;
  }
  const path = call.name === 'Read' ? (call.input.file_path ?? call.input.path) : call.name === 'Grep' ? call.input.path : null;
  if (!path) return null;
  const p = String(path).replace(/\\/g, '/');
  return docNameOf(p) ?? /(?:^|\/)\.claude\/skills\/([^/]+)\/?$/.exec(p)?.[1] ?? null;
}

/** What a call read: a corpus document's name, an index file, or nothing. */
function readOf(call) {
  if (call.name === 'Read') {
    const path = call.input.file_path ?? call.input.path ?? '';
    return { doc: docNameOf(path), index: INDEX_FILES.test(String(path).replace(/\\/g, '/')) };
  }
  if (call.name === 'Skill') return { doc: call.input.skill ?? call.input.name ?? null, index: false };
  return { doc: null, index: false };
}

const SEARCHES = new Set(['Grep', 'Glob']);

/** One run's measures, from its stream. */
export function scoreRun(events, truth) {
  const calls = toolCalls(events);
  const init = events.find((e) => e.type === 'system' && e.subtype === 'init') ?? {};
  const result = events.find((e) => e.type === 'result') ?? {};
  const usage = result.usage ?? {};
  const truths = new Set(truth);
  let hitAt = null;
  const wrong = [];
  const truthRead = [];
  let indexReads = 0;
  for (const call of calls) {
    const { doc, index } = readOf(call);
    if (index) indexReads += 1;
    if (!doc) continue;
    if (truths.has(doc)) {
      if (hitAt === null) hitAt = call.n;
      if (!truthRead.includes(doc)) truthRead.push(doc);
    } else if (!wrong.includes(doc)) {
      wrong.push(doc);
    }
  }
  const before = hitAt === null ? calls : calls.slice(0, hitAt - 1);
  const wrongBeforeHit = [...new Set(before.map((c) => readOf(c).doc).filter((d) => d && !truths.has(d)))];
  const targetedAt = calls.find((c) => truths.has(targetOf(c)))?.n ?? null;
  return {
    hit: hitAt !== null,
    hitAt,
    targetedAt,
    callsBeforeHit: hitAt === null ? null : hitAt - 1,
    searchesBeforeHit: hitAt === null ? null : before.filter((c) => SEARCHES.has(c.name)).length,
    truthRead,
    wrongRead: wrong,
    wrongBeforeHit,
    indexReads,
    calls: calls.length,
    searches: calls.filter((c) => SEARCHES.has(c.name)).length,
    searchTerms: calls.filter((c) => SEARCHES.has(c.name) || c.name === 'Skill')
      .map((c) => `${c.name}: ${c.input.pattern ?? c.input.skill ?? ''}`),
    turns: result.num_turns ?? null,
    inputTokens: usage.input_tokens ?? 0,
    outputTokens: usage.output_tokens ?? 0,
    cacheRead: usage.cache_read_input_tokens ?? 0,
    cacheCreation: usage.cache_creation_input_tokens ?? 0,
    costUsd: result.total_cost_usd ?? null,
    durationMs: result.duration_ms ?? null,
    subtype: result.subtype ?? null,
    isError: result.is_error ?? null,
    model: init.model ?? null,
    harness: init.claude_code_version ?? null,
    answer: typeof result.result === 'string' ? result.result : null,
  };
}

const median = (xs) => {
  const s = xs.filter((x) => x !== null && x !== undefined).sort((a, b) => a - b);
  if (!s.length) return null;
  const mid = Math.floor(s.length / 2);
  return s.length % 2 ? s[mid] : (s[mid - 1] + s[mid]) / 2;
};

/** Per design: runs, hits, hit rate, median calls to the hit, tokens per hit, wrong reads, wall time, cost. */
export function aggregate(runs) {
  const designs = [...new Set(runs.map((r) => r.design))];
  return designs.map((design) => {
    const mine = runs.filter((r) => r.design === design);
    const hits = mine.filter((r) => r.hit);
    const tokens = mine.reduce((n, r) => n + r.inputTokens + r.outputTokens + r.cacheRead + r.cacheCreation, 0);
    return {
      design,
      runs: mine.length,
      hits: hits.length,
      hitRate: mine.length ? hits.length / mine.length : 0,
      medianCallsToHit: median(hits.map((r) => r.hitAt)),
      medianCallsToTarget: median(mine.map((r) => r.targetedAt)),
      medianSearchesBeforeHit: median(hits.map((r) => r.searchesBeforeHit)),
      meanWrongReads: mine.length ? mine.reduce((n, r) => n + r.wrongRead.length, 0) / mine.length : 0,
      tokens,
      tokensPerHit: hits.length ? Math.round(tokens / hits.length) : null,
      outputTokens: mine.reduce((n, r) => n + r.outputTokens, 0),
      medianTurns: median(mine.map((r) => r.turns)),
      medianWallMs: median(mine.map((r) => r.wallMs)),
      costUsd: mine.reduce((n, r) => n + (r.costUsd ?? 0), 0),
      costPerHit: hits.length ? mine.reduce((n, r) => n + (r.costUsd ?? 0), 0) / hits.length : null,
    };
  });
}

// ---------------------------------------------------------------------------------------------------------
// The runner
