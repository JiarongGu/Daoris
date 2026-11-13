/** The repository files a page's Vitest file reads, by the text of the test: what the lane table must send to the web gate. */
import { posix } from 'node:path';

/**
 * The paths outside the page a page's vitest reads. A read from disk goes through `node:fs`, so a test that does not
 * import it reads nothing from disk: a path it says (`docs/code-map.json`, a map's data) is no read. In one that does, a read
 * is a run of string literals, joined, naming a tracked file or folder from where vitest runs, the page
 * (`join(process.cwd(), '..', 'Daoris.Cli', …)`), from the repository (`join(root, 'src', …)`) or from the test's own folder;
 * or, where the run names nothing, one literal of it that does (`declared('service', 'src/…/NoteParts.cs')`). A folder is read
 * for each file in it the test names (`driver('Trace.Chain.cs')`), else for any file in it. A relative import naming a file
 * outside the page is a read in any test. The page's own files are no read: the web gate runs with them.
 */
export function pageReads(file: string, text: string, files: readonly string[]): string[] {
  const page = 'src/Daoris.Web';
  const tracked = new Set(files);
  const folders = new Set(files.flatMap((path) => path.split('/').slice(0, -1).map((_, i, parts) => parts.slice(0, i + 1).join('/'))));
  const outside = (path: string) => path.includes('/') && !path.startsWith('..') && path !== page && !path.startsWith(`${page}/`);
  const reads = new Set<string>();
  for (const match of text.matchAll(/(?:\bfrom\s+|\bimport\s*\(\s*)'(\.{1,2}\/[^'?]+)'/g)) {
    const path = posix.normalize(posix.join(posix.dirname(file), match[1]!));
    const found = ['', '.ts', '.tsx', '.mjs', '.js', '.json'].map((extension) => `${path}${extension}`).find((candidate) => tracked.has(candidate));
    if (found && outside(found)) reads.add(found);
  }
  // A twin that reads the driver by name (`driverSource('Trace.Chain.cs')`, or a name it hands on to it) reads
  // each driver source it names, the one tracked file of that name at any depth of the driver's folder.
  if (/\bdriverSource\b/.test(text)) {
    const driver = 'src/Daoris.Desktop/Daoris.Desktop.Driver/';
    for (const match of text.matchAll(/'([^'\\\r\n/]+)'/g)) {
      const named = files.filter((path) => path.startsWith(driver) && posix.basename(path) === match[1]);
      if (named.length === 1) reads.add(named[0]!);
    }
  }
  if (!/\bfrom\s+'(?:node:)?fs(?:\/promises)?'/.test(text)) return [...reads];
  const resolve = (path: string) => [page, '.', posix.dirname(file)]
    .map((base) => posix.normalize(posix.join(base, path)))
    .find((candidate) => outside(candidate) && (tracked.has(candidate) || folders.has(candidate)));
  const literals = [...text.matchAll(/'([^'\\\r\n]*)'/g)].map((match) => match[1]!);
  for (const match of text.matchAll(/'([^'\\\r\n]*)'((?:\s*,\s*'[^'\\\r\n]*')*)/g)) {
    const run = [match[1]!, ...[...match[2]!.matchAll(/'([^']*)'/g)].map((part) => part[1]!)];
    const whole = resolve(run.join('/'));
    const found = whole ? [whole] : run.filter((one) => one.includes('/')).map(resolve).filter((path): path is string => path !== undefined);
    for (const path of found) {
      if (tracked.has(path)) {
        reads.add(path);
        continue;
      }
      const named = literals.filter((name) => name && !name.includes('/') && tracked.has(`${path}/${name}`));
      for (const name of named.length > 0 ? named : ['probe.txt']) reads.add(`${path}/${name}`);
    }
  }
  return [...reads];
}
