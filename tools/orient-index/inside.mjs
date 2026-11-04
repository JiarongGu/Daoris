/**
 * C#: which lines start inside a block comment, a verbatim string or a raw string, the constructs that span lines.
 * A regular string or a character ends at its line's end at the latest, so a misread one costs that line only.
 */
function csInside(lines) {
  const inside = [];
  let state = null;
  for (const line of lines) {
    inside.push(state !== null);
    let i = 0;
    while (i < line.length) {
      if (state?.kind === 'block') {
        const end = line.indexOf('*/', i);
        if (end < 0) break;
        i = end + 2;
        state = null;
        continue;
      }
      if (state?.kind === 'verbatim') {
        const quote = line.indexOf('"', i);
        if (quote < 0) break;
        if (line[quote + 1] === '"') {
          i = quote + 2;
          continue;
        }
        i = quote + 1;
        state = null;
        continue;
      }
      if (state?.kind === 'raw') {
        const close = line.indexOf(state.run, i);
        if (close < 0) break;
        i = close + state.run.length;
        state = null;
        continue;
      }
      const c = line[i];
      if (c === '/' && line[i + 1] === '/') break;
      if (c === '/' && line[i + 1] === '*') {
        state = { kind: 'block' };
        i += 2;
        continue;
      }
      if (c === "'") {
        const char = /'(?:\\.|[^'\\])+'/y;
        char.lastIndex = i;
        i = char.test(line) ? char.lastIndex : i + 1;
        continue;
      }
      if (c === '"' || ((c === '$' || c === '@') && /^[$@]*"/.test(line.slice(i, i + 8)))) {
        let j = i;
        let verbatim = false;
        while (line[j] === '$' || line[j] === '@') {
          if (line[j] === '@') verbatim = true;
          j += 1;
        }
        let q = j;
        while (line[q] === '"') q += 1;
        const quotes = q - j;
        if (quotes >= 3) {
          const run = '"'.repeat(quotes);
          const close = line.indexOf(run, q);
          if (close < 0) {
            state = { kind: 'raw', run };
            break;
          }
          i = close + quotes;
          continue;
        }
        if (quotes === 2) {
          i = q;
          continue;
        }
        if (verbatim) {
          let k = q;
          for (; k < line.length; k += 1) {
            if (line[k] !== '"') continue;
            if (line[k + 1] === '"') k += 1;
            else break;
          }
          if (k >= line.length) {
            state = { kind: 'verbatim' };
            break;
          }
          i = k + 1;
          continue;
        }
        let k = q;
        while (k < line.length && line[k] !== '"') k += line[k] === '\\' ? 2 : 1;
        i = k + 1;
        continue;
      }
      i += 1;
    }
  }
  return { inside, clean: state === null };
}

const REGEX_AFTER = new Set(['', '(', ',', '=', ':', '[', '!', '&', '|', '?', '{', '}', ';', '+', '-', '*', '%', '>', '~', '^']);
const REGEX_AFTER_WORD = new Set(['return', 'typeof', 'case', 'in', 'of', 'delete', 'void', 'throw', 'new', 'else', 'do', 'yield', 'await']);

/**
 * TypeScript and JavaScript: which lines start inside a block comment or a template literal (holes and nested
 * templates followed). A regular expression is told from a division by what stands before it, as a lexer does.
 */
function tsInside(lines) {
  const inside = [];
  const stack = [];
  for (const line of lines) {
    inside.push(stack.length > 0);
    let prev = '';
    let i = 0;
    while (i < line.length) {
      const top = stack.at(-1);
      if (top === 'block') {
        const end = line.indexOf('*/', i);
        if (end < 0) break;
        stack.pop();
        i = end + 2;
        continue;
      }
      if (top === 'template') {
        const c = line[i];
        if (c === '\\') i += 2;
        else if (c === '`') {
          stack.pop();
          prev = '`';
          i += 1;
        } else if (c === '$' && line[i + 1] === '{') {
          stack.push({ hole: 1 });
          prev = '{';
          i += 2;
        } else i += 1;
        continue;
      }
      const c = line[i];
      if (c === ' ' || c === '\t') {
        i += 1;
        continue;
      }
      if (c === '/' && line[i + 1] === '/') break;
      if (c === '/' && line[i + 1] === '*') {
        stack.push('block');
        i += 2;
        continue;
      }
      if (c === '`') {
        stack.push('template');
        i += 1;
        continue;
      }
      if (c === "'" || c === '"') {
        let k = i + 1;
        while (k < line.length && line[k] !== c) k += line[k] === '\\' ? 2 : 1;
        i = k + 1;
        prev = c;
        continue;
      }
      if (c === '/' && (REGEX_AFTER.has(prev) || REGEX_AFTER_WORD.has(prev))) {
        let k = i + 1;
        let inClass = false;
        for (; k < line.length; k += 1) {
          const d = line[k];
          if (d === '\\') k += 1;
          else if (inClass) inClass = d !== ']';
          else if (d === '[') inClass = true;
          else if (d === '/') break;
        }
        i = k + 1;
        while (i < line.length && /[a-z]/.test(line[i])) i += 1;
        prev = '/';
        continue;
      }
      if (/[A-Za-z0-9_$]/.test(c)) {
        const word = /[A-Za-z0-9_$]+/y;
        word.lastIndex = i;
        word.test(line);
        prev = line.slice(i, word.lastIndex);
        i = word.lastIndex;
        continue;
      }
      if (top && typeof top === 'object') {
        if (c === '{') top.hole += 1;
        else if (c === '}' && --top.hole === 0) {
          stack.pop();
          prev = '';
          i += 1;
          continue;
        }
      }
      // `=>` before a regular expression reads as `>`, which allows one; `<` never does (a closing JSX tag).
      prev = c === '<' ? '<' : c;
      i += 1;
    }
  }
  return { inside, clean: stack.length === 0 };
}

/**
 * Which lines start inside a construct that spans lines, and whether the scan ended outside one. A scan that did not
 * end clean misread something, so its answer is not used: the outline then reads every line as code.
 */
export function continued(lines, language) {
  return language === 'cs' ? csInside(lines) : tsInside(lines);
}

export const trusted = (lines, language) => {
  const scan = continued(lines, language);
  return scan.clean ? scan.inside : lines.map(() => false);
};

// ---------------------------------------------------------------------------------------------------
// Outlines
