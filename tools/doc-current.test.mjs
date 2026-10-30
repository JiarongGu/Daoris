/**
 * KNOWFRESH1a-iii: the report of present records naming what the code lacks.
 *
 *   node --test tools/doc-current.test.mjs
 *
 * The cases are the design's negative fixtures (`docs/2026-10-30-knowledge-freshness-design.md` §5 iii): each reports as it should
 * be. The landing run on this repository is reviewed by hand and its count pinned nowhere (D220). The one case that reads git
 * builds a scratch repository in a gitignored folder.
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { auditCurrent, sinceDiff, stripComments } from './doc-current.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const scratch = join(here, '..', 'local', 'scratch', 'doc-current-test');
after(() => rmSync(scratch, { recursive: true, force: true }));

const audit = (contents, { present = {}, ignored = [], diff = null, homes = ['docs/README.md'] } = {}) => auditCurrent({
  manifest: { target: '.claude', documents: { present: { homes, ...present } } },
  files: Object.keys(contents),
  read: (file) => {
    if (!(file in contents)) throw new Error(`missing ${file}`);
    return contents[file];
  },
  ignored: (paths) => new Set(paths.filter((path) => ignored.includes(path))),
  diff,
});
const said = (result) => result.findings.map((f) => `${f.kind} ${f.span}`);
const record = (...lines) => ({ 'docs/README.md': `${lines.join('\n')}\n` });

test('a symbol kept only in a comment is absent', () => {
  const result = audit({
    ...record('The `RetiredName` helper.'),
    'src/a.cs': '// RetiredName was here\n/* RetiredName */\nclass Live { }\n',
    'src/b.ts': '/** RetiredName */\nexport const live = 1;\n',
  });
  assert.deepEqual(said(result), ['symbol-absent RetiredName']);
});

test('a symbol kept only in JSON data is absent', () => {
  const result = audit({
    ...record('The `RetiredName` helper.'),
    'src/a.cs': 'class Live { }\n',
    'src/data.json': '{ "RetiredName": 1 }\n',
  });
  assert.deepEqual(said(result), ['symbol-absent RetiredName']);
});

test('a symbol kept only in test source is test-only, and printed as such', () => {
  const result = audit({
    ...record('The `RetiredName` helper.'),
    'src/a.cs': 'class Live { }\n',
    'src/Daoris.Tests/ATests.cs': 'class ATests { void RetiredName() { } }\n',
  });
  assert.deepEqual(said(result), ['symbol-test-only RetiredName']);
});

test('a name in product source is present, in code or in a string', () => {
  const result = audit({
    ...record('`Live`, `sayHello` and `Env.Var`.'),
    'src/a.cs': 'class Live { }\n// not a comment inside a string: "http://x" and SayHello\nvar s = "Env";\n',
    'src/b.ts': 'export const Var = 1;\nexport const SayHello = 2;\n',
  });
  assert.deepEqual(said(result), []);
});

test('a camelCase name is found by its PascalCase form, and each part of a dotted name must exist', () => {
  const result = audit({
    ...record('`movesMissing`, `Gone.Part` and `Live.Part`.'),
    'src/a.cs': 'class Live { int Part; int MovesMissing; }\n',
  });
  assert.deepEqual(said(result), ['symbol-absent Gone.Part']);
});

test('a call and generic arguments are not part of the name', () => {
  const result = audit({
    ...record('`Live.Part()`, `Live.Part(x, y)` and `Live<Part>`.'),
    'src/a.cs': 'class Live { void Part() { } }\n',
  });
  assert.deepEqual(said(result), []);
});

test('a span that is the stem of a tracked source file is a file reference', () => {
  const result = audit({
    ...record('`PluginSteps` and `Trace.Words`, but not `Ghost`.'),
    'src/PluginSteps.cs': 'class Other { }\n',
    'src/Trace.Words.cs': 'class Other { }\n',
  });
  assert.deepEqual(said(result), ['symbol-absent Ghost']);
});

test('lowercase words, acronyms, constants and prose are not symbols', () => {
  const result = audit({
    ...record('`git`, `HTTP`, `D220`, `MAX_SIZE`, `kebab-case`, `snake_case`, `npm run verify`, `Co-Authored-By`.'),
    'src/a.cs': 'class Live { }\n',
  });
  assert.deepEqual(said(result), []);
  assert.equal(result.counts.symbols, 0);
});

test('a path is present when tracked, with its :line stripped and never range-checked', () => {
  const result = audit({
    ...record('`src/a.cs:9999`, `src/a.cs:10-20`, `src/a.cs:3, :9` and `src/`.'),
    'src/a.cs': 'class Live { }\n',
  });
  assert.deepEqual(said(result), []);
  assert.equal(result.counts.paths.present, 4);
});

test('a moved file is never rescued by its name', () => {
  const result = audit({
    ...record('The reader is `tools/old/reader.mjs`.'),
    'tools/new/reader.mjs': 'export const reader = 1;\n',
  });
  assert.deepEqual(said(result), ['path-missing tools/old/reader.mjs']);
});

test('a path resolves against the home\'s folder, then the root, then each declared base', () => {
  const result = audit({
    'docs/README.md': '`guide/x.md`, `docs/guide/x.md`, `locales/en.json`, `web/missing.ts`.\n',
    'docs/guide/x.md': 'x',
    'src/web/locales/en.json': '{}',
  }, { present: { bases: { 'docs/README.md': ['src/web'] } } });
  // `locales/en.json` is under no top-level entry, so it only resolves through the base; `web/missing.ts` is unanchored.
  assert.deepEqual(said(result), []);
  assert.equal(result.counts.paths.present, 3);
  assert.equal(result.counts.paths.unchecked.unanchored, 1);
});

test('a base declared for another home does not apply', () => {
  const result = audit({
    'docs/README.md': '`src/locales/en.json`.\n',
    'src/web/src/locales/en.json': '{}',
  }, { present: { bases: { 'other.md': ['src/web'] } } });
  assert.deepEqual(said(result), ['path-missing src/locales/en.json']);
});

test('an ignored path is present, and a placeholder, a bare file name, a runtime root and an unanchored path are unchecked', () => {
  const result = audit({
    ...record('`local/scratch/x`, `src/<name>/a.cs`, `src/{id}/a.cs`, `README.md`, `a.json:12`, `.daoris/state.json`,',
      '`~/.daoris/home.json`, `and/or`, `Foo/Bar.cs`, `src/*.cs`, `https://example.com/src/a`.'),
    'src/a.cs': 'x',
  }, { ignored: ['local/scratch/x'], present: { runtime: ['.daoris/'] } });
  assert.deepEqual(said(result), []);
  assert.equal(result.counts.paths.present, 1);
  assert.deepEqual(result.counts.paths.unchecked, { placeholder: 3, bare: 2, runtime: 2, unanchored: 2 });
});

test('a path under a tracked folder is present as a folder, and a missing one is a finding', () => {
  const result = audit({
    ...record('`src/deep`, `src/deep/`, `src/gone/`.'),
    'src/deep/a.cs': 'x',
  });
  assert.deepEqual(said(result), ['path-missing src/gone/']);
});

test('a span inside a fenced block is not read, nor is one broken over a line', () => {
  const result = audit({
    ...record('```', '`src/gone.cs` and `Ghost`', '```', 'A `Ghost', 'Name` split.'),
    'src/a.cs': 'x',
  });
  assert.deepEqual(said(result), []);
});

test('an api route is checked against the routes index, whatever its parameter is called', () => {
  const result = audit({
    ...record('`/api/entries`, `GET /api/entries/<id>`, `/api/search?q=x` and `/api/gone`.'),
    'docs/index/routes.md': '| GET /api/entries | `A.cs:1` |\n| GET /api/entries/{id} | `A.cs:2` |\n| POST /api/search | `A.cs:3` |\n',
  });
  assert.deepEqual(said(result), ['route-unknown /api/gone']);
});

test('a DAORIS_ variable is checked against product source, in a string, and not against a comment', () => {
  const result = audit({
    ...record('`DAORIS_LIVE`, `DAORIS_LIVE=1`, `DAORIS_GONE` and `DAORIS_CMT`.'),
    'src/a.cs': '// DAORIS_CMT\nvar v = Environment.GetEnvironmentVariable("DAORIS_LIVE");\n',
  });
  assert.deepEqual(said(result), ['variable-absent DAORIS_GONE', 'variable-absent DAORIS_CMT']);
});

test('an allow-listed span is let through, and an entry that names nothing is reported', () => {
  const result = audit({
    ...record('`tools/call` and `ExternalThing`.'),
    'tools/a.mjs': 'x',
  }, { present: { allow: { 'tools/call': 'a protocol method', ExternalThing: 'another repository\'s type', Unused: 'never named' } } });
  assert.deepEqual(said(result), []);
  assert.deepEqual(result.unusedAllow, ['Unused']);
});

test('an allow-list entry without a reason is refused', () => {
  assert.throws(() => audit(record('`X`'), { present: { allow: { X: '' } } }), /reason/);
});

test('findings name the home and the line, and only declared homes are read', () => {
  const result = audit({
    'docs/README.md': 'one\ntwo `Ghost`\n',
    'docs/design.md': '`Phantom`\n',
  });
  assert.deepEqual(result.findings.map((f) => `${f.home}:${f.line}`), ['docs/README.md:2']);
});

test('AGENTS.md is read only above its generated doctrine region', () => {
  const result = audit({
    'AGENTS.md': 'brief `Ghost`\n<!-- daoris:rules — generated -->\n# Doctrine\n`Phantom`\n',
  }, { homes: ['AGENTS.md'] });
  assert.deepEqual(said(result), ['symbol-absent Ghost']);
});

test('a home folder is the files the repository owns, not those the canon writes', () => {
  const result = audit({
    '.claude/knowledge/own.md': '`Ghost`\n',
    '.claude/knowledge/canon.md': '`Phantom`\n',
    '.claude/knowledge/sub/own2.md': '`Wraith`\n',
    'daoris.lock': JSON.stringify({ entries: [{ target: 'knowledge/canon.md' }] }),
  }, { homes: ['.claude/knowledge/'] });
  assert.deepEqual(result.findings.map((f) => `${f.home} ${f.span}`), [
    '.claude/knowledge/own.md Ghost', '.claude/knowledge/sub/own2.md Wraith']);
});

test('a declared home that is no file is refused', () => {
  assert.throws(() => audit({ 'docs/README.md': 'x' }, { homes: ['docs/gone.md'] }), /gone\.md/);
});

test('a missing declaration is a tool error', () => {
  assert.throws(() => auditCurrent({ manifest: {}, files: [], read: () => '', ignored: () => new Set(), diff: null }), /documents\.present/);
});

test('a span quoting what the diff deleted or renamed is listed, with what became of it', () => {
  const result = audit({
    ...record('`src/old.cs`, `src/before.cs`, `Retired`, `Kept`, `src/same.cs` and `Vanished`.'),
    'src/new.cs': 'class Kept { }\n',
    'src/same.cs': 'x',
  }, {
    diff: {
      changed: [
        { status: 'D', path: 'src/old.cs' },
        { status: 'R', from: 'src/before.cs', path: 'src/new.cs' },
        { status: 'M', path: 'src/same.cs' },
        { status: 'D', path: 'src/Vanished.cs' },
      ],
      removed: new Set(['Retired', 'Kept']),
    },
  });
  assert.deepEqual(result.since.quoting.map((q) => `${q.span}: ${q.what}`), [
    'src/old.cs: deleted', 'src/before.cs: renamed to src/new.cs', 'Retired: removed from product source', 'Vanished: file deleted']);
});

test('with a diff, the homes it touched are named', () => {
  const result = audit({
    'docs/README.md': '`Ghost`\n',
    'docs/development.md': '`Phantom`\n',
  }, { homes: ['docs/README.md', 'docs/development.md'], diff: { changed: [{ status: 'M', path: 'docs/development.md' }], removed: new Set() } });
  assert.deepEqual(result.since.touched, ['docs/development.md']);
});

test('stripComments keeps strings and drops comments in each language', () => {
  assert.equal(stripComments('a // x\nb /* y */ c "// kept" \'/*\' d', 'cs').replace(/\s+/g, ' '), 'a b c "// kept" \'/*\' d');
  assert.equal(stripComments('a = `// kept ${b}`; // gone\n/** doc */ c', 'ts').replace(/\s+/g, ' '), 'a = `// kept ${b}`; c');
  assert.equal(stripComments('x # gone\n<# block #> y "# kept"', 'ps1').replace(/\s+/g, ' '), 'x y "# kept"');
  assert.equal(stripComments('var s = @"a ""//"" b"; // gone', 'cs').trim(), 'var s = @"a ""//"" b";');
});

// ---------------------------------------------------------------------------------------------------------
// sinceDiff: the one read of git

test('sinceDiff reads the paths a diff deleted or renamed and the identifiers it removed from product source', () => {
  const repo = join(scratch, 'since');
  rmSync(repo, { recursive: true, force: true });
  mkdirSync(join(repo, 'src'), { recursive: true });
  const env = { ...process.env, GIT_CONFIG_COUNT: '3', GIT_CONFIG_KEY_0: 'user.name', GIT_CONFIG_VALUE_0: 'F',
    GIT_CONFIG_KEY_1: 'user.email', GIT_CONFIG_VALUE_1: 'f@example.invalid', GIT_CONFIG_KEY_2: 'core.autocrlf', GIT_CONFIG_VALUE_2: 'false' };
  for (const key of ['GIT_DIR', 'GIT_WORK_TREE', 'GIT_INDEX_FILE']) delete env[key];
  const git = (...args) => {
    const r = spawnSync('git', args, { cwd: repo, env, encoding: 'utf8' });
    assert.equal(r.status, 0, r.stderr);
  };
  git('init', '-q', '-b', 'main');
  writeFileSync(join(repo, 'src', 'a.cs'), 'class Alpha { void Keep() { } void Drop() { } }\n');
  writeFileSync(join(repo, 'src', 'b.cs'), 'class Beta { }\nclass Moved { void Same() { } void Same2() { } void Same3() { } }\n');
  writeFileSync(join(repo, 'src', 'a.test.ts'), 'const TestOnlyOld = 1;\n');
  git('add', '-A');
  git('commit', '-q', '-m', 'base');
  writeFileSync(join(repo, 'src', 'a.cs'), 'class Alpha { void Keep() { } }\n');
  git('mv', 'src/b.cs', 'src/c.cs');
  writeFileSync(join(repo, 'src', 'a.test.ts'), 'const TestOnlyNew = 1;\n');
  git('add', '-A');
  git('commit', '-q', '-m', 'change');
  const diff = sinceDiff(repo, 'HEAD~1', { env });
  assert.deepEqual(diff.changed.map((c) => `${c.status} ${c.from ?? ''}>${c.path}`).sort(),
    ['M >src/a.cs', 'M >src/a.test.ts', 'R src/b.cs>src/c.cs']);
  assert.ok(diff.removed.has('Drop'));
  assert.ok(!diff.removed.has('TestOnlyOld'), 'a test source line is not product source');
});
