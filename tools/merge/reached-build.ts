/**
 * One build per test project per tree for the reached run: each project its plan runs classes of is restored and built once, timed
 * as a step, and its classes then run with `--no-build --no-restore`. A project already built for its current inputs is not built
 * again: its stamp (`_fixtures/reached-build/`) holds the digest of the inputs its build read and of the output that build left,
 * and both still match. The inputs are what MSBuild reads for it, read from its project files: every project it references, read
 * through (one built only to be started too), each file or folder a project file or a build file it imports names by a relative
 * path, the build files applied from the folders above, the private files a gate is handed, and the SDK.
 *
 * The run says which projects stand built (`built` in its outcome line), so the merge's later gates on the same tree (the censuses)
 * run their `dotnet test` with `--no-build` (`build-once.ts`).
 */
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { join, posix } from 'node:path';

/** What a project's build reads of the tree: whole folders and single files, repository paths. */
export type BuildInputs = { folders: string[]; files: string[] };

/** A project's last build: the digests of its inputs and its output, and when. */
export type BuildStamp = { project: string; inputs: string; outputs: string; at: string };

export type BuildDecision = { build: boolean; why: string };

export const BUILD_STAMPS = '_fixtures/reached-build';

/** The build files MSBuild and NuGet apply from a project's folder and every folder above it. */
const APPLIED = /^(directory\.build\.props|directory\.build\.targets|directory\.packages\.props|directory\.build\.rsp|global\.json|nuget\.config)$/i;
const MSBUILD = /\.(csproj|props|targets)$/i;
const THIS_FOLDER = /\$\(MSBuild(?:ThisFileDirectory|ProjectDirectory)\)/g;

const folderOf = (path: string): string => (posix.dirname(path) === '.' ? '' : posix.dirname(path));
const joined = (folder: string, relative: string): string => posix.normalize(posix.join(folder, relative.replaceAll('\\', '/'))).replace(/\/+$/, '');

/** The repository paths an MSBuild file names: each `Include`, `Project` or `Update` value and each path after `$(MSBuildThisFileDirectory)`. */
function namedPaths(file: string, xml: string): string[] {
  const folder = folderOf(file);
  const values = [
    ...[...xml.matchAll(/\b(?:Include|Project|Update)\s*=\s*"([^"]+)"/g)].flatMap((found) => (found[1] ?? '').split(';')),
    ...[...xml.matchAll(/\$\(MSBuild(?:ThisFileDirectory|ProjectDirectory)\)([^'";<>)]+)/g)].map((found) => `$(MSBuildThisFileDirectory)${found[1] ?? ''}`),
  ];
  const paths: string[] = [];
  for (const raw of values) {
    const value = raw.trim().replace(THIS_FOLDER, '');
    if (!value || /[$@%]\(/.test(value) || /^[a-z]+:/i.test(value)) continue;
    // A wildcard reads the folder it starts in.
    const star = value.search(/[*?]/);
    const path = joined(folder, star >= 0 ? value.slice(0, star).replace(/[^\\/]*$/, '') : value);
    if (path && path !== '.' && !path.startsWith('..')) paths.push(path);
  }
  return paths;
}

/**
 * What the build of the project in `project` (a folder) reads of the tree whose tracked files are `files`, read by `read`: its
 * folder and each referenced project's, read through, with every file or folder their project and build files name and the
 * build files applied from the folders above each.
 */
export function buildInputs(project: string, files: readonly string[], read: (path: string) => string): BuildInputs {
  const tracked = new Set(files);
  const folders = new Set<string>();
  for (const file of files) for (let at = file.indexOf('/'); at > 0; at = file.indexOf('/', at + 1)) folders.add(file.slice(0, at));
  const inputFolders = new Set<string>();
  const inputFiles = new Set<string>();
  const seenFiles = new Set<string>();
  const seenProjects = new Set<string>();
  const projects: string[] = [project];
  const readMsbuild = (file: string): void => {
    if (seenFiles.has(file)) return;
    seenFiles.add(file);
    inputFiles.add(file);
    for (const path of namedPaths(file, read(file))) {
      if (tracked.has(path)) {
        if (path.endsWith('.csproj')) projects.push(folderOf(path));
        else if (MSBUILD.test(path)) readMsbuild(path);
        else inputFiles.add(path);
      } else if (folders.has(path)) inputFolders.add(path);
    }
  };
  for (let next = projects.shift(); next !== undefined; next = projects.shift()) {
    if (seenProjects.has(next)) continue;
    seenProjects.add(next);
    inputFolders.add(next);
    for (const file of files) if (folderOf(file) === next && MSBUILD.test(file)) readMsbuild(file);
    // The build files applied from each folder above the project, the root's included.
    const parts = next.split('/');
    for (let depth = parts.length - 1; depth >= 0; depth -= 1) {
      const above = parts.slice(0, depth).join('/');
      for (const file of files) if (folderOf(file) === above && APPLIED.test(posix.basename(file))) (MSBUILD.test(file) ? readMsbuild(file) : inputFiles.add(file));
    }
  }
  // A file inside a folder already read whole is read with it.
  const within = (path: string): boolean => [...inputFolders].some((folder) => path.startsWith(`${folder}/`));
  return { folders: [...inputFolders].sort(), files: [...inputFiles].filter((file) => !within(file)).sort() };
}

/** The digest of a project's inputs: each input path's object id in the tree (`idsOf`, absent ones `missing`) and what else builds it (`extra`). */
export function inputsDigest(inputs: BuildInputs, idsOf: (paths: string[]) => ReadonlyMap<string, string>, extra: string): string {
  const paths = [...inputs.folders, ...inputs.files].sort();
  const ids = idsOf(paths);
  return createHash('sha1').update(`${extra}\n${paths.map((path) => `${path}\0${ids.get(path) ?? 'missing'}`).join('\n')}`).digest('hex');
}

/** The object id of each of `paths` (a file's blob, a folder's tree) in the content tree `tree` of the checkout at `root`. */
export function treeIds(root: string, tree: string, paths: readonly string[]): Map<string, string> {
  const ids = new Map<string, string>();
  if (!paths.length) return ids;
  const listed = spawnSync('git', ['--no-optional-locks', 'ls-tree', '-z', '--full-tree', tree, '--', ...paths], { cwd: root, windowsHide: true, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  if (listed.status !== 0) throw new Error(`git could not list ${tree}: ${String(listed.stderr).trim()}`);
  for (const entry of listed.stdout.split('\0').filter(Boolean)) {
    const found = /^\d+ \w+ ([0-9a-f]+)\t(.*)$/s.exec(entry);
    if (found?.[1] && found[2]) ids.set(found[2], found[1]);
  }
  return ids;
}

/** Whether a project builds before its tests run: not where its stamp holds these inputs and the output it left is still there unchanged. */
export function buildDecision({ inputs, outputs, stamp }: { inputs: string; outputs: string | null; stamp: BuildStamp | null }): BuildDecision {
  if (outputs === null) return { build: true, why: 'it has no build output' };
  if (!stamp) return { build: true, why: 'no build of it is stamped' };
  if (stamp.inputs !== inputs) return { build: true, why: 'its inputs changed since its stamped build' };
  if (stamp.outputs !== outputs) return { build: true, why: 'its build output changed since its stamped build' };
  return { build: false, why: `built for these inputs at ${stamp.at}` };
}

/** The digest of a project's build output: each file of its `bin/Debug/<framework>` folders by name, size and time; null with none. */
export function outputsDigest(root: string, project: string): string | null {
  const debug = join(root, project, 'bin', 'Debug');
  if (!existsSync(debug)) return null;
  const lines: string[] = [];
  for (const framework of readdirSync(debug, { withFileTypes: true }).filter((entry) => entry.isDirectory()).map((entry) => entry.name).sort()) {
    for (const entry of readdirSync(join(debug, framework), { withFileTypes: true }).filter((each) => each.isFile()).map((each) => each.name).sort()) {
      const stat = statSync(join(debug, framework, entry));
      lines.push(`${framework}/${entry}\0${stat.size}\0${stat.mtimeMs}`);
    }
  }
  return lines.length ? createHash('sha1').update(lines.join('\n')).digest('hex') : null;
}

const stampFile = (root: string, project: string): string => join(root, BUILD_STAMPS, `${project.replace(/[\\/]+/g, '_')}.json`);

export function readStamp(root: string, project: string): BuildStamp | null {
  try {
    const stamp = JSON.parse(readFileSync(stampFile(root, project), 'utf8')) as Partial<BuildStamp>;
    return typeof stamp.inputs === 'string' && typeof stamp.outputs === 'string' ? { project, inputs: stamp.inputs, outputs: stamp.outputs, at: String(stamp.at ?? '') } : null;
  } catch {
    return null;
  }
}

export function writeStamp(root: string, stamp: BuildStamp): void {
  mkdirSync(join(root, BUILD_STAMPS), { recursive: true });
  writeFileSync(stampFile(root, stamp.project), `${JSON.stringify(stamp)}\n`);
}

/** A test command told to run on the build made before it. */
export const onBuilt = (args: readonly string[]): string[] => [...args.filter((arg) => arg !== '--no-build' && arg !== '--no-restore'), '--no-build', '--no-restore'];

/** The name a step gives a project: its folder's last part. */
export const projectName = (project: string): string => project.split(/[\\/]/).filter(Boolean).at(-1) ?? project;

export type BuildSeams = {
  root: string;
  exec: (command: string, args: string[], cwd: string) => number | null | Promise<number | null>;
  say: (line: string) => void;
  time: <T>(step: string, fn: () => T | Promise<T>) => Promise<T>;
  /** The digest of the project's inputs on the tree as it stands; null where it cannot be read, so the project builds. */
  inputsOf: (project: string) => string | null;
  now?: () => string;
};

/** The projects of `units` built beside the plan: each whose build reads a changed path and stands unbuilt, most classes first. */
export function aheadOf(units: readonly { project?: string | undefined }[], changed: readonly string[], seams: {
  inputsOf: (project: string) => BuildInputs | null; decide: (project: string) => BuildDecision;
}): string[] {
  const counts = new Map<string, number>();
  for (const { project } of units) if (project) counts.set(project, (counts.get(project) ?? 0) + 1);
  const reads = (inputs: BuildInputs, path: string): boolean => inputs.files.includes(path) || inputs.folders.some((folder) => path.startsWith(`${folder}/`));
  return [...counts.keys()].filter((project) => {
    const inputs = seams.inputsOf(project);
    return inputs !== null && changed.some((path) => reads(inputs, path)) && seams.decide(project).build;
  }).sort((a, b) => (counts.get(b) ?? 0) - (counts.get(a) ?? 0) || a.localeCompare(b));
}

/** Each of `projects` built in turn by one `dotnet build`, restore and all (so it runs through a synchronous plan), and stamped. */
export async function buildAhead(projects: readonly string[], seams: BuildSeams): Promise<void> {
  const { root, exec: run, time } = seams;
  for (const project of projects) {
    const inputs = seams.inputsOf(project);
    const code = await time(`build ${projectName(project)} beside the plan`, () => run('dotnet', ['build', project], root));
    const outputs = code === 0 ? outputsDigest(root, project) : null;
    if (inputs !== null && outputs !== null) writeStamp(root, { project, inputs, outputs, at: (seams.now ?? (() => new Date().toISOString()))() });
  }
}

/** Each of `projects` restored and built once, unless its stamp holds; the projects ready to run on, and those whose build failed. */
export async function buildOnce(projects: readonly string[], seams: BuildSeams): Promise<{ ready: string[]; failed: Map<string, string> }> {
  // The seam is the caller's runner, which starts its children hidden.
  const { root, exec: run, say, time } = seams;
  const ready: string[] = [];
  const failed = new Map<string, string>();
  for (const project of projects) {
    const name = projectName(project);
    const inputs = seams.inputsOf(project);
    const decision = inputs === null
      ? { build: true, why: 'its inputs could not be read' }
      : buildDecision({ inputs, outputs: outputsDigest(root, project), stamp: readStamp(root, project) });
    if (!decision.build) {
      say(`reached: ${name}: ${decision.why}, so its classes run on that build`);
      ready.push(project);
      continue;
    }
    say(`reached: ${name}: ${decision.why}: restored and built once, its classes run on that build`);
    const restored = await time(`restore ${name}`, () => run('dotnet', ['restore', project], root));
    const code = restored === 0 ? await time(`build ${name}`, () => run('dotnet', ['build', project, '--no-restore'], root)) : restored;
    if (code !== 0) {
      failed.set(project, `its ${restored === 0 ? 'build' : 'restore'} failed (exit ${code})`);
      continue;
    }
    const outputs = outputsDigest(root, project);
    if (inputs !== null && outputs !== null) writeStamp(root, { project, inputs, outputs, at: (seams.now ?? (() => new Date().toISOString()))() });
    ready.push(project);
  }
  return { ready, failed };
}
