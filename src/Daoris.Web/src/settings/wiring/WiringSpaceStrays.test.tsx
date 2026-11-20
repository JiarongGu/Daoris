import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { size } from '../../locales/format';
import { show } from '../../test/shellHarness';
import { WiringSpace } from './WiringSpace';
import { SPACE, SPACE_STRAYS } from './wiringFixtures';

describe('Disk space: paths no entry of the home names', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  const row = (label: string) => within(screen.getByText(label).closest('[data-setting-label]')!.parentElement!);

  it('names each path with its size, and says it in 中文', async () => {
    show(<WiringSpace space={SPACE_STRAYS} onCount={vi.fn()} />);
    const strays = row('Not in the table');
    expect(strays.getByText('data/harnesses')).toBeTruthy();
    expect(strays.getByText('data/old.json')).toBeTruthy();
    // The row's total and the first path's own size read alike at this size.
    expect(strays.getAllByText(size(3 * 1024 ** 3))).toHaveLength(2);

    await i18n.changeLanguage('zh');
    expect(row('不在表中').getByText('data/old.json')).toBeTruthy();
  });

  it('shows no row where none is named', () => {
    show(<WiringSpace space={{ ...SPACE, strays: { paths: [], partial: false } }} onCount={vi.fn()} />);
    expect(screen.queryByText('Not in the table')).toBeNull();
  });
});
