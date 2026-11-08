/**
 * The opt-in local Shenora build.
 *
 *   node --test tools/shenora-local.test.mjs
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { copyPrivateFiles, privateGateFiles } from './private-gate-files.mjs';
import { buildLocal, releasedVersion } from './shenora-local.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const repo = join(here, '..');
const scratch = join(repo, '_fixtures', 'shenora-local-test');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const propsFile = join(repo, 'src', 'Daoris.Desktop', 'Directory.Packages.props');
const released = releasedVersion(readFileSync(propsFile, 'utf8'));
const PACKAGES = ['Shenora', 'Shenora.Chromium', 'Shenora.Windows'];

/** A stand-in sibling checkout: a git repository on `main` holding one commit. */
function standIn(name) {
  const sibling = join(scratch, name);
  mkdirSync(sibling, { recursive: true });
  writeFileSync(join(sibling, 'Shenora.slnx'), '<Solution />');
  const git = (...args) => {
    const run = spawnSync('git', ['-c', 'user.name=t', '-c', 'user.email=t@example.invalid', '-c', 'commit.gpgsign=false', ...args], { cwd: sibling, encoding: 'utf8', windowsHide: true });
    assert.equal(run.status, 0, run.stderr);
    return run.stdout.trim();
  };
  git('init', '-b', 'main');
  git('add', '.');
  git('commit', '-m', 'one');
  return { sibling, git, sha: git('rev-parse', '--short=7', 'HEAD') };
}

/** A repository stand-in holding the desktop's pin, so the tool reads the released version from where it really is. */
function stand(name) {
  const at = join(scratch, name);
  mkdirSync(join(at, 'src', 'Daoris.Desktop'), { recursive: true });
  writeFileSync(join(at, 'src', 'Daoris.Desktop', 'Directory.Packages.props'), readFileSync(propsFile, 'utf8'));
  return at;
}

test('the pin is read from the desktop props', () => {
  assert.match(released ?? '', /^\d+\.\d+\.\d+$/);
});

test('a run packs the sibling at the released version and its commit, once, and writes the props in the main checkout and the worktree', () => {
  const { sibling, sha } = standIn('Shenora-one');
  const main = stand('main');
  const tree = stand('tree');
  const calls = [];
  const options = { repo: tree, main, sibling, say: () => {}, pack: (from, version, feed) => { calls.push({ from, version, feed }); writeFileSync(join(feed, `Shenora.Chromium.${version}.nupkg`), 'x'); return true; } };
  assert.equal(buildLocal(options), 0);
  const version = `${released}-local.${sha}`;
  assert.deepEqual(calls, [{ from: sibling, version, feed: join(main, 'local', 'shenora-feed') }]);
  for (const at of [main, tree]) {
    const props = readFileSync(join(at, 'local', 'shenora-local.props'), 'utf8');
    assert.match(props, new RegExp(`<DaorisShenoraVersion>${version.replaceAll('.', '\\.')}</DaorisShenoraVersion>`));
    assert.ok(props.includes(join(main, 'local', 'shenora-feed').replaceAll('\\', '/')));
  }
  assert.equal(buildLocal({ ...options, pack: () => assert.fail('packed again') }), 0);
});

test('a checkout off its main line is refused and nothing is packed', () => {
  const { sibling, git } = standIn('Shenora-branch');
  git('checkout', '-b', 'session');
  writeFileSync(join(sibling, 'more.txt'), 'x');
  git('add', '.');
  git('commit', '-m', 'two');
  const code = buildLocal({ repo: stand('main-b'), main: stand('main-b'), sibling, say: () => {}, pack: () => assert.fail('packed a session branch') });
  assert.equal(code, 1);
});

test('a missing sibling is refused with 1', () => {
  assert.equal(buildLocal({ repo: stand('main-c'), main: stand('main-c'), sibling: join(scratch, 'absent'), say: () => {}, pack: () => assert.fail('packed') }), 1);
});

const localFile = join(repo, 'local', 'shenora-local.props');

/** Runs `fn` with the local props set to `content` (absent for null), and puts the owner's file back after. */
const withProps = (content, fn) => {
  const had = existsSync(localFile) ? readFileSync(localFile) : null;
  mkdirSync(dirname(localFile), { recursive: true });
  try {
    if (content === null) rmSync(localFile, { force: true });
    else writeFileSync(localFile, content);
    fn();
  } finally {
    if (had === null) rmSync(localFile, { force: true });
    else writeFileSync(localFile, had);
  }
};

const evaluate = () => {
  const project = join(repo, 'src', 'Daoris.Desktop', 'Daoris.Desktop.App', 'Daoris.Desktop.App.csproj');
  const run = spawnSync('dotnet', ['msbuild', project, '-getItem:PackageVersion', '-getProperty:RestoreAdditionalProjectSources'],
    { encoding: 'utf8', windowsHide: true });
  assert.equal(run.status, 0, run.stdout + run.stderr);
  const json = JSON.parse(run.stdout);
  return {
    versions: Object.fromEntries(json.Items.PackageVersion.filter((item) => PACKAGES.includes(item.Identity)).map((item) => [item.Identity, item.Version])),
    sources: json.Properties.RestoreAdditionalProjectSources,
  };
};

test('without the props the restored Shenora version is the released pin, with them the local one', () => {
  const local = `${released}-local.abc1234`;
  const written = `<Project><PropertyGroup><DaorisShenoraVersion>${local}</DaorisShenoraVersion>`
    + `<RestoreAdditionalProjectSources>$(RestoreAdditionalProjectSources);${join(repo, 'local', 'shenora-feed').replaceAll('\\', '/')}</RestoreAdditionalProjectSources></PropertyGroup></Project>`;
  withProps(null, () => {
    const off = evaluate();
    assert.deepEqual(off.versions, Object.fromEntries(PACKAGES.map((id) => [id, released])));
    assert.ok(!String(off.sources).includes('shenora-feed'));
  });
  withProps(written, () => {
    const on = evaluate();
    assert.deepEqual(on.versions, Object.fromEntries(PACKAGES.map((id) => [id, local])));
    assert.ok(String(on.sources).includes('shenora-feed'));
  });
});

test('the gate trees are handed the same props from the main checkout', () => {
  const checkout = join(scratch, 'checkout');
  const tree = join(scratch, 'gate-tree');
  mkdirSync(join(checkout, 'local'), { recursive: true });
  writeFileSync(join(checkout, 'daoris.gates.json'), '{"gates":[]}');
  writeFileSync(join(checkout, 'local', 'shenora-local.props'), '<Project />');
  const files = privateGateFiles(checkout);
  assert.ok(files.includes('local/shenora-local.props'));
  copyPrivateFiles(checkout, tree, files);
  assert.equal(readFileSync(join(tree, 'local', 'shenora-local.props'), 'utf8'), '<Project />');
});
