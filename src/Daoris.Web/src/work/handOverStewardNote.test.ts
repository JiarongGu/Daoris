import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { NotePart } from '../data/api';
import { noteLines } from './noteLines';

const asked: NotePart = { code: 'handover.asked-steward', values: { agent: 'codex-acp' }, text: '…' };

describe("a hand-over the steward asked", () => {
  it('names the steward, never the person, in English and 中文', () => {
    expect(noteLines(i18n.getFixedT('en'), { parts: [asked] }, 'en')[0].text).toBe(
      'The steward asked to hand this session over to codex-acp: Daoris takes it at its next look.');
    expect(noteLines(i18n.getFixedT('zh'), { parts: [asked] }, 'zh')[0].text).toBe('管家要求把这个会话转交给 codex-acp：Daoris 会在下一次查看时接手。');
  });
});
