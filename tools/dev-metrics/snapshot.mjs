import { existsSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { writeAtomic } from '../fsx.mjs';
import { json } from './records.mjs';

export function writeSnapshot(report, { directory = 'docs/metrics', date = report.range.until ?? report.generatedAt.slice(0, 10) } = {}) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || Number.isNaN(Date.parse(date)) || new Date(date).toISOString().slice(0, 10) !== date) throw new Error('snapshot date must be YYYY-MM-DD');
  mkdirSync(directory, { recursive: true });
  const file = join(directory, `${date}.json`);
  const prior = json(file);
  if (existsSync(file) && prior === null) throw new Error('snapshot does not read as JSON; kept unchanged');
  if (prior && (prior.schema !== 1 || !Array.isArray(prior.receipts))) throw new Error('snapshot has an unreadable schema');
  const receipt = structuredClone(report);
  receipt.source.quests = receipt.source.quests.startsWith('service ') ? 'service read' : receipt.source.quests.startsWith('file ') ? 'supplied quest file' : receipt.source.quests;
  const key = r => JSON.stringify([r.range, r.scope.requested.length ? [r.scope.quests, r.scope.asks] : 'daily']);
  const receipts = [...(prior?.receipts ?? []).filter(r => key(r) !== key(receipt)), receipt];
  const text = JSON.stringify({ schema: 1, date, receipts }, null, 2) + '\n';
  if (/[A-Za-z]:[\\/]|\\\\[\w.-]+\\/.test(text)) throw new Error('snapshot contains a machine path');
  writeAtomic(file, text);
  return file;
}
