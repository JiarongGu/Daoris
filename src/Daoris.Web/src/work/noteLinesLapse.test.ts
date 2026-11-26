import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { NotePart } from '../data/api';
import { moment } from '../locales/format';
import { noteLines } from './noteLines';

const t = i18n.getFixedT('en');
const zh = i18n.getFixedT('zh');

const coded = (code: string, text: string, values: Record<string, unknown> = {}): NotePart => ({ code, values, text });

describe('a week that lapses by measure', () => {
  it('is worded with its rate and the hours its room takes, in English and 中文', () => {
    const lapses = coded('opening.opened', 'opened on `account-3`: …', {
      account: 'account-3', why: 'lapses-rate', window: 'weekly', unused: 50, at: '2026-10-03T23:00:00Z', seen: '2026-10-02T11:50:00Z', rate: 1, needs: 46,
    });
    expect(noteLines(t, { parts: [lapses] }, 'en')[0].text).toBe(
      `Opened on \`account-3\`: its weekly window resets at ${moment('2026-10-03T23:00:00Z', 'en')} with 50% of it unused, `
      + `as read at ${moment('2026-10-02T11:50:00Z', 'en')}, and at 1% an hour, the rate Daoris's sessions reached on it, its room takes 46 h to spend.`,
    );
    expect(noteLines(zh, { parts: [lapses] }, 'zh')[0].text).toContain('每小时 1%');
  });
});
