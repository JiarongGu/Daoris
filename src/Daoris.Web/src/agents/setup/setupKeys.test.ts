import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { areas } from '../../locales';

// SETUPUX1j (the setup-UX design §5): the set-up's own delivery names and lines are gone. A delivery is worded by its id from the page's
// catalogues (`workflow.shipped.delivery.*`) and *Not driven* by a chrome key, so a key census finds the old ones gone from both languages
// and from every source that read them.

const GONE = ['agents.setup.' + 'defaultName', 'agents.setup.set.' + 'line.'];

const sources = (folder: string): string[] =>
  readdirSync(folder, { withFileTypes: true }).flatMap((entry) => {
    const path = join(folder, entry.name);
    if (entry.isDirectory()) return entry.name === 'locales' ? [] : sources(path);
    return /\.(ts|tsx)$/.test(entry.name) && !/\.(test|stories)\.tsx?$/.test(entry.name) ? [path] : [];
  });

describe('the set-up\'s old delivery words are gone', () => {
  it.each(['en', 'zh'] as const)('holds no %s key for them, and the chrome keys that replace them', (locale) => {
    const keys = Object.keys(areas[locale].agents as Record<string, string>);
    expect(keys.filter((key) => GONE.some((prefix) => key.startsWith(prefix)))).toEqual([]);
    for (const key of ['agents.setup.set.keep', 'agents.setup.set.notDriven', 'agents.setup.set.lands']) expect(keys).toContain(key);
  });

  it('is read by no source, and no `PRESETS` table stands for the deliveries', () => {
    const found = sources(join(process.cwd(), 'src')).filter((file) => {
      const text = readFileSync(file, 'utf8');
      return GONE.some((prefix) => text.includes(prefix)) || /export const PRESETS\b[^\n]*not-driven/.test(text);
    });
    expect(found).toEqual([]);
  });
});
