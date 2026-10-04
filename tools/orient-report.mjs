#!/usr/bin/env node
/**
 * What a session spent finding its way before its first edit, read from a harness's transcript (ORIENT1d) or, for the
 * sessions Daoris drives, from the typed events Daoris keeps of them (ORIENT1e).
 *
 * ## Why
 *
 * ORIENT1 was measured by a scratch script over ten branches of 2026-10-04: 60 to 100 calls and 370 to 600 KB read
 * before the first edit, about a third of each branch, 278 shell greps and 117 shell dumps between them. The
 * orientation index (`tools/orient-index.mjs`) is meant to halve both. This is that script made a tool, so the
 * same numbers are read the same way after as before. ORIENT2 asks the same of every repository Daoris drives, and
 * its before is read first, while none of them keeps an index (D151 §7, the orientation-everywhere design §5).
 *
 * ## A transcript
 *
 * A transcript is the harness's JSONL: one object a line, a tool call in an assistant message and its result in
 * the next user message. A line that does not parse is skipped. The first edit is the first `Edit`, `MultiEdit`,
 * `Write` or `NotebookEdit` of a file inside the session's working directory and outside its `local/`: a plan
 * written to a scratch folder changes nothing of the work. Bytes are the UTF-8 size of each result's text.
 *
 * A shell command is classed by its words, first match wins, in the scratch script's order so the numbers compare:
 * a search (grep, rg, Select-String), a dump (sed -n, cat, head, tail, Get-Content), a listing (ls, find,
 * Get-ChildItem), a git read, a build or test, or other. Decision files are counted as one row among the files
 * read most, as the measurement counted them.
 *
 * ## A Daoris home
 *
 * A driven session's own transcript lives in an account's home, which Daoris never reads (D125). What Daoris keeps is
 * each session's typed events, `<home>/sessions/<id>.events.jsonl` (D76 §2), named by `session.started` in the
 * machine log under `<home>/logs/` (read with `usage-report.mjs`' reader). The `.log` beside the events is rendered
 * text and is never read. Every session whose start says `driven` is read; a set-up's is left out.
 *
 * - A call is a tool event's id: later events with that id update it, each field the last one said, its content
 *   replaced as ACP replaces it. Its kind is the event's tool kind. A shell call is one of kind `execute`, or a
 *   native PowerShell call, which that door names `other`; its command, the input's `command`, is classed as a
 *   transcript's is. A `knowledge_search` is counted by its title.
 * - Sizes are characters, never bytes, since the events keep characters: the larger of a call's text content and its
 *   output, a cut read at the length it says (`… (N chars)`). A call whose events carry neither is counted, and its
 *   size is said unknown, never zero.
 * - The first edit is the first call of kind edit, delete or move whose first place lies in the session's tree and
 *   outside its `local/`. The tree is `<home>/trees/<workspace>/<repository>/<name>/`, known by the home, or by the
 *   name the driver mints when the home read is a copy; a place spelled relative is the tree's.
 * - A read is whole when its input names neither an offset nor a limit. A whole read that returned 40 × 1024
 *   characters or more ("40K+") is one of a file the orientation index would outline (files over 40 KB).
 * - The index is `docs/index/`, and the folder of the path a tree's `daoris.json` declares under `documents.index`
 *   (ORIENT2b), read from a session's tree while it is on disk and kept for its repository's other sessions. A read
 *   inside it, or a shell command naming it, opens it.
 * - A subagent's calls run beside the session in a stream of their own and are not in its events: its call is one.
 *
 * Then per repository, per workspace and over all, the median editing session before its first edit (design §5.3):
 * its calls, characters, shell searches and dumps, and large whole reads; how many opened the index first; the parks
 * (§5.4); and per repository the files read most.
 *
 * It reads only. It touches no process and writes nothing under the home, and what it prints stays on this machine
 * (D47 §4): a design that quotes it records counts.
 *
 *   node tools/orient-report.mjs --home <dir> [--repository <name>] [--since <date>] [--json]
 *   node tools/orient-report.mjs [--json] <transcript.jsonl>…
 *
 * Exit codes: 0 reported · 2 a usage error, a home with no machine log, or a transcript it could not read (the rest
 * are still reported).
 */
import { existsSync, readFileSync } from 'node:fs';
import { basename, join, posix, resolve } from 'node:path';
import { isMain } from './fsx.mjs';
import { readLogs } from './usage-report.mjs';

const EDITS = new Set(['Edit', 'MultiEdit', 'Write', 'NotebookEdit']);
const SHELLS = new Set(['Bash', 'PowerShell']);
const KINDS = ['search', 'dump', 'list', 'git', 'build', 'other'];
const INDEX = 'docs/index/';

/** The tool kinds that change a file (ACP's vocabulary, which both doors speak). */
const EDIT_KINDS = new Set(['edit', 'delete', 'move']);

/** A whole read this large is of a file `orient-index.mjs` outlines (files over 40 KB). */
const OUTLINED = 40 * 1024;

/** How the record says it cut a field: `SessionEvents.Cut`, and the protocol door's `Compact`. */
const CUT = /… \((\d+) chars\)$/;

/** A session tree's place, by the name `SessionTrees.OpenAsync` mints: `s-` and eight hex digits. */
const MINTED = /(?:^|\/)trees\/([^/]+)\/([^/]+)\/(s-[0-9a-f]{8})(?:\/(.*))?$/i;
const ABSOLUTE = /^(?:[A-Za-z]:\/|\/)/;

/** What a session id may be (`SessionEvents.IsId`): a name, never a path. */
const SESSION_ID = /^[A-Za-z0-9][A-Za-z0-9_.:-]*$/;

/** The input fields read from an input the record cut, which is no longer JSON. */
const INPUT_FIELDS = ['command', 'file_path', 'notebook_path', 'path', 'offset', 'limit'];

export const USAGE = [
  'usage: node tools/orient-report.mjs --home <dir> [--repository <name>] [--since <date>] [--json]',
  '       node tools/orient-report.mjs [--json] <transcript.jsonl>…',
].join('\n');

const kb = (bytes) => Math.round(bytes / 1024);
const thousands = (chars) => `${Math.round(chars / 1000)}K`;
const slash = (path) => String(path ?? '').replace(/\\/g, '/');
const named = (value) => (typeof value === 'string' && value.length > 0 ? value : null);
const plural = (n, one, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;
const decision = (path) => path.replace(/(^|\/)docs\/decisions\/D\d+\.md$/, '$1docs/decisions/D<n>.md');

/** A shell command's kind, by its words, the first that matches. */
export function shellKind(command) {
  const c = String(command);
  if (/\bgrep\b|\brg\b|\bSelect-String\b|\bfindstr\b/i.test(c)) return 'search';
  // PowerShell's aliases count only as a command's first word: `type` and `dir` are common words in arguments.
  if (/\bsed -n|\bcat\b|\bhead\b|\btail\b|\bGet-Content\b|(?:^|[;&|]\s*)(?:type|gc)\s/i.test(c)) return 'dump';
  if (/\bls\b|\bfind\b|\bGet-ChildItem\b|(?:^|[;&|]\s*)(?:dir|gci)\b/i.test(c)) return 'list';
  if (/git (log|show|diff|status|grep|blame)/.test(c)) return 'git';
  if (/dotnet|npm|node --test|vitest/.test(c)) return 'build';
  return 'other';
}

/** The size of a tool result's text: a string, or the text parts of a list of blocks. */
function resultBytes(content) {
  if (typeof content === 'string') return Buffer.byteLength(content, 'utf8');
  if (!Array.isArray(content)) return content == null ? 0 : Buffer.byteLength(JSON.stringify(content), 'utf8');
  return content.reduce((sum, block) => sum + Buffer.byteLength(block?.type === 'text' ? String(block.text ?? '') : JSON.stringify(block ?? ''), 'utf8'), 0);
}

/** A transcript's working directory and its tool calls in order, each with the size of what came back. */
export function readTranscript(text) {
  let cwd = null;
  const calls = [];
  const byId = new Map();
  for (const line of text.split('\n')) {
    if (!line.trim()) continue;
    let entry;
    try {
      entry = JSON.parse(line);
    } catch {
      continue;
    }
    if (cwd === null && typeof entry?.cwd === 'string') cwd = slash(entry.cwd);
    const content = entry?.message?.content;
    if (!Array.isArray(content)) continue;
    for (const block of content) {
      if (block?.type === 'tool_use') {
        const call = { id: block.id, name: String(block.name), input: block.input ?? {}, bytes: 0 };
        calls.push(call);
        byId.set(block.id, call);
      } else if (block?.type === 'tool_result' && byId.has(block.tool_use_id)) {
        byId.get(block.tool_use_id).bytes += resultBytes(block.content);
      }
    }
  }
  return { cwd, calls };
}

/** A path as the repository names it: relative to the session's directory, or to a worktree's top, `/`-separated. */
function relative(path, cwd) {
  const at = slash(path);
  const top = cwd ? cwd.replace(/\/+$/, '') : null;
  if (top && at.toLowerCase().startsWith(`${top.toLowerCase()}/`)) return at.slice(top.length + 1);
  const worktree = /\/\.claude\/worktrees\/[^/]+\/(.*)$/.exec(at);
  return worktree ? worktree[1] : at;
}

/** Whether a call edits the work: a file inside the session's directory and outside its `local/`. */
function editsWork(call, cwd) {
  if (!EDITS.has(call.name)) return false;
  const path = slash(call.input.file_path ?? call.input.notebook_path);
  if (!cwd) return true;
  const inside = path.toLowerCase().startsWith(`${cwd.replace(/\/+$/, '').toLowerCase()}/`);
  return inside && !relative(path, cwd).startsWith('local/');
}

/** What a session spent before its first edit. */
export function summarize({ cwd, calls }, label) {
  const first = calls.findIndex((call) => editsWork(call, cwd));
  const before = first < 0 ? calls : calls.slice(0, first);
  const byTool = {};
  const bytesByTool = {};
  const shell = Object.fromEntries(KINDS.map((kind) => [kind, 0]));
  const reads = [];
  for (const call of before) {
    byTool[call.name] = (byTool[call.name] ?? 0) + 1;
    bytesByTool[call.name] = (bytesByTool[call.name] ?? 0) + call.bytes;
    if (SHELLS.has(call.name)) shell[shellKind(call.input.command ?? '')] += 1;
    if (call.name === 'Read') {
      const whole = call.input.offset === undefined && call.input.limit === undefined;
      reads.push({ path: relative(call.input.file_path, cwd), bytes: call.bytes, whole });
    }
  }
  return {
    label,
    total: calls.length,
    before: before.length,
    byTool,
    bytes: before.reduce((sum, call) => sum + call.bytes, 0),
    bytesByTool,
    shell,
    reads,
    indexReads: reads.filter((read) => read.path.startsWith(INDEX)).length,
    firstEdit: first < 0 ? null : { name: calls[first].name, path: relative(calls[first].input.file_path ?? calls[first].input.notebook_path, cwd) },
  };
}

const median = (values) => {
  const sorted = [...values].sort((a, b) => a - b);
  if (sorted.length === 0) return 0;
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[middle] : Math.round((sorted[middle - 1] + sorted[middle]) / 2);
};

/** The report: a paragraph per branch, the totals, and the files read most before the first edit. */
export function report(summaries, { top = 20 } = {}) {
  const out = [];
  for (const s of summaries) {
    const share = s.total ? Math.round((100 * s.before) / s.total) : 0;
    const edit = s.firstEdit ? `first edit: ${s.firstEdit.name} ${s.firstEdit.path}` : 'no edit';
    const tools = Object.entries(s.byTool).sort((a, b) => b[1] - a[1] || (a[0] < b[0] ? -1 : 1))
      .map(([name, n]) => `${name} ${n} (${kb(s.bytesByTool[name])} KB)`);
    const whole = s.reads.filter((read) => read.whole).length;
    out.push(
      `${s.label}: ${s.total} calls, ${s.before} before the first edit (${share}%), ${kb(s.bytes)} KB read before it; ${edit}`,
      `  by tool: ${tools.join(', ') || 'none'}`,
      `  reads ${s.reads.length} (${whole} whole, ${s.reads.length - whole} ranged), ${s.indexReads} of ${INDEX}; shell: ${KINDS.map((kind) => `${kind} ${s.shell[kind]}`).join(', ')}`,
    );
  }
  const sum = (pick) => summaries.reduce((total, s) => total + pick(s), 0);
  out.push('', `all ${summaries.length}: ${sum((s) => s.before)} calls before the first edit (median ${median(summaries.map((s) => s.before))}), `
    + `${kb(sum((s) => s.bytes))} KB read before it (median ${kb(median(summaries.map((s) => s.bytes)))} KB); `
    + `shell search ${sum((s) => s.shell.search)}, dump ${sum((s) => s.shell.dump)}`);
  const files = new Map();
  for (const s of summaries) {
    for (const read of s.reads) {
      const path = decision(read.path);
      const row = files.get(path) ?? { count: 0, bytes: 0 };
      row.count += 1;
      row.bytes += read.bytes;
      files.set(path, row);
    }
  }
  const most = [...files].sort((a, b) => b[1].count - a[1].count || b[1].bytes - a[1].bytes || (a[0] < b[0] ? -1 : 1)).slice(0, top);
  if (most.length) {
    out.push('', 'files read most before the first edit (reads, KB, path):');
    for (const [path, row] of most) out.push(`${String(row.count).padStart(5)} ${String(kb(row.bytes)).padStart(5)} KB  ${path}`);
  }
  return `${out.join('\n')}\n`;
}

// ORIENT1e: a driven session's typed events.

/** A text's length, or the length its cut says it had. */
function stated(text) {
  const cut = CUT.exec(text);
  return cut ? Math.max(Number(cut[1]), text.length - cut[0].length) : text.length;
}

/** A string read out of a cut input, its escapes undone where they are whole. */
function unescaped(text) {
  try {
    return JSON.parse(`"${text.replace(/\\$/, '')}"`);
  } catch {
    return text;
  }
}

/** A call's input as fields: its JSON, or, where the record cut it, the fields it opens with. */
function inputOf(raw) {
  if (typeof raw !== 'string') return {};
  try {
    const parsed = JSON.parse(raw);
    return parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : {};
  } catch {
    const fields = {};
    for (const name of INPUT_FIELDS) {
      const text = new RegExp(`"${name}"\\s*:\\s*"((?:[^"\\\\]|\\\\.)*)`).exec(raw);
      const number = new RegExp(`"${name}"\\s*:\\s*(-?\\d+)`).exec(raw);
      if (text) fields[name] = unescaped(text[1]);
      else if (number) fields[name] = Number(number[1]);
    }
    return fields;
  }
}

/** Every tool call in a session's events file, in the order each was first said, its updates merged under its id. */
export function readEvents(text) {
  const calls = [];
  const byId = new Map();
  for (const line of String(text).split(/\r?\n/)) {
    if (!line.trim()) continue;
    let event;
    try {
      event = JSON.parse(line);
    } catch {
      // A torn line costs itself, as it does the driver's own reader.
      continue;
    }
    if (event === null || typeof event !== 'object' || event.kind !== 'tool') continue;
    const id = named(event.id);
    let call = id === null ? undefined : byId.get(id);
    if (!call) {
      call = { id, kind: null, title: null, locations: null, line: null, status: null, input: null, output: null, content: null };
      calls.push(call);
      if (id !== null) byId.set(id, call);
    }
    if (typeof event.toolKind === 'string') call.kind = event.toolKind;
    if (typeof event.title === 'string') call.title = event.title;
    if (typeof event.status === 'string') call.status = event.status;
    if (typeof event.input === 'string') call.input = event.input;
    if (typeof event.output === 'string') call.output = event.output;
    if (Number.isInteger(event.line)) call.line = event.line;
    if (Array.isArray(event.locations)) {
      const paths = event.locations.filter((path) => named(path));
      if (paths.length) call.locations = paths;
    }
    if (Array.isArray(event.content)) call.content = event.content.filter((part) => part !== null && typeof part === 'object');
  }
  return calls.map(({ content, output, ...call }) => {
    const texts = (content ?? []).filter((part) => part.type === 'text' && typeof part.text === 'string');
    const diff = (content ?? []).find((part) => part.type === 'diff' && named(part.path));
    return {
      ...call,
      args: inputOf(call.input),
      diffPath: diff ? diff.path : null,
      chars: Math.max(texts.reduce((sum, part) => sum + stated(part.text), 0), output === null ? 0 : stated(output)),
      sized: texts.length > 0 || output !== null,
    };
  });
}

/** The home as a `/`-spelled absolute path, without a trailing slash. */
function topOf(home) {
  const at = slash(home).replace(/\/+$/, '');
  return ABSOLUTE.test(`${at}/`) ? at : slash(resolve(home)).replace(/\/+$/, '');
}

/**
 * Where a path lies: in a session tree, as the tree's three names and the path inside it, or outside every tree. A path
 * spelled relative is the session's tree's, since the tree is where it runs. Outside, the home is said `<home>`.
 */
export function placeOf(path, home) {
  const at = slash(path).trim().replace(/^(?:\.\/)+/, '');
  if (!at) return null;
  if (!ABSOLUTE.test(at)) return { tree: null, rel: at, inside: !(at === '..' || at.startsWith('../')) };
  const top = home ? topOf(home) : null;
  const lower = at.toLowerCase();
  if (top) {
    const trees = `${top.toLowerCase()}/trees/`;
    const parts = lower.startsWith(trees) ? at.slice(trees.length).split('/') : [];
    if (parts.length >= 3 && parts.slice(0, 3).every((part) => part && part !== '.' && part !== '..')) {
      return { tree: parts.slice(0, 3), rel: parts.slice(3).join('/'), inside: true };
    }
  }
  const minted = MINTED.exec(at);
  if (minted && minted[1] !== '..' && minted[2] !== '..') return { tree: [minted[1], minted[2], minted[3]], rel: minted[4] ?? '', inside: true };
  if (top && lower.startsWith(`${top.toLowerCase()}/`)) return { tree: null, rel: `<home>/${at.slice(top.length + 1)}`, inside: false };
  return { tree: null, rel: at, inside: false };
}

/** The place a call names first: its first location, else its input's path, else its diff's. */
function firstPath(call) {
  return call.locations?.[0] ?? named(call.args.file_path) ?? named(call.args.notebook_path) ?? named(call.args.path) ?? call.diffPath ?? null;
}

/** A session's tree, the first one its calls name, as its three names; null when none names one. */
export function treeOf(calls, home) {
  for (const call of calls) {
    const path = firstPath(call);
    const place = path === null ? null : placeOf(path, home);
    if (place?.tree) return place.tree;
  }
  return null;
}

/** An index's place from its declared path: its folder, `/`-ended, or the file itself where it lies at the top. */
function indexEntry(declared) {
  const at = slash(declared).trim().replace(/^(?:\.\/)+/, '');
  if (!at) return null;
  if (at.endsWith('/')) return at;
  if (!/\.[A-Za-z0-9]+$/.test(posix.basename(at))) return `${at}/`;
  const folder = posix.dirname(at);
  return folder === '.' ? at : `${folder}/`;
}

/** The index a tree's `daoris.json` declares (`documents.index`, a path or `{ path }`), as its place; null for none. */
export function declaredIndex(tree) {
  let manifest;
  try {
    manifest = JSON.parse(readFileSync(join(tree, 'daoris.json'), 'utf8'));
  } catch {
    return null;
  }
  const role = manifest?.documents?.index;
  const path = named(role) ?? named(role?.path);
  return path ? indexEntry(path) : null;
}

/** What a driven session spent before its first edit, from its calls; `index` the places that are its index. */
export function summarizeEvents(calls, { home = null, index = [INDEX] } = {}) {
  const entries = [...new Set(index.map(indexEntry).filter(Boolean))];
  const own = treeOf(calls, home)?.join('/').toLowerCase() ?? null;
  const where = (path) => {
    const place = placeOf(path, home);
    if (!place?.tree || own === null || place.tree.join('/').toLowerCase() === own) return place;
    return { tree: place.tree, rel: `trees/${place.tree.join('/')}/${place.rel}`, inside: false };
  };
  const inIndex = (rel) => entries.some((entry) => (entry.endsWith('/') ? rel.toLowerCase().startsWith(entry.toLowerCase()) : rel.toLowerCase() === entry.toLowerCase()));
  const isShell = (call) => call.kind === 'execute' || call.title === 'PowerShell';
  const editsWork = (call) => {
    if (!EDIT_KINDS.has(call.kind)) return false;
    const path = firstPath(call);
    // A change that names no place is the work's: the session runs in its tree.
    if (path === null) return true;
    const place = where(path);
    return place.inside && !/^local(?:\/|$)/i.test(place.rel);
  };

  const first = calls.findIndex(editsWork);
  const before = first < 0 ? calls : calls.slice(0, first);
  const byKind = {};
  const shell = Object.fromEntries(KINDS.map((kind) => [kind, 0]));
  const reads = [];
  let chars = 0;
  let unsized = 0;
  let knowledge = 0;
  let indexReads = 0;
  for (const call of before) {
    const kind = isShell(call) ? 'execute' : call.kind ?? '(unsaid)';
    byKind[kind] = (byKind[kind] ?? 0) + 1;
    chars += call.chars;
    if (!call.sized) unsized += 1;
    if (/knowledge_search/.test(call.title ?? '')) knowledge += 1;
    if (isShell(call)) {
      const command = named(call.args.command) ?? (call.title ?? '').replace(/^`|`$/g, '');
      shell[shellKind(command)] += 1;
      if (entries.some((entry) => slash(command).toLowerCase().includes(entry.toLowerCase()))) indexReads += 1;
    } else if (call.kind === 'read') {
      const path = firstPath(call);
      const place = path === null ? { rel: call.title ?? '(unplaced)', inside: false } : where(path);
      const whole = call.args.offset == null && call.args.limit == null;
      reads.push({ path: place.rel, chars: call.chars, whole, inside: place.inside });
      if (place.inside && inIndex(place.rel)) indexReads += 1;
    }
  }
  const whole = reads.filter((read) => read.whole);
  const edit = first < 0 ? null : calls[first];
  const editPath = edit ? firstPath(edit) : null;
  return {
    total: calls.length,
    before: before.length,
    byKind,
    chars,
    unsized,
    shell,
    knowledge,
    reads,
    whole: whole.length,
    ranged: reads.length - whole.length,
    wholeLarge: whole.filter((read) => read.chars >= OUTLINED).length,
    index: entries,
    indexReads,
    firstEdit: edit ? { kind: edit.kind, path: editPath === null ? '(unplaced)' : where(editPath).rel } : null,
  };
}

/** The files read most among these reads: each path's reads and characters, the decisions as one row. */
function mostRead(reads, top) {
  const files = new Map();
  for (const read of reads) {
    const path = decision(read.path);
    const row = files.get(path) ?? { path, reads: 0, chars: 0 };
    row.reads += 1;
    row.chars += read.chars;
    files.set(path, row);
  }
  return [...files.values()].sort((a, b) => b.reads - a.reads || b.chars - a.chars || (a.path < b.path ? -1 : 1)).slice(0, top);
}

/** A group of sessions: the median editing session before its first edit, the index opened first, the parks, the files read most. */
export function groupOf(name, sessions, { top = 10 } = {}) {
  const read = sessions.filter((session) => session.summary);
  const editing = read.filter((session) => session.summary.firstEdit);
  const middle = (pick) => median(editing.map((session) => pick(session.summary)));
  return {
    name,
    sessions: sessions.length,
    withoutEvents: sessions.length - read.length,
    editing: editing.length,
    openedIndex: editing.filter((session) => session.summary.indexReads > 0).length,
    parked: sessions.reduce((sum, session) => sum + session.parked, 0),
    median: editing.length === 0 ? null : {
      before: middle((s) => s.before),
      chars: middle((s) => s.chars),
      searchesAndDumps: middle((s) => s.shell.search + s.shell.dump),
      wholeLarge: middle((s) => s.wholeLarge),
    },
    files: mostRead(read.flatMap((session) => session.summary.reads), top),
  };
}

/** Sessions grouped by a field's value, by name. */
function groupsBy(sessions, field) {
  const groups = new Map();
  for (const session of sessions) groups.set(session[field], [...(groups.get(session[field]) ?? []), session]);
  return [...groups].sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)).map(([name, members]) => groupOf(name, members));
}

/**
 * A home's driven sessions (`session.started` with kind `driven`, a set-up's left out), each with its parks and what it
 * spent before its first edit, then each repository, workspace and all. `repository` keeps one repository's (by name,
 * in any case), `since` the sessions started at or after it. A session whose events file is missing has no summary.
 */
export function readHome(home, { repository = null, since = null } = {}) {
  const { lines, skipped } = readLogs(join(home, 'logs'), since ? { since } : {});
  const sessions = [];
  const seen = new Set();
  const left = { setups: 0, other: 0 };
  for (const line of lines) {
    if (line.event !== 'session.started') continue;
    const session = named(line.data.session);
    if (!session || seen.has(session)) continue;
    seen.add(session);
    const name = named(line.data.repository) ?? '(unnamed)';
    if (repository && name.toLowerCase() !== repository.toLowerCase()) continue;
    if (line.data.kind !== 'driven') left.other += 1;
    else if (line.data.setup === true) left.setups += 1;
    else {
      sessions.push({
        session, repository: name, workspace: named(line.data.workspace) ?? '(unnamed)', adapter: named(line.data.adapter) ?? '(unnamed)',
        started: line.stamp, parked: 0, summary: null,
      });
    }
  }
  const byId = new Map(sessions.map((entry) => [entry.session, entry]));
  for (const line of lines) {
    const entry = line.event === 'session.parked' ? byId.get(named(line.data.session)) : undefined;
    if (entry) entry.parked += 1;
  }

  const kept = new Map();
  for (const entry of sessions) {
    if (!SESSION_ID.test(entry.session) || entry.session.includes('..')) continue;
    let text;
    try {
      text = readFileSync(join(home, 'sessions', `${entry.session}.events.jsonl`), 'utf8');
    } catch {
      continue;
    }
    const calls = readEvents(text);
    const tree = treeOf(calls, home);
    kept.set(entry.session, { calls, index: tree ? declaredIndex(join(home, 'trees', ...tree)) : null });
  }
  // A tree is removed once its work has landed, so a repository's declaration is kept from whichever tree still has it.
  const declared = new Map();
  for (const entry of sessions) {
    const index = kept.get(entry.session)?.index;
    if (index && !declared.has(entry.repository)) declared.set(entry.repository, index);
  }
  for (const entry of sessions) {
    const events = kept.get(entry.session);
    if (!events) continue;
    const index = [INDEX, events.index ?? declared.get(entry.repository)].filter(Boolean);
    entry.summary = summarizeEvents(events.calls, { home, index });
  }

  return {
    home, skipped, left, sessions,
    repositories: groupsBy(sessions, 'repository'),
    workspaces: groupsBy(sessions, 'workspace'),
    all: groupOf('all', sessions),
  };
}

/** A group's line: its sessions, its median editing session before the first edit, the index opened first, its parks. */
function groupLine(label, group) {
  const m = group.median;
  const spent = m
    ? `the median editing session before its first edit: ${plural(m.before, 'call')}, ${thousands(m.chars)} chars, `
      + `${plural(m.searchesAndDumps, 'shell search or dump', 'shell searches and dumps')}, ${plural(m.wholeLarge, 'whole read')} of 40K+ chars`
    : 'no session edited';
  return `${label}: ${plural(group.sessions, 'session')}, ${group.editing} editing, ${group.withoutEvents} without events; ${spent}; `
    + `${group.openedIndex} of ${group.editing} opened the index first; ${group.parked} parked`;
}

/** The home's report: what it read, a paragraph per session, then each repository with its files read most, each workspace, and all. */
export function homeReport(read) {
  const without = read.sessions.filter((session) => !session.summary);
  const out = [
    `orient-report: read the machine log under ${join(read.home, 'logs')}: ${plural(read.sessions.length, 'driven session')} `
      + `(${plural(read.left.setups, 'set-up')} and ${plural(read.left.other, 'other session')} left out); `
      + `${read.sessions.length - without.length} with events, ${without.length} without${without.length ? ` (${without.map((s) => s.session).join(', ')})` : ''}`
      + `${read.skipped ? `; ${plural(read.skipped, 'log line')} unreadable` : ''}`,
    'Sizes are characters, not bytes: the typed events keep characters, so none compares with a transcript\'s bytes.',
  ];
  for (const session of read.sessions) {
    const head = `${session.session} ${session.repository} (${session.workspace}, ${session.adapter}, ${session.started.slice(0, 16)}Z)`;
    const s = session.summary;
    if (!s) {
      out.push('', `${head}: no events file`);
      continue;
    }
    const share = s.total ? Math.round((100 * s.before) / s.total) : 0;
    const edit = s.firstEdit ? `first edit: ${s.firstEdit.kind} ${s.firstEdit.path}` : 'no edit';
    const kinds = Object.entries(s.byKind).sort((a, b) => b[1] - a[1] || (a[0] < b[0] ? -1 : 1)).map(([kind, n]) => `${kind} ${n}`);
    const sized = s.unsized ? `${plural(s.unsized, 'call')} with no result size` : 'every call\'s size known';
    out.push(
      '',
      `${head}: ${s.total} calls, ${s.before} before the first edit (${share}%), ${thousands(s.chars)} chars read before it; ${edit}`,
      `  by kind: ${kinds.join(', ') || 'none'}`,
      `  reads ${s.reads.length} (${s.whole} whole, ${s.ranged} ranged; ${s.wholeLarge} whole of 40K+ chars), ${s.indexReads} of the index (${s.index.join(', ')}); `
        + `shell: ${KINDS.map((kind) => `${kind} ${s.shell[kind]}`).join(', ')}; knowledge_search ${s.knowledge}; ${sized}; parked ${session.parked}`,
    );
  }
  out.push('');
  for (const group of read.repositories) {
    out.push(groupLine(`repository ${group.name}`, group));
    if (group.files.length) {
      out.push('  files read most before the first edit (reads, chars, path):');
      for (const file of group.files) out.push(`${String(file.reads).padStart(5)} ${thousands(file.chars).padStart(5)}  ${file.path}`);
    }
  }
  for (const group of read.workspaces) out.push(groupLine(`workspace ${group.name}`, group));
  out.push(groupLine('all', read.all));
  return `${out.join('\n')}\n`;
}

/** `--home <dir>` with `--repository <name>` and `--since <date>`, or transcripts; `--json` with either. Throws a sentence. */
export function parseArguments(argv) {
  let home = null;
  let repository = null;
  let since = null;
  let json = false;
  const files = [];
  for (let i = 0; i < argv.length; i++) {
    const flag = argv[i];
    const value = argv[i + 1];
    const taken = () => {
      if (!value || value.startsWith('--')) throw new Error(`${flag} takes ${flag === '--home' ? 'a folder' : flag === '--since' ? 'a date' : 'a name'}`);
      i++;
      return value;
    };
    if (flag === '--home') home = taken();
    else if (flag === '--repository') repository = taken();
    else if (flag === '--since') {
      const date = taken();
      const when = /^\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,3})?)?Z)?$/.test(date) ? new Date(date) : null;
      if (!when || Number.isNaN(when.getTime())) throw new Error('--since takes a date, such as 2026-10-01');
      since = when;
    } else if (flag === '--json') json = true;
    else if (flag.startsWith('--')) throw new Error(`\`${flag}\` is not an option`);
    else files.push(flag);
  }
  if (home && files.length) throw new Error('name a home or transcripts, not both');
  if (!home && (repository || since)) throw new Error('--repository and --since read a home: name it with --home');
  if (!home && files.length === 0) throw new Error('name a home (--home <dir>) or the transcripts to read');
  return { home, repository, since, json, files };
}

/** The runner: 0 reported · 2 a flag it cannot use, a home with no machine log, or a transcript it could not read. */
export function main(argv, io = { out: (text) => process.stdout.write(text), err: (text) => console.error(text) }) {
  let options;
  try {
    options = parseArguments(argv);
  } catch (error) {
    io.err(`orient-report: ${error.message}.`);
    io.err(USAGE);
    return 2;
  }

  if (options.home) {
    const logs = join(options.home, 'logs');
    if (!existsSync(logs)) {
      io.err(`orient-report: no machine log in ${logs}: nothing has written one there, or the home is another.`);
      return 2;
    }
    const read = readHome(options.home, options);
    io.out(options.json ? `${JSON.stringify(read, null, 2)}\n` : homeReport(read));
    return 0;
  }

  const summaries = [];
  const unread = [];
  for (const file of options.files) {
    let text;
    try {
      text = readFileSync(file, 'utf8');
    } catch (error) {
      unread.push(`${file}: ${error.code ?? error.message}`);
      continue;
    }
    summaries.push(summarize(readTranscript(text), basename(file).replace(/\.(jsonl|output|json)$/, '')));
  }
  if (summaries.length) io.out(options.json ? `${JSON.stringify(summaries, null, 2)}\n` : report(summaries));
  if (unread.length) {
    io.err(`orient-report: could not read ${unread.length} transcript(s):\n${unread.map((line) => `  ${line}`).join('\n')}`);
    return 2;
  }
  return 0;
}

if (isMain(import.meta.url)) {
  process.exitCode = main(process.argv.slice(2));
}
