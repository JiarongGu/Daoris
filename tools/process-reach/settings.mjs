import { join } from 'node:path';

/** A run settings file's `TestCaseFilter`, its entities read, or null where it holds none. */
export function settingsFilter(xml) {
  const found = /<TestCaseFilter>([\s\S]*?)<\/TestCaseFilter>/.exec(xml.replace(/<!--[\s\S]*?-->/g, ''));
  if (!found) return null;
  return found[1].trim().replace(/&(amp|lt|gt|quot|apos);/g, (_, e) => ({ amp: '&', lt: '<', gt: '>', quot: '"', apos: "'" })[e]);
}

/**
 * Whether a class whose traits are `traits` (a map of name to its values) can hold a test VSTest's `filter` selects: `&`, `|`,
 * `!`, parentheses, and `=`, `!=`, `~`, `!~` on a trait, and on `FullyQualifiedName` against a test's full `name` where given. Any
 * other condition on a name holds: a class read into a half it is not in runs nothing more there, as its filter chooses its tests.
 */
export function filterHolds(filter, traits, name = null) {
  const tokens = filter.match(/\\.|!=|!~|[()&|!=~]|[^()&|!=~\\]+/g) ?? [];
  let at = 0;
  const peek = () => tokens[at];
  const condition = () => {
    let text = '';
    while (at < tokens.length && !['(', ')', '&', '|'].includes(peek())) text += tokens[at++];
    const parts = /^\s*([^!=~]+?)\s*(!=|!~|=|~)\s*(.*?)\s*$/.exec(text);
    if (!parts) return true;
    const [, field, op, escaped] = parts;
    const value = escaped.replace(/\\(.)/g, '$1');
    const named = field === 'FullyQualifiedName' && name !== null ? [name] : null;
    if (!named && /^(FullyQualifiedName|Name|ClassName|DisplayName)$/.test(field)) return true;
    const has = [...(named ?? traits.get(field) ?? [])].some((each) => (op.endsWith('~') ? each.includes(value) : each === value));
    return op.startsWith('!') ? !has : has;
  };
  const factor = () => {
    if (peek() === '!') {
      at += 1;
      return !factor();
    }
    if (peek() === '(') {
      at += 1;
      const value = expression();
      if (peek() === ')') at += 1;
      return value;
    }
    return condition();
  };
  const term = () => {
    let value = factor();
    while (peek() === '&') {
      at += 1;
      value = factor() && value;
    }
    return value;
  };
  function expression() {
    let value = term();
    while (peek() === '|') {
      at += 1;
      value = term() || value;
    }
    return value;
  }
  return expression();
}

/** A filter value with VSTest's special characters escaped. */
const filterValue = (text) => text.replace(/[\\()&|=!~]/g, (c) => `\\${c}`);

/**
 * The run settings a half runs by reach with: the half's own, its filter ANDed with one `FullyQualifiedName!~`
 * condition per kept class, the class's full name and a dot, so a class whose name another's begins with is not kept with it.
 * In a file, not on the command line: VSTest ANDs the file's filter with one a re-run alone adds (the merge tool's flake re-run),
 * and two hundred names pass cmd's 8,191 characters. A class kept out is the only one that does not run: one this census never
 * saw still runs.
 */
export function reachSettings(xml, kept) {
  const filter = settingsFilter(xml);
  if (filter === null) throw new Error('the run settings hold no TestCaseFilter to narrow');
  if (!kept.length) return xml;
  const narrowed = `(${filter})${kept.map((name) => `&FullyQualifiedName!~${filterValue(`${name}.`)}`).join('')}`;
  const escaped = narrowed.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  const element = /<TestCaseFilter>[\s\S]*?<\/TestCaseFilter>/.exec(xml.replace(/<!--[\s\S]*?-->/g, (comment) => ' '.repeat(comment.length)));
  return `${xml.slice(0, element.index)}<TestCaseFilter>${escaped}</TestCaseFilter>${xml.slice(element.index + element[0].length)}`;
}

// ---------------------------------------------------------------------------------------------------
// The census
