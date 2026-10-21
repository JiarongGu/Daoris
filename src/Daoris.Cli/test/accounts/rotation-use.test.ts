import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readHarnessSettings, writeHarnessSettings } from '../../src/agents/toolchain.ts';
import {
  CURVE_MOST_POINTS, CURVE_WINDOWS, SWITCHES, USE_DEFAULTS, USE_MODES, curveProblem, resolveScope, scopeProblem, withRotation, withUse,
} from '../../src/accounts/rotation.ts';
import type { RotationUse, UseChange } from '../../src/accounts/rotation.ts';
import { homeEntry } from '../../src/platform/home.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * How a scope's list is used (TOOL6a; D130 §2, §3.1, §4.6, §14, as §16.6 amends them; D165 point 2, SWITCH1a):
 * `rotationUse` and `workspaceRotationUse` in `harnesses.json`, beside the lists — *use accounts* (`use`: `goal` or
 * `order`), *keep for conversations* (`keep`), and when to switch (`switch`: `smart`, `curve` or `off`; and `curve`, the
 * person's own points per window), read on from the old names `early` and `near` — and the rule binding a scope's default
 * and kept account to its list. The CLI's half of a TWIN with the driver's `RotationUse.cs`:🔴 both hold the table in
 * `fixtures/rotation-use.json`, row for row, the driver's `RotationUseTwinTests` reading it as this file does.
 *
 * Nothing here counts accounts (§2 rule 6): a row with one account and a row with six are read by the same rules, and no
 * refusal is of a list for its length.
 */

type Cell = string | null;

const here = dirname(dirname(fileURLToPath(import.meta.url)));
const TABLE = JSON.parse(readFileSync(join(here, 'fixtures', 'rotation-use.json'), 'utf8')) as {
  read: [why: string, file: unknown, agent: string, workspace: Cell, from: string, list: string[] | null, begins: Cell, said: string][];
  write: [why: string, before: unknown, after: Record<string, unknown>][];
  change: [why: string, before: unknown, agent: string, change: UseChange | null, workspace: Cell, after: Record<string, unknown>][];
  order: [why: string, before: unknown, agent: string, order: string[] | null, workspace: Cell, after: Record<string, unknown>][];
  problem: [why: string, scopeDefault: Cell, list: string[], keep: Cell, refused: Cell][];
  refusal: [why: string, curve: string, refused: Cell][];
};

function at(fx: { root: string }): string {
  const path = homeEntry(fx.root, 'harnesses');
  mkdirSync(dirname(path), { recursive: true });
  return path;
}

/** A file cell: the text on disk where it is a string, else that JSON. */
function read(fx: { root: string }, file: unknown) {
  writeFileSync(at(fx), typeof file === 'string' ? file : JSON.stringify(file), 'utf8');
  return readHarnessSettings(at(fx));
}

/** The named sections of the file as written, those it holds. */
function written(fx: { root: string }, ...sections: string[]): Record<string, unknown> {
  const root = JSON.parse(readFileSync(at(fx), 'utf8')) as Record<string, unknown>;
  return Object.fromEntries(sections.filter((section) => section in root).map((section) => [section, root[section]]));
}

/** The sections a row's `after` names, as written: key for key, in order. */
function writtenAs(fx: { root: string }): string {
  return JSON.stringify(written(fx, 'rotationUse', 'workspaceRotationUse'));
}

/**
 * A scope's settings in one word each, today's defaults left out, then `?name` for each value or setting this build does
 * not know — the cell both twins compare. The defaults are spelled here on purpose, not read from `USE_DEFAULTS`: the
 * table says what today's defaults are, and a test below holds the constant to them.
 */
function said(use: RotationUse, unknown: string[]): string {
  const parts = [
    use.use !== 'goal' ? use.use : null,
    use.keep !== null ? `keep=${use.keep}` : null,
    use.switch !== 'smart' ? `switch=${use.switch}` : null,
    ...(['session', 'weekly'] as const).map((window) => {
      const points = use.curve[window];
      return points ? `${window}=${points.map(([elapsed, used]) => `${elapsed}:${used}`).join(',')}` : null;
    }),
    use.near !== 90 ? `near=${use.near}` : null,
    ...unknown.map((name) => `?${name}`),
  ].filter((part): part is string => part !== null);
  return parts.length > 0 ? parts.join(' ') : 'none';
}

test('today\'s defaults live in one place: make the most of them, keeping none, switching by Daoris\'s curve', () => {
  assert.deepEqual({ ...USE_DEFAULTS }, { use: 'goal', keep: null, switch: 'smart', curve: {}, early: true, near: 90 });
  assert.deepEqual([...USE_MODES], ['goal', 'order']);
  assert.deepEqual([...SWITCHES], ['smart', 'curve', 'off']);
  assert.deepEqual([...CURVE_WINDOWS], ['session', 'weekly']);
  assert.equal(CURVE_MOST_POINTS, 12);
});

// ——— Read and resolved (§2 rule 1, §14, §16.6; D165 §6): the scope a start reads, its list, where it begins, and its
// settings, the old names read on.

test('a scope is read as the driver reads it: its list, where it begins, and its settings only with that list (the shared table)', () => {
  const fx = makeFixture('rotation-use-scope');
  for (const [why, file, agent, workspace, from, list, begins, use] of TABLE.read) {
    const scope = resolveScope(read(fx, file), agent, workspace);
    assert.deepEqual(
      { from: scope.from, list: scope.list, begins: scope.begins, use: said(scope.use, scope.unknown) },
      { from, list: list ?? [], begins, use },
      why);
  }
  fx.cleanup();
});

// ——— Written (D165 §6): the old names written as `switch` and `curve`, never again as themselves; a curve in one order.

test('settings are written as the driver writes them, the old names as the new (the shared table)', () => {
  const fx = makeFixture('rotation-use-write');
  for (const [why, before, after] of TABLE.write) {
    writeHarnessSettings(at(fx), read(fx, before));
    assert.equal(writtenAs(fx), JSON.stringify(after), why);
  }
  fx.cleanup();
});

// ——— Edited (§2, §16.6; D165 §6): a change merged into the scope's settings, each choice written as made, so a later
// default never overturns one; what this build does not know kept as written; `null` clears the scope's settings, and a
// scope with none is dropped. The old names, which the doors still take, are mapped onto the switch and the curve.

test('settings are set and cleared as the driver writes them (the shared table)', () => {
  const fx = makeFixture('rotation-use-edit');
  for (const [why, before, agent, edit, workspace, after] of TABLE.change) {
    writeHarnessSettings(at(fx), withUse(read(fx, before), agent, edit, workspace));
    assert.equal(writtenAs(fx), JSON.stringify(after), why);
  }
  fx.cleanup();
});

// ——— A list's settings come with it (§2 rule 1): a list cleared takes its scope's settings; a list replaced keeps them.

test('a list\'s settings go with it, as the driver writes them (the shared table)', () => {
  const fx = makeFixture('rotation-use-order');
  for (const [why, before, agent, order, workspace, after] of TABLE.order) {
    writeHarnessSettings(at(fx), withRotation(read(fx, before), agent, order, workspace));
    assert.equal(writtenAs(fx), JSON.stringify(after), why);
  }
  fx.cleanup();
});

// ——— The rule binding a scope (§3.1, §4.6): its default and its kept account are of its list, and driven work keeps an
// account. The first problem is the one said; no list is refused for how many it names.

test('a scope is refused as the driver refuses it (the shared table)', () => {
  for (const [why, scopeDefault, list, keep, refused] of TABLE.problem) {
    const problem = scopeProblem({ default: scopeDefault, list, keep });
    assert.equal(problem === null ? null : `${problem.kind} ${problem.account}`, refused, why);
  }
});

// ——— A curve's rules (D165 point 2, the switch design §2): one to twelve whole-number points per window, elapsed from 0
// to 100 and rising, used from 0 to 100 and never falling; the first problem said, which window, which point, which rule.

test('a curve is refused as the driver refuses it, in the same sentence (the shared table)', () => {
  for (const [why, curve, refused] of TABLE.refusal) assert.equal(curveProblem(JSON.parse(curve)), refused, why);
});

// ——— The doors (§3.1, §4.6, §16.6; the switch design §5): `profile use` and `agent list`, which say how each list is used,
// are the driver's program's since PUSH1f2e3 and PUSH1f2e4, held by `AgentsTextTests`' rows.
