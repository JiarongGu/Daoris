import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { ATTENTION_GROUP, attentionOffers, attentionWorded, type PushedAttention } from './attention';

// A delivery plan's open question as a row of What needs you and the bell: the owner's rule for questions is that it says what it is, what
// yes does and what it blocks, never an id alone, in each shipped language.

const ROW: PushedAttention = {
  kind: 'plan-question', id: '7c1e9a04b2d5:contract:viewers', since: '2026-11-14T09:00:00Z', ask: '7c1e9a04b2d5',
  question: { key: 'contract:viewers', kind: 'open', text: 'Do admins or viewers see candidates?', yes: 'Both see them.', blocks: 'The web app’s release.', default: 'Admins only.' },
};

describe('a delivery plan’s question on What needs you', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', 'Yes: Both see them. · Waits on it: The web app’s release. · If not answered: Admins only.'],
    ['zh', '选是：Both see them. · 等待它：The web app’s release. · 不回答则：Admins only.'],
  ])('says what it is, what yes does and what it blocks, in %s', async (language, detail) => {
    await i18n.changeLanguage(language);

    const worded = attentionWorded(ROW);

    expect(worded.title).toBe('Do admins or viewers see candidates?');
    expect(worded.detail).toBe(detail);
    expect(worded.ask).toBe('7c1e9a04b2d5');
  });

  it('waits for the person’s word, and its door opens the ask where it is answered', () => {
    expect(ATTENTION_GROUP['plan-question']).toBe('word');
    expect(attentionOffers(attentionWorded(ROW))).toEqual([]);
  });
});
