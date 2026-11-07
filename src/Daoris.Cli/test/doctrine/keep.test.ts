import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rmSync } from 'node:fs';
import { join } from 'node:path';
import { captureError, makeFixture, type Fixture } from '../_fixture.ts';
import { readLock } from '../../src/doctrine/config.ts';
import { commandStatus } from '../../src/doctrine/commands.ts';
import { readCanon } from '../../src/doctrine/canon.ts';
import { commandSync } from '../../src/doctrine/materialize.ts';
import { commandCheck } from '../../src/doctrine/drift.ts';
import { canonFileDigest } from '../../src/doctrine/keep.ts';
import { DaorisError } from '../../src/platform/errors.ts';

/**
 * A keep: a canonical file this repository keeps its own text of, recorded in `daoris.json` with its reason and the
 * canon file's digest it is judged against. `sync` passes over it, `--force` too; `check` names it and does not call it
 * drift; `status` names it apart from an update, and again once the canon's file moves. A core file's absence is never kept.
 */

const doc = (name: string, body = 'x') => `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n${body}\n`;

/** An adopter synced from canon 0.1.0 with one rule, two knowledge documents and one pack's document. */
function adopter(name: string): { canon: Fixture; repo: Fixture; done(): void } {
  const canon = makeFixture(`${name}-canon`);
  canon.write('canon.json', '{"version":"0.1.0"}');
  canon.write('core/rules/sensitive-info.md', doc('sensitive-info', 'never a machine path'));
  canon.write('core/knowledge/storage.md', doc('storage'));
  canon.write('core/knowledge/notes.md', doc('notes'));
  canon.write('packs/extras/pack.json', '{"name":"extras","description":"d"}');
  canon.write('packs/extras/knowledge/extra.md', doc('extra'));
  canon.write('packs/extras/knowledge/more.md', doc('more'));
  const repo = makeFixture(`${name}-repo`);
  repo.write('daoris.json', '{"source":"s","packs":["extras"]}');
  process.env.DAORIS_CANON = canon.root;
  assert.equal(sync(repo).code, 0);
  return {
    canon, repo,
    done() {
      delete process.env.DAORIS_CANON;
      canon.cleanup();
      repo.cleanup();
    },
  };
}

/** A command's exit code and what it printed, a refusal's message among it, as the CLI reports one. */
function run(command: typeof commandSync, repo: Fixture, argv: string[]): { code: number; text: string } {
  const out: string[] = [];
  try {
    const code = command({ root: repo.root, argv, write: (s: string) => out.push(s), packageRoot: '' });
    return { code: code as number, text: out.join('\n') };
  } catch (error) {
    if (!(error instanceof DaorisError)) throw error;
    return { code: error.exitCode, text: [...out, error.message].join('\n') };
  }
}
const sync = (repo: Fixture, ...argv: string[]) => run(commandSync, repo, argv);
const status = (repo: Fixture, ...argv: string[]) => run(commandStatus as typeof commandSync, repo, argv);
const check = (repo: Fixture) => run(commandCheck as typeof commandSync, repo, []);

/** The canon file's digest a keep records, as `daoris reconcile` gives it. */
const digestOf = (canon: Fixture, source: string) => canonFileDigest(readCanon(canon.root), { source });

function keep(repo: Fixture, kept: Record<string, { reason: string; canon: string }>): void {
  const manifest = JSON.parse(repo.read('daoris.json'));
  repo.write('daoris.json', `${JSON.stringify({ ...manifest, kept }, null, 2)}\n`);
}

const edited = (repo: Fixture, rel: string) => repo.write(rel, `${repo.read(rel)}this repository's own line\n`);

test('a kept edit: sync and sync --force pass over it, its lock entry stays, check names it and stays clean', () => {
  const fx = adopter('keep-edit');
  const entry = readLock(fx.repo.root)!.entries.find((e) => e.target === 'knowledge/notes.md');
  edited(fx.repo, '.claude/knowledge/notes.md');
  const text = fx.repo.read('.claude/knowledge/notes.md');
  keep(fx.repo, { 'knowledge/notes.md': { reason: 'our review step differs', canon: digestOf(fx.canon, 'core/knowledge/notes.md') } });
  fx.canon.write('core/knowledge/storage.md', doc('storage', 'REWORDED'));

  const plain = sync(fx.repo);
  assert.equal(plain.code, 0, plain.text);
  assert.match(plain.text, /^ {2}kept {6}knowledge\/notes\.md — this repository's own text: our review step differs$/m);
  assert.equal(fx.repo.read('.claude/knowledge/notes.md'), text);
  assert.match(fx.repo.read('.claude/knowledge/storage.md'), /REWORDED/, 'the rest still updates');

  const forced = sync(fx.repo, '--force');
  assert.equal(forced.code, 0, forced.text);
  assert.doesNotMatch(forced.text, /overwrote/);
  assert.equal(fx.repo.read('.claude/knowledge/notes.md'), text, '--force passes over a kept file too');
  assert.deepEqual(readLock(fx.repo.root)!.entries.find((e) => e.target === 'knowledge/notes.md'), entry);

  const checked = check(fx.repo);
  assert.equal(checked.code, 0, checked.text);
  assert.match(checked.text, /^ {2}kept {6}knowledge\/notes\.md — this repository's own text: our review step differs$/m);
  assert.doesNotMatch(checked.text, /drifted/);

  fx.done();
});

test('a kept file is not reported again until its canon file moves', () => {
  const fx = adopter('keep-reopen');
  fx.canon.write('core/knowledge/storage.md', doc('storage', 'SECOND'));
  keep(fx.repo, { 'knowledge/storage.md': { reason: 'the first text suits us', canon: digestOf(fx.canon, 'core/knowledge/storage.md') } });

  const quiet = JSON.parse(status(fx.repo, '--json').text);
  assert.equal(quiet.update, null, 'the only canon change is the kept one');
  assert.deepEqual(quiet.drifted, []);
  assert.equal(quiet.kept.length, 1);
  assert.equal(quiet.kept[0].reopened, false);
  assert.doesNotMatch(status(fx.repo).text, /reconcile/);

  fx.canon.write('core/knowledge/storage.md', doc('storage', 'THIRD'));
  const again = JSON.parse(status(fx.repo, '--json').text);
  assert.equal(again.kept[0].reopened, true);
  assert.equal(again.kept[0].canon, quiet.kept[0].canon);
  assert.notEqual(again.kept[0].now, again.kept[0].canon);
  assert.match(status(fx.repo).text,
    /^ {2}kept {10}knowledge\/storage\.md — this repository's own text: the first text suits us; the canon's file moved since it was kept, and 'daoris reconcile' lists it$/m);
  // Still passed over: the keep stands until this repository decides it again.
  assert.equal(sync(fx.repo).code, 0);
  assert.doesNotMatch(fx.repo.read('.claude/knowledge/storage.md'), /THIRD/);

  fx.done();
});

test('a kept rule in the region: sync rebuilds the region with this repository\'s text', () => {
  const fx = adopter('keep-span');
  const region = fx.repo.read('AGENTS.md');
  fx.repo.write('AGENTS.md', region.replace('never a machine path', 'never a machine path, nor a host name'));
  keep(fx.repo, { 'rules/sensitive-info.md': { reason: 'host names are private here', canon: digestOf(fx.canon, 'core/rules/sensitive-info.md') } });
  fx.canon.write('core/knowledge/notes.md', doc('notes', 'moved on'));

  assert.equal(sync(fx.repo, '--force').code, 0);
  assert.match(fx.repo.rule('core/core/rules/sensitive-info.md') ?? '', /never a machine path, nor a host name/);
  assert.equal(check(fx.repo).code, 0);

  fx.done();
});

test('a core file is never kept absent; a pack\'s file may be', () => {
  const fx = adopter('keep-absent');
  rmSync(join(fx.repo.root, '.claude/knowledge/storage.md'));
  rmSync(join(fx.repo.root, '.claude/knowledge/extra.md'));
  keep(fx.repo, {
    'knowledge/storage.md': { reason: 'not wanted', canon: digestOf(fx.canon, 'core/knowledge/storage.md') },
    'knowledge/extra.md': { reason: 'a tool of our own answers it', canon: digestOf(fx.canon, 'packs/extras/knowledge/extra.md') },
  });

  for (const argv of [[], ['--force']]) {
    const refused = sync(fx.repo, ...argv);
    assert.equal(refused.code, 1);
    assert.match(refused.text, /1 core file\(s\) kept in daoris\.json and absent here: knowledge\/storage\.md/);
  }
  assert.equal(sync(fx.repo, '--dry-run').code, 1);

  keep(fx.repo, { 'knowledge/extra.md': { reason: 'a tool of our own answers it', canon: digestOf(fx.canon, 'packs/extras/knowledge/extra.md') } });
  assert.equal(sync(fx.repo).code, 0);
  assert.equal(fx.repo.exists('.claude/knowledge/extra.md'), false);
  assert.equal(readLock(fx.repo.root)!.entries.some((e) => e.target === 'knowledge/extra.md'), false);
  const checked = check(fx.repo);
  assert.equal(checked.code, 0, checked.text);

  fx.done();
});

test('a keep the canon retired is dropped, and D19\'s retirement applies', () => {
  const fx = adopter('keep-retired');
  keep(fx.repo, { 'knowledge/notes.md': { reason: 'kept', canon: digestOf(fx.canon, 'core/knowledge/notes.md') } });
  rmSync(join(fx.canon.root, 'core/knowledge/notes.md'));

  const said = status(fx.repo);
  assert.match(said.text, /^ {2}kept {10}knowledge\/notes\.md — the canon no longer ships it here, so the keep is dropped; take it out of daoris\.json$/m);
  assert.deepEqual(JSON.parse(status(fx.repo, '--json').text).keepsDropped, ['knowledge/notes.md']);

  edited(fx.repo, '.claude/knowledge/notes.md');
  const refused = sync(fx.repo);
  assert.equal(refused.code, 1, 'an edited retirement still refuses');
  assert.match(refused.text, /retired upstream, but edited here: knowledge\/notes\.md/);

  fx.done();
});

test('a keep with no reason, or no digest, is refused at the manifest', () => {
  const fx = adopter('keep-shape');
  for (const kept of [{ 'knowledge/notes.md': { reason: ' ', canon: 'a'.repeat(64) } }, { 'knowledge/notes.md': { reason: 'r' } }, ['knowledge/notes.md']]) {
    const manifest = JSON.parse(fx.repo.read('daoris.json'));
    fx.repo.write('daoris.json', JSON.stringify({ ...manifest, kept }));
    const error = captureError(() => commandSync({ root: fx.repo.root, argv: [], write: () => {}, packageRoot: '' }));
    assert.equal(error.exitCode, 2);
    assert.match(error.message, /declares kept as .* "canon": "<sha256>"/);
  }

  fx.done();
});
