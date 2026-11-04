import { join } from 'node:path';

export const KEYWORDS = new Set([
  'abstract', 'as', 'base', 'bool', 'break', 'byte', 'case', 'catch', 'char', 'checked', 'class', 'const', 'continue', 'decimal',
  'default', 'delegate', 'do', 'double', 'else', 'enum', 'event', 'explicit', 'extern', 'false', 'finally', 'fixed', 'float', 'for',
  'foreach', 'goto', 'if', 'implicit', 'in', 'int', 'interface', 'internal', 'is', 'lock', 'long', 'namespace', 'new', 'null',
  'object', 'operator', 'out', 'override', 'params', 'private', 'protected', 'public', 'readonly', 'ref', 'return', 'sbyte',
  'sealed', 'short', 'sizeof', 'stackalloc', 'static', 'string', 'struct', 'switch', 'this', 'throw', 'true', 'try', 'typeof',
  'uint', 'ulong', 'unchecked', 'unsafe', 'ushort', 'using', 'virtual', 'void', 'volatile', 'while', 'where', 'with', 'record',
  'var', 'when', 'and', 'or', 'not', 'select', 'from', 'into', 'let', 'on', 'equals', 'by', 'group', 'join', 'orderby',
  'ascending', 'descending', 'get', 'set', 'init', 'value', 'yield', 'await', 'async', 'file', 'required', 'scoped', 'global',
]);

/**
 * C# source read two ways, each the length of the text with its newlines kept: `code`, with its comments and the contents of its
 * string and character literals blanked, so its braces and words are code alone; and `literal`, with its comments blanked and
 * its literals kept, for an attribute's arguments and the names a test's strings hold. An interpolated string's holes stay code.
 * A raw string's holes are blanked with it: a word missed there only narrows what a class is read to name, and no test here
 * interpolates a type into one.
 */
export function readCs(text) {
  const n = text.length;
  const code = text.split('');
  const literal = text.split('');
  const blank = (from, to, both) => {
    for (let k = from; k < to && k < n; k += 1) {
      if (code[k] === '\n' || code[k] === '\r') continue;
      code[k] = ' ';
      if (both) literal[k] = ' ';
    }
  };
  const lineEnd = (from) => {
    const at = text.indexOf('\n', from);
    return at < 0 ? n : at;
  };

  // A string from its first prefix character: blanked in `code` but for its holes, which are scanned as code.
  const scanString = (start) => {
    let i = start;
    let dollars = 0;
    let verbatim = false;
    while (text[i] === '$' || text[i] === '@') {
      if (text[i] === '$') dollars += 1;
      else verbatim = true;
      i += 1;
    }
    let quotes = 0;
    while (text[i + quotes] === '"') quotes += 1;
    if (quotes >= 3) {
      // A raw string: its content runs to the same number of quotes.
      const end = text.indexOf('"'.repeat(quotes), i + quotes);
      const stop = end < 0 ? n : end + quotes;
      blank(start, stop, false);
      return stop;
    }
    if (quotes === 2) {
      blank(start, i + 2, false);
      return i + 2;
    }
    let j = i + 1;
    let from = start;
    while (j < n) {
      const c = text[j];
      if (!verbatim && c === '\\') {
        j += 2;
        continue;
      }
      if (c === '"') {
        if (verbatim && text[j + 1] === '"') {
          j += 2;
          continue;
        }
        blank(from, j + 1, false);
        return j + 1;
      }
      if (!verbatim && c === '\n') {
        blank(from, j, false);
        return j;
      }
      if (dollars && c === '{') {
        if (text[j + 1] === '{') {
          j += 2;
          continue;
        }
        blank(from, j + 1, false);
        const close = scanCode(j + 1, true);
        from = close;
        j = close + 1;
        continue;
      }
      j += 1;
    }
    blank(from, n, false);
    return n;
  };

  const scanChar = (start) => {
    let j = start + 1;
    if (text[j] === '\\') j += 2;
    else j += 1;
    while (j < n && text[j] !== '\'' && text[j] !== '\n') j += 1;
    const stop = Math.min(n, j + 1);
    blank(start, stop, false);
    return stop;
  };

  // Code from `start`; in a hole, until the brace that closes it, whose index it answers.
  function scanCode(start, hole) {
    let i = start;
    let depth = 0;
    while (i < n) {
      const c = text[i];
      if (c === '/' && text[i + 1] === '/') {
        const end = lineEnd(i);
        blank(i, end, true);
        i = end;
      } else if (c === '/' && text[i + 1] === '*') {
        const close = text.indexOf('*/', i + 2);
        const end = close < 0 ? n : close + 2;
        blank(i, end, true);
        i = end;
      } else if (c === '\'') {
        i = scanChar(i);
      } else if (c === '"' || ((c === '$' || c === '@') && /^[$@]+"/.test(text.slice(i, i + 8)))) {
        i = scanString(i);
      } else {
        if (hole && c === '{') depth += 1;
        else if (hole && c === '}') {
          if (depth === 0) return i;
          depth -= 1;
        }
        i += 1;
      }
    }
    return n;
  }

  scanCode(0, false);
  return { code: code.join(''), literal: literal.join('') };
}

/** The brace depth before each character of `code`, an array one longer than it. */
export function depths(code) {
  const at = new Int32Array(code.length + 1);
  let depth = 0;
  for (let i = 0; i < code.length; i += 1) {
    at[i] = depth;
    if (code[i] === '{') depth += 1;
    else if (code[i] === '}') depth = Math.max(0, depth - 1);
  }
  at[code.length] = depth;
  return at;
}

/** The index of the brace that closes the one at `open`, or the text's end. */
export function closing(code, open) {
  let depth = 0;
  for (let i = open; i < code.length; i += 1) {
    if (code[i] === '{') depth += 1;
    else if (code[i] === '}') {
      depth -= 1;
      if (depth === 0) return i;
    }
  }
  return code.length;
}

/** Every identifier the code names, as a set: its keywords left out. */
export function namesIn(code) {
  const found = new Set();
  for (const [word] of code.matchAll(/@?[A-Za-z_][A-Za-z0-9_]*/g)) {
    const name = word.replace(/^@/, '');
    if (!KEYWORDS.has(name)) found.add(name);
  }
  return found;
}
