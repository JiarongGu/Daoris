import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import { checkLocales, languagesIn } from '../../scripts/i18n-check.mjs';

// The parity gate holds every catalogue present, `en` the source, so a language added as a folder is held the moment it exists.
const ROOT = join(process.cwd(), '..', '..', '_fixtures', 'i18n-check', 'planted');

function plant(files: Record<string, Record<string, string>>) {
  rmSync(ROOT, { recursive: true, force: true });
  for (const [path, keys] of Object.entries(files)) {
    mkdirSync(dirname(join(ROOT, path)), { recursive: true });
    writeFileSync(join(ROOT, path), JSON.stringify(keys), 'utf8');
  }
}

afterEach(() => rmSync(ROOT, { recursive: true, force: true }));

const BASE = {
  'en/nav.json': { 'nav.quests': 'Quests', 'nav.count': '{{count}} quests' },
  'zh/nav.json': { 'nav.quests': '委托', 'nav.count': '{{count}} 个委托' },
};

describe('the parity gate across every catalogue present', () => {
  it('finds the languages as the folders present, English first', () => {
    plant({ ...BASE, 'fr/nav.json': { 'nav.quests': 'Quêtes', 'nav.count': '{{count}} quêtes' } });
    expect(languagesIn(ROOT)).toEqual(['en', 'fr', 'zh']);
  });

  it('passes with English and Chinese alone', () => {
    plant(BASE);
    expect(checkLocales(ROOT).problems).toEqual([]);
  });

  it('fails a third catalogue missing a key, and passes once it holds it', () => {
    plant({ ...BASE, 'fr/nav.json': { 'nav.quests': 'Quêtes' } });
    const missing = checkLocales(ROOT).problems.join('\n');
    expect(missing).toContain('missing in fr/nav.json');
    expect(missing).toContain('nav.count');

    plant({ ...BASE, 'fr/nav.json': { 'nav.quests': 'Quêtes', 'nav.count': '{{count}} quêtes' } });
    expect(checkLocales(ROOT).problems).toEqual([]);
  });

  it('fails a third catalogue holding a key English lacks, one without an area, and one that drops a placeholder', () => {
    plant({ ...BASE, 'fr/nav.json': { 'nav.quests': 'Quêtes', 'nav.count': '{{count}} quêtes', 'nav.old': 'Vieux' } });
    expect(checkLocales(ROOT).problems.join('\n')).toContain('in fr/nav.json but not en (stale)');

    plant({ ...BASE, 'fr/nav.json': { 'nav.quests': 'Quêtes', 'nav.count': 'quêtes' } });
    expect(checkLocales(ROOT).problems.join('\n')).toContain('nav.count: fr drops {{count}}');

    plant({ ...BASE, 'fr/other.json': { 'other.x': 'X' } });
    const areas = checkLocales(ROOT).problems.join('\n');
    expect(areas).toContain('en has no other.json (fr has)');
    expect(areas).toContain('fr has no nav.json (en has)');
  });
});
