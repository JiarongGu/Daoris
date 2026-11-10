import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { NotePart } from '../data/api';
import { noteLines } from './noteLines';

const t = i18n.getFixedT('en');
const zh = i18n.getFixedT('zh');

const coded = (code: string, text: string): NotePart => ({ code, values: {}, text });

describe('the landing notes that need nothing of the person', () => {
  /** A line that already holds the branch, and a merge queued in its line's integration, never say a review waits, in either language. */
  it.each([
    ['landing.line-holds', 'Nothing to land:', '无需落地'],
    ['landing.queued', 'Waiting in its line', '排队'],
  ])('words %s as its own line with no review waiting', (code, en, cn) => {
    const parts = [coded(code, 'as the driver said it')];

    const [english] = noteLines(t, { parts }, 'en');
    const [chinese] = noteLines(zh, { parts }, 'zh');

    expect(english).toMatchObject({ kind: 'said' });
    expect(english.text).toContain(en);
    expect(english.text.toLowerCase()).toContain('no review is waiting on you');
    expect(english.text).not.toContain('waits for your review');
    expect(chinese.text).toContain(cn);
    expect(chinese.text).toContain('没有需要你审阅的事');
  });
});
