// `daoris tool` — the programs Daoris runs beside its agents, and which file each one is (TOOLS2, D121;
// `docs/2026-10-01-tools-design.md` §2.1–§2.3). A tool is run one of three ways: the system's, from PATH;
// managed, one exact version downloaded into the home; or a file the person names. The way lives in
// `$DAORIS_HOME/tools.json`, one file for both doors (D50), and absent means the system's.
//
// 🔴 A TWIN of the driver's `Tools.cs`. The two share no code — the FILE is the contract — and each
// carries the same tables (`tools.test.ts` here, `ToolsTests.cs` there), row for row. A rule changed here
// is changed there, in the same commit:
//
//   1. No file, no entry, or `"use": "system"` is the system's.
//   2. `managed` needs an exact version of one to four numbers; `file` needs a whole path. An entry that
//      names another way's field, or lacks its own, is refused whole: its tool is never run another way.
//   3. A tool id this build does not declare is kept as written and never applied.
//   4. `git` holds only keys on the allow-list: refused on a write; on a read kept, not applied, said.
//   5. `locations` holds only https://, or http:// to this machine: refused on a write; skipped and said.
//   6. Setting one way clears the others, and a writer keeps what it has no field for.
//
// Then the resolution: the way set decides which file starts. A managed version nobody downloaded, or a
// named file that is gone, refuses and NEVER falls back to PATH (D57's pin rule, read for a tool).
//
// It stays PURE: it reads and writes files and answers "which program". It spawns nothing — a tool's
// version is asked in `toolchain.ts`, the one module that may spawn — and it opens no connection, so no
// doctrine command could reach either through it (the dogfood tests hold both).

import { existsSync, statSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { operands } from './args.ts';
import { DaorisError, type ExitCode } from './errors.ts';
import { onPath, readJsonObject, writeJsonAtomic } from './fsx.ts';
import { HOME_SENTENCE, daorisHome } from './home.ts';
import type { CommandArgs } from './types.ts';

type Env = Record<string, string | undefined>;

/** The file under the home. The driver's `Tools.FileName`. */
export const TOOLS_FILE = 'tools.json';

/** Where managed versions live: `<home>/tools/<tool>/<version>/`. The driver's `Tools.Folder`. */
export const TOOLS_FOLDER = 'tools';

/**
 * A downloaded version's record, in its version folder: finding it is the proof the download verified
 * (§3.6). TOOLS4 writes it; this reads its `exe`, which names the executable inside `package/`, where the
 * archive was unpacked whole. The driver's `Tools.Record` and `Tools.Package`.
 */
export const TOOL_RECORD = 'tool.json';
export const TOOL_PACKAGE = 'package';

export type ToolWay = 'system' | 'managed' | 'file';

/** A program Daoris starts, or hands to a session, that is neither an agent (D57) nor Daoris's own (§2.1). */
export interface ToolDeclaration {
  readonly id: string;
  /** Its product's name, as a person knows it. */
  readonly name: string;
  /** The names it answers for on a child's PATH; the first is the program itself. */
  readonly answers: readonly string[];
  /** The arguments that ask its version. */
  readonly version: readonly string[];
  /** The settings it may carry (§2.5): git's allow-list, and none for the rest. */
  readonly settings: readonly string[];
}

/**
 * The tools this build runs, in the order a child's PATH takes them (§2.4). 🔴 Declared in code on both
 * sides (`Tools.Declared` is the other copy), never read from a file: a list may offer versions of these,
 * and can never make Daoris run a program its code does not name. A sixth is a reviewed row in both.
 */
export const TOOLS: readonly ToolDeclaration[] = [
  { id: 'git', name: 'Git', answers: ['git'], version: ['--version'], settings: ['core.sshCommand'] },
  { id: 'node', name: 'Node.js', answers: ['node', 'npm', 'npx'], version: ['--version'], settings: [] },
  { id: 'pwsh', name: 'PowerShell', answers: ['pwsh'], version: ['--version'], settings: [] },
  { id: 'gh', name: 'GitHub CLI', answers: ['gh'], version: ['--version'], settings: [] },
  { id: 'az', name: 'Azure CLI', answers: ['az'], version: ['version'], settings: [] },
];

/** What Daoris's git may carry (§2.5): one key, the one WSR7 measured the need for. */
export const GIT_SETTINGS: readonly string[] = TOOLS.find((tool) => tool.id === 'git')!.settings;

/** One tool's entry as read: its way, or why it is refused. */
export interface ToolEntry {
  /** Null when the entry, or the file, could not say one. */
  way: ToolWay | null;
  version: string | null;
  file: string | null;
  /** Why the entry is refused: its tool is never run another way. Null when it reads. */
  problem: string | null;
}

export interface ToolsRead {
  path: string;
  exists: boolean;
  /** Every declared tool, in the declared order. */
  entries: Record<string, ToolEntry>;
  /** The ids the file names that this build does not declare: kept, never applied (rule 3). */
  unknown: string[];
  /** The settings Daoris's git carries (rule 4). */
  git: Record<string, string>;
  /** The resource locations, in order (rule 5). */
  locations: string[];
  /** What the file keeps without applying, or skips, and why. */
  notes: string[];
  /** Why the whole file does not read; every tool is then refused. */
  problem: string | null;
}

/** Which file a tool is, or why none. */
export interface ToolResolution {
  tool: string;
  way: ToolWay | null;
  version: string | null;
  /** The file that starts; null for a refusal, and for a system tool PATH does not find. */
  file: string | null;
  /**
   * True when the way set cannot run: it never falls back to PATH. False with a problem is a system tool
   * PATH does not find, which is said, and whose callers keep today's behaviour (§2.3).
   */
  refused: boolean;
  problem: string | null;
}

const WAYS: readonly string[] = ['system', 'managed', 'file'];
/** One to four numbers (§3.2), ASCII digits only, as `[0-9]` is on the other side. */
const EXACT_VERSION = /^[0-9]+(?:\.[0-9]+){0,3}$/;
const NEVER = 'it never falls back to PATH';

export function toolsFile(home: string): string {
  return join(home, TOOLS_FILE);
}

/** A declared tool by its id, or null. */
export function declaredTool(id: string): ToolDeclaration | null {
  return TOOLS.find((tool) => tool.id === id) ?? null;
}

/**
 * Whether a path is whole: .NET's `Path.IsPathFullyQualified`, spelled here so the twins agree. On
 * Windows a drive with its root, or a UNC path; `\x` and `C:x` are not whole, since each depends on where
 * a process stands. Elsewhere, a path from `/`.
 */
export function isWholePath(path: string): boolean {
  if (process.platform === 'win32') return /^[A-Za-z]:[\\/]/.test(path) || /^[\\/]{2}/.test(path);
  return path.startsWith('/');
}

const isObject = (value: unknown): value is Record<string, unknown> =>
  value !== null && typeof value === 'object' && !Array.isArray(value);

/** A field that is there: JSON null is none, as the driver's reader sees it. */
const present = (entry: Record<string, unknown>, key: string): boolean => entry[key] !== undefined && entry[key] !== null;

/** How a way reads in a sentence. */
const WAY_WORDS: Record<ToolWay, string> = { system: "the system's", managed: 'managed', file: 'a file' };

/** One entry of `tools`, judged (rule 2): its way, or the reason it is refused. */
function judgeEntry(value: unknown): { way: ToolWay; version: string | null; file: string | null } | string {
  if (!isObject(value)) return 'it is not an object';
  if (typeof value.use !== 'string') return 'it names no way — `use` is system, managed or file';
  if (!WAYS.includes(value.use)) return `\`use\` is \`${value.use}\`, not system, managed or file`;
  const way = value.use as ToolWay;

  // Another way's field makes the entry two ways, and a tool holds exactly one (§2.3).
  const others = way === 'system' ? ['version', 'file'] : way === 'managed' ? ['file'] : ['version'];
  const other = others.find((key) => present(value, key));
  if (other) return `it is ${WAY_WORDS[way]}, and names a ${other} too — a tool is run one way`;

  if (way === 'managed') {
    if (!present(value, 'version')) return 'managed needs a `version`, one to four numbers';
    if (typeof value.version !== 'string') return 'its `version` is not text';
    if (!EXACT_VERSION.test(value.version)) return `\`${value.version}\` is not an exact version — one to four numbers, like 2.51.0`;
    return { way, version: value.version, file: null };
  }

  if (way === 'file') {
    if (!present(value, 'file')) return 'a file needs its `file`, a whole path';
    if (typeof value.file !== 'string') return 'its `file` is not text';
    if (!isWholePath(value.file)) return `\`${value.file}\` is not a whole path`;
    return { way, version: null, file: value.file };
  }

  return { way, version: null, file: null };
}

/**
 * The file as it stands (rules 1–5). Reading creates nothing, and a file that does not read refuses
 * every tool — never read as empty, which would run PATH's program in place of the one chosen.
 */
export function readTools(home: string): ToolsRead {
  const path = toolsFile(home);
  const system = (): ToolEntry => ({ way: 'system', version: null, file: null, problem: null });
  const read: ToolsRead = {
    path, exists: existsSync(path), entries: {}, unknown: [], git: {}, locations: [], notes: [], problem: null,
  };

  const { value, problem } = readJsonObject(path);
  const tools = value?.tools;
  const fileProblem = problem
    ?? (value !== null && tools !== undefined && tools !== null && !isObject(tools) ? `${path}'s \`tools\` is not an object` : null);
  if (fileProblem !== null) {
    read.problem = `${fileProblem}, and every tool is refused, never run another way: fix it, or delete it to run every `
      + 'tool from PATH';
    for (const tool of TOOLS) read.entries[tool.id] = { way: null, version: null, file: null, problem: read.problem };
    return read;
  }

  const held = isObject(tools) ? tools : {};
  for (const tool of TOOLS) {
    if (!(tool.id in held) || held[tool.id] === null) {
      read.entries[tool.id] = system();
      continue;
    }

    const judged = judgeEntry(held[tool.id]);
    read.entries[tool.id] = typeof judged === 'string'
      ? {
        way: null, version: null, file: null,
        problem: `the entry for \`${tool.id}\` in ${path} does not read (${judged}), and ${tool.name} is never run `
          + `another way: fix the entry, or \`daoris tool use ${tool.id} system\` writes a new one`,
      }
      : { ...judged, problem: null };
  }

  // Rule 3: an id a newer build may know.
  for (const id of Object.keys(held)) {
    if (declaredTool(id)) continue;
    read.unknown.push(id);
    read.notes.push(`\`${id}\` in \`tools\` is not a tool this build runs: it is kept as written, and never applied`);
  }

  // Rule 4: what git carries.
  if (value !== null && present(value, 'git')) {
    if (!isObject(value.git)) {
      read.notes.push('`git` is not an object: it is kept, and nothing in it is applied');
    } else {
      for (const [key, setting] of Object.entries(value.git)) {
        if (!GIT_SETTINGS.includes(key)) {
          read.notes.push(`\`${key}\` in \`git\` is not a setting Daoris's git carries: it is kept, and not applied`);
        } else if (typeof setting !== 'string') {
          read.notes.push(`\`${key}\` in \`git\` is not text: it is kept, and not applied`);
        } else {
          read.git[key] = setting;
        }
      }
    }
  }

  // Rule 5: where versions come from.
  if (value !== null && present(value, 'locations')) {
    if (!Array.isArray(value.locations)) {
      read.notes.push('`locations` is not a list: it is kept, and no location is read');
    } else {
      for (const address of value.locations) {
        if (typeof address !== 'string') read.notes.push('an entry in `locations` is not text, and is skipped');
        else if (locationProblem(address) !== null) {
          read.notes.push(`\`${address}\` in \`locations\` is skipped: an address is https://, or http:// to this machine`);
        } else read.locations.push(address);
      }
    }
  }

  return read;
}

/** A key off git's allow-list, refused on a write (rule 4); null for a key on it. */
export function gitKeyProblem(key: string): string | null {
  return GIT_SETTINGS.includes(key)
    ? null
    : `\`${key}\` is not a setting Daoris's git carries — the one it carries is ${GIT_SETTINGS.join(', ')}`;
}

/** This machine, named: the hosts an http:// location may have (rule 5). */
const LOOPBACK: readonly string[] = ['localhost', '127.0.0.1', '[::1]'];

/** An address that is no resource location, refused on a write (rule 5); null for one that is. */
export function locationProblem(address: string): string | null {
  const refused = `\`${address}\` is not a resource location — an address is https://, or http:// to this machine `
    + `(${LOOPBACK.slice(0, 2).join(', ')} or ${LOOPBACK[2]})`;
  let url: URL;
  try {
    url = new URL(address);
  } catch {
    return refused;
  }
  if (!url.hostname) return refused;
  if (url.protocol === 'https:') return null;
  return url.protocol === 'http:' && LOOPBACK.includes(url.hostname) ? null : refused;
}

/** A downloaded version's executable, or why there is none (§3.6's layout). */
function managedFile(home: string, tool: ToolDeclaration, version: string): { file: string | null; problem: string | null } {
  const folder = join(home, TOOLS_FOLDER, tool.id, version);
  const back = `— ${NEVER}. \`daoris tool use ${tool.id} system\` runs the one on PATH`;
  const record = join(folder, TOOL_RECORD);
  if (!isFile(record)) {
    return { file: null, problem: `${tool.name} is managed at ${version}, and that version is not downloaded (${folder}) ${back}` };
  }

  const { value, problem } = readJsonObject(record);
  const unread = (why: string) => ({
    file: null, problem: `${tool.name} is managed at ${version}, and its record does not read (${why}) ${back}`,
  });
  if (problem !== null) return unread(problem);
  if (typeof value!.exe !== 'string' || value!.exe === '') return unread(`${record}: it names no \`exe\``);
  const exe = value!.exe;
  const parts = exe.split('/');
  if (/[\\:]/.test(exe) || parts.some((part) => part === '' || part === '.' || part === '..')) {
    return unread(`${record}: its \`exe\` is not a relative path inside the package`);
  }

  const file = join(folder, TOOL_PACKAGE, ...parts);
  return isFile(file)
    ? { file, problem: null }
    : { file: null, problem: `${tool.name} is managed at ${version}, and its record names ${file}, and there is no file there ${back}` };
}

/** A file, as .NET's `File.Exists` answers: a folder is not one. */
function isFile(path: string): boolean {
  try {
    return statSync(path).isFile();
  } catch {
    return false;
  }
}

/**
 * Which file a tool is, from a read of the file (§2.3). The way set decides: a named file, a downloaded
 * version, or PATH by PATHEXT — the one resolver for a bare name, `onPath`, with `startable`.
 */
export function resolveFrom(read: ToolsRead, home: string, id: string, env: Env = process.env): ToolResolution {
  const tool = declaredTool(id);
  if (!tool) throw new DaorisError(undeclared(id));
  const entry = read.entries[id]!;
  const base = { tool: id, way: entry.way, version: entry.version };

  if (entry.problem !== null) return { ...base, file: null, refused: true, problem: entry.problem };

  if (entry.way === 'file') {
    return isFile(entry.file!)
      ? { ...base, file: entry.file, refused: false, problem: null }
      : {
        ...base, file: null, refused: true,
        problem: `${tool.name} runs the file ${entry.file}, and there is no file there — ${NEVER}. `
          + `\`daoris tool use ${id} file <path>\` names another, and \`daoris tool use ${id} system\` runs the one on PATH`,
      };
  }

  if (entry.way === 'managed') {
    const { file, problem } = managedFile(home, tool, entry.version!);
    return { ...base, file, refused: file === null, problem };
  }

  const found = onPath(tool.answers[0]!, { env, startable: true });
  return found
    ? { ...base, file: found, refused: false, problem: null }
    : {
      ...base, file: null, refused: false,
      problem: `\`${tool.answers[0]}\` is not on this machine's PATH. A tool is run as the system's, managed, or from a `
        + `file you name: \`daoris tool use ${id} file <path>\` names one`,
    };
}

/** Which file a tool is, read from the home's file now. */
export function resolveTool(home: string, id: string, env: Env = process.env): ToolResolution {
  return resolveFrom(readTools(home), home, id, env);
}

function undeclared(id: string): string {
  return `\`${id}\` is not a tool this build runs — one of: ${TOOLS.map((tool) => tool.id).join(', ')}. `
    + 'A tool is added in the code, never by a file.';
}

/**
 * Set one tool's way (rule 6): the other ways' fields go, and every key the writer has no field for stays,
 * on the entry and on the file. Refused over a file it could not read, and over a `tools` that is no object.
 */
function setWay(home: string, id: string, way: 'system' | 'file', file: string | null): void {
  if (!declaredTool(id)) throw new DaorisError(undeclared(id));
  const path = toolsFile(home);
  const { value, problem } = readJsonObject(path);
  if (problem !== null) {
    throw new DaorisError(`${problem}. Fix it, or delete it to start from nothing — nothing was written.`);
  }

  const root = value ?? {};
  if (present(root, 'tools') && !isObject(root.tools)) {
    throw new DaorisError(`${path}'s \`tools\` is not an object, so nothing was written — fix it, or delete the file to start from nothing.`);
  }

  const tools = isObject(root.tools) ? root.tools : {};
  const entry = isObject(tools[id]) ? { ...tools[id] } : {};
  delete entry.version;
  delete entry.file;
  entry.use = way;
  if (file !== null) entry.file = file;
  writeJsonAtomic(path, { ...root, tools: { ...tools, [id]: entry } });
}

/** The system's: the one PATH finds, as before. */
export function useSystem(home: string, id: string): void {
  setWay(home, id, 'system', null);
}

/** A file the person names, by its whole path; one that is not there is refused. */
export function useFile(home: string, id: string, file: string): void {
  if (!declaredTool(id)) throw new DaorisError(undeclared(id));
  if (!isWholePath(file)) throw new DaorisError(`\`${file}\` is not a whole path — name the executable by its whole path.`);
  if (!isFile(file)) throw new DaorisError(`no file at ${file} — a tool is a file that is there.`);
  setWay(home, id, 'file', file);
}

/** The home, or the refusal every management verb gives without one (D63). */
function requireHome(env: Env): string {
  const home = daorisHome(env);
  if (!home) throw new DaorisError(`${HOME_SENTENCE} (wanted: ${TOOLS_FILE})`);
  return home;
}

/** How one tool reads on a row of `list`. */
function row(tool: ToolDeclaration, resolution: ToolResolution): string {
  const lead = `  ${tool.id.padEnd(5)} ${tool.name.padEnd(10)} `;
  const how = resolution.way === 'managed' ? `managed ${resolution.version}` : resolution.way ? WAY_WORDS[resolution.way] : null;
  if (how === null) return `${lead}refused — ${resolution.problem}`;
  if (resolution.file !== null) return `${lead}${how}: ${resolution.file}`;
  return `${lead}${how}: ${resolution.refused ? 'refused' : 'not found'} — ${resolution.problem}`;
}

/**
 * `daoris tool list|path|use` — the terminal's door onto `tools.json` (D50). Management class: it edits one
 * file under the home, spawns nothing and opens no connection. `managed` arrives with the download (TOOLS4).
 */
export function commandTool({ root, argv, write }: CommandArgs, env: Env = process.env): ExitCode {
  const [verb = 'list', id, way, named] = operands(argv, new Set());
  const ids = TOOLS.map((tool) => tool.id).join(', ');

  switch (verb) {
    case 'list': {
      const home = requireHome(env);
      const read = readTools(home);
      write(read.exists ? `daoris: ${read.path}` : `daoris: ${read.path} — no file, so every tool is the system's, from PATH.`);
      let refused = false;
      for (const tool of TOOLS) {
        const resolution = resolveFrom(read, home, tool.id, env);
        refused ||= resolution.refused;
        write(row(tool, resolution));
      }
      for (const note of read.notes) write(`  ${note}`);
      write('');
      write('  `daoris tool use <tool> system` or `daoris tool use <tool> file <path>` sets one; nothing switches on its own.');
      return refused ? 1 : 0;
    }

    case 'path': {
      if (!id) throw new DaorisError(`\`tool path\` needs a tool — one of: ${ids}`);
      if (!declaredTool(id)) throw new DaorisError(undeclared(id));
      const resolution = resolveTool(requireHome(env), id, env);
      if (resolution.file === null) {
        write(`daoris: ${resolution.problem}`);
        return 1;
      }
      // Alone on its line, for a script to take.
      write(resolution.file);
      return 0;
    }

    case 'use': {
      if (!id) throw new DaorisError(`\`tool use\` needs a tool — one of: ${ids}`);
      const tool = declaredTool(id);
      if (!tool) throw new DaorisError(undeclared(id));
      const home = requireHome(env);
      const written = toolsFile(home);

      if (way === 'system') {
        useSystem(home, id);
        const resolution = resolveTool(home, id, env);
        write(`daoris: ${tool.name} is set to the system's, from PATH — written to ${written}.`);
        write(resolution.file !== null
          ? `  \`daoris tool path ${id}\` answers ${resolution.file}.`
          : `  \`daoris tool path ${id}\` answers: ${resolution.problem}.`);
        return 0;
      }

      if (way === 'file') {
        if (!named) throw new DaorisError(`\`tool use ${id} file\` needs a path — the executable to run`);
        const file = isWholePath(named) ? named : resolve(root, named);
        useFile(home, id, file);
        write(`daoris: ${tool.name} is set to the file ${file} — written to ${written}.`);
        write(`  \`daoris tool path ${id}\` answers ${file}; \`daoris tool use ${id} system\` puts it back on PATH.`);
        return 0;
      }

      if (way === 'managed') {
        throw new DaorisError(`\`tool use ${id} managed\` is not offered yet: a managed version is downloaded from a list `
          + 'of versions, and this build has none. `system` and `file <path>` are the ways it sets');
      }

      throw new DaorisError(`\`tool use ${id}\` takes \`system\` or \`file <path>\``);
    }

    default:
      throw new DaorisError(`unknown tool verb '${verb}' — one of: list, path, use`);
  }
}
