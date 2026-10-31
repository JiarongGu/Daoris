#!/usr/bin/env node
/**
 * LYNTAILOCAL1: an opt-in local build of the Lyntai sibling for development (D22's note).
 *
 *   node tools/lyntai-local.mjs [path-to-Lyntai-checkout]
 *
 * Packs the sibling's main line (`master`, as its checkout holds it; never a session branch), and does nothing when the
 * tip is already packed, at `<released>-local.<sha7>` into the ignored `local/lyntai-feed/` and writes the ignored
 * `local/lyntai-local.props`, which the service's package props import whenever it exists. The merge tool copies it
 * into its gate trees. Delete it to restore the released pin.
 * The checkout is the argument, `DAORIS_LYNTAI_PATH`, or `Lyntai` beside the main checkout. It is only read.
 *
 * Exit codes: 0 done, 1 the sibling is not where it should be, 2 a tool error.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, spawnGit, writeAtomic } from './fsx.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');

/** The released pin, read from the props that declare it. */
export function releasedVersion(propsText) {
  return /<DaorisLyntaiReleased>([^<]+)<\/DaorisLyntaiReleased>/.exec(propsText)?.[1].trim() ?? null;
}

/** The props the opt-in build imports: the local version and the feed as an added restore source. */
export function localProps({ version, feed }) {
  const source = feed.replaceAll('\\', '/');
  return `<Project>\n  <PropertyGroup>\n    <DaorisLyntaiVersion>${version}</DaorisLyntaiVersion>\n`
    + `    <RestoreAdditionalProjectSources>$(RestoreAdditionalProjectSources);${source}</RestoreAdditionalProjectSources>\n`
    + `  </PropertyGroup>\n</Project>\n`;
}

const revision = (cwd, ref) => {
  const run = spawnGit(['rev-parse', '--verify', '--short=7', `${ref}^{commit}`], { cwd, encoding: 'utf8' });
  return run.status === 0 ? run.stdout.trim() : null;
};

/** The tip of the sibling's main line (`master`, else `main`) and the commit its checkout holds. */
const gitHead = (cwd) => ({ tip: revision(cwd, 'master') ?? revision(cwd, 'main'), current: revision(cwd, 'HEAD') });

const dotnetPack = (sibling, version, feed) =>
  spawnSync('dotnet', ['pack', sibling, '-c', 'Release', `-p:Version=${version}`, '-o', feed, '-nodeReuse:false'],
    { cwd: sibling, stdio: 'inherit', windowsHide: true }).status === 0;

/** The main checkout, which a worktree finds through git's common directory. */
export function mainCheckout(repo = root) {
  const run = spawnGit(['rev-parse', '--path-format=absolute', '--git-common-dir'], { cwd: repo, encoding: 'utf8' });
  return run.status === 0 ? dirname(run.stdout.trim()) : repo;
}

/** `Lyntai` beside the main checkout. */
export function defaultSibling(repo = root) {
  return resolve(mainCheckout(repo), '..', 'Lyntai');
}

/**
 * Packs the sibling into the main checkout's feed, one feed every worktree shares, and writes the props both there (the merge
 * tool hands the main checkout's to its gate trees) and in this worktree. `pack(sibling, version, feed)` returns true on success.
 */
export function buildLocal({ repo = root, main = mainCheckout(repo), sibling, pack = dotnetPack, head = gitHead, say = console.log } = {}) {
  const props = join(repo, 'src', 'Daoris.Service', 'Directory.Packages.props');
  const released = existsSync(props) ? releasedVersion(readFileSync(props, 'utf8')) : null;
  if (!released) { say('No DaorisLyntaiReleased in Directory.Packages.props.'); return 2; }
  if (!sibling || !existsSync(sibling)) { say(`The Lyntai checkout is not at ${sibling}; give its path as an argument.`); return 1; }
  const { tip: sha, current } = head(sibling);
  if (!sha) { say(`${sibling} is not a git checkout with a master or main branch.`); return 1; }
  if (current !== sha) { say(`The Lyntai checkout holds ${current}, not its main line's tip ${sha}; check out its main line first.`); return 1; }
  const version = `${released}-local.${sha}`;
  const feed = join(main, 'local', 'lyntai-feed');
  const propsFiles = [...new Set([join(main, 'local', 'lyntai-local.props'), join(repo, 'local', 'lyntai-local.props')])];
  const written = (file) => existsSync(file) && readFileSync(file, 'utf8').includes(`<DaorisLyntaiVersion>${version}<`);
  const packed = existsSync(join(feed, `Lyntai.Core.${version}.nupkg`));
  if (packed && propsFiles.every(written)) {
    say(`Lyntai ${version} (commit ${sha}) is already packed and in use.`);
    return 0;
  }
  if (!packed) {
    mkdirSync(feed, { recursive: true });
    if (!pack(sibling, version, feed)) { say('dotnet pack failed.'); return 2; }
  }
  for (const file of propsFiles) writeAtomic(file, localProps({ version, feed }));
  say(`Lyntai commit ${sha}: ${version} is in ${feed}. The next restore uses it.`);
  return 0;
}

if (isMain(import.meta.url)) {
  const sibling = process.argv[2] ?? process.env.DAORIS_LYNTAI_PATH ?? defaultSibling();
  process.exitCode = buildLocal({ sibling: resolve(sibling) });
}
