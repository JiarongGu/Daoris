/**
 * Where a JavaScript template literal ends, for the tools that blank or read a source's comments and strings.
 *
 * A `${ … }` expression is code: it holds braces of its own, strings, comments and templates, so counting only its own opening
 * and closing braces ends the literal at the first `}` of an object literal or of a template nested inside it, and the rest of
 * the file is then read as the inside of a string.
 */

/**
 * The index past the closing quote of the template literal whose opening quote is at `start`; the end of `src` where it never closes.
 * A comment inside an expression is pushed to `comments` as `[from, to)`.
 */
export function templateEnd(src, start, comments = []) {
  let i = start + 1;
  while (i < src.length) {
    if (src[i] === '\\') i += 2;
    else if (src[i] === '`') return i + 1;
    else if (src[i] === '$' && src[i + 1] === '{') i = expressionEnd(src, i + 2, comments);
    else i++;
  }
  return src.length;
}

/** The index past the `}` closing the expression that starts at `from`, inside a template's `${`. */
function expressionEnd(src, from, comments) {
  let depth = 0;
  let i = from;
  // The last significant character, 'x' after a token: a `/` after a value divides, after anything else it opens a pattern.
  let last = '';
  while (i < src.length) {
    const c = src[i];
    if (c === '{') depth++;
    else if (c === '}' && depth-- === 0) return i + 1;
    if (c === '`') {
      i = templateEnd(src, i, comments);
      last = 'x';
    } else if (c === '"' || c === "'") {
      i++;
      while (i < src.length && src[i] !== c && src[i] !== '\n') i += src[i] === '\\' ? 2 : 1;
      i++;
      last = 'x';
    } else if (c === '/' && src[i + 1] === '/') {
      const end = src.indexOf('\n', i);
      comments.push([i, end < 0 ? src.length : end]);
      i = end < 0 ? src.length : end;
    } else if (c === '/' && src[i + 1] === '*') {
      const end = src.indexOf('*/', i + 2);
      comments.push([i, end < 0 ? src.length : end + 2]);
      i = end < 0 ? src.length : end + 2;
    } else if (c === '/' && !/[\w)\]}$]/.test(last)) {
      i++;
      for (let inClass = false; i < src.length && src[i] !== '\n'; i++) {
        if (src[i] === '\\') i++;
        else if (src[i] === '[') inClass = true;
        else if (src[i] === ']') inClass = false;
        else if (src[i] === '/' && !inClass) break;
      }
      i++;
      last = 'x';
    } else {
      if (!/\s/.test(c)) last = c;
      i++;
    }
  }
  return src.length;
}
