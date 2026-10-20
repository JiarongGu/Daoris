import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { addRule, readPermissions, removeRule, switchDefault, writePermissions, type PermissionFile } from '../src/agents/permissions.ts';
import { withRotation, withUse } from '../src/accounts/rotation.ts';
import { addKeyAccount, profileHome, readHarnessSettings, replaceKeyAccount, withDefault, writeHarnessSettings } from '../src/agents/toolchain.ts';
import { homeEntry } from '../src/platform/home.ts';
import { disablePlugin, enablePlugin } from '../src/plugins/plugins.ts';
import { makeFixture } from './_fixture.ts';

/**
 * KEEPALL1 and KEEPALL1b (FIX-LOG 2026-10-17): every home file a door edits keeps what its writer does not own, as `driver.json`'s writers do
 * (`driver/configkeeps.test.ts`). One table both twins read, `fixtures/home-files-keep.json`, row for row with the driver's
 * `HomeFilesKeepTests`.
 */

type Edit = {
  op: string; id?: string; scope?: 'machine' | 'workspace' | 'repository'; name?: string; list?: 'allow' | 'ask' | 'deny'; rule?: string; on?: boolean;
  agent?: string; account?: string; key?: string; workspace?: string; use?: string; version?: string; order?: string[];
};
type Change = { from: string[]; to: string[] };
type Row = { name: string; file: string; edit: Edit; changes: Change[]; start?: Change[]; between?: Change[]; crlf?: boolean; refused?: string };

const here = dirname(fileURLToPath(import.meta.url));
const TABLE = JSON.parse(readFileSync(join(here, 'fixtures', 'home-files-keep.json'), 'utf8')) as { files: Record<string, string[]>; rows: Row[] };

/** `file` with `changes` made, its lines ended by CRLF where `crlf` says. */
function changed(file: string, changes: Change[] = [], crlf = false): string {
  let text = `${TABLE.files[file]!.join('\n')}\n`;
  for (const change of changes) {
    const from = change.from.join('\n');
    const at = text.indexOf(from);
    assert.ok(at >= 0 && text.indexOf(from, at + 1) < 0, `a change's \`from\` is in the file once: ${from}`);
    const to = change.to.join('\n');
    // A change to nothing takes its line's end with it.
    const end = to.length === 0 && text[at + from.length] === '\n' ? at + from.length + 1 : at + from.length;
    text = text.slice(0, at) + to + text.slice(end);
  }
  return crlf ? text.replaceAll('\n', '\r\n') : text;
}

/** The edit through the door's own functions, as the terminal's verbs call them. */
function applied(home: string, row: Row, between: () => void): void {
  const { edit } = row;
  if (row.file === 'plugins.json') {
    between();
    if (edit.op === 'plugin-disable') disablePlugin(home, edit.id!);
    else if (edit.op === 'plugin-enable') enablePlugin(home, edit.id!);
    else throw new Error(`the table names an edit this test does not make: ${edit.op}`);
    return;
  }
  if (row.file === 'permissions.json') {
    const path = homeEntry(home, 'permissions');
    const read = readPermissions(path);
    between();
    const edited: PermissionFile = edit.op === 'none' ? read
      : edit.op === 'permission-add' ? addRule(read, edit.scope!, edit.name ?? null, edit.list!, edit.rule!)
        : edit.op === 'permission-remove' ? removeRule(read, edit.scope!, edit.name ?? null, edit.rule!)
          : edit.op === 'permission-default' ? switchDefault(read, edit.id!, edit.on!)
            : assert.fail(`the table names an edit this test does not make: ${edit.op}`);
    writePermissions(path, edited);
    return;
  }
  if (row.file === 'keys.json') {
    between();
    if (edit.op === 'key-replace') {
      // An account the key replaces is one on this machine: its directory is there.
      mkdirSync(profileHome(home, edit.agent!, edit.account!), { recursive: true });
      replaceKeyAccount(home, edit.agent!, edit.account!, edit.key!, () => {});
    } else if (edit.op === 'key-add') addKeyAccount(home, edit.agent!, edit.key!, () => {});
    else throw new Error(`the table names an edit this test does not make: ${edit.op}`);
    return;
  }
  if (row.file === 'harnesses.json') {
    const path = homeEntry(home, 'harnesses');
    const read = readHarnessSettings(path);
    between();
    const workspace = edit.workspace ?? null;
    const edited = edit.op === 'none' ? read
      : edit.op === 'harness-default' ? withDefault(read, edit.agent!, edit.account!, workspace)
        : edit.op === 'harness-use' ? withUse(read, edit.agent!, { use: edit.use as 'goal' }, workspace)
          // As `daoris agent pin` sets a machine's pin.
          : edit.op === 'harness-pin' ? { ...read, versions: { ...read.versions, [edit.agent!]: edit.version! } }
            : edit.op === 'harness-order' ? withRotation(read, edit.agent!, edit.order!, workspace)
              : assert.fail(`the table names an edit this test does not make: ${edit.op}`);
    writeHarnessSettings(path, edited);
    return;
  }
  throw new Error(`the table names a file this test does not edit: ${row.file}`);
}

// A test a row, so each row passes or fails on its own.
TABLE.rows.forEach((row, index) => {
  test(`homefileskeep: the shared table writes the bytes the driver writes: ${row.name}`, () => {
    const fx = makeFixture(`home-files-keep-${index}`);
    fx.write(row.file, changed(row.file, row.start, row.crlf));
    const write = () => applied(fx.root, row, () => {
      // Another writer's change, made after this one read the file and before it writes.
      if (row.between !== undefined) fx.write(row.file, changed(row.file, row.between, row.crlf));
    });
    if (row.refused === undefined) write();
    else assert.throws(write, (error: Error) => error.message.includes(row.refused!), row.name);
    assert.equal(fx.read(row.file), changed(row.file, row.changes, row.crlf));
  });
});
