import { describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import type { Consideration } from '../../data/signals';
import { attentionActs, attentionGroup, attentionInOrder, attentionWorded, placeOf, type PushedAttention, rowRun } from './attention';

/** A quest its finished session left taken, which nothing carries on: a row holding work until the person's done or word. */
describe('a quest its finished session left taken, worded', () => {
  const left: Consideration = {
    quest: 'q1', repository: 'engine', verdict: 'Unclosed', reason: 'the planner said so.', said: { code: 'unclosed', session: '031217fe' },
  };
  const row = (id: string, since: string | null): PushedAttention => ({ kind: 'unclosed', id, since, where: 'engine', session: '031217fe', sitting: left });

  it('says the session and the doors by the planner’s typed sentence, offers no act and opens the quest from its place', () => {
    const item = attentionWorded(row('q1', '2026-11-08T08:26:45Z'), {});

    expect(item.detail).toBe(
      'Session `031217fe` ended and left `#q1` taken, and nothing here carries it on: Mark done… closes it, or a word to the session goes on with it.');
    expect(attentionActs(item)).toEqual([]);
    expect(rowRun(item)).toEqual({ session: '031217fe', quest: 'q1' });
    expect(placeOf(item, [])).toBe('quests');
  });

  it('holds work after a policy’s hold, the longest waiting first, and says it in 中文', async () => {
    expect(attentionGroup(row('q1', null))).toBe('holding');
    expect(attentionInOrder([
      row('q-new', '2026-11-09T10:00:00Z'), { kind: 'policy-hold', id: 'q-old', since: '2026-11-08T10:00:00Z' },
    ]).map((each) => `${each.kind}:${each.id}`)).toEqual(['policy-hold:q-old', 'unclosed:q-new']);
    await i18n.changeLanguage('zh');
    try {
      expect(attentionWorded(row('q1', null), {}).detail)
        .toBe('会话 `031217fe` 已停下，`#q1` 仍是已接下，本机不会再继续它：「标为完成…」关闭它，或给该会话留言让它继续。');
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
