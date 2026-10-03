import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { MANIFEST, commandPlugin, pluginsRoot, readIcon, readManifest, readPlugins } from '../src/plugins.ts';
import { driverRows } from './_csharp.ts';
import { makeFixture } from './_fixture.ts';

/**
 * PLUGUI2, D140 (the catalogue design §3.1) — a plugin's icon, the CLI's half of a twin with the driver's
 * `PluginIcon.cs`: the manifest's `icon`, a path inside the plugin's folder to an SVG or a PNG, read by the catalogue,
 * and its file judged where it is listed. 🔴 `PluginIconTests.cs`'s theory is parsed below and held to `ICON_ROWS`,
 * cell for cell and in order, so a row changed on one side alone fails `npm run verify`.
 *
 * 🔴 An icon's problem never refuses the plugin: it is how a plugin is recognised, never what it does.
 */

const DRIVER_TESTS = join(
  dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'PluginIconTests.cs',
);

/**
 * [case, the manifest's `icon` as JSON or null for none, the file a row writes from the plugin's folder or null, its
 * kind, what it draws as or null, a fragment of its problem or null]. The twin is
 * `PluginIconTests.The_icon_reads_as_the_cli_reads_it`.
 */
const ICON_ROWS: [string, string | null, string | null, string | null, string | null, string | null][] = [
  ['no icon', null, null, null, null, null],
  ['null is none', 'null', null, null, null, null],
  ['an svg', '"icon.svg"', 'icon.svg', 'svg', 'svg', null],
  ['a png in a folder of its own', '"assets/icon.png"', 'assets/icon.png', 'png', 'png', null],
  ['an extension in capitals', '"ICON.SVG"', 'ICON.SVG', 'svg', 'svg', null],
  ['not text', '42', null, null, null, "must be the path of an .svg or .png file in the plugin's folder"],
  ['blank', '"  "', null, null, null, "must be the path of an .svg or .png file in the plugin's folder"],
  ['a whole path', '"/icon.svg"', null, null, null, "is not a path inside the plugin's folder"],
  ['a drive', '"C:/icon.svg"', null, null, null, "is not a path inside the plugin's folder"],
  ['an address', '"https://example.com/icon.svg"', null, null, null, "is not a path inside the plugin's folder"],
  ['a backslash', '"assets\\\\icon.svg"', null, null, null, "is not a path inside the plugin's folder"],
  ['out of the folder', '"../icon.svg"', null, null, null, "is not a path inside the plugin's folder"],
  ['a dot for the folder', '"./icon.svg"', null, null, null, "is not a path inside the plugin's folder"],
  ['an empty name', '"assets//icon.svg"', null, null, null, "is not a path inside the plugin's folder"],
  ['another format', '"icon.gif"', null, null, null, 'is neither an .svg nor a .png file'],
  ['no extension', '"icon"', null, null, null, 'is neither an .svg nor a .png file'],
  ['not there', '"icon.svg"', null, null, null, "is not a file in the plugin's folder"],
  ['a folder by that name', '"icon.svg"', 'icon.svg', 'folder', null, "is not a file in the plugin's folder"],
  ['too large', '"icon.svg"', 'icon.svg', 'svg-big', null, 'is larger than 32 KiB'],
  ['text named png', '"icon.png"', 'icon.png', 'text', null, 'is not a PNG image'],
  ['an svg named png', '"icon.png"', 'icon.png', 'svg', null, 'is not a PNG image'],
  ['too many pixels', '"icon.png"', 'icon.png', 'png-huge', null, 'is 1024×1024 pixels, and an icon is at most 512 on a side'],
  ['empty', '"icon.svg"', 'icon.svg', 'empty', null, 'is not an SVG image'],
  ['text named svg', '"icon.svg"', 'icon.svg', 'text', null, 'is not an SVG image'],
  ['a png named svg', '"icon.svg"', 'icon.svg', 'png', null, 'is not an SVG image'],
  ['an entity', '"icon.svg"', 'icon.svg', 'svg-entity', null, 'declares an XML entity, which an icon may not'],
];

const SVG = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16"><circle cx="8" cy="8" r="6"/></svg>';

/** PNG's signature, then its IHDR chunk with the width and the height; then IEND. The driver's `Png`. */
function png(width: number, height: number): Buffer {
  const bytes = Buffer.alloc(8 + 25 + 12);
  Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]).copy(bytes, 0);
  bytes.writeUInt32BE(13, 8);
  bytes.write('IHDR', 12, 'ascii');
  bytes.writeUInt32BE(width, 16);
  bytes.writeUInt32BE(height, 20);
  bytes[24] = 8;
  bytes[25] = 6;
  bytes.write('IEND', 8 + 25 + 4, 'ascii');
  return bytes;
}

/** The file a row writes, by its kind: the same bytes the driver's `Write` makes for the same word. */
function write(path: string, kind: string): void {
  mkdirSync(dirname(path), { recursive: true });
  switch (kind) {
    case 'folder': mkdirSync(path, { recursive: true }); return;
    case 'svg': writeFileSync(path, SVG, 'utf8'); return;
    case 'svg-big': writeFileSync(path, SVG.replace('</svg>', `<!--${'x'.repeat(33_000)}--></svg>`), 'utf8'); return;
    case 'svg-entity': writeFileSync(path, `<?xml version="1.0"?><!DOCTYPE svg [<!ENTITY a "aaaa">]>${SVG}`, 'utf8'); return;
    case 'png': writeFileSync(path, png(64, 64)); return;
    case 'png-huge': writeFileSync(path, png(1024, 1024)); return;
    case 'text': writeFileSync(path, 'hello', 'utf8'); return;
    case 'empty': writeFileSync(path, ''); return;
    default: throw new Error(`no file kind \`${kind}\``);
  }
}

function plugin(home: string, id: string, icon: string | null, extra = ''): string {
  const folder = join(pluginsRoot(home), id);
  mkdirSync(folder, { recursive: true });
  const field = icon === null ? '' : `, "icon": ${icon}`;
  writeFileSync(join(folder, MANIFEST),
    `{ "id": "${id}", "name": "Acme gate", "hooks": { "command": ["node", "gate.mjs"], "points": ["quest/consider"] }${field}${extra} }`);
  return folder;
}

test('the driver’s icon table is this table, row for row and in this order', () => {
  assert.deepEqual(driverRows(readFileSync(DRIVER_TESTS, 'utf8'), 'The_icon_reads_as_the_cli_reads_it'), ICON_ROWS);
});

test('the icon reads as the driver reads it: an svg or a png drawn, anything else a named problem, never the plugin’s', () => {
  ICON_ROWS.forEach(([name, icon, file, kind, drawn, problem], at) => {
    const fx = makeFixture(`plugicon-${at}`);
    const folder = plugin(fx.root, 'acme.gate', icon);
    if (file !== null) write(join(folder, ...file.split('/')), kind!);

    const [entry] = readPlugins(fx.root).plugins;
    const read = readIcon(entry!.folder, entry!.manifest);

    assert.equal(entry!.problem, null, `${name}: ${entry!.problem}`);
    assert.equal(read.type, drawn, `${name}: drawn as ${read.type ?? 'nothing'}, ${read.problem}`);
    if (problem === null) assert.equal(read.problem, null, `${name}: ${read.problem}`);
    else assert.ok(read.problem?.includes(problem), `${name}: ${read.problem}`);
    fx.cleanup();
  });
});

test('a problem names the icon as the manifest writes it, and the plugin still contributes', () => {
  const fx = makeFixture('plugicon-named');
  plugin(fx.root, 'acme.gate', '"../icon.svg"');

  const catalog = readPlugins(fx.root);

  assert.equal(catalog.plugins[0]!.manifest.icon, null);
  assert.equal(catalog.plugins[0]!.manifest.iconProblem,
    "`icon` `../icon.svg` is not a path inside the plugin's folder: it is written from the folder, with `/` "
    + 'between names and no `.` or `..`.');
  assert.equal(catalog.contributing.length, 1);
  fx.cleanup();
});

test('a refused plugin keeps its icon: it is how the person recognises it, never something it contributes', () => {
  const fx = makeFixture('plugicon-refused');
  const folder = join(pluginsRoot(fx.root), 'acme.later');
  mkdirSync(folder, { recursive: true });
  writeFileSync(join(folder, MANIFEST), '{ "id": "acme.later", "icon": "icon.svg", "harnesses": [ { "name": "claude-code", "command": ["claude"] } ] }');
  write(join(folder, 'icon.svg'), 'svg');

  const [refused] = readPlugins(fx.root).plugins;

  assert.notEqual(refused!.problem, null);
  assert.deepEqual(refused!.manifest.harnesses, []);
  assert.equal(readIcon(refused!.folder, refused!.manifest).type, 'svg');
  fx.cleanup();
});

test('a manifest read as written reads its icon too, as an offer is read', () => {
  const fx = makeFixture('plugicon-written');
  const folder = plugin(fx.root, 'acme.gate', '"icon.svg"');

  const { manifest, problem } = readManifest('acme.gate', folder, true);

  assert.equal(problem, null);
  assert.equal(manifest.icon, 'icon.svg');
  assert.equal(manifest.iconProblem, null);
  fx.cleanup();
});

test('plugin list says why a declared icon is not drawn, and nothing for one that draws or none', () => {
  const fx = makeFixture('plugicon-list');
  plugin(fx.root, 'acme.broken', '"icon.svg"');
  const drawn = plugin(fx.root, 'acme.drawn', '"icon.svg"');
  write(join(drawn, 'icon.svg'), 'svg');
  plugin(fx.root, 'acme.plain', null);

  const saved = process.env.DAORIS_HOME;
  process.env.DAORIS_HOME = fx.root;
  const lines: string[] = [];
  try {
    assert.equal(commandPlugin({ root: process.cwd(), argv: ['list'], write: (line) => lines.push(line), packageRoot: process.cwd() }), 0);
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = saved;
  }

  const out = lines.join('\n');
  assert.equal(out.match(/icon not drawn/g)?.length, 1, out);
  assert.ok(out.includes("icon not drawn: `icon` `icon.svg` is not a file in the plugin's folder."), out);
  fx.cleanup();
});
