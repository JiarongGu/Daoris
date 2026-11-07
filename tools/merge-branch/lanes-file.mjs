/**
 * The lanes file's grammar: a lane's globs, and whether a path is the lane's.
 */
// ---------------------------------------------------------------------------------------------------
// Lanes

const escapeRegExp = (text) => text.replace(/[.+^${}()|[\]\\]/g, '\\$&');

/** A lane path as a pattern: `*` stays in a folder, `**` crosses folders, `{a,b}` is either. */
export function globToRegExp(glob) {
  let source = '';
  for (let i = 0; i < glob.length; i += 1) {
    const c = glob[i];
    if (c === '*') {
      if (glob[i + 1] === '*') {
        i += 1;
        if (glob[i + 1] === '/') {
          i += 1;
          source += '(?:.*/)?';
        } else source += '.*';
      } else source += '[^/]*';
    } else if (c === '?') source += '[^/]';
    else if (c === '{' && glob.indexOf('}', i) > i) {
      const end = glob.indexOf('}', i);
      source += `(?:${glob.slice(i + 1, end).split(',').map(escapeRegExp).join('|')})`;
      i = end;
    } else source += escapeRegExp(c);
  }
  return new RegExp(`^${source}$`);
}

/**
 * A lane's paths as one test: a path is the lane's when one of its globs matches it and none of its `!`
 * globs does. A `!` glob carves a narrower lane's paths out of a wider one: the web's shell owns the
 * page but its Settings (LEFT1).
 */
export function laneMatcher(paths) {
  const include = paths.filter((path) => !path.startsWith('!')).map(globToRegExp);
  const exclude = paths.filter((path) => path.startsWith('!')).map((path) => globToRegExp(path.slice(1)));
  return (path) => include.some((pattern) => pattern.test(path)) && !exclude.some((pattern) => pattern.test(path));
}
