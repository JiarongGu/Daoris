import { describe, expect, it } from 'vitest';
import { previewText } from './preview';

// ASKHIST1c: a row of Ask Daoris's history read an answer's raw Markdown (`**#d208d4**`, backticks). Its line is plain text:
// the delimiters go, the words and the code's own contents stay, and nothing in it is a link or a button.

describe('a conversation’s line as plain text', () => {
  it.each([
    ['**#d208d4** is the accent', '#d208d4 is the accent'],
    ['Run `daoris driver drive engine` first.', 'Run daoris driver drive engine first.'],
    ['## Landing on a branch', 'Landing on a branch'],
    ['> quoted, then *emphasised* and ~~struck~~', 'quoted, then emphasised and struck'],
    ['- [x] the first step', 'the first step'],
    ['1. Repositories → engine → Setup', 'Repositories → engine → Setup'],
    ['See [the ticket](https://example.atlassian.net/browse/TK-2205) for more', 'See the ticket for more'],
    ['![the screen](shot.png) shows it', 'the screen shows it'],
    ['<https://example.atlassian.net/browse/TK-2205>', 'https://example.atlassian.net/browse/TK-2205'],
    ['```bash npm run verify```', 'npm run verify'],
    ['| key | value |', 'key value'],
    ['a\\*literal\\* star', 'a*literal* star'],
    ['`__init__` and snake_case stay names', '__init__ and snake_case stay names'],
    ['line one\nline two', 'line one line two'],
    ['任务**已完成**：`daoris check` 通过', '任务已完成：daoris check 通过'],
  ])('%j reads %j', (markdown, plain) => {
    expect(previewText(markdown)).toBe(plain);
  });

  it('keeps an answer the driver cut mid-delimiter readable', () => {
    expect(previewText('Set the accent to **#d208d4 and the line to `main…')).toBe('Set the accent to #d208d4 and the line to main…');
  });

  it('says nothing for nothing', () => {
    expect(previewText('')).toBe('');
    expect(previewText('   ')).toBe('');
  });
});
