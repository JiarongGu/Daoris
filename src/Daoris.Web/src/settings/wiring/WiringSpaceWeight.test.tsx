import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { size } from '../../locales/format';
import { show } from '../../test/shellHarness';
import { WiringSpace } from './WiringSpace';
import { SPACE, SPACE_WEIGHT } from './wiringFixtures';

describe('Disk space: what the home weighs', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  const row = (label: string) => within(screen.getByText(label).closest('[data-setting-label]')!.parentElement!);

  it('names a place over its budget, the largest files and identical files, and says it in 中文', async () => {
    show(<WiringSpace space={SPACE_WEIGHT} onCount={vi.fn()} />);
    const over = row('Over its budget');
    expect(over.getByText('work/trees/games/Daoris/s-aaaa1111')).toBeTruthy();
    expect(over.getByText(`${size(5 * 1024 ** 3)} of ${size(4 * 1024 ** 3)}`)).toBeTruthy();
    expect(row('Largest files').getByText('data/knowledge.db')).toBeTruthy();
    const copies = row('Identical files');
    expect(copies.getByText('work/trees/games/Daoris/s-bbbb2222/esbuild.exe')).toBeTruthy();
    expect(copies.getByText(`${size(2 * 1024 ** 2)} lost`)).toBeTruthy();

    await i18n.changeLanguage('zh');
    expect(row('超出预算').getByText('work/trees/games/Daoris/s-aaaa1111')).toBeTruthy();
  });

  it('shows no row where the home is within its budgets, and none while the first count runs', () => {
    show(<WiringSpace space={{ ...SPACE, weight: { over: [], largest: [], duplicates: [], partial: false } }} onCount={vi.fn()} />);
    expect(screen.queryByText('Over its budget')).toBeNull();
    expect(screen.queryByText('Largest files')).toBeNull();
    expect(screen.queryByText('Identical files')).toBeNull();
  });
});
