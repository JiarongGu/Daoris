import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import i18n from './i18n';
import { ago, compact, sittingDays } from './format';

describe('compact', () => {
  it('keeps small counts as locale numbers', () => {
    expect(compact(854)).toBe('854');
  });

  it('compacts ten-thousands to K and drops a trailing .0', () => {
    expect(compact(12_900)).toBe('12.9K');
    expect(compact(12_000)).toBe('12K');
  });

  it('compacts millions to M', () => {
    expect(compact(4_200_000)).toBe('4.2M');
  });
});

describe('ago and sittingDays', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-19T12:00:00Z'));
  });
  afterEach(() => vi.useRealTimers());

  it('says just now under a minute', () => {
    expect(ago('2026-09-19T11:59:30Z')).toBe('just now');
  });

  it('counts minutes, hours, then days', () => {
    expect(ago('2026-09-19T11:15:00Z')).toBe('45m ago');
    expect(ago('2026-09-19T05:00:00Z')).toBe('7h ago');
    expect(ago('2026-09-16T12:00:00Z')).toBe('3d ago');
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    expect(ago('2026-09-16T12:00:00Z')).toBe('3天前');
    await i18n.changeLanguage('en');
  });

  it('counts whole sitting days — the number the Overview leads with', () => {
    expect(sittingDays('2026-09-07T00:00:00Z')).toBe(12);
  });
});
