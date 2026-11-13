/**
 * The lanes file's grammar: a lane's globs, and whether a path is the lane's.
 */
const escapeRegExp = (text: string): string => text.replace(/[.+^${}()|[\]\\]/g, '\\$&');

/** The end of the group opened at `open` and the commas that part its alternatives, or null where it never closes. */
function groupAt(glob: string, open: number): { end: number; commas: number[] } | null {
  let depth = 0;
  const commas: number[] = [];
  for (let at = open; at < glob.length; at += 1) {
    if (glob[at] === '{') depth += 1;
    else if (glob[at] === '}' && --depth === 0) return { end: at, commas };
    else if (glob[at] === ',' && depth === 1) commas.push(at);
  }
  return null;
}

function body(glob: string): string {
  let source = '';
  for (let i = 0; i < glob.length; i += 1) {
    const c = glob[i] as string;
    const group = c === '{' ? groupAt(glob, i) : null;
    if (c === '*') {
      if (glob[i + 1] === '*') {
        i += 1;
        if (glob[i + 1] === '/') {
          i += 1;
          source += '(?:.*/)?';
        } else source += '.*';
      } else source += '[^/]*';
    } else if (c === '?') source += '[^/]';
    else if (group) {
      const edges = [i, ...group.commas, group.end];
      source += `(?:${edges.slice(1).map((edge, k) => body(glob.slice((edges[k] as number) + 1, edge))).join('|')})`;
      i = group.end;
    } else source += escapeRegExp(c);
  }
  return source;
}

/** A lane path as a pattern: `*` stays in a folder, `**` crosses folders, `{a,b}` is either, each alternative a glob of its own. */
export const globToRegExp = (glob: string): RegExp => new RegExp(`^${body(glob)}$`);

/**
 * A lane's paths as one test: a path is the lane's when one of its globs matches it and none of its `!`
 * globs does. A `!` glob carves a narrower lane's paths out of a wider one: the web's shell owns the
 * page but its Settings.
 */
export function laneMatcher(paths: string[]): (path: string) => boolean {
  const include = paths.filter((path) => !path.startsWith('!')).map(globToRegExp);
  const exclude = paths.filter((path) => path.startsWith('!')).map((path) => globToRegExp(path.slice(1)));
  return (path) => include.some((pattern) => pattern.test(path)) && !exclude.some((pattern) => pattern.test(path));
}
