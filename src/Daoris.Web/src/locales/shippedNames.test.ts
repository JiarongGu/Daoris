import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { areas } from '.';

/**
 * The census of the shipped workflows' words (SETUPUX1a, D221): the driver's data holds one English name and purpose for each workflow and
 * delivery and no per-language field, so the page words them from its own catalogue by the id, in every locale it ships. A workflow or a
 * delivery added to the data with no key here would be shown in English alone.
 */
const folder = join(process.cwd(), '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'Defaults', 'workflows');
type Shipped = { id: string; name: string; for: string; delivery: { id: string; name: string; for: string } };
const shipped: Shipped[] = readdirSync(folder).filter((file) => file.endsWith('.json'))
  .map((file) => JSON.parse(readFileSync(join(folder, file), 'utf8')) as Shipped);

describe('the shipped workflows and deliveries are worded in every locale', () => {
  const locales = Object.keys(areas) as (keyof typeof areas)[];

  it('reads the driver\'s data and at least the two shipped locales', () => {
    expect(shipped.length).toBeGreaterThanOrEqual(8);
    expect(locales).toEqual(expect.arrayContaining(['en', 'zh']));
    expect(shipped.every((each) => typeof each.name === 'string' && typeof each.delivery.id === 'string')).toBe(true);
  });

  it.each(['en', 'zh'] as const)('names every workflow and delivery id in %s', (locale) => {
    const catalogue = areas[locale]['workflow.shipped'] as Record<string, string>;
    const keys = shipped.flatMap((each) => [
      `workflow.shipped.${each.id}.name`, `workflow.shipped.${each.id}.for`,
      `workflow.shipped.delivery.${each.delivery.id}.name`, `workflow.shipped.delivery.${each.delivery.id}.for`,
    ]);
    expect(keys.filter((key) => !(catalogue[key]?.trim()))).toEqual([]);
  });

  it('holds the same keys in every locale, and the English ones say what the data says', () => {
    const [first, ...rest] = locales.map((locale) => Object.keys(areas[locale]['workflow.shipped'] as Record<string, string>).sort());
    for (const other of rest) expect(other).toEqual(first);
    const en = areas.en['workflow.shipped'] as Record<string, string>;
    for (const each of shipped) {
      expect(en[`workflow.shipped.${each.id}.name`]).toBe(each.name);
      expect(en[`workflow.shipped.delivery.${each.delivery.id}.name`]).toBe(each.delivery.name);
    }
  });
});
