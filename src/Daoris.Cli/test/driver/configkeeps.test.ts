import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { landingEdited, readDriverChoices, writeDriverChoices, type DriverChoices, type LandingRule } from '../../src/driver/driverconfig.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * FABRIC-GRAPH1's review, findings 1 and 5 (FIX-LOG 2026-10-17): a door that edits `driver.json` keeps every key the writer does not own,
 * in its place, and changes only what it edits, every number it did not change written by its own text. One table both twins read,
 * `fixtures/driver-config-keeps.json`, row for row with the driver's `DriverConfigKeepsTests`.
 */

type Edit = { op: string; repository?: string; workspace?: string; on?: boolean; rule?: LandingRule; branch?: string; adapter?: string; kind?: string };
type Change = { from: string[]; to: string[] };
type Row = { name: string; edit: Edit; changes: Change[] };

const here = dirname(dirname(fileURLToPath(import.meta.url)));
const TABLE = JSON.parse(readFileSync(join(here, 'fixtures', 'driver-config-keeps.json'), 'utf8')) as { file: string[]; rows: Row[] };
const FILE = `${TABLE.file.join('\n')}\n`;

/** The edit as the door that makes it makes it: the `landing` verb through `landingEdited`, the rest as their verbs set the field. */
function applied(choices: DriverChoices, edit: Edit): DriverChoices {
  switch (edit.op) {
    case 'none': return choices;
    case 'drivable': return { ...choices, drivable: edit.on ? [...choices.drivable, edit.repository!] : choices.drivable.filter((name) => name !== edit.repository) };
    case 'landing': return landingEdited(choices, edit.repository!, null, edit.rule!);
    case 'workspace-landing': return landingEdited(choices, '', edit.workspace!, edit.rule!);
    case 'landing-clear': return landingEdited(choices, edit.repository!, null, null);
    case 'line': return { ...choices, lines: { ...choices.lines, [edit.repository!]: edit.branch! } };
    case 'intake': return { ...choices, intakeAdapter: edit.adapter! };
    case 'notify': return { ...choices, notify: edit.on! };
    case 'work-cycle-clear': {
      const { [edit.kind!]: _gone, ...kinds } = choices.workCycles[edit.repository!] ?? {};
      return { ...choices, workCycles: { ...choices.workCycles, [edit.repository!]: kinds } };
    }
    default: throw new Error(`the table names an edit this test does not make: ${edit.op}`);
  }
}

function expected(changes: Change[]): string {
  let text = FILE;
  for (const change of changes) {
    const from = change.from.join('\n');
    const at = text.indexOf(from);
    assert.ok(at >= 0 && text.indexOf(from, at + 1) < 0, `a change's \`from\` is in the file once: ${from}`);
    text = text.slice(0, at) + change.to.join('\n') + text.slice(at + from.length);
  }
  return text;
}

test('configkeeps: each row of the shared table writes the bytes the driver writes', () => {
  const fx = makeFixture('config-keeps');
  for (const row of TABLE.rows) {
    const path = fx.write('driver.json', FILE);
    writeDriverChoices(path, applied(readDriverChoices(path), row.edit));
    assert.equal(`${row.name}\n${fx.read('driver.json')}`, `${row.name}\n${expected(row.changes)}`);
  }
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
