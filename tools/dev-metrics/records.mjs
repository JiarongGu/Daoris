import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { homeEntry } from '../../src/Daoris.Cli/src/platform/home.ts';

export const missing = (where) => `not recorded: ${where}`;
export const json = (file, fallback = null) => { try { return JSON.parse(readFileSync(file, 'utf8')); } catch { return fallback; } };
export const files = (dir, suffix) => { try { return readdirSync(dir).filter(n => n.endsWith(suffix)).sort(); } catch { return []; } };
export const documents = (dir) => files(dir, '.json').map(f => json(join(dir, f))).filter(Boolean);
export const moment = (at, where) => Number.isFinite(Date.parse(at)) ? at : missing(where);
export const elapsed = (from, to, where) => {
  const ms = Date.parse(to) - Date.parse(from);
  return Number.isFinite(ms) && ms >= 0 ? ms : missing(where);
};
export const earliest = (values) => values.filter(v => Number.isFinite(Date.parse(v))).sort((a, b) => Date.parse(a) - Date.parse(b))[0];

const tables = ['quests', 'sessions', 'asks', 'quest_log', 'channel', 'channel_acks', 'channel_addressed', 'channel_addressed_acks', 'event_journal', 'said_settled'];
const aliases = { sender: 'from', receiver: 'to', took_quest: 'tookQuest', from_session: 'from', from_why: 'fromWhy', from_by: 'fromBy', note_parts: 'noteParts' };
function decoded(row) {
  const out = {};
  for (const [k, v] of Object.entries(row)) {
    let value = v;
    if (typeof v === 'string' && /^[\[{]/.test(v)) { try { value = JSON.parse(v); } catch { /* retain the recorded value */ } }
    out[aliases[k] ?? k] = value;
  }
  return out;
}

export async function readHome(home) {
  const result = { tables: {}, availability: {}, runs: documents(homeEntry(home, 'workflows', 'runs')), units: documents(homeEntry(home, 'workflows', 'units')) };
  const file = homeEntry(home, 'knowledge');
  let db;
  try {
    if (!existsSync(file)) throw new Error('database absent');
    const { DatabaseSync } = await import('node:sqlite');
    db = new DatabaseSync(file, { readOnly: true });
    db.exec('BEGIN');
    for (const name of tables) {
      try {
        result.tables[name] = db.prepare(`SELECT * FROM ${name}`).all().map(decoded);
        result.availability[name] = true;
      } catch { result.availability[name] = missing(`knowledge.db ${name} (table missing or unreadable)`); }
    }
  } catch { /* each missing table names its source below */ }
  finally { db?.close(); }
  for (const name of tables) {
    result.tables[name] ??= [];
    result.availability[name] ??= missing(`knowledge.db ${name} (database absent or unreadable)`);
  }
  result.helpers = files(homeEntry(home, 'helpers'), '.ask.json').map(f => {
    const ask = json(homeEntry(home, 'helpers', f));
    if (!ask) return null;
    const id = f.slice(0, -'.ask.json'.length);
    return { ...ask, state: json(homeEntry(home, 'helpers', `${id}.state.json`)), answer: json(homeEntry(home, 'helpers', `${id}.answer.json`)) };
  }).filter(Boolean);
  return result;
}
