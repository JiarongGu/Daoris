import assert from 'node:assert/strict';

/**
 * The driver's `[InlineData(…)]` rows, read as data (TOOLS2, TOOLS3): how a CLI test holds a twin's table to
 * its own, cell for cell and in order, so a row changed on one side alone fails `npm run verify`. The C#
 * side keeps each row on one line, its cells literals.
 */

export type Cell = string | number | boolean | null;

/**
 * One row's arguments: a raw `"""…"""`, verbatim `@"…"` or plain `"…"` string, `null`, `true`, `false`, a
 * whole number, or a named constant in `words` (the placeholder each side spells for itself).
 */
export function csharpRow(line: string, words: Record<string, Cell> = {}): Cell[] {
  const cells: Cell[] = [];
  let at = line.indexOf('[InlineData(') + '[InlineData('.length;
  while (!line.startsWith(')]', at)) {
    if (line[at] === ',' || line[at] === ' ') {
      at += 1;
    } else if (line.startsWith('"""', at)) {
      const end = line.indexOf('"""', at + 3);
      cells.push(line.slice(at + 3, end));
      at = end + 3;
    } else if (line.startsWith('@"', at)) {
      const end = line.indexOf('"', at + 2);
      cells.push(line.slice(at + 2, end));
      at = end + 1;
    } else if (line[at] === '"') {
      let text = '';
      at += 1;
      while (line[at] !== '"') {
        text += line[at] === '\\' ? line[at + 1] : line[at];
        at += line[at] === '\\' ? 2 : 1;
      }
      cells.push(text);
      at += 1;
    } else {
      const word = /^(null|true|false|-?[0-9]+|[A-Z]\w*)\b/.exec(line.slice(at))?.[1];
      assert.ok(word !== undefined && (!/^[A-Z]/.test(word) || word in words), `a cell this reader does not know, at ${at}: ${line}`);
      cells.push(word === 'null' ? null
        : word === 'true' ? true
          : word === 'false' ? false
            : /^-?[0-9]/.test(word) ? Number(word)
              : words[word]!);
      at += word.length;
    }
  }
  return cells;
}

/** The rows of one theory in a C# test class's source, in its order. */
export function driverRows(source: string, method: string, words: Record<string, Cell> = {}, owner = 'the driver’s tests'): Cell[][] {
  const end = source.indexOf(`public void ${method}(`);
  assert.ok(end > 0, `${owner} have no ${method}`);
  const start = source.lastIndexOf('[Theory]', end);
  return source.slice(start, end).split('\n').filter((line) => line.trim().startsWith('[InlineData(')).map((line) => csharpRow(line, words));
}
