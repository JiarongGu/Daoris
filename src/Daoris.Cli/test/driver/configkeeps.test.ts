import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  DRIVER_SETS, landingEdited, readDriverChoices, withTarget, writeDriverChoices, type AccessSetting, type DriverChoices, type HelpMode,
  type MappedHeavyGate, type LandingRule,
} from '../../src/driver/driverconfig.ts';
import { applyHandOverEdit } from '../../src/driver/handover.ts';
import { sameName } from '../../src/platform/casefold.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * FABRIC-GRAPH1's review, findings 1 and 5 (FIX-LOG 2026-10-17): a door that edits `driver.json` keeps every key the writer does not own,
 * in its place, and changes only what it edits, every number it did not change written by its own text. One table both twins read,
 * `fixtures/driver-config-keeps.json`, row for row with the driver's `DriverConfigKeepsTests`.
 */

type Edit = {
  op: string; repository?: string; workspace?: string; on?: boolean; rule?: LandingRule; branch?: string; adapter?: string; kind?: string;
  mode?: HelpMode; setting?: AccessSetting; gates?: MappedHeavyGate[] | null; to?: string;
};
type Change = { from: string[]; to: string[] };
type Row = { name: string; edit: Edit; changes: Change[]; between?: Change[]; crlf?: boolean; refused?: string; missing?: boolean };
type Twice = { changes: Change[]; notify: boolean; workCycles: Record<string, Record<string, unknown>>; refused: string };
type Named = { name: string; map: keyof DriverChoices; file: Record<string, unknown>; read: Record<string, unknown> };

const here = dirname(dirname(fileURLToPath(import.meta.url)));
const TABLE = JSON.parse(readFileSync(join(here, 'fixtures', 'driver-config-keeps.json'), 'utf8')) as {
  file: string[]; rows: Row[]; twice: Twice; names: Named[]; sets: string[];
};

// CONFIGMERGE1 (D173's CONFIG1 note): which arrays are sets, merged by their names, is one table both twins carry, the driver's
// `DriverConfig.Sets`; every other array is a value, written whole.
test('configkeeps: the arrays merged by their names are the table\'s, as the driver\'s are', () => {
  assert.deepEqual([...DRIVER_SETS], TABLE.sets);
});
const FILE = `${TABLE.file.join('\n')}\n`;

/** A repository toggled in a list as the `drive`, `hold` and `trees` verbs toggle it: matched without case, added at the end. */
function toggled(names: string[], repository: string, on: boolean): string[] {
  const kept = names.filter((name) => !sameName(name, repository));
  return on ? [...kept, repository] : kept;
}

/** The edit as the door that makes it makes it: the `landing` verb through `landingEdited`, the rest as their verbs set the field. */
function applied(choices: DriverChoices, edit: Edit): DriverChoices {
  switch (edit.op) {
    case 'none': return choices;
    case 'drivable': return { ...choices, drivable: toggled(choices.drivable, edit.repository!, edit.on!) };
    case 'hold': return { ...choices, holds: toggled(choices.holds, edit.repository!, edit.on!) };
    case 'trees': return { ...choices, trees: toggled(choices.trees, edit.repository!, edit.on!) };
    // CONFIGMERGE1: a target declared or taken back as the `write-across` verb does it.
    case 'write-across': return { ...choices, writeAcross: withTarget(choices.writeAcross, edit.repository!, edit.to!, edit.on!) };
    case 'landing': return landingEdited(choices, edit.repository!, null, edit.rule!);
    case 'workspace-landing': return landingEdited(choices, '', edit.workspace!, edit.rule!);
    case 'landing-clear': return landingEdited(choices, edit.repository!, null, null);
    case 'line': return { ...choices, lines: { ...choices.lines, [edit.repository!]: edit.branch! } };
    case 'intake': return { ...choices, intakeAdapter: edit.adapter! };
    case 'help-mode': return { ...choices, helpMode: edit.mode! };
    // AUTONOMY1a: the level as `daoris-driver access` leaves it, which only the driver edits.
    case 'access': return { ...choices, access: { ...choices.access, [edit.repository!]: edit.setting! } };
    case 'machine-access': return { ...choices, machineAccess: edit.setting! };
    // HEAVYRUN1b2a: a repository's heavy gates as `daoris-driver heavy` leaves them, which only the driver edits.
    case 'heavy': {
      const { [edit.repository!]: _gone, ...others } = choices.heavyGates;
      return { ...choices, heavyGates: edit.gates ? { ...choices.heavyGates, [edit.repository!]: edit.gates } : others };
    }
    // MIXAGENT1c: a repository's hand-over rule as `daoris driver handover` sets it, through the twin's edit.
    case 'hand-over': return { ...choices, ...applyHandOverEdit(choices, { repository: edit.repository!, set: { mode: edit.mode as string } }) };
    case 'notify': return { ...choices, notify: edit.on! };
    case 'work-cycle-clear': {
      const { [edit.kind!]: _gone, ...kinds } = choices.workCycles[edit.repository!] ?? {};
      return { ...choices, workCycles: { ...choices.workCycles, [edit.repository!]: kinds } };
    }
    default: throw new Error(`the table names an edit this test does not make: ${edit.op}`);
  }
}

/** The file with `changes` made, its lines ended by CRLF where `crlf` says. */
function expected(changes: Change[], crlf = false): string {
  let text = FILE;
  for (const change of changes) {
    const from = change.from.join('\n');
    const at = text.indexOf(from);
    assert.ok(at >= 0 && text.indexOf(from, at + 1) < 0, `a change's \`from\` is in the file once: ${from}`);
    text = text.slice(0, at) + change.to.join('\n') + text.slice(at + from.length);
  }
  return crlf ? text.replaceAll('\n', '\r\n') : text;
}

// A test a row, so each row passes or fails on its own (the second re-check's rows were each seen red alone).
TABLE.rows.forEach((row, index) => {
  test(`configkeeps: the shared table writes the bytes the driver writes: ${row.name}`, () => {
    const fx = makeFixture(`config-keeps-${index}`);
    const path = fx.write('driver.json', expected([], row.crlf));
    // The second re-check's finding 2: a row `missing` is read where no file was.
    if (row.missing === true) rmSync(path);
    const choices = readDriverChoices(path);
    // The re-check's finding 1: another writer's change, made after this one read the file and before it writes.
    if (row.between !== undefined) fx.write('driver.json', expected(row.between, row.crlf));
    const write = () => writeDriverChoices(path, applied(choices, row.edit));
    if (row.refused === undefined) write();
    else assert.throws(write, (error: Error) => error.message.includes(row.refused!), row.name);
    assert.equal(fx.read('driver.json'), expected(row.changes, row.crlf));
  });
});

// CASEMAP1 (D173's CONFIG1 note): every per-name map reads a name written twice in any case where first readable, its first spelling
// and value, row for row with the driver's `DriverConfigKeepsTests`.
TABLE.names.forEach((row, index) => {
  test(`configkeeps: a name written twice in any case reads as the driver reads it: ${row.name}`, () => {
    const fx = makeFixture(`config-names-${index}`);
    const choices = readDriverChoices(fx.write('driver.json', JSON.stringify(row.file)));
    assert.deepEqual(Object.entries(choices[row.map] as Record<string, unknown>), Object.entries(row.read), row.name);
  });
});

test('configkeeps: choices read where no file was, written where there still is none, write the edit\'s keys alone', () => {
  const fx = makeFixture('config-keeps-none');
  const path = fx.entry('driver');
  const choices = readDriverChoices(path);
  assert.equal(choices.source, '{}');
  writeDriverChoices(path, { ...choices, notify: false });
  assert.equal(readFileSync(path, 'utf8'), '{\n  "notify": false\n}\n');
});

test('configkeeps: a key written twice in one object reads as its last, in its first place, and refuses every edit naming it', () => {
  const fx = makeFixture('config-keeps-twice');
  const text = expected(TABLE.twice.changes);
  const path = fx.write('driver.json', text);
  const choices = readDriverChoices(path);
  assert.equal(choices.notify, TABLE.twice.notify);
  assert.deepEqual(Object.entries(choices.workCycles), Object.entries(TABLE.twice.workCycles));
  assert.deepEqual(Object.keys(choices.workCycles['engine']!), Object.keys(TABLE.twice.workCycles['engine']!));
  assert.throws(() => writeDriverChoices(path, { ...choices, cap: 3 }), (error: Error) => error.message.includes(TABLE.twice.refused));
  assert.equal(fx.read('driver.json'), text);
});

test('configkeeps: a number JavaScript cannot hold, and one it would write shorter, survive an edit elsewhere', () => {
  const fx = makeFixture('config-keeps-numbers');
  const path = fx.write('driver.json', '{ "drivable": [], "workCycles": { "engine": { "quest": { "end": "qa", "budget": 9007199254740993 } } }, "ratio": 1.50 }');
  writeDriverChoices(path, { ...readDriverChoices(path), notify: false });
  const text = fx.read('driver.json');
  assert.match(text, /"budget": 9007199254740993\n/);
  assert.match(text, /"ratio": 1\.50\n/);
  assert.match(text, /"notify": false/);
});
