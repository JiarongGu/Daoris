import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { NotePart } from '../data/api';
import { noteLines } from './noteLines';

const t = i18n.getFixedT('en');
const zh = i18n.getFixedT('zh');

const coded = (code: string, text: string, values: Record<string, unknown> = {}): NotePart => ({ code, values, text });

describe('noteLines, a model family’s cooling', () => {
  /** A model family's limit is worded as that family's, never as the whole account's, in a record's line and in a start's hold. */
  it('words a model family’s limit as the family’s own, in either language', () => {
    const until = '2026-10-03T16:00:00Z';
    const limit = coded('account.cooling-model', 'The `claude-code` account it ran on reached its `opus` limit …', {
      until, why: 'stated', owner: 'claude-code', model: 'opus',
    });
    expect(noteLines(t, { parts: [limit] }, 'en')[0].text).toMatch(
      /^The claude-code account it ran on reached its `opus` limit and is cooling for `opus` until .+ \(the agent said so\); nothing that asks for `opus` starts on it until then\.$/,
    );
    expect(noteLines(zh, { parts: [limit] }, 'zh')[0].text).toMatch(/^它运行时用的 claude-code 账户已达到 `opus` 的限额，`opus` 冷却至 .+（智能体如此说明），在此之前不会在它上面启动任何要求 `opus` 的会话。$/);

    const own = coded('held.cooling-model-own', '…', { owner: 'claude-code', model: 'opus', until, why: 'stated' });
    expect(noteLines(t, { parts: [own] }, 'en')[0].text).toMatch(
      /^`claude-code`'s own sign-in reached its `opus` limit and is cooling for `opus` until .+ \(the agent said so\)\. Daoris starts nothing on it that asks for `opus` until then\.$/,
    );
    expect(noteLines(zh, { parts: [own] }, 'zh')[0]).toMatchObject({ kind: 'said' });
  });
});
