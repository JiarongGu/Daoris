import { describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { landingIntegrateSays, landingProcessSays } from './Landings';

// The old landing rule is read, never edited (WORKFLOWCLEAN1h, D228): its sentences are what a repository's or a workspace's Landing row says
// under Current.

describe("the landing rule's sentences", () => {
  const says = { form: 'branch', integrate: { batch: 4, order: 'arrival', gates: 'declared' } };

  it('is worded in 中文 as in English, and its row says the batch, order and gates', async () => {
    expect(landingIntegrateSays(i18n.t.bind(i18n), says)).toEqual(['Integrated into the line: batch 4, oldest first, gates declared.']);
    expect(landingIntegrateSays(i18n.t.bind(i18n), { form: 'merge' })).toEqual([]);
    await i18n.changeLanguage('zh');
    try {
      expect(landingIntegrateSays(i18n.t.bind(i18n), says)).toEqual(['集成到主线：每个候选 4 个成员，先到先集成，按声明运行检查项。']);
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('names what finds a ticket and what squashes, for a branch only', () => {
    const t = i18n.t.bind(i18n);
    expect(landingProcessSays(t, { form: 'merge', squash: true })).toEqual([]);
    expect(landingProcessSays(t, { form: 'branch', pattern: 'work/{quest}', squash: true })).toHaveLength(1);
    expect(landingProcessSays(t, { form: 'branch', pattern: 'work/{ticket}' })).toHaveLength(1);
  });
});
