import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { shellWord, shellWords } from './shellWord';

/**
 * An argument as a terminal hint prints it (ACCTQUOTE1, D125's ACCTQUOTE1 note). The page's half of a TWIN with the driver's
 * `ShellWord`: both read `fixtures/shell-words.json` among the driver's tests, row for row, so the screen's twin of a command and
 * the terminal's own spell an argument the same way. The CLI's suite holds the table against the shells themselves.
 */

type Row = [why: string, value: string, word: string];
// From the page's package, where its suite runs, as the names check reads the package's own files.
const TABLE = join(process.cwd(), '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'fixtures', 'shell-words.json');
const ROWS = JSON.parse(readFileSync(TABLE, 'utf8')) as Row[];

describe('an argument in a terminal hint', () => {
  it('reads the table at all: a table that holds nothing passes every row below', () => {
    expect(ROWS.length).toBeGreaterThan(0);
  });

  for (const [why, value, word] of ROWS) {
    it(`${why}: ${JSON.stringify(value)} is ${word}`, () => {
      expect(shellWord(value, '<name>')).toBe(word);
    });
  }

  it('spells several one by one, a space between', () => {
    expect(shellWords(['work', 'my team', 'R&D'], '<account>')).toBe('work "my team" <account>');
  });
});
