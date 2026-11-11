import { existsSync, mkdirSync, readFileSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { copyTree, renameHeld, writeAtomic } from '../fsx.mjs';

/**
 * The engine's locale files an install keeps, for the window and the browser alike, which share one
 * Chromium: the two languages Daoris speaks, and the two `EngineBrowser.Locale` ever asks
 * for. The kit lays out only these languages (`ShenoraChromiumLocales` names zh-CN; en-US is its
 * fallback), with grammatical-gender stubs beside each that the publish leaves out; the deployment
 * gate reads the two back off the install.
 */
export const KEPT_LOCALES = Object.freeze(['en-US.pak', 'zh-CN.pak']);

/** The shell's own home, which it creates on first start. */
export const HOME = 'data';

/**
 * Where an install carries Daoris's own example plugins as offers, as path segments:
 * beside the application in `app/`, never under `data/plugins/`, so none is installed until a person
 * presses Install (or runs `daoris plugin add --offer <id>`). A twin of the CLI's `OFFERS_DIR` and the
 * driver's `PluginOffers.Layout`, which find them; `desktop-publish.test.ts` reads all three.
 */
export const PLUGIN_OFFERS = Object.freeze(['app', 'plugin-offers']);

/**
 * The tracked examples an install offers: the two that land work, and the one that hands a
 * session the install's own browser. Not `hold-by-title`, which the rehearsals install as their
 * fixture, and not `browser`, which launches a browser of its own for a machine without the shell and
 * claims the same server name as `daoris-in-app-browser`, so the second installed would contribute nothing.
 */
export const OFFERED_PLUGINS = Object.freeze(['daoris-github', 'daoris-azure-devops', 'daoris-in-app-browser']);

/**
 * Lay the offers out in an install: each of {@link OFFERED_PLUGINS} copied whole from `examples` into
 * `<install>/app/plugin-offers/`, which is replaced wholesale — staged beside, then swapped — so a stale
 * file or an offer dropped since does not survive a republish. Nothing is written under the home.
 *
 * The swap goes through `renameHeld`: a scanner holding the staging folder just copied stopped a deployment rehearsal's
 * publish with EPERM (2026-10-08), the one rename the held-folder sweep had left bare.
 *
 * @returns the ids laid out, in the order offered.
 * @throws when an offered example has no `plugin.json`, before anything is replaced.
 */
export function layOffers(examples, install, renameOptions = {}) {
  const missing = OFFERED_PLUGINS.filter((id) => !existsSync(join(examples, id, 'plugin.json')));
  if (missing.length > 0) {
    throw new Error(`desktop-publish: no plugin.json for the offered ${missing.join(', ')} under ${examples}`);
  }

  const target = join(install, ...PLUGIN_OFFERS);
  const parent = dirname(target);
  mkdirSync(parent, { recursive: true });
  const staged = join(parent, `.${PLUGIN_OFFERS.at(-1)}-staging`);
  rmSync(staged, { recursive: true, force: true });
  for (const id of OFFERED_PLUGINS) copyTree(join(examples, id), join(staged, id));
  rmSync(target, { recursive: true, force: true });
  renameHeld(staged, target, renameOptions);
  return [...OFFERED_PLUGINS];
}

/**
 * Where an install carries the list built in, as path segments: beside the application
 * in `app/`, where the driver reads it from its own folder, never under `data/`, since a republish
 * replaces it whole and a newer list arrives as a resource location. A twin of the CLI's
 * `BUILT_IN_LAYOUT`, which finds it beside the home, and the driver's `ToolResources.Layout`;
 * `desktop-publish.test.ts` reads all three.
 */
export const RESOURCES = Object.freeze(['app', 'resources.json']);

/**
 * The list's one source in the workspace: the driver's own, which every build carries beside itself. A resource, not a source
 * the driver's module folders take: the driver's project file copies it from the project's top, so it is
 * named there, and `desktop-publish.test.ts` holds the two together.
 */
export const RESOURCES_SOURCE = Object.freeze(['src', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'resources.json']);

/**
 * Lay the list built in out in an install: `source` copied byte for byte to `<install>/app/resources.json`,
 * written beside and renamed, so a republish replaces it whole and a stopped one leaves the last. The
 * bytes are the tracked file's, so the hash the screen shows for the list built in is the list's own.
 *
 * @returns the path written.
 * @throws when `source` does not read as a schema 1 list, before anything is written: an install never
 *   carries a list its own readers refuse whole.
 */
export function layResources(source, install) {
  let list;
  try {
    list = JSON.parse(readFileSync(source, 'utf8').replace(/^﻿/, ''));
  } catch (error) {
    throw new Error(`desktop-publish: ${source} is not readable JSON (${error.message})`);
  }
  if (list === null || typeof list !== 'object' || Array.isArray(list) || list.schema !== 1) {
    throw new Error(`desktop-publish: ${source} is not a schema 1 resources.json, which every reader of the install refuses`);
  }

  const target = join(install, ...RESOURCES);
  writeAtomic(target, readFileSync(source));
  return target;
}
