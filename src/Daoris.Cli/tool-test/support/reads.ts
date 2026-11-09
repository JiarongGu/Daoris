/**
 * The scan the lane table's tests hold it to: which repository files a test reads, by a name, a walk of a tree, a
 * source root or a local holding a folder, and never a name, a sentence or a folder the test made.
 */
import { posix } from 'node:path';
import { folderSet } from './folder-set.ts';

// The census of test units reads this file's strings as the scan's patterns, not as reads.
// test-map: strings name paths as data

/**
 * The paths a .NET test reads through `SourceFiles`, which finds a source by its name at any depth of its
 * project, so the read is the one tracked file of that name under the project (`SourceFiles.Driver("Landing.cs")`,
 * `SourceFiles.Find("Daoris.Desktop.Modules", "DriverLoop.cs")`). A walk of a project for a pattern (`SourceFiles.All`), or a
 * name the scan cannot read (`SourceFiles.Driver($"{name}.cs")`), reads the project's folder, for its C# sources when the
 * pattern names them.
 */
export function namedReads(text: string, files: readonly string[]): string[] {
  const driver = 'Daoris.Desktop.Driver';
  const under = (project: string) => `src/Daoris.Desktop/${project}/`;
  const reads = new Set<string>();
  const one = (project: string, name: string) => {
    const found = files.filter((path) => path.startsWith(under(project)) && posix.basename(path) === name);
    if (found.length === 1) reads.add(found[0]!);
  };
  for (const match of text.matchAll(/\bSourceFiles\.Driver\(\s*"([^"\\]+)"\s*\)/g)) one(driver, match[1]!);
  for (const match of text.matchAll(/\bSourceFiles\.Find\(\s*"([^"\\]+)"\s*,\s*"([^"\\]+)"\s*\)/g)) one(match[1]!, match[2]!);
  for (const match of text.matchAll(/\bSourceFiles\.All\(\s*(?:"([^"\\]+)"|SourceFiles\.DriverProject)\s*,\s*"([^"\\]+)"\s*\)/g)) {
    reads.add(`${under(match[1] ?? driver)}${match[2]!.endsWith('.cs') ? 'probe.cs' : 'probe.txt'}`);
  }
  if (/\bSourceFiles\.Driver\(\s*\$/.test(text)) reads.add(`${under(driver)}probe.cs`);
  return [...reads];
}

/** A read names something here, a tracked file or a file in a tracked folder (`probe.txt`, `probe.cs`), or it holds nothing. */
export function isReal(read: string, files: readonly string[], folders: ReadonlySet<string>): boolean {
  return files.includes(read) || (/\/probe\.(?:txt|cs)$/.test(read) && folders.has(posix.dirname(read)));
}

/**
 * The paths a .NET source reads under this repository's top-level folders but `src`, whose reads the scan above
 * matches by its own pattern. A read is a run of string literals whose joined path is tracked, as
 * `Path.Combine(Root(), "tools", "orient-index-fixtures", "decision-notes.json")`, so a workspace's name or a sentence
 * is none. Its base, the argument before the run, is a call or a member (`Root()`, `WorkspaceRoot.Folder`), as every
 * read of `src/` is: a bare name is a folder the test made, often in this repository's own layout (`room`, `_home`), and
 * a string is the start of a longer run. A folder read is a file in it. A top-level folder alone (`"tools"`) is a
 * workspace's name far more often than a read, and a top-level file (`daoris.json`, the marker the tests walk up to) is
 * not read this way; neither is taken. A run that is a local's whole initializer is no read itself: what is read
 * through the local is (`localReads`).
 */
export function treeReads(text: string, files: readonly string[]): string[] {
  const tracked = new Set(files);
  const folders = folderSet(files);
  const { declarations } = localRoots(text, files);
  const reads: string[] = [];
  for (const match of text.matchAll(/,\s*"([^"\\\s]+)"((?:\s*,\s*"[^"\\]*")*)/g)) {
    if (declarations.some(([start, end]) => match.index >= start && match.index < end)) continue;
    const base = text.slice(0, match.index).trimEnd();
    if (base.endsWith('"') || BARE_NAME.test(base)) continue;
    const path = [match[1]!, ...[...match[2]!.matchAll(/"([^"]*)"/g)].map((part) => part[1]!)].join('/');
    const top = path.split('/')[0]!;
    if (top === 'src' || !folders.has(top) || top === path) continue;
    if (tracked.has(path)) reads.push(path);
    else if (folders.has(path)) reads.push(`${path}/probe.txt`);
  }
  return reads;
}

/**
 * The paths a .NET source reads from a source root, a folder above the test other than the repository, which the
 * `src/` pattern and `treeReads` cannot see: `Path.Combine(SourceRoot(), "Daoris.Desktop.Modules", "DriverLoop.cs")`. A
 * source-root helper is a method of the file with no parameters that walks up from the test binary until a named folder
 * is there (`!Directory.Exists(Path.Combine(folder.FullName, "Daoris.Desktop.Driver"))`) and returns the folder it
 * stopped at, or a path under it (`Path.Combine(folder.FullName, "Daoris.Service.Core")`). It resolves as the walk does:
 * to the nearest folder above the test that holds the named one, with what the return adds. A walk to the repository's
 * marker finds the repository, which is no source root.
 *
 * Under each root, a run of string literals whose joined path is tracked is a read of it, through the helper or through
 * the file's own wrapper of it (`Source("Daoris.Desktop.Driver", "Hooks.cs")`). Of a run that joins to nothing tracked,
 * each literal naming a folder directly under the root is a read of that folder: the project list a scan hands to the
 * root (`Projects`, then `Path.Combine(root, project)`). A folder is read for its C# sources (`probe.cs`) when every
 * search pattern the file enumerates names C# sources, and for any file in it otherwise. The helper's own body is no
 * read: the folder it names is the marker its walk looks for.
 */
export function sourceRootReads(file: string, text: string, files: readonly string[]): string[] {
  const tracked = new Set(files);
  const folders = folderSet(files);
  const roots: string[] = [];
  let rest = text;
  for (const helper of text.matchAll(/^([ \t]*)(?:(?:private|internal|public|protected)\s+)?static\s+string\s+\w+\(\)\s*\r?\n\1\{([\s\S]*?)\r?\n\1\}/gm)) {
    const body = helper[2]!;
    const marker = /Exists\(Path\.Combine\(\w+\.FullName,\s*"([^"]+)"\)\)/.exec(body)?.[1];
    if (!marker) continue;
    let at = posix.dirname(file);
    while (at !== '.' && !tracked.has(`${at}/${marker}`) && !folders.has(`${at}/${marker}`)) at = posix.dirname(at);
    if (at === '.') continue;
    const added = /\breturn\b[\s\S]*?Path\.Combine\(\w+\.FullName((?:\s*,\s*"[^"]*")+)\)/.exec(body)?.[1] ?? '';
    roots.push([at, ...[...added.matchAll(/"([^"]*)"/g)].map((part) => part[1]!)].join('/'));
    rest = rest.replace(helper[0], '');
  }
  const probe = probeFor(rest);
  const reads = new Set<string>();
  for (const root of roots) {
    for (const match of rest.matchAll(/"([^"\\\r\n]*)"((?:\s*,\s*"[^"\\\r\n]*")*)/g)) {
      const run = [match[1]!, ...[...match[2]!.matchAll(/"([^"]*)"/g)].map((part) => part[1]!)];
      const path = `${root}/${run.join('/')}`;
      if (tracked.has(path)) reads.add(path);
      else if (folders.has(path)) reads.add(`${path}/${probe}`);
      else for (const name of run) if (name && !name.includes('/') && folders.has(`${root}/${name}`)) reads.add(`${root}/${name}/${probe}`);
    }
  }
  return [...reads];
}

/**
 * The paths a .NET source reads under `src/Daoris.…`, a run of string literals from `"src"`. Its base, the
 * argument before the run, is a call or a member (`RepositoryRoot()`, `WorkspaceRoot.Folder`), as `treeReads` takes it: a
 * bare name is a folder the test made (`Path.Combine(workspace, "src", "Daoris.Desktop", "app", "bin")`, a scratch workspace
 * on a field the test set) or a local holding a repository folder, whose reads `localReads` takes; and a string is the middle
 * of a longer run. A run that is a local's whole initializer is no read itself, since what is read through the local is. A
 * run that names no file is a folder read, a file in it.
 */
export function srcReads(text: string, files: readonly string[]): string[] {
  const { declarations } = localRoots(text, files);
  const reads: string[] = [];
  for (const match of text.matchAll(/"src",\s*"(Daoris\.[A-Za-z.]+)"((?:,\s*"[^"]*")*)/g)) {
    if (declarations.some(([start, end]) => match.index >= start && match.index < end)) continue;
    const base = text.slice(0, match.index).trimEnd().replace(/,$/, '').trimEnd();
    if (base.endsWith('"') || BARE_NAME.test(base)) continue;
    const segments = ['src', match[1]!, ...[...match[2]!.matchAll(/"([^"]*)"/g)].map((part) => part[1]!)];
    // A tracked file is read as itself whatever its extension (`process.runsettings`), never as a folder.
    if (/\.(cs|ts|tsx|mjs|js|json|css|md|props|txt)$/.test(segments.at(-1)!) || files.includes(segments.join('/'))) {
      reads.push(segments.join('/'));
      continue;
    }
    // A folder searched for one name at any depth (`Directory.EnumerateFiles(Path.Combine(…, "src"), "documents.ts",
    // SearchOption.AllDirectories)`) reads the file of that name wherever it lies under it, and nothing else of the folder.
    const folder = segments.join('/');
    const named = /^\s*\)\s*,\s*"([^"*?\\/]+)"\s*,\s*SearchOption\.AllDirectories\b/.exec(text.slice(match.index + match[0].length));
    const found = named ? files.filter((file) => file.startsWith(`${folder}/`) && file.split('/').at(-1) === named[1]) : [];
    reads.push(...(named ? (found.length ? found : [`${folder}/${named[1]}`]) : [`${folder}/probe.txt`]));
  }
  return reads;
}

/** A base that is a name alone, a local, a field or a parameter, rather than a call or a member: a folder the test made. */
const BARE_NAME = /(?:^|[^\w.!?])@?[A-Za-z_]\w*$/;

/**
 * The locals, fields and expression-bodied helpers of a .NET source that hold a folder of this repository, each by
 * name, and where each is declared. One is declared as `Path.Combine` of a base and a run of string literals whose joined
 * path is a tracked folder below the top level: `var root = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop")`, or
 * `static string Page => Path.Combine(…)`. The base is the repository's root, a call or a member as for every other read
 * (never a temporary folder's), or another such local (`Path.Combine(desktop, "Daoris.Desktop.App")`). A local on anything
 * else (a field the test set to a scratch folder, a parameter) holds a folder the test made.
 */
function localRoots(text: string, files: readonly string[]): { roots: Map<string, string[]>; declarations: [number, number][] } {
  const folders = folderSet(files);
  const roots = new Map<string, string[]>();
  const declarations: [number, number][] = [];
  const found = [...text.matchAll(/\b(?:var|(?:static\s+)?(?:readonly\s+)?string)\s+(\w+)\s*(?:\(\s*\))?\s*=>?\s*Path\.Combine\(\s*([^,;"]+?)\s*((?:,\s*"[^"\\\r\n]*")+)\s*\)\s*;/g)];
  const settled = new Set<number>();
  for (let more = true; more;) {
    more = false;
    for (const [i, match] of found.entries()) {
      if (settled.has(i)) continue;
      const base = match[2]!;
      const under = /^@?[A-Za-z_]\w*$/.test(base) ? roots.get(base) : /(?:\)|\.\w+)$/.test(base) && !/temp/i.test(base) ? [''] : [];
      if (!under) continue;
      settled.add(i);
      const run = [...match[3]!.matchAll(/"([^"]*)"/g)].map((part) => part[1]!);
      const held = under.map((at) => [at, ...run].filter(Boolean).join('/')).filter((path) => path.includes('/') && folders.has(path));
      if (!held.length) continue;
      roots.set(match[1]!, [...(roots.get(match[1]!) ?? []), ...held]);
      declarations.push([match.index, match.index + match[0].length]);
      more = true;
    }
  }
  return { roots, declarations };
}

/**
 * The paths a .NET source reads through a local holding a repository folder (`localRoots`), which the `src/` pattern
 * read as the whole folder: `src/Daoris.Desktop`, which no rule places, since it holds every desktop lane. A use the code
 * makes, never one in a comment or a string, is read by its shape:
 * - `Path.Combine(local, "Program.cs")`: the run of string literals joined under the folder, when it names a tracked file
 *   or folder; a path that names nothing here (a check that a retired project is gone) is no read.
 * - `Path.Combine(local, project)` or `Path.Combine(local, $"{name}.cs")`: a name the scan cannot read. The folders directly
 *   under the local's that the file names are read, the project list a scan hands it (as a source root's are read), and
 *   with none named, any file in the local's folder.
 * - the base of a narrower local, whose own uses are read, or `Path.GetRelativePath(local, …)`, which names a path and reads
 *   nothing: no read.
 * - any other use (handed to a read, an enumeration, a return or a helper): any file in the local's folder.
 * A folder is read for its C# sources when every search pattern the file enumerates names C# sources.
 */
export function localReads(text: string, files: readonly string[]): string[] {
  const tracked = new Set(files);
  const folders = folderSet(files);
  const { roots, declarations } = localRoots(text, files);
  const probe = probeFor(text);
  // Strings blanked, then comments, keeping every offset: a name in a sentence or a comment is no use.
  const code = text.replace(/"(?:[^"\\\r\n]|\\.)*"/g, (literal) => `"${' '.repeat(literal.length - 2)}"`).replace(/\/\/[^\r\n]*/g, (comment) => ' '.repeat(comment.length));
  const literals = [...text.matchAll(/"([^"\\\r\n]*)"/g)].map((match) => match[1]!);
  const reads = new Set<string>();
  for (const [name, held] of roots) {
    for (const use of code.matchAll(new RegExp(`(?<![\\w.$@])${name}\\b(?:\\s*\\(\\s*\\))?`, 'g'))) {
      if (declarations.some(([start, end]) => use.index >= start && use.index < end)) continue;
      const before = code.slice(0, use.index);
      if (/Path\.GetRelativePath\(\s*$/.test(before)) continue;
      if (!/Path\.Combine\(\s*$/.test(before)) {
        for (const folder of held) reads.add(`${folder}/${probe}`);
        continue;
      }
      const args = /^\s*((?:,\s*"[^"\\\r\n]*")*)\s*(,)?/.exec(text.slice(use.index + use[0].length))!;
      const run = [...args[1]!.matchAll(/"([^"]*)"/g)].map((part) => part[1]!);
      for (const folder of held) {
        if (run.length) {
          const path = `${folder}/${run.join('/')}`;
          if (tracked.has(path)) reads.add(path);
          else if (folders.has(path)) reads.add(`${path}/${probe}`);
          continue;
        }
        const named = args[2] ? literals.filter((one) => one && !one.includes('/') && folders.has(`${folder}/${one}`)) : [];
        for (const one of named.length ? named : ['']) reads.add([folder, one, probe].filter(Boolean).join('/'));
      }
    }
  }
  return [...reads];
}

/** A folder a file reads is read for its C# sources when every search pattern the file enumerates names C# sources. */
function probeFor(text: string): string {
  const patterns = [...text.matchAll(/\b(?:Enumerate|Get)Files\s*\([^;]*?"([^"]*\*[^"]*)"/g)].map((match) => match[1]!);
  return patterns.length > 0 && patterns.every((pattern) => pattern.endsWith('.cs')) ? 'probe.cs' : 'probe.txt';
}
