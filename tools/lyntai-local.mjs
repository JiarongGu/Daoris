#!/usr/bin/env node
/**
 * An opt-in local build of the Lyntai sibling for development.
 *
 *   node tools/lyntai-local.mjs [path-to-Lyntai-checkout]
 *
 * Packs the sibling's main line (`master`, as its checkout holds it; never a session branch), and does nothing when the
 * tip is already packed, at `<released>-local.<sha7>` into the ignored `local/lyntai-feed/` and writes the ignored
 * `local/lyntai-local.props`, which the service's and the desktop's package props import whenever it exists. The merge tool copies it
 * into its gate trees. Delete it to restore the released pin.
 * The checkout is the argument, `DAORIS_LYNTAI_PATH`, or `Lyntai` beside the main checkout. It is only read.
 * The feed and props go under the main checkout's `local/` even from a worktree; the worktree gets a copy of the props.
 *
 * Exit codes: 0 done, 1 the sibling is not where it should be, 2 a tool error.
 */
import { resolve } from 'node:path';
import { isMain } from './fsx.mjs';
import { buildSiblingLocal, defaultSibling as siblingBeside, localProps as propsFor, releasedVersion as pinIn } from './sibling-local.mjs';

const spec = {
  label: 'Lyntai',
  releasedProperty: 'DaorisLyntaiReleased',
  versionProperty: 'DaorisLyntaiVersion',
  pinProps: ['src', 'Daoris.Service', 'Directory.Packages.props'],
  feedName: 'lyntai-feed',
  propsName: 'lyntai-local.props',
  probe: 'Lyntai.Core',
};

/** The released pin, read from the props that declare it. */
export const releasedVersion = (propsText) => pinIn(propsText, spec.releasedProperty);

/** The props the opt-in build imports: the local version and the feed as an added restore source. */
export const localProps = ({ version, feed }) => propsFor({ property: spec.versionProperty, version, feed });

/** `Lyntai` beside the main checkout. */
export const defaultSibling = (repo) => siblingBeside('Lyntai', repo);

export const buildLocal = (options) => buildSiblingLocal(spec, options);

if (isMain(import.meta.url)) {
  const sibling = process.argv[2] ?? process.env.DAORIS_LYNTAI_PATH ?? defaultSibling();
  process.exitCode = buildLocal({ sibling: resolve(sibling) });
}
