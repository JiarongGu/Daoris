import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { join } from 'node:path';
import { copyTree } from '../fsx.mjs';

/**
 * Bundle the doctrine tool the install carries into `into`: every module of `src/Daoris.Cli/src` as one `daoris.mjs`, the workspace's
 * canon beside it as `canon/`, and a manifest of the package's name and version, which `daoris --version` and the
 * publish's own check read. The release's npm package is made by `npm pack` and is not this.
 *
 * The bundler is esbuild, which the web build installs; the CLI has no runtime dependency, so nothing else goes into the file.
 *
 * @returns the package's version.
 */
export async function bundleCli(repoRoot, into) {
  const cliRoot = join(repoRoot, 'src', 'Daoris.Cli');
  const { build } = createRequire(join(repoRoot, 'src', 'Daoris.Web', 'package.json'))('esbuild');
  rmSync(into, { recursive: true, force: true });
  mkdirSync(into, { recursive: true });
  await build({
    entryPoints: [join(repoRoot, 'tools', 'desktop-publish', 'cli-bundle-entry.mjs')],
    outfile: join(into, 'daoris.mjs'),
    bundle: true,
    platform: 'node',
    format: 'esm',
    target: 'node22',
    legalComments: 'none',
    logLevel: 'warning',
  });
  copyTree(join(repoRoot, 'canon'), join(into, 'canon'));
  const { name, version } = JSON.parse(readFileSync(join(cliRoot, 'package.json'), 'utf8'));
  writeFileSync(join(into, 'package.json'), `${JSON.stringify({ name, version, type: 'module' }, null, 2)}\n`);
  return version;
}
