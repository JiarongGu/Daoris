import assert from 'node:assert/strict';
import { test } from 'node:test';
import { auditDocuments } from './doc-system.mjs';

const base = {
  'docs/README.md': '| `../README.md` | guide |\n| `development.md` | method |\n| `archive/` | record |\n',
  'docs/development.md': '# Development',
  'docs/archive/README.md': '| `old.md` | historical |',
  'docs/archive/old.md': '# Old evidence',
  'README.md': '# Product',
  'daoris.gates.json': JSON.stringify({ docs: { tracked: [] } }),
};
const audit = (extra = {}, removed = []) => {
  const contents = { ...base, ...extra };
  for (const file of removed) delete contents[file];
  return auditDocuments(Object.keys(contents), (file) => {
    if (!(file in contents)) throw new Error(`missing ${file}`);
    return contents[file];
  });
};

test('an unrouted new contract fails with its path', () => {
  assert.ok(audit({ 'docs/new-design.md': '# New contract' }).findings.some((x) => x.includes('docs/new-design.md')));
});

test('a stale router row fails even when the row is an inline code path', () => {
  assert.ok(audit({}, ['docs/development.md']).findings.some((x) => x.includes('docs/development.md')));
});

test('a nested guide must be routed; the archive routes its own files', () => {
  assert.ok(audit({ 'examples/plugins/new/README.md': '# New plugin' }).findings.some((x) => x.includes('examples/plugins/new/README.md')));
  assert.ok(audit({ 'docs/archive/forgotten.md': '# Forgotten' }).findings.some((x) => x.includes('docs/archive/forgotten.md')));
});

test('component guides require nonempty code-freshness declarations', () => {
  const extra = {
    'src/Daoris.Web/README.md': '# Web',
    'docs/README.md': `${base['docs/README.md']}| [Web](../src/Daoris.Web/README.md) | guide |\n`,
    'daoris.gates.json': JSON.stringify({ docs: { tracked: [{ document: 'src/Daoris.Web/README.md', describes: [] }] } }),
  };
  assert.ok(audit(extra).findings.some((x) => x.includes('src/Daoris.Web/README.md') && x.includes('freshness')));
});

test('generated indexes, doctrine mirrors and prompt fixtures do not need individual router rows', () => {
  const result = audit({
    'docs/index/README.md': '# Index',
    'docs/index/outlines/source.md': '# Outline',
    'docs/decisions/D1.md': '# Decision',
    'canon/core/skills/a/SKILL.md': '# Canon',
    '.claude/skills/a/SKILL.md': '# Mirror',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/golden/prompt/full.md': '# Fixture',
  });
  assert.deepEqual(result.findings, []);
  assert.equal(result.inventory.generated, 2);
  assert.equal(result.inventory.fixture, 1);
  assert.equal(result.inventory.canon, 1);
  assert.equal(result.inventory.doctrine, 1);
});

test('links and inline paths are resolved relative to each router and directory rows cover directories', () => {
  assert.deepEqual(audit({
    'docs/README.md': '| [Product](../README.md) | guide |\n| `development.md` | method |\n| `adoption/` | record |',
    'docs/adoption/mechanics.md': '# Local mechanics',
  }).findings, []);
});

test('duplicate freshness declarations fail rather than silently picking one', () => {
  const tracked = { document: 'README.md', describes: ['src/Daoris.Cli/src'] };
  assert.ok(audit({ 'daoris.gates.json': JSON.stringify({ docs: { tracked: [tracked, tracked] } }) })
    .findings.some((x) => x.includes('README.md') && x.includes('twice')));
});

test('a renamed described source file fails even when its replacement exists', () => {
  const result = audit({
    'src/App/new.ts': '// Renamed source',
    'daoris.gates.json': JSON.stringify({ docs: { tracked: [{ document: 'README.md', describes: ['src/App/old.ts'] }] } }),
  });
  assert.ok(result.findings.some((x) => x.includes('README.md') && x.includes('src/App/old.ts')));
});

test('a described source directory must contain an inventoried file at its exact boundary', () => {
  const result = audit({
    'src/Application/main.ts': '// Other directory',
    'daoris.gates.json': JSON.stringify({ docs: { tracked: [{ document: 'README.md', describes: ['src/App'] }] } }),
  });
  assert.ok(result.findings.some((x) => x.includes('README.md') && x.includes('src/App')));
});

test('every tracked guide needs described source paths, including the root guide', () => {
  for (const row of [{ document: 'README.md' }, { document: 'README.md', describes: [] }]) {
    const result = audit({ 'daoris.gates.json': JSON.stringify({ docs: { tracked: [row] } }) });
    assert.ok(result.findings.some((x) => x.includes('README.md') && x.includes('source')));
  }
});

test('invalid described paths produce findings rather than passing or throwing', () => {
  for (const path of [null, 7, '', '../outside', '/outside', 'https://example.invalid/code']) {
    const result = audit({
      'daoris.gates.json': JSON.stringify({ docs: { tracked: [{ document: 'README.md', describes: [path] }] } }),
    });
    assert.ok(result.findings.some((x) => x.includes('README.md') && x.includes('source')));
  }
});

test('source mappings accept existing files and directories with relative path normalization', () => {
  assert.deepEqual(audit({
    'src/App/main.ts': '// Source',
    'tools/build.mjs': '// Build',
    'daoris.gates.json': JSON.stringify({ docs: { tracked: [{ document: 'README.md', describes: ['./src/App/', 'tools/build.mjs'] }] } }),
  }).findings, []);
  assert.deepEqual(audit({
    'src/App/main.ts': '// Source',
    'tools/build.mjs': '// Build',
    'daoris.gates.json': JSON.stringify({ docs: { tracked: [{ document: './README.md', describes: ['.\\src\\App\\', 'tools\\build.mjs'] }] } }),
  }).findings, []);
});

test('a router link with a section fragment covers the document and still catches a missing target', () => {
  const extra = { 'docs/README.md': '| [Product](../README.md#commands) | guide |\n| [Development](development.md#choose-the-checks) | method |' };
  assert.deepEqual(audit(extra).findings, []);
  assert.ok(audit(extra, ['docs/development.md']).findings.some((x) => x.includes('stale') && x.includes('docs/development.md')));
});
