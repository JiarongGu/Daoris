import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { homeEntry } from '../../src/Daoris.Cli/src/platform/home.ts';
import { missing } from './records.mjs';

export const KNOWN = ['claude-code', 'codex', 'dsh'];
export const MIN_SAMPLES = 5;

const PATTERNS = [['claude-code', /^claude/], ['codex', /^(codex|gpt|o\d)/], ['dsh', /^(dsh|deepseek)/]];
const TOKENS = ['input', 'output', 'cacheRead', 'cacheWrite'];

/** The family a door, model or product names, or null where it names none of the known three. */
export const familyOf = name => PATTERNS.find(([, pattern]) => pattern.test(String(name ?? '').trim().toLowerCase()))?.[0] ?? null;

const round = (value, places = 1) => Math.round(value * 10 ** places) / 10 ** places;
const mean = values => values.length ? round(values.reduce((a, b) => a + b, 0) / values.length) : null;
const median = values => {
  const sorted = [...values].sort((a, b) => a - b);
  const mid = sorted.length >> 1;
  return sorted.length ? sorted.length % 2 ? sorted[mid] : round((sorted[mid - 1] + sorted[mid]) / 2) : null;
};
const share = (part, whole) => whole ? round(part / whole, 3) : null;
const sumTokens = rows => Object.fromEntries(TOKENS.map(k => [k, rows.reduce((n, r) => n + (Number.isFinite(r?.[k]) ? r[k] : 0), 0)]));

const parseJson = text => { try { return JSON.parse(text); } catch { return null; } };
export const inWindow = (at, { since, until }) => {
  const day = String(at ?? '').slice(0, 10);
  return !!day && (!since || day >= since) && (!until || day <= until);
};

/** Every opinion pass the home's knowledge database holds, read-only: its family, state, findings, the working session's latest answers and the reviewer session's id. */
export async function readOpinions(home) {
  const file = homeEntry(home, 'knowledge');
  if (!existsSync(file)) return { rows: [], missing: missing(`knowledge.db opinions (no database at the home)`) };
  let db;
  try {
    const { DatabaseSync } = await import('node:sqlite');
    db = new DatabaseSync(file, { readOnly: true });
    const rows = db.prepare('SELECT id, session, pass, asked, asking, given, answers, handed_to FROM opinions').all().flatMap(row => {
      const asking = parseJson(row.asking);
      const reviewer = asking?.reviewer;
      if (!reviewer?.door) return [];
      const given = row.given ? parseJson(row.given) : null;
      const answers = row.answers ? parseJson(row.answers) : null;
      return [{
        id: row.id, session: row.session, pass: row.pass, asked: row.asked,
        family: familyOf(reviewer.door) ?? familyOf(reviewer.product) ?? familyOf(reviewer.maker) ?? String(reviewer.door),
        given: given && Array.isArray(given.findings) ? { findings: given.findings, at: given.at } : null,
        handed: !!row.handed_to,
        answers: Array.isArray(answers) ? answers : [],
      }];
    });
    return { rows };
  } catch {
    return { rows: [], missing: missing('knowledge.db opinions (table missing or unreadable)') };
  } finally {
    db?.close();
  }
}

/** The usage-history turns of the home, by session id, summed to tokens per session. */
export function tokensBySession(home, sessions) {
  const folder = homeEntry(home, 'usage-history');
  let names = [];
  try { names = readdirSync(folder).filter(n => /^turns-.*\.jsonl$/.test(n)).sort(); } catch { return new Map(); }
  const seen = new Set();
  const turns = new Map();
  for (const name of names) {
    for (const line of readFileSync(join(folder, name), 'utf8').split(/\r?\n/).filter(Boolean)) {
      const turn = parseJson(line);
      if (!turn || !sessions.has(turn.session) || seen.has(turn.id)) continue;
      seen.add(turn.id);
      turns.set(turn.session, [...(turns.get(turn.session) ?? []), turn]);
    }
  }
  return new Map([...turns].map(([session, rows]) => [session, sumTokens(rows)]));
}

/** The working session's standing word on each finding of a pass: the latest answer, none reading as unresolved. */
function wordsOf(opinion) {
  const latest = new Map();
  for (const answer of opinion.answers) {
    const known = latest.get(answer.finding);
    if (!known || Date.parse(answer.at) >= Date.parse(known.at) || Number.isNaN(Date.parse(known.at))) latest.set(answer.finding, answer);
  }
  const words = { fixed: 0, rejected: 0, unresolved: 0 };
  for (const finding of opinion.given.findings) {
    const said = latest.get(finding.number)?.said;
    words[said === 'fixed' || said === 'rejected' ? said : 'unresolved']++;
  }
  return words;
}

function opinionStats(rows, tokens) {
  const given = rows.filter(r => r.given);
  const handed = given.filter(r => r.handed);
  const words = handed.map(wordsOf).reduce((n, w) => ({ fixed: n.fixed + w.fixed, rejected: n.rejected + w.rejected, unresolved: n.unresolved + w.unresolved }), { fixed: 0, rejected: 0, unresolved: 0 });
  const answered = words.fixed + words.rejected + words.unresolved;
  const findings = given.reduce((n, r) => n + r.given.findings.length, 0);
  const minutes = given.map(r => (Date.parse(r.given.at) - Date.parse(r.asked)) / 60000).filter(Number.isFinite);
  const spent = rows.filter(r => tokens.has(r.session)).map(r => tokens.get(r.session));
  return {
    passes: rows.length,
    given: given.length,
    findings,
    findingsPerPass: given.length ? round(findings / given.length) : null,
    handedPasses: handed.length,
    answered,
    fixed: words.fixed, rejected: words.rejected, unresolved: words.unresolved,
    fixedShare: share(words.fixed, answered),
    rejectedShare: share(words.rejected, answered),
    unresolvedShare: share(words.unresolved, answered),
    medianMinutes: median(minutes),
    tokens: spent.some(t => Object.values(t).some(n => n > 0)) ? sumTokens(spent) : missing('usage-history turns of the reviewer sessions with tokens'),
  };
}

function workStats(records) {
  const gated = records.filter(r => r.gates);
  const built = records.filter(r => r.builder);
  return {
    landings: records.length,
    withGates: gated.length,
    firstPass: gated.filter(r => r.gates.firstPass).length,
    firstPassRate: share(gated.filter(r => r.gates.firstPass).length, gated.length),
    meanWallMinutes: mean(built.map(r => r.builder.wallMinutes).filter(Number.isFinite)),
    tokens: built.length ? sumTokens(built.map(r => r.builder.tokens)) : missing('landing records with a builder transcript'),
  };
}

/** Families ranked by `score` (higher first, then `tie` as the lower the better), those with fewer than `MIN_SAMPLES` left out, and the sentence saying why where fewer than two can be ranked. */
export function rankFamilies(stats, { samples, score, tie }) {
  const enough = Object.entries(stats).filter(([, s]) => samples(s) >= MIN_SAMPLES && score(s) !== null);
  const unranked = Object.entries(stats).filter(([f]) => !enough.some(([e]) => e === f)).map(([family, s]) => ({ family, samples: samples(s) }));
  if (enough.length < 2) {
    return { ranked: [], unranked, note: `too few samples to rank: two families need ${MIN_SAMPLES} or more samples each, ${enough.length} has` };
  }
  const ranked = enough.sort(([, a], [, b]) => score(b) - score(a) || tie(a) - tie(b)).map(([family, s], i) => ({ rank: i + 1, family, score: score(s) }));
  return { ranked, unranked, note: unranked.length ? `${unranked.map(u => u.family).join(', ')} not ranked: fewer than ${MIN_SAMPLES} samples` : null };
}

const familiesIn = grouped => [...new Set([...KNOWN, ...Object.keys(grouped)])];
const groupBy = (rows, key) => rows.reduce((g, r) => { (g[key(r)] ??= []).push(r); return g; }, {});

/** The per-family, per-role measures over the opinion passes and the landing records of a window. */
export function summarizeFamilies({ opinions, tokens = new Map(), landings, opinionsMissing = null }) {
  const byOpinion = groupBy(opinions, r => r.family);
  const attributed = landings.filter(r => familyOf(r.builder?.model));
  const byLanding = groupBy(attributed, r => familyOf(r.builder.model));
  const opinionStatsBy = Object.fromEntries(familiesIn(byOpinion).map(f => [f, opinionStats(byOpinion[f] ?? [], tokens)]));
  const workStatsBy = Object.fromEntries(familiesIn(byLanding).map(f => [f, workStats(byLanding[f] ?? [])]));
  return {
    secondOpinion: {
      families: opinionStatsBy,
      ...(opinionsMissing ? { missing: opinionsMissing } : {}),
      ranking: rankFamilies(opinionStatsBy, { samples: s => s.handedPasses, score: s => s.fixedShare, tie: s => -(s.findingsPerPass ?? 0) }),
    },
    work: {
      families: workStatsBy,
      unattributed: landings.length - attributed.length,
      ranking: rankFamilies(workStatsBy, { samples: s => s.withGates, score: s => s.firstPassRate, tie: s => s.meanWallMinutes ?? Infinity }),
    },
    spend: 'tokens only: no currency amount is recorded for a pass or a landing',
    window: { minSamples: MIN_SAMPLES },
  };
}

