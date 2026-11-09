/**
 * Where a JSX element ends, for the tools that read a source's comments: an element's text is not code, so a `//` in it (a URL)
 * opens no comment, while a `{ … }` in it, an attribute's or a child's, is code and may hold elements and comments of its own.
 */
import { expressionEnd } from './js-template.mjs';

const OPENING = /^<(?:(>)|[A-Za-z_$][\w$.:-]*(?=[\s/>{]))/;
const GENERIC = /^\s+extends\s+[^=>/\s]/;

/**
 * The index past the element whose `<` is at `start`; the end of `src` where it never closes; -1 where what follows `<` is no
 * element's opening (a generic's `<T,>`). A comment inside an expression is pushed to `comments` as `[from, to)`.
 */
export function jsxEnd(src, start, comments = []) {
  const opening = OPENING.exec(src.slice(start, start + 256));
  if (!opening) return -1;
  // `<T extends X>(` is a generic arrow's type parameter, as the compiler reads it; an attribute named extends is `extends=`.
  if (GENERIC.test(src.slice(start + opening[0].length, start + 256))) return -1;
  let i = start + opening[0].length;
  // The opening tag's attributes are names, `=`, quoted strings (which may hold a `>`) and expressions.
  while (!opening[1] && i < src.length && src[i] !== '>') {
    if (src[i] === '/' && src[i + 1] === '>') return i + 2;
    if (src[i] === '{') i = expressionEnd(src, i + 1, comments, jsxEnd);
    else if (src[i] === '"' || src[i] === "'") {
      const close = src.indexOf(src[i], i + 1);
      i = close < 0 ? src.length : close + 1;
    } else i++;
  }
  if (!opening[1]) i++;
  while (i < src.length) {
    if (src[i] === '<' && src[i + 1] === '/') {
      const close = src.indexOf('>', i);
      return close < 0 ? src.length : close + 1;
    }
    if (src[i] === '<') {
      const end = jsxEnd(src, i, comments);
      i = end < 0 ? i + 1 : end;
    } else if (src[i] === '{') i = expressionEnd(src, i + 1, comments, jsxEnd);
    else i++;
  }
  return src.length;
}
