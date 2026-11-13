import { missing } from './records.mjs';
import { askOf } from './scope.mjs';

const TOKENS = ['input', 'output', 'cacheRead', 'cacheWrite'];
export const zeroTokens = () => Object.fromEntries(TOKENS.map(k => [k, 0]));
export function addTokens(into, turn) {
  for (const k of TOKENS) {
    if (!Number.isFinite(turn[k])) into[k] = missing(`usage-history turn ${k}`);
    else if (typeof into[k] === 'number') into[k] += turn[k];
  }
  return into;
}
export const sumTokens = rows => rows.length ? rows.reduce(addTokens, zeroTokens()) : missing('usage-history turns in the selected range and scope');

export function costBreakdown(turns, sessions, quests, asks, links, inRange) {
  const rows = turns.filter(t => inRange(t.ended ?? t.written));
  const bySession = new Map(sessions.map(s => [s.id, s]));
  const askFor = id => {
    const s = bySession.get(id);
    return s?.ask ?? /^ask:([^/]+)/.exec(s?.entry ?? '')?.[1] ?? askOf(quests.find(q => q.id === links.get(id)) ?? {});
  };
  const group = key => {
    const groups = new Map();
    for (const row of rows) {
      const id = key(row);
      groups.set(id, [...(groups.get(id) ?? []), row]);
    }
    return [...groups].map(([name, entries]) => ({ name, turns: entries.length, tokens: sumTokens(entries) }));
  };
  return {
    perRole: group(t => bySession.get(t.session)?.actor ?? t.actor ?? missing('sessions.actor or usage-history actor')),
    perModel: group(t => Array.isArray(t.models) && t.models.length ? t.models.join(' + ') : missing('usage-history turn reported models; session-wide ran is not per-turn allocation')),
    perAsk: [...new Set([...asks.map(a => a.id), ...quests.map(askOf).filter(Boolean)])].map(ask => ({ ask, tokens: sumTokens(rows.filter(t => askFor(t.session) === ask)) })),
    modelAllocation: 'a multi-model turn is a joint total; the record does not allocate its tokens between models',
  };
}
