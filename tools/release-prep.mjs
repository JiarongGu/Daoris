#!/usr/bin/env node
/**
 * Release preparation — set the version everywhere it appears, and stamp the
 * changelogs. Run by the release workflow; **never run by hand**.
 *
 * ## Why a tool rather than five edits
 *
 * The version lives in four files and the changelog headings in two more, and
 * they only mean anything when they agree. The family has already paid for both
 * halves of getting this wrong: a hand-bump made the next release skip a version
 * outright, and a hand-stamped heading left the workflow nothing to stamp, so a
 * release shipped with the previous version's title on its section.
 *
 * Consistency was never the property at risk — **authorship** was. A hand-bump
 * leaves everything perfectly consistent and still wrong.
 *
 *   node tools/release-prep.mjs --version 0.1.0     # rewrite and stamp
 *   node tools/release-prep.mjs --check             # assert agreement, write nothing
 */
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const read = (rel) => readFileSync(join(repoRoot, rel), 'utf8');
const write = (rel, text) => writeFileSync(join(repoRoot, rel), text, 'utf8');

const REPO_REF = 'github:JiarongGu/Daoris#v';

/**
 * The example family's manifests carry the same pin as the real one — a stale example teaches the
 * wrong thing with a straight face. Discovered rather than listed, so adding an example does not
 * require remembering this file.
 */
const exampleManifests = () =>
  existsSync(join(repoRoot, 'examples'))
    ? readdirSync(join(repoRoot, 'examples'), { withFileTypes: true })
        .filter((entry) => entry.isDirectory())
        .map((entry) => `examples/${entry.name}/daoris.json`)
        .filter((rel) => existsSync(join(repoRoot, rel)))
    : [];
const CLI_PKG = 'src/Daoris.Cli/package.json'; // the only published package
// The devkit binary's own version (REV3): its comment said the release stamped it, and nothing did.
const DEVKIT = 'src/Daoris.Devkit/Daoris.Devkit.Cli/Repository.cs';
const DEVKIT_VERSION = /public const string DevkitVersion = "([^"]+)";/;
const fail = (message) => {
  console.error(`release-prep: ${message}`);
  process.exit(1);
};

/**
 * The heading a release stamps. Refuses an empty section: a `### Added` with
 * nothing under it is exactly what a half-finished release leaves behind, and it
 * would satisfy any looser test. An unreleased section that is present and empty
 * is the signal that the work being released is not the work you think it is.
 */
function stampChangelog(rel, heading) {
  const text = read(rel);
  const match = /^## Unreleased.*$/m.exec(text);
  if (!match) {
    fail(
      `${rel} has no '## Unreleased' heading to stamp.\n` +
        `  Either it was stamped by hand — which is the workflow's job — or the commits\n` +
        `  you mean to release are not on the remote. Check the second one first.`,
    );
  }
  const after = text.slice(match.index + match[0].length);
  const body = after.split(/^## /m)[0];
  if (!/^\s*[-*]\s+\S/m.test(body)) {
    fail(
      `${rel}'s '## Unreleased' section has no entries.\n` +
        `  A release with nothing to say about it is almost always a release of the wrong\n` +
        `  tree — confirm the commits you mean to ship are pushed before writing prose.`,
    );
  }
  write(rel, text.slice(0, match.index) + heading + after);
}

/** Each file the version lives in, and how it is rewritten to carry `version`. */
const rewrites = (version) => [
  [CLI_PKG, (text) => text.replace(/"version": "[^"]+"/, `"version": "${version}"`)],
  ['canon/canon.json', () => `{\n  "version": "${version}"\n}\n`],
  ['daoris.json', (text) => text.replace(/github:[^"#]+#v[\d.]+/, `${REPO_REF}${version}`)],
  ['README.md', (text) => text.replace(/github:JiarongGu\/Daoris#v[\d.]+/g, `${REPO_REF}${version}`)],
  [DEVKIT, (text) => text.replace(DEVKIT_VERSION, `public const string DevkitVersion = "${version}";`)],
  ...exampleManifests().map((rel) =>
    [rel, (text) => text.replace(/github:[^"#]+#v[\d.]+/, `${REPO_REF}${version}`)]),
];

/** The changelogs a release stamps: the release-facing one first, then the canon's. */
const CHANGELOGS = ['CHANGELOG.md', 'canon/CHANGELOG.md'];

/**
 * Every file a release rewrites — what the release commit must stage (REV3 CLEAN1). The workflow's
 * `git add` is a second list, and it had already missed the example manifests once, which left
 * `verify` red on main after a release; `dogfood.test.ts` holds that list against this one.
 */
export const written = () => [...rewrites('0.0.0').map(([rel]) => rel), ...CHANGELOGS];

function setVersion(version, today) {
  if (!/^\d+\.\d+\.\d+$/.test(version)) fail(`'${version}' is not a semver triple`);

  for (const [rel, rewrite] of rewrites(version)) write(rel, rewrite(read(rel)));

  // The release-facing log carries the date; the canon's own log is read by
  // consumers upgrading between versions, where the version alone is the key.
  stampChangelog(CHANGELOGS[0], `## ${version} — ${today}`);
  stampChangelog(CHANGELOGS[1], `## ${version}`);

  console.log(`release-prep: set ${version} across package.json, canon.json, the manifest, the README and the devkit`);
  console.log(`release-prep: stamped CHANGELOG.md and canon/CHANGELOG.md`);
}

/** Assert every place agrees. The release gate runs this; it never rewrites. */
function checkAgreement() {
  const version = JSON.parse(read(CLI_PKG)).version;
  const problems = [];

  if (JSON.parse(read('canon/canon.json')).version !== version) {
    problems.push(`canon/canon.json is not ${version}`);
  }
  if (!JSON.parse(read('daoris.json')).source.endsWith(`#v${version}`)) {
    problems.push(`daoris.json does not pin ${version}`);
  }
  for (const ref of [...read('README.md').matchAll(/Daoris#v([\d.]+)/g)].map((m) => m[1])) {
    if (ref !== version) problems.push(`README pins ${ref}, not ${version}`);
  }
  for (const rel of exampleManifests()) {
    if (!JSON.parse(read(rel)).source.endsWith(`#v${version}`)) {
      problems.push(`${rel} does not pin ${version}`);
    }
  }
  const devkit = DEVKIT_VERSION.exec(read(DEVKIT))?.[1];
  if (devkit !== version) problems.push(`${DEVKIT} says ${devkit ?? 'no version'}, not ${version}`);
  if (problems.length) fail(`version drift:\n  ${problems.join('\n  ')}`);
  console.log(`release-prep: ${version} agrees across every shipped reference`);
}

// Guarded, because `written` is imported by a test — and importing this file must not release anything.
if (isMain(import.meta.url)) {
  const argv = process.argv.slice(2);
  if (argv.includes('--check')) {
    checkAgreement();
  } else {
    const at = argv.indexOf('--version');
    if (at === -1 || !argv[at + 1]) fail('usage: release-prep.mjs --version X.Y.Z | --check');
    const dateArg = argv.indexOf('--date');
    const today = dateArg === -1 ? new Date().toISOString().slice(0, 10) : argv[dateArg + 1];
    setVersion(argv[at + 1], today);
  }
}
