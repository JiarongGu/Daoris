import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, posix } from 'node:path';
import { fileURLToPath } from 'node:url';
import { driverFile } from '../test/_csharp.ts';
import { makeFixture } from '../test/_fixture.ts';
// @ts-expect-error — untyped workspace tooling; see desktop-publish.test.ts
import { CLI_BIN, CLI_LAUNCHERS, COMMAND, COMMAND_PROJECT, DRIVER_EXE, DRIVER_HOME, commandPublishCommand, layCommand } from '../../../tools/desktop-publish.mjs';

/** The command is a program, never a batch file: its name and its path to the desktop's program are held here to the publish's. */
test('daoris’s command is one program that runs the desktop’s program from its own folder, whole', () => {
  assert.deepEqual([...CLI_LAUNCHERS], ['daoris.exe']);
  assert.equal(COMMAND, CLI_LAUNCHERS[0]);
  const repository = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
  const csproj = readFileSync(join(repository, COMMAND_PROJECT, 'Daoris.Desktop.Command.csproj'), 'utf8');
  assert.ok(csproj.includes(`<AssemblyName>${COMMAND.replace(/\.exe$/, '')}</AssemblyName>`), 'the project’s binary is the command’s name');

  // The one driver file it compiles in names its own file and the program it starts, which the publish lays where this says.
  const launcher = readFileSync(driverFile('CommandLauncher.cs'), 'utf8');
  assert.ok(launcher.includes(`public const string Program = "${COMMAND}";`), 'CommandLauncher.Program');
  const target = /Target = \[([^\]]+)\]/.exec(launcher)?.[1]?.split(',').map((part) => part.trim().replace(/^"|"$/g, '')) ?? [];
  assert.equal(posix.normalize(posix.join(CLI_BIN.join('/'), ...target)), [...DRIVER_HOME, DRIVER_EXE].join('/'), `the command runs ${target.join('/')}`);
  assert.doesNotMatch(launcher, /\bnode\b.*Process|cmd\.exe|ShellExecute = true/, 'the command starts the program itself, through no shell');
});

test('the command is published framework-dependent and laid out whole in app/bin/, and a publish that made none is refused', () => {
  const folder = join(tmpdir(), 'an-install', 'command');
  const command: string = commandPublishCommand(folder);
  assert.ok(command.startsWith(`dotnet publish "${COMMAND_PROJECT}" -c Release -r win-x64 --self-contained false`), command);
  assert.ok(command.endsWith(` -o "${folder}" --nologo`), command);
  assert.doesNotMatch(command, /PublishSingleFile/, 'beside its library and the runtime’s two files, as the git doors are');

  const fx = makeFixture('publish-command-lay');
  const published = join(fx.root, 'published');
  const install = join(fx.root, 'install');
  mkdirSync(published, { recursive: true });
  assert.throws(() => layCommand(published, install), new RegExp(`made no ${COMMAND.replace('.', '\\.')}`));

  for (const name of [COMMAND, 'daoris.dll', 'daoris.runtimeconfig.json', 'daoris.deps.json']) writeFileSync(join(published, name), name);
  layCommand(published, install);
  assert.deepEqual(readdirSync(join(install, ...CLI_BIN)).sort(), ['daoris.deps.json', 'daoris.dll', COMMAND, 'daoris.runtimeconfig.json'].sort());
  assert.equal(readFileSync(join(install, ...CLI_BIN, COMMAND), 'utf8'), COMMAND);
  fx.cleanup();
});

