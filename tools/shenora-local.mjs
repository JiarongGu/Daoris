#!/usr/bin/env node
/**
 * An opt-in local build of the Shenora desktop kit for development.
 *
 *   node tools/shenora-local.mjs [path-to-Shenora-checkout]
 *
 * Packs the sibling's main line (`main`, as its checkout holds it; never a session branch) at `<released>-local.<sha7>`, all
 * three packages Daoris references (`Shenora`, `Shenora.Windows`, `Shenora.Chromium`) at the one version, into the ignored
 * `local/shenora-feed/`, and writes the ignored `local/shenora-local.props`, which the desktop's package props import whenever
 * it exists. It does nothing when the tip is already packed. The merge tool copies the props into its gate trees. Delete it
 * to restore the released pin.
 * The checkout is the argument, `DAORIS_SHENORA_PATH`, or `Shenora` beside the main checkout. It is only read.
 * The feed and props go under the main checkout's `local/` even from a worktree; the worktree gets a copy of the props.
 *
 * Exit codes: 0 done, 1 the sibling is not where it should be, 2 a tool error.
 */
import { resolve } from 'node:path';
import { isMain } from './fsx.mjs';
import { buildSiblingLocal, defaultSibling as siblingBeside, localProps as propsFor, releasedVersion as pinIn } from './sibling-local.mjs';

const spec = {
  label: 'Shenora',
  releasedProperty: 'DaorisShenoraReleased',
  versionProperty: 'DaorisShenoraVersion',
  pinProps: ['src', 'Daoris.Desktop', 'Directory.Packages.props'],
  feedName: 'shenora-feed',
  propsName: 'shenora-local.props',
  probe: 'Shenora.Chromium',
  projects: ['src/Shenora/Shenora.csproj', 'src/Shenora.Windows/Shenora.Windows.csproj', 'src/Shenora.Chromium/Shenora.Chromium.csproj'],
};

/** The released pin, read from the props that declare it. */
export const releasedVersion = (propsText) => pinIn(propsText, spec.releasedProperty);

/** The props the opt-in build imports: the local version and the feed as an added restore source. */
export const localProps = ({ version, feed }) => propsFor({ property: spec.versionProperty, version, feed });

/** `Shenora` beside the main checkout. */
export const defaultSibling = (repo) => siblingBeside('Shenora', repo);

export const buildLocal = (options) => buildSiblingLocal(spec, options);

if (isMain(import.meta.url)) {
  const sibling = process.argv[2] ?? process.env.DAORIS_SHENORA_PATH ?? defaultSibling();
  process.exitCode = buildLocal({ sibling: resolve(sibling) });
}
