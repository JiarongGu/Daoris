/**
 * LYNTAILOCAL1: the opt-in local Lyntai build.
 *
 *   node --test tools/lyntai-local.test.mjs
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { copyPrivateFiles, privateGateFiles } from './merge-branch.mjs';
import { buildLocal, releasedVersion } from './lyntai-local.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const repo = join(here, '..');
const scratch = join(repo, 'local', 'scratch', 'lyntai-local-test');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const propsFile = join(repo, 'src', 'Daoris.Service', 'Directory.Packages.props');
const released = releasedVersion(readFileSync(propsFile, 'utf8'));

test('the tool packs at the released version and the commit, and writes the props and feed it names', () => {
  const fake = join(scratch, 'fake');
  const sibling = join(scratch, 'Lyntai');
  mkdirSync(fake, { recursive: true });
  mkdirSync(sibling, { recursive: true });
  mkdirSync(join(fake, 'src', 'Daoris.Service'), { recursive: true });
  const packed = [];
  const code = buildLocal({
    repo: fake,
    sibling,
    head: () => ({ tip: 'abc1234', current: 'abc1234' }),
    pack: (from, version, feed) => { packed.push({ from, version, feed }); return true; },
    say: () => {},
  });
  // The fake repository has no props to read the pin from: a tool error, nothing written.
  assert.equal(code, 2);
  assert.equal(existsSync(join(fake, 'local', 'lyntai-local.props')), false);

  let code2;
  withProps(null, () => {
    const stub = { repo, sibling, head: () => ({ tip: 'abc1234', current: 'abc1234' }), say: () => {} };
    code2 = buildLocal({ ...stub, pack: (from, version, feed) => { packed.push({ from, version, feed }); return true; } });
    const props = readFileSync(localFile, 'utf8');
    assert.match(props, new RegExp(`<DaorisLyntaiVersion>${released}-local\\.abc1234</DaorisLyntaiVersion>`));
    assert.ok(props.includes(join(repo, 'local', 'lyntai-feed').replaceAll('\\', '/')));
    // The same tip again packs nothing once its package is in the feed.
    writeFileSync(join(repo, 'local', 'lyntai-feed', `Lyntai.Core.${released}-local.abc1234.nupkg`), 'x');
    assert.equal(buildLocal({ ...stub, pack: () => assert.fail('packed again') }), 0);
    rmSync(join(repo, 'local', 'lyntai-feed', `Lyntai.Core.${released}-local.abc1234.nupkg`), { force: true });
    // A checkout off its main line is refused, and says which commit it holds.
    assert.equal(buildLocal({ ...stub, head: () => ({ tip: 'abc1234', current: 'def5678' }), pack: () => assert.fail('packed a session branch') }), 1);
  });
  assert.equal(code2, 0);
  assert.equal(packed.length, 1);
  assert.equal(packed[0].version, `${released}-local.abc1234`);
  assert.equal(packed[0].feed, join(repo, 'local', 'lyntai-feed'));
});

test('a missing sibling is refused with 1 and nothing is packed', () => {
  let packed = false;
  const code = buildLocal({ repo, sibling: join(scratch, 'absent'), pack: () => { packed = true; return true; }, say: () => {} });
  assert.equal(code, 1);
  assert.equal(packed, false);
});

const localFile = join(repo, 'local', 'lyntai-local.props');

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
  const project = join(repo, 'src', 'Daoris.Service', 'Daoris.Service.Core', 'Daoris.Service.Core.csproj');
  const run = spawnSync('dotnet', ['msbuild', project, '-getItem:PackageVersion', '-getProperty:RestoreAdditionalProjectSources'],
    { encoding: 'utf8', windowsHide: true });
  assert.equal(run.status, 0, run.stdout + run.stderr);
  const json = JSON.parse(run.stdout);
  return {
    versions: Object.fromEntries(json.Items.PackageVersion.filter((item) => item.Identity.startsWith('Lyntai.')).map((item) => [item.Identity, item.Version])),
    sources: json.Properties.RestoreAdditionalProjectSources,
  };
};

test('without the props the restored Lyntai version is the released pin, with them the local one', () => {
  const feed = join(repo, 'local', 'lyntai-feed').replaceAll('\\', '/');
  const written = `<Project><PropertyGroup><DaorisLyntaiVersion>${released}-local.abc1234</DaorisLyntaiVersion>`
    + `<RestoreAdditionalProjectSources>$(RestoreAdditionalProjectSources);${feed}</RestoreAdditionalProjectSources></PropertyGroup></Project>`;
  withProps(null, () => {
    const off = evaluate();
    assert.deepEqual(off.versions, { 'Lyntai.Core': released, 'Lyntai.Providers.Basic': released });
    assert.ok(!String(off.sources).includes('lyntai-feed'));
  });
  withProps(written, () => {
    const on = evaluate();
    assert.deepEqual(on.versions, { 'Lyntai.Core': `${released}-local.abc1234`, 'Lyntai.Providers.Basic': `${released}-local.abc1234` });
    assert.ok(String(on.sources).includes('lyntai-feed'));
  });
});

test('the gate trees are handed the same props from the main checkout', () => {
  const home = join(scratch, 'home');
  const tree = join(scratch, 'tree');
  mkdirSync(join(home, 'local'), { recursive: true });
  writeFileSync(join(home, 'daoris.gates.json'), '{"gates":[]}');
  writeFileSync(join(home, 'local', 'lyntai-local.props'), '<Project />');
  const files = privateGateFiles(home);
  assert.ok(files.includes('local/lyntai-local.props'));
  copyPrivateFiles(home, tree, files);
  assert.equal(readFileSync(join(tree, 'local', 'lyntai-local.props'), 'utf8'), '<Project />');
});
