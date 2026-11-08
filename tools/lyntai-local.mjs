#!/usr/bin/env node
/**
 * An opt-in local build of the Lyntai sibling: `node tools/lyntai-local.mjs [path-to-Lyntai-checkout]` (`sibling-local.mjs`
 * and `docs/development.md` say how). The checkout is the argument, `DAORIS_LYNTAI_PATH`, or `Lyntai` beside the main checkout.
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

export const releasedVersion = (propsText) => pinIn(propsText, spec.releasedProperty);

export const localProps = ({ version, feed }) => propsFor({ property: spec.versionProperty, version, feed });

export const defaultSibling = (repo) => siblingBeside('Lyntai', repo);

export const buildLocal = (options) => buildSiblingLocal(spec, options);

if (isMain(import.meta.url)) {
  const sibling = process.argv[2] ?? process.env.DAORIS_LYNTAI_PATH ?? defaultSibling();
  process.exitCode = buildLocal({ sibling: resolve(sibling) });
}
