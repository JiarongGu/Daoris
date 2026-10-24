import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, readFileSync, rmSync } from 'node:fs';
import { delimiter, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { extractTarGz } from '../src/platform/tarball.ts';

/**
 * GITHUBDOCS1 (D211): the README npm shows is the one the packed CLI carries, so it is read from the tarball, not the
 * tree. The package is packed whole, its prepack (the build and the stager) and postpack included, from a copy of what
 * the pack reads, laid out as in the repository under `_fixtures/`: packing the CLI's own folder would stage `dist/`
 * and `canon/` into it while the other tool-test files run beside this one. Run by the `tool-tests` gate.
 */
// reaches: tools/stage-package.mjs — the copy's prepack runs it
// reaches: README.md, CHANGELOG.md, LICENSE, canon — the stager copies them into the package

const tool = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-branch.test.ts documents
  '../../../tools/doc-links.mjs') as {
  shippedReadmeFindings: (text: string, file?: string) => string[];
  linkFindings: (files: string[], read: (file: string) => string) => { findings: string[]; counts: { absolute: number } };
  REPOSITORY: string;
};
const fsx = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../tools/fsx.mjs') as { copyTree: (from: string, to: string) => void; repositoryFiles: (root: string) => string[] };

const repoRoot = dirname(dirname(dirname(dirname(fileURLToPath(import.meta.url)))));
const scratch = join(repoRoot, '_fixtures', 'packed-readme');

/** The CLI packed as npm would publish it, unpacked: the folder holding `package/`. */
async function packed(): Promise<string> {
  rmSync(scratch, { recursive: true, force: true });
  const copy = join(scratch, 'repo');
  const cli = join(copy, 'src', 'Daoris.Cli');
  mkdirSync(join(copy, 'tools'), { recursive: true });
  for (const file of ['README.md', 'CHANGELOG.md', 'LICENSE', 'tools/stage-package.mjs', 'tools/fsx.mjs']) {
    copyFileSync(join(repoRoot, file), join(copy, file));
  }
  fsx.copyTree(join(repoRoot, 'canon'), join(copy, 'canon'));
  for (const part of ['bin', 'src']) fsx.copyTree(join(repoRoot, 'src', 'Daoris.Cli', part), join(cli, part));
  for (const file of ['package.json', 'tsconfig.json', 'tsconfig.build.json']) copyFileSync(join(repoRoot, 'src', 'Daoris.Cli', file), join(cli, file));

  // The build's tsc and @types/node are the workspace's: the copy has no node_modules of its own.
  const env = { ...process.env, PATH: `${join(repoRoot, 'node_modules', '.bin')}${delimiter}${process.env.PATH ?? ''}` };
  const out = execSync(`npm pack --pack-destination "${scratch}"`, { cwd: cli, env, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  const tarball = join(scratch, out.trim().split('\n').pop()!.trim());
  assert.ok(existsSync(tarball), `npm pack made no tarball: ${out}`);
  assert.ok(!existsSync(join(cli, 'dist')) && !existsSync(join(cli, 'README.md')), 'postpack left the staged files behind');
  const unpacked = join(scratch, 'unpacked');
  mkdirSync(unpacked, { recursive: true });
  await extractTarGz(tarball, unpacked);
  return join(unpacked, 'package');
}

test('the packed CLI\'s README reads right as npm renders it, and the changelog, icon and keywords ship with it', { timeout: 300_000 }, async () => {
  const pkg = await packed();
  try {
    const readme = readFileSync(join(pkg, 'README.md'), 'utf8');
    assert.equal(readme, readFileSync(join(repoRoot, 'README.md'), 'utf8'), 'the package carries the root README as it is');

    // npm renders it away from the repository: only absolute links have a target, and raw HTML is dropped.
    assert.deepEqual(tool.shippedReadmeFindings(readme, 'package/README.md'), []);
    // Every absolute link into the repository names a file a clone holds, the packed README read in place of the root's.
    const files = fsx.repositoryFiles(repoRoot);
    const { findings, counts } = tool.linkFindings(files,
      (file) => (file === 'README.md' ? readme : readFileSync(join(repoRoot, file), 'utf8')));
    assert.deepEqual(findings.filter((f) => f.startsWith('README.md:')), []);
    assert.ok(counts.absolute >= 10, `the README links absolutely ${counts.absolute} times`);

    // The page opens on the product: the logo an absolute markdown image in the title, the pitch, the quickstart.
    const [title] = readme.split('\n');
    assert.match(title!, new RegExp(`^# !\\[[^\\]]*\\]\\(https://raw\\.githubusercontent\\.com/${tool.REPOSITORY}/main/docs/assets/daoris-logo\\.svg\\) Daoris`));
    assert.ok(files.includes('docs/assets/daoris-logo.svg') && files.includes('docs/assets/daoris-icon.png'), 'the logo and the icon are tracked');
    for (const heading of ['## Quickstart', '## Packages', '## Status', '## Rules for coding agents']) assert.ok(readme.includes(`\n${heading}\n`), heading);
    assert.match(readme, /npx daoris@\d+\.\d+\.\d+ init/);

    // The changelog is the package's release notes; the manifest says what it serves and where it came from.
    assert.equal(readFileSync(join(pkg, 'CHANGELOG.md'), 'utf8'), readFileSync(join(repoRoot, 'CHANGELOG.md'), 'utf8'));
    const manifest = JSON.parse(readFileSync(join(pkg, 'package.json'), 'utf8')) as { keywords?: string[]; repository?: { url: string }; homepage?: string };
    assert.ok((manifest.keywords ?? []).includes('agents-md') && (manifest.keywords ?? []).length >= 5, 'keywords');
    assert.equal(manifest.repository?.url, `git+https://github.com/${tool.REPOSITORY}.git`);
    assert.equal(manifest.homepage, `https://github.com/${tool.REPOSITORY}#readme`);
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
});
