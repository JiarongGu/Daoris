import { describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { attentionActs, attentionGroup, attentionWorded, bellCount, placeOf, type PushedAttention } from './attention';

describe('a check after the quest is done', () => {
  it.each(['en', 'zh'])('names the check and commit for the person in %s', async (language) => {
    await i18n.changeLanguage(language);
    const pushed: PushedAttention = {
      kind: 'check-failed', id: 'a1', since: '2026-11-14T04:39:00Z', session: 's1', workspace: 'studio', where: 'engine',
      check: { name: 'unit-tests', tip: 'abc123', tail: 'Assert.Equal failed', state: 'failed' },
    };

    const row = attentionWorded(pushed);

    expect(row.title).toContain('unit-tests');
    expect(row.detail).toContain('abc123');
    expect(row.detail).toContain('Assert.Equal failed');
    expect(row.session).toBe('s1');
    expect(attentionGroup(row)).toBe('word');
    expect(placeOf(row, undefined)).toBe('sessions');
    expect(bellCount([pushed], [])).toBe(1);
    expect(attentionActs(row)).toEqual([]);
    await i18n.changeLanguage('en');
  });
});
