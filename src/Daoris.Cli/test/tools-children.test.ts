import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { delimiter, dirname, join, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  PATH_VARIABLE, TOOLS, TOOLS_FILE, TOOLS_FOLDER, TOOL_PACKAGE, TOOL_RECORD, childEnvironment, childPath,
  commandOnTheSystem, commandThroughTools, handTools, installBin, readTools, resolveCommand,
} from '../src/tools.ts';
import { probe, readHarnessSettings } from '../src/toolchain.ts';
import type { Toolchain } from '../src/toolchain.ts';
import { driverRows as csharpRows } from './_csharp.ts';
import { captureError, makeFixture } from './_fixture.ts';

/**
 * One answer for Daoris and every child (TOOLS5, D121; the tools design §2.4, §2.7) — the CLI's half of a TWIN
 * with the driver's `Tools.Children.cs`. 🔴 `ToolsChildrenTests.cs` holds the same tables, row for row and in the
 * same order, and *the driver's tables are these tables*, below, holds them cell for cell.
 *
 * A child's PATH is each tool that is managed or a named file, in the declared order, its folders first, then the
 * PATH it inherited; with every tool the system's it is the inherited one exactly. An install's `app/bin/` beside the
 * home comes first of all (WSSETUP3, D124 §1.3), and no install is no change. A command's first word a tool
 * answers for is that tool's, by its way, and never falls back to PATH.
 *
 * Every case runs in a scratch home, laid out as the driver's test lays it out. Nothing here starts a program but
 * the last test's stand-in harness, which says the PATH it was handed.
 */

const windows = process.platform === 'win32';

/** What the fixture lays out under the home, the same on both sides. */
const LAID: readonly [tool: string, version: string, exe: string, paths: string | null][] = [
  ['git', '2.51.0', 'cmd/git.exe', '["cmd","mingw64/bin"]'],
  ['node', '22.20.0', 'node-v22.20.0-win-x64/node.exe', null],
  ['pwsh', '7.5.3', 'pwsh.exe', '["."]'],
  ['gh', '2.62.0', 'bin/gh.exe', null],
  ['az', '2.77.0', 'bin/az.cmd', '["bin"]'],
];

/** A program's file name on this platform: what PATHEXT finds on Windows, the bare name elsewhere. */
const program = (name: string) => (!windows ? name : name === 'npm' || name === 'npx' ? `${name}.cmd` : `${name}.exe`);

function touch(file: string): void {
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, '');
}

function laidOut(name: string) {
  const fx = makeFixture(name);
  const root = fx.root;
  const home = join(root, 'data');
  mkdirSync(home, { recursive: true });
  for (const [tool, version, exe, paths] of LAID) {
    const folder = join(home, TOOLS_FOLDER, tool, version);
    mkdirSync(folder, { recursive: true });
    writeFileSync(join(folder, TOOL_RECORD), paths === null ? `{"exe":"${exe}"}` : `{"exe":"${exe}","paths":${paths}}`);
    touch(join(folder, TOOL_PACKAGE, ...exe.split('/')));
  }
  const managedNode = join(home, TOOLS_FOLDER, 'node', '22.20.0', TOOL_PACKAGE, 'node-v22.20.0-win-x64');
  touch(join(managedNode, program('npm')));
  touch(join(managedNode, program('npx')));
  const named = (tool: string) => join(root, 'named', tool, program(tool));
  for (const tool of TOOLS) touch(named(tool.id));
  touch(join(root, 'named', 'node', program('npm')));
  touch(join(root, 'named', 'node', program('npx')));
  touch(join(root, 'named', 'lonely', program('node')));
  touch(join(root, 'path', program('node')));
  touch(join(root, 'path', program('npm')));
  mkdirSync(join(root, 'more'), { recursive: true });

  const inherited = (cell: string | null) =>
    cell === 'two' ? join(root, 'path') + delimiter + join(root, 'more') : cell === 'empty' ? '' : null;

  const write = (json: string | null) => {
    if (json === null) return;
    let text = json;
    for (const each of ['gone', 'lonely', ...TOOLS.map((tool) => tool.id)]) {
      const file = each === 'gone' ? join(root, 'named', 'gone', program('gh'))
        : each === 'lonely' ? join(root, 'named', 'lonely', program('node')) : named(each);
      text = text.replaceAll(`"@${each}"`, JSON.stringify(file));
    }
    writeFileSync(join(home, TOOLS_FILE), text);
  };

  /**
   * Lay out beside the home what the install cell names: `none`, nothing; `bin`, an install's `app/bin/` with its two
   * launchers; `app`, an `app/` with no `bin/` (an install from before the doctrine tool); `file`, an `app/bin` that is
   * a file.
   */
  const install = (cell: string) => {
    const bin = join(root, 'app', 'bin');
    if (cell === 'bin') {
      touch(join(bin, 'daoris'));
      touch(join(bin, 'daoris.cmd'));
    } else if (cell === 'app') {
      mkdirSync(join(root, 'app'), { recursive: true });
    } else if (cell === 'file') {
      touch(bin);
    }
  };

  /** A folder token: `managed:<tool>/<path>`, `file:<tool>`, `bin` (the install's, beside the home), or `inherited`. */
  const folder = (token: string, from: string | null) => {
    if (token === 'inherited') return from!;
    if (token === 'bin') return join(root, 'app', 'bin');
    if (token.startsWith('file:')) return dirname(named(token.slice(5)));
    const slash = token.indexOf('/');
    const tool = token.slice(8, slash);
    const path = token.slice(slash + 1);
    const pkg = join(home, TOOLS_FOLDER, tool, LAID.find(([id]) => id === tool)![1], TOOL_PACKAGE);
    return path === '.' ? pkg : join(pkg, ...path.split('/'));
  };

  return { fx, root, home, named, inherited, write, folder, install };
}

const spelled = (path: string | null | undefined) => (path && windows ? path.toLowerCase() : path ?? null);

// ——— The environment (§2.4).

const ENVIRONMENT_ROWS: [string, string | null, string | null, string][] = [
  ['no file', null, 'two', 'unchanged'],
  ['every tool the system\'s', '{"tools":{"git":{"use":"system"},"node":{"use":"system"},"pwsh":{"use":"system"},"gh":{"use":"system"},"az":{"use":"system"}}}', 'two', 'unchanged'],
  ['git managed: the record\'s folders, in its order', '{"tools":{"git":{"use":"managed","version":"2.51.0"}}}', 'two', 'managed:git/cmd managed:git/mingw64/bin inherited'],
  ['node managed: the folder its executable is in, when the record names none', '{"tools":{"node":{"use":"managed","version":"22.20.0"}}}', 'two', 'managed:node/node-v22.20.0-win-x64 inherited'],
  ['pwsh managed: a dot is the package itself', '{"tools":{"pwsh":{"use":"managed","version":"7.5.3"}}}', 'two', 'managed:pwsh/. inherited'],
  ['gh a named file: its folder', '{"tools":{"gh":{"use":"file","file":"@gh"}}}', 'two', 'file:gh inherited'],
  ['every way at once: the declared order, whatever the file\'s', '{"tools":{"az":{"use":"managed","version":"2.77.0"},"gh":{"use":"file","file":"@gh"},"pwsh":{"use":"system"},"node":{"use":"file","file":"@node"},"git":{"use":"managed","version":"2.51.0"}}}', 'two', 'managed:git/cmd managed:git/mingw64/bin file:node file:gh managed:az/bin inherited'],
  ['a version nobody downloaded puts nothing first', '{"tools":{"git":{"use":"managed","version":"9.9.9"}}}', 'two', 'unchanged'],
  ['a named file that is gone puts nothing first', '{"tools":{"gh":{"use":"file","file":"@gone"}}}', 'two', 'unchanged'],
  ['an entry that does not read puts nothing first', '{"tools":{"git":{"use":"managed"}}}', 'two', 'unchanged'],
  ['a file that does not read puts nothing first', 'not json', 'two', 'unchanged'],
  ['an empty inherited PATH adds no empty folder', '{"tools":{"git":{"use":"managed","version":"2.51.0"}}}', 'empty', 'managed:git/cmd managed:git/mingw64/bin'],
  ['no inherited PATH', '{"tools":{"git":{"use":"managed","version":"2.51.0"}}}', null, 'managed:git/cmd managed:git/mingw64/bin'],
];

test('a child’s PATH is the tools’ folders, then what it inherited, as the driver builds it', () => {
  for (const [index, [name, json, inheritedCell, expected]] of ENVIRONMENT_ROWS.entries()) {
    const at = laidOut(`tools-children-env-${index}`);
    at.write(json);
    const from = at.inherited(inheritedCell);

    const path = childPath(readTools(at.home), at.home, from);

    const wanted = expected === 'unchanged' ? null : expected.split(' ').map((token) => at.folder(token, from)).join(delimiter);
    assert.equal(spelled(path), spelled(wanted), name);
    const variables = childEnvironment(readTools(at.home), at.home, from);
    assert.deepEqual(Object.keys(variables), wanted === null ? [] : [PATH_VARIABLE], name);
    at.fx.cleanup();
  }
});

// ——— The install's doctrine tool (D124 §1.3, WSSETUP3).

const INSTALL_ROWS: [string, string | null, string | null, string, string][] = [
  ['no install beside the home: byte for byte as before', null, 'two', 'none', 'unchanged'],
  ['an install beside the home: its bin first, with every tool the system\'s', null, 'two', 'bin', 'bin inherited'],
  ['its bin before a managed tool\'s folders', '{"tools":{"git":{"use":"managed","version":"2.51.0"}}}', 'two', 'bin', 'bin managed:git/cmd managed:git/mingw64/bin inherited'],
  ['its bin before a named file\'s folder', '{"tools":{"node":{"use":"file","file":"@node"}}}', 'two', 'bin', 'bin file:node inherited'],
  ['its bin, and an empty inherited PATH adds no empty folder', null, 'empty', 'bin', 'bin'],
  ['its bin, and no inherited PATH', null, null, 'bin', 'bin'],
  ['an app folder with no bin, an install from before the doctrine tool: as before', null, 'two', 'app', 'unchanged'],
  ['a bin that is a file is no folder to put first', null, 'two', 'file', 'unchanged'],
];

test('a child’s PATH begins with the install’s doctrine tool beside the home, as the driver builds it', () => {
  for (const [index, [name, json, inheritedCell, installCell, expected]] of INSTALL_ROWS.entries()) {
    const at = laidOut(`tools-children-install-bin-${index}`);
    at.write(json);
    at.install(installCell);
    const from = at.inherited(inheritedCell);

    const path = childPath(readTools(at.home), at.home, from);

    const wanted = expected === 'unchanged' ? null : expected.split(' ').map((token) => at.folder(token, from)).join(delimiter);
    assert.equal(spelled(path), spelled(wanted), name);
    const variables = childEnvironment(readTools(at.home), at.home, from);
    assert.deepEqual(Object.keys(variables), wanted === null ? [] : [PATH_VARIABLE], name);
    at.fx.cleanup();
  }
});

test('a child is handed the install’s bin with every tool the system’s', () => {
  const at = laidOut('tools-children-install-handed');
  at.install('bin');
  const key = windows ? 'Path' : 'PATH';
  const env = { [key]: at.inherited('two')!, OTHER: 'x' };

  const handed = handTools(env, at.home);

  assert.equal(handed[key], join(at.root, 'app', 'bin') + delimiter + at.inherited('two'));
  assert.deepEqual(Object.keys(handed).sort(), Object.keys(env).sort(), 'never both `Path` and `PATH`');
  at.fx.cleanup();
});

test('the install’s bin is found beside the home, however the home is spelled', () => {
  const at = laidOut('tools-children-install-spelled');
  at.install('bin');

  assert.equal(installBin(at.home + sep), join(at.root, 'app', 'bin'));
  assert.equal(installBin(join(at.root, 'data', '..', 'data')), join(at.root, 'app', 'bin'));
  assert.equal(installBin(join(at.root, 'elsewhere', 'data')), null);
  at.fx.cleanup();
});

test('with every tool the system’s a child is handed the environment it inherited, byte for byte', () => {
  const at = laidOut('tools-children-unchanged');
  const env = { Path: 'a', PATHEXT: '.EXE', OTHER: 'x' };

  assert.equal(handTools(env, at.home), env, 'no file: the same environment, not even a copy');
  at.write('{"tools":{"git":{"use":"system"}}}');
  assert.deepEqual(handTools(env, at.home), env);
  assert.equal(handTools(env, null), env, 'no home changes nothing');
  at.fx.cleanup();
});

test('a child is handed the tools’ PATH over the one it holds, under the spelling it holds, and nothing else changes', () => {
  const at = laidOut('tools-children-handed');
  at.write('{"tools":{"gh":{"use":"managed","version":"2.62.0"}}}');
  const key = windows ? 'Path' : 'PATH';
  const env = { [key]: at.inherited('two')!, OTHER: 'x' };

  const handed = handTools(env, at.home);

  assert.deepEqual(Object.keys(handed).sort(), Object.keys(env).sort(), 'never both `Path` and `PATH`');
  assert.equal(handed[key], at.folder('managed:gh/bin', null) + delimiter + at.inherited('two'));
  assert.equal(handed.OTHER, 'x');
  assert.equal(env[key], at.inherited('two'), 'the environment handed in is not rewritten');
  at.fx.cleanup();
});

// ——— A command's first word (§2.4, §2.7).

const COMMAND_ROWS: [string, string | null, string, string][] = [
  ['a name no tool answers for is the caller\'s', null, 'python', 'not a tool'],
  ['node, the system\'s: the one PATH finds', null, 'node', 'path'],
  ['npm, the system\'s: the one PATH finds', null, 'npm', 'path'],
  ['npx, the system\'s, not on PATH: said, not refused', null, 'npx', 'none'],
  ['node managed: its record\'s executable', '{"tools":{"node":{"use":"managed","version":"22.20.0"}}}', 'node', 'managed'],
  ['npm, node managed: the npm beside it', '{"tools":{"node":{"use":"managed","version":"22.20.0"}}}', 'npm', 'managed'],
  ['npx, node managed: the npx beside it', '{"tools":{"node":{"use":"managed","version":"22.20.0"}}}', 'npx', 'managed'],
  ['npm, node a named file: the npm beside the file', '{"tools":{"node":{"use":"file","file":"@node"}}}', 'npm', 'file'],
  ['npm, node a named file with none beside it: refused, never PATH\'s', '{"tools":{"node":{"use":"file","file":"@lonely"}}}', 'npm', 'refused'],
  ['npm, node managed at a version nobody downloaded: refused', '{"tools":{"node":{"use":"managed","version":"9.9.9"}}}', 'npm', 'refused'],
  ['git managed: its record\'s executable', '{"tools":{"git":{"use":"managed","version":"2.51.0"}}}', 'git', 'managed'],
  ['az managed: a batch file is its executable', '{"tools":{"az":{"use":"managed","version":"2.77.0"}}}', 'az', 'managed'],
];

test('a command’s first word is the tool that answers for it, as the driver resolves it', () => {
  for (const [index, [name, json, word, expected]] of COMMAND_ROWS.entries()) {
    const at = laidOut(`tools-children-command-${index}`);
    at.write(json);

    const resolution = resolveCommand(readTools(at.home), at.home, word, { PATH: at.inherited('two')!, PATHEXT: process.env.PATHEXT });

    if (expected === 'not a tool') {
      assert.equal(resolution, null, name);
      at.fx.cleanup();
      continue;
    }
    assert.ok(resolution, name);
    const tool = TOOLS.find((each) => each.answers.includes(word))!;
    const wanted = expected === 'path' ? join(at.root, 'path', program(word))
      : expected === 'managed' && word === tool.answers[0]
        ? at.folder(`managed:${tool.id}/${LAID.find(([id]) => id === tool.id)![2]}`, null)
        : expected === 'managed' ? join(at.folder('managed:node/node-v22.20.0-win-x64', null), program(word))
          : expected === 'file' ? join(at.root, 'named', 'node', program(word)) : null;
    assert.equal(spelled(resolution.file), spelled(wanted), `${name}: ${resolution.file} (${resolution.problem})`);
    assert.equal(resolution.refused, expected === 'refused', name);
    if (expected === 'none') assert.ok(resolution.problem?.includes('is not on this machine\'s PATH'), name);
    if (expected === 'refused') assert.ok(resolution.problem?.includes('never falls back to PATH'), `${name}: ${resolution.problem}`);
    if (expected !== 'none' && expected !== 'refused') assert.equal(resolution.problem, null, name);
    at.fx.cleanup();
  }
});

test('a name beside a named file that is not there says so, naming the tool and the way back', () => {
  const at = laidOut('tools-children-lonely');
  at.write('{"tools":{"node":{"use":"file","file":"@lonely"}}}');

  const resolution = resolveCommand(readTools(at.home), at.home, 'npm', { PATH: at.inherited('two')! })!;

  assert.match(resolution.problem!, /`npm`/);
  assert.match(resolution.problem!, /Node\.js/);
  assert.match(resolution.problem!, /daoris tool use node system/);
  at.fx.cleanup();
});

// ——— npm in a pin, and agent install on the system's (§2.7).

test('a pin’s npm is the one the tools resolve, and one that cannot run is refused before anything starts', () => {
  const at = laidOut('tools-children-pin');
  at.write('{"tools":{"node":{"use":"managed","version":"22.20.0"}}}');

  const command = commandThroughTools(at.home, ['npm', 'install', '--prefix', 'x', 'p@1.0.0'], { PATH: at.inherited('two')! });
  assert.equal(spelled(command[0]), spelled(join(at.folder('managed:node/node-v22.20.0-win-x64', null), program('npm'))));
  assert.deepEqual(command.slice(1), ['install', '--prefix', 'x', 'p@1.0.0']);

  const standIn = join(at.root, 'stand-in.mjs');
  assert.deepEqual(commandThroughTools(at.home, [standIn, 'view']), [standIn, 'view'], 'a whole path is run as named');

  at.write('{"tools":{"node":{"use":"managed","version":"9.9.9"}}}');
  const refused = captureError(() => commandThroughTools(at.home, ['npm', 'view', 'p', 'version']));
  assert.match(refused.message, /never falls back to PATH/);
  at.fx.cleanup();
});

test('agent install keeps the system’s npm, and refuses naming agent pin where there is none', () => {
  const at = laidOut('tools-children-install');
  at.write('{"tools":{"node":{"use":"managed","version":"22.20.0"}}}');
  const env = { PATH: at.inherited('two')! };

  assert.equal(spelled(commandOnTheSystem(['npm', 'install', '-g', 'p'], env)[0]), spelled(join(at.root, 'path', program('npm'))));
  assert.match(captureError(() => commandOnTheSystem(['npx', 'p'], env)).message, /agent pin/);
  assert.deepEqual(commandOnTheSystem(['claude', 'install'], env), ['claude', 'install']);
  at.fx.cleanup();
});

// ——— A child of the toolchain, started for real: a probe sees the tools' PATH.

test('a harness probe is started on the tools’ PATH, as a session of the desktop’s is', () => {
  const at = laidOut('tools-children-probe');
  at.write('{"tools":{"gh":{"use":"file","file":"@gh"}}}');
  const script = join(at.root, 'says-its-path.mjs');
  writeFileSync(script, "const key = Object.keys(process.env).find((k) => k.toUpperCase() === 'PATH');\nconsole.log(process.env[key]);\n");
  const toolchain: Toolchain = { binary: [process.execPath, script], version: [], profileVariable: 'FAKE_HOME' };

  const saved = process.env.DAORIS_HOME;
  process.env.DAORIS_HOME = at.home;
  try {
    const report = probe('fake', toolchain, at.root, readHarnessSettings(join(at.root, 'harnesses.json')));
    assert.ok(report.present, report.problem ?? '');
    assert.ok(spelled(report.version)!.startsWith(spelled(dirname(at.named('gh')) + delimiter)!), `the probe said: ${report.version}`);
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = saved;
    at.fx.cleanup();
  }
});

// ——— The twin, held: the driver's tables are these tables, row for row and in this order.

const DRIVER_TABLES = join(
  dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'ToolsChildrenTests.cs');

test('the driver’s tables are these tables, row for row and in this order', () => {
  const source = readFileSync(DRIVER_TABLES, 'utf8').replace(/\r\n/g, '\n');
  const rows = (method: string) => csharpRows(source, method, {}, 'ToolsChildrenTests');

  assert.deepEqual(rows('A_childs_PATH_is_the_tools_folders_then_what_it_inherited'), ENVIRONMENT_ROWS);
  assert.deepEqual(rows('A_childs_PATH_begins_with_the_installs_doctrine_tool_beside_the_home'), INSTALL_ROWS);
  assert.deepEqual(rows('A_commands_first_word_is_the_tool_that_answers_for_it'), COMMAND_ROWS);
});
