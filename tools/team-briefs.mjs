#!/usr/bin/env node
/**
 * Composes each part's worker brief from an ask's plan, so a planned batch needs no hand-written brief.
 *
 * The plan is the connector's `team_plan` read with `json: true` (saved to a file under `local/scratch/`); each part of its newest
 * plans becomes one brief in the dispatch skill's shape: the part's outcome, its area and change pointers from the pack, its
 * tests and words, the paths other parts claim as what not to touch, and what it proves. A brief is composed, never reworded: a
 * part with no pack still gets the lines the plan has, and says which it lacks.
 *
 *   node tools/team-briefs.mjs <plan.json> [--part <id>] [--base <sha>]
 *
 * Exit codes: 0 briefs written, 2 the plan could not be read or names no such part.
 */
import { readFileSync } from 'node:fs';
import { isMain } from './fsx.mjs';

const pointer = (p) => `${p.file}${p.range ? `:${p.range}` : ''}`;
const list = (items) => (items && items.length > 0 ? items.join('; ') : 'none');

/** Every part of the plan's latest revisions, with the quest each is and the plan it belongs to. */
export function partsOf(plan) {
  return (plan.plans ?? []).flatMap((each) => (each.parts ?? []).map((part) => ({ part, plan: each, quest: plan.quests?.[part.id] ?? null })));
}

/** The brief of one part: the dispatch skill's worker brief, from the plan. */
export function briefOf(plan, id, base = 'main') {
  const all = partsOf(plan);
  const found = all.find((row) => row.part.id === id);
  if (!found) throw new Error(`the plan holds no part \`${id}\``);
  const { part, quest } = found;
  const pack = part.pack ?? {};
  const pointers = pack.pointers ?? [];
  const where = (role) => pointers.filter((p) => p.role === role).map(pointer);
  const others = all.filter((row) => row.part.id !== id && row.part.repository === part.repository);
  const edges = (found.plan.edges ?? []).filter((edge) => edge.part === id);
  const waits = edges.map((edge) => `${edge.kind === 'orders' ? 'it lands after' : 'it builds on'} \`${edge.on}\`${edge.milestone ? ` (${edge.milestone})` : ''}: ${edge.why}`);
  const lines = [
    `Task: ${part.id}: ${part.outcome}${quest ? ` (quest #${quest})` : ''}.`,
    `Area: ${list(where('area'))}. Beyond it: ${list([...where('knowledge'), ...(part.reads ?? [])])}.`,
    `Start from: ${base}${waits.length > 0 ? `, after ${waits.join(' | ')}` : ''}.`,
    `Change: ${list(where('change'))}; claims ${list(part.claims)}.`,
    `Mirror: ${list(where('mirror'))}.`,
    `Tests: ${list(pack.tests)}.`,
    `Words or fields read by suites you cannot run: ${list(pack.words)}.`,
    `Do not touch: ${list(others.flatMap((row) => row.part.claims ?? []))}; records and generated files are the integrator's: ${list(pack.generated)}.`,
    'Decision: none.',
    `Done when: ${list(part.proof)}.`,
  ];
  if (pointers.length === 0) lines.push('Pack: none yet; read the area and the index, and say what you needed that the pack lacked.');
  if ((pack.unknowns ?? []).length > 0) lines.push(`Unknowns the scout left: ${pack.unknowns.join('; ')}.`);
  lines.push('Follow the `change` skill. Hand back as your definition says.');
  return lines.join('\n');
}

if (isMain(import.meta.url)) {
  const args = process.argv.slice(2);
  const take = (name) => (args.includes(name) ? args[args.indexOf(name) + 1] : undefined);
  try {
    if (!args[0] || args[0].startsWith('--')) throw new Error('usage: team-briefs.mjs <plan.json> [--part <id>] [--base <sha>]');
    const plan = JSON.parse(readFileSync(args[0], 'utf8'));
    const ids = take('--part') ? [take('--part')] : partsOf(plan).map((row) => row.part.id);
    for (const id of ids) process.stdout.write(`${briefOf(plan, id, take('--base') ?? 'main')}\n\n`);
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exit(2);
  }
}
