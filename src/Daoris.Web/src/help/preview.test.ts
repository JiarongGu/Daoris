import { describe, expect, it } from 'vitest';
import { previewText, ROW_BEFORE, ROW_LINE, rowLine } from './preview';

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

// ASKHIST1d2: the driver hands a line whole up to 1000 characters, an answer's first and the one a search found its words on
// (D158's ASKHIST1d1 note). The page parses it, then cuts it to what a row holds: from a little before the words found, so a
// clamp to two lines never hides them.

/** Whether a cut left half of a character outside the basic plane. */
const split = (text: string) => /[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/.test(text);

describe('a parsed line cut to a row', () => {
  it('keeps a line that fits whole', () => {
    expect(rowLine('A circle of repositories that share a remote.')).toBe('A circle of repositories that share a remote.');
    expect(rowLine('the remote is near its start', 'remote')).toBe('the remote is near its start');
  });

  it('cuts a long line between words, with an ellipsis where it goes on', () => {
    const cut = rowLine('word '.repeat(100).trim());
    expect(cut.length).toBeLessThanOrEqual(ROW_LINE + 1);
    expect(cut).toMatch(/^word( word)* word…$/);
  });

  it('starts a little before the words a search found, between words, so a row’s two lines show them', () => {
    const cut = rowLine(`${'before '.repeat(60)}the Remote is here ${'after '.repeat(60)}`.trim(), 'remote');
    expect(cut).toMatch(/^…before( before)* the Remote is here after/);
    expect(cut.indexOf('Remote')).toBeLessThanOrEqual(ROW_BEFORE + 1);
    expect(cut.endsWith('after…')).toBe(true);
  });

  it('reads from the start when the words are not in what it says', () => {
    expect(rowLine(`${'before '.repeat(60)}`.trim(), 'remote')).toMatch(/^before /);
  });

  it('cuts a line with no spaces by its characters, never splitting one', () => {
    expect(rowLine(`${'前'.repeat(300)}落地${'后'.repeat(300)}`, '落地'))
      .toBe(`…${'前'.repeat(ROW_BEFORE)}落地${'后'.repeat(ROW_LINE - ROW_BEFORE - 2)}…`);
    for (const at of [0, 1]) {
      const cut = rowLine(`${'x'.repeat(at)}${'𠀀'.repeat(300)}`);
      expect(split(cut), cut).toBe(false);
    }
    expect(split(rowLine(`${'𠀀'.repeat(100)}x落地${'𠀀'.repeat(100)}`, '落地'))).toBe(false);
  });

  it('cuts what was parsed, so a delimiter the cut would fall inside is gone first', () => {
    const markdown = `${'**bold** and `code` '.repeat(30)}then the \`remote\` is **here**`;
    const cut = rowLine(previewText(markdown), 'remote');
    expect(cut).not.toMatch(/[*`]/);
    expect(cut).toMatch(/the remote is here$/);
  });
});
