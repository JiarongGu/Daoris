import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { ATTENTION_GROUP, attentionWorded, type PushedAttention } from './attention';

// A design pass's closed question as a row of What needs you: it says what it is, what yes and what no change and where to look,
// as the comparison wrote them, in each shipped language; the answer is drafted as a decision the person keeps.

const ROW: PushedAttention = {
  kind: 'design-question', id: 'b7:Q1', since: '2026-11-16T09:00:00Z', brainstorm: 'b7', options: ['yes', 'no'],
  design: { key: 'Q1', question: 'Evolve the engine in place?', yes: 'One engine.', no: 'A second engine.', where: 'docs/areas/workflows.md' },
};

describe('a design pass’s question on What needs you', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', 'Yes: One engine. · No: A second engine. · Look at docs/areas/workflows.md'],
    ['zh', '是：One engine. · 否：A second engine. · 参见 docs/areas/workflows.md'],
  ])('says what yes and no change and where to look, in %s', async (language, detail) => {
    await i18n.changeLanguage(language);

    const worded = attentionWorded(ROW);

    expect(worded.title).toBe('Evolve the engine in place?');
    expect(worded.detail).toBe(detail);
  });

  it('waits for the person’s word', () => {
    expect(ATTENTION_GROUP['design-question']).toBe('word');
  });
});
