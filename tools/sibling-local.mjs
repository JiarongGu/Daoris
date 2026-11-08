/**
 * The opt-in local build of a family sibling, one way for every sibling (Lyntai, Shenora).
 *
 * A sibling's spec names its pin (the property that holds the released version, the props file that declares it), the
 * property the local props set, the ignored feed and props under the main checkout's `local/`, and the projects to pack.
 * Only the sibling's checkout is read; the pack goes to this repository's feed.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnGit, writeAtomic } from './fsx.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');

/** The released pin, read from the props text that declares it in `property`. */
export function releasedVersion(propsText, property) {
  return new RegExp(`<${property}>([^<]+)</${property}>`).exec(propsText)?.[1].trim() ?? null;
}

/** The props the opt-in build imports: the local version and the feed as an added restore source. */
export function localProps({ property, version, feed }) {
  const source = feed.replaceAll('\\', '/');
  return `<Project>\n  <PropertyGroup>\n    <${property}>${version}</${property}>\n`
    + `    <RestoreAdditionalProjectSources>$(RestoreAdditionalProjectSources);${source}</RestoreAdditionalProjectSources>\n`
    + `  </PropertyGroup>\n</Project>\n`;
}

const revision = (cwd, ref) => {
  const run = spawnGit(['rev-parse', '--verify', '--short=7', `${ref}^{commit}`], { cwd, encoding: 'utf8' });
  return run.status === 0 ? run.stdout.trim() : null;
};

/** The tip of the sibling's main line (`master`, else `main`) and the commit its checkout holds. */
export const gitHead = (cwd) => ({ tip: revision(cwd, 'master') ?? revision(cwd, 'main'), current: revision(cwd, 'HEAD') });

/** A pack of each of `projects` (paths under the sibling; the checkout itself where none), all at one version into one feed. */
export const dotnetPack = (projects = ['.']) => (sibling, version, feed) =>
  projects.every((project) => spawnSync('dotnet', ['pack', resolve(sibling, project), '-c', 'Release', `-p:Version=${version}`, '-o', feed, '-nodeReuse:false'],
    { cwd: sibling, stdio: 'inherit', windowsHide: true }).status === 0);

/** The main checkout, which a worktree finds through git's common directory. */
export function mainCheckout(repo = root) {
  const run = spawnGit(['rev-parse', '--path-format=absolute', '--git-common-dir'], { cwd: repo, encoding: 'utf8' });
  return run.status === 0 ? dirname(run.stdout.trim()) : repo;
}

/** The sibling named `name` beside the main checkout. */
export function defaultSibling(name, repo = root) {
  return resolve(mainCheckout(repo), '..', name);
}

/**
 * Packs the sibling into the main checkout's feed, one feed every worktree shares, and writes the props both there (the merge
 * tool hands the main checkout's to its gate trees) and in this worktree. `pack(sibling, version, feed)` returns true on success.
 */
export function buildSiblingLocal(spec, { repo = root, main = mainCheckout(repo), sibling, pack = dotnetPack(spec.projects), head = gitHead, say = console.log } = {}) {
  const pins = join(repo, ...spec.pinProps);
  const released = existsSync(pins) ? releasedVersion(readFileSync(pins, 'utf8'), spec.releasedProperty) : null;
  if (!released) { say(`No ${spec.releasedProperty} in ${spec.pinProps.at(-1)}.`); return 2; }
  if (!sibling || !existsSync(sibling)) { say(`The ${spec.label} checkout is not at ${sibling}; give its path as an argument.`); return 1; }
  const { tip: sha, current } = head(sibling);
  if (!sha) { say(`${sibling} is not a git checkout with a master or main branch.`); return 1; }
  if (current !== sha) { say(`The ${spec.label} checkout holds ${current}, not its main line's tip ${sha}; check out its main line first.`); return 1; }
  const version = `${released}-local.${sha}`;
  const feed = join(main, 'local', spec.feedName);
  const propsFiles = [...new Set([join(main, 'local', spec.propsName), join(repo, 'local', spec.propsName)])];
  const written = (file) => existsSync(file) && readFileSync(file, 'utf8').includes(`<${spec.versionProperty}>${version}<`);
  const packed = existsSync(join(feed, `${spec.probe}.${version}.nupkg`));
  if (packed && propsFiles.every(written)) {
    say(`${spec.label} ${version} (commit ${sha}) is already packed and in use.`);
    return 0;
  }
  if (!packed) {
    mkdirSync(feed, { recursive: true });
    if (!pack(sibling, version, feed)) { say('dotnet pack failed.'); return 2; }
  }
  for (const file of propsFiles) writeAtomic(file, localProps({ property: spec.versionProperty, version, feed }));
  say(`${spec.label} commit ${sha}: ${version} is in ${feed}. The next restore uses it.`);
  return 0;
}
