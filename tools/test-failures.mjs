#!/usr/bin/env node
/**
 * The failure reader and the signature (CISCOPE1c, D207; design `docs/2026-10-24-ci-scope-design.md` §4.1 point 4, §3.5).
 *
 * ## Why
 *
 * Every judgement of a failure compares runs made on different trees: a candidate and its base, the prefixes of a batch, the
 * commits of recall's search. The merge tool's readers name a failing test with its theory's case cut off (`testSummary`,
 * `classResults`), and a message carries what each run makes fresh (a GUID, a session's tree id, a hash, an actual value), so
 * neither tells one failure from another across runs. This reads what a failure **is**: the test with its case, the exception
 * type, and where it failed, as the frame's method and the text of its source line, never the line's number, since a line
 * inserted above an assertion moves its number and not the failure.
 *
 * ## The reader
 *
 * - **trx** (`dotnet test --logger trx`): each `UnitTestResult` whose outcome is a failure, its `testName` whole (the case
 *   kept), its class from its definition, the first line of its `Message` and its `StackTrace`'s frames.
 * - **vitest** (`--reporter=json`): each assertion result that failed, its file, its titles joined, and the first failure
 *   message's first line and frames.
 * - **`node --test`** (`--test-reporter=tap`): each `not ok` that is a test's own failure (a suite failing only because a subtest
 *   did is not), its titles joined, its `error` and `name`, and its `stack`'s frames.
 *
 * A failure reads `{ runner, unit, test, exception, message, frames }`; a frame is `{ method, file, line }` with `file` as the
 * stack gave it.
 *
 * ## The signature
 *
 * `signatureOf(failure, read)`: `{ test, exception, location }` where the location is the first frame in the test's own file
 * (the trx's class's file, the vitest or `node --test` unit) as `{ method, text, ordinal }`: the method with its compiler's async
 * wrapping removed, the source line's text read from the tree the run used (`read(path)`, repository-relative) and normalised
 * as messages are, and its ordinal among identical lines of that method. Where no location can be read the message line,
 * normalised, takes its place (`{ test, exception, message }`). A gate that names no failing test is signed by
 * `gateSignature`: the gate, the step, and for a build the **set** of its compiler error codes with their files, since MSBuild
 * prints each project's errors as its node finishes; otherwise the step's first error line. `sameSignature` compares two.
 *
 *   node tools/test-failures.mjs <report>…     each failure a report holds, and its signature against this tree
 *
 * Exit codes: 0 read · 2 a tool error (no such file, a report it cannot read).
 */
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { isMain } from './fsx.mjs';

// ---------------------------------------------------------------------------------------------------
// Normalising a line

/**
 * A message or source line with what a run makes fresh replaced: GUIDs, hex runs of eight characters or more, absolute paths,
 * then numbers; its spaces collapsed. Two runs of one failure read the same line.
 */
export function normalise(line) {
  return String(line ?? '')
    .replace(/\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/gi, '<guid>')
    .replace(/(?:file:\/\/\/?)?(?:[A-Za-z]:[\\/]|\\\\|\/(?=[\w.-]+\/))[^\s'"`:,;)\]}>]*/g, '<path>')
    .replace(/\b(?=[0-9a-f]*[a-f])(?=[0-9a-f]*\d)[0-9a-f]{8,}\b/gi, '<hex>')
    .replace(/\d+(?:\.\d+)?/g, '<n>')
    .replace(/\s+/g, ' ')
    .trim();
}

// ---------------------------------------------------------------------------------------------------
// Frames

const XML_ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'" };
const unxml = (text) => String(text ?? '').replace(/&(amp|lt|gt|quot|apos|#(\d+)|#x([0-9a-f]+));/gi, (_, entity, dec, hex) =>
  dec ? String.fromCodePoint(Number(dec)) : hex ? String.fromCodePoint(parseInt(hex, 16)) : XML_ENTITIES[entity]);
const attributes = (tag) => Object.fromEntries([...tag.matchAll(/([A-Za-z_:][\w:.-]*)="([^"]*)"/g)].map(([, name, value]) => [name, unxml(value)]));

/**
 * A .NET method as a frame names it, its compiler's wrapping removed: `N.C.<Run>d__5.MoveNext()` is `N.C.Run`, a lambda's
 * `N.C.<>c__DisplayClass3_0.<Run>b__0()` is `N.C.Run`, a local function's `N.C.<Run>g__Local|2_0` is `N.C.Run`.
 */
export function dotnetMethod(method) {
  let text = String(method).replace(/\(.*$/, '').trim();
  text = text.replace(/\.<>c(?:__DisplayClass[\w]*)?\./g, '.');
  text = text.replace(/\.<([^>]+)>[a-z]__[^.]*(?:\.MoveNext)?$/, '.$1');
  text = text.replace(/\.<([^>]+)>[a-z]__[^.]*\.MoveNext$/, '.$1');
  return text.replace(/\+<>c$/, '');
}

/** The frames of a .NET stack trace: `at N.C.M() in <file>:line <n>`; a frame without a file is kept with none. */
export function dotnetFrames(stack) {
  const frames = [];
  for (const line of String(stack ?? '').split(/\r?\n/)) {
    const at = /^\s*at\s+(.+?)(?:\s+in\s+(.+):line\s+(\d+))?\s*$/.exec(line);
    if (!at) continue;
    frames.push({ method: dotnetMethod(at[1]), file: at[2] ?? null, line: at[3] ? Number(at[3]) : null });
  }
  return frames;
}

/**
 * The frames of a V8 stack (vitest's, `node --test`'s): `at M (<file>:<line>:<col>)` or `at <file>:<line>:<col>`, a
 * `file://` URL read as its path; a TAP stack's lines carry no `at`.
 */
export function v8Frames(stack) {
  const frames = [];
  for (const raw of String(stack ?? '').split(/\r?\n/)) {
    const line = raw.trim().replace(/^at\s+/, '');
    const framed = /^(.*?)\s*\((.+):(\d+):(\d+)\)$/.exec(line) ?? /^()(\S.*):(\d+):(\d+)$/.exec(line);
    if (!framed) continue;
    let file = framed[2];
    if (file.startsWith('file://')) file = decodeURIComponent(file.replace(/^file:\/\/\/?/, '')).replace(/^(?=[A-Za-z]:)/, '');
    if (/^(node:|internal\/)/.test(file)) continue;
    frames.push({ method: framed[1] || '<anonymous>', file, line: Number(framed[3]) });
  }
  return frames;
}

/** A path as a frame gives it, made relative to the repository: from its root where it is under it, else from its first `src/` or `tools/` part. */
export function repositoryPath(file, root = null) {
  if (!file) return null;
  const path = String(file).split('\\').join('/');
  if (root) {
    const base = String(root).split('\\').join('/').replace(/\/$/, '');
    if (path.toLowerCase().startsWith(`${base.toLowerCase()}/`)) return path.slice(base.length + 1);
  }
  if (!/^([A-Za-z]:)?\//.test(path)) return path;
  const at = path.search(/\/(src|tools|examples|canon)\//);
  return at >= 0 ? path.slice(at + 1) : null;
}

// ---------------------------------------------------------------------------------------------------
// The exception type

/** A failure's exception type, from its message's first line: `System.X : …` or `TypeError: …`, an xUnit `Assert.Equal()` by its assertion. */
export function exceptionOf(message, named = null) {
  if (named) return named;
  const first = String(message ?? '').split(/\r?\n/)[0].trim();
  const typed = /^([A-Za-z_][\w.`]*(?:Exception|Error))(?:\s*\[[^\]]*\])?\s*:/.exec(first) ?? /^([A-Za-z_][\w.`]*(?:Exception|Error))$/.exec(first);
  if (typed) return typed[1];
  const asserted = /^(Assert\.\w+)\(\)/.exec(first);
  if (asserted) return asserted[1];
  return '';
}

const firstLine = (message) => String(message ?? '').split(/\r?\n/).find((line) => line.trim())?.trim() ?? '';

// ---------------------------------------------------------------------------------------------------
// The readers

/** The failures of a trx: each failed result with its case, class, exception, message line and frames. */
export function readTrx(trx) {
  const classOf = new Map();
  for (const [, id, body] of trx.matchAll(/<UnitTest\b[^>]*?\bid="([^"]+)"[^>]*>([\s\S]*?)<\/UnitTest>/g)) {
    const method = /<TestMethod\b[^>]*>/.exec(body);
    if (method) classOf.set(id, attributes(method[0]).className ?? '');
  }
  const failures = [];
  for (const [whole, tag] of trx.matchAll(/(<UnitTestResult\b[^>]*?)(?:\/>|>[\s\S]*?<\/UnitTestResult>)/g)) {
    const result = attributes(tag);
    if (result.outcome !== 'Failed' && result.outcome !== 'Error' && result.outcome !== 'Timeout' && result.outcome !== 'Aborted') continue;
    const test = result.testName ?? '';
    const unit = classOf.get(result.testId) || test.replace(/\(.*$/, '').split('.').slice(0, -1).join('.');
    const message = unxml(/<Message>([\s\S]*?)<\/Message>/.exec(whole)?.[1] ?? '');
    const stack = unxml(/<StackTrace>([\s\S]*?)<\/StackTrace>/.exec(whole)?.[1] ?? '');
    failures.push({ runner: 'trx', unit, test, exception: exceptionOf(message), message: firstLine(message), frames: dotnetFrames(stack) });
  }
  return failures;
}

/** The failures of vitest's JSON report: each failed assertion result with its file, its titles, exception, message line and frames. */
export function readVitest(json) {
  const report = typeof json === 'string' ? JSON.parse(json) : json;
  const failures = [];
  for (const file of report?.testResults ?? []) {
    for (const result of file.assertionResults ?? []) {
      if (result.status !== 'failed') continue;
      const text = String(result.failureMessages?.[0] ?? '');
      const test = result.fullName || [...(result.ancestorTitles ?? []), result.title].join(' > ');
      failures.push({
        runner: 'vitest', unit: file.name, test, exception: exceptionOf(text), message: firstLine(text), frames: v8Frames(text),
      });
    }
  }
  return failures;
}

/** A TAP block's YAML value: a plain or quoted scalar, or a `|-` block's lines. */
function yamlValue(lines, key, indent) {
  const at = lines.findIndex((line) => line.startsWith(`${indent}${key}:`));
  if (at < 0) return null;
  const rest = lines[at].slice(indent.length + key.length + 1).trim();
  if (rest === '|-' || rest === '|' || rest === '>-') {
    const out = [];
    for (const line of lines.slice(at + 1)) {
      if (!line.startsWith(`${indent}  `) && line.trim()) break;
      out.push(line.slice(indent.length + 2));
    }
    return out.join('\n');
  }
  return rest.replace(/^'(.*)'$/, (_, inner) => inner.replace(/''/g, "'")).replace(/^"(.*)"$/, '$1');
}

/**
 * The failures of a `node --test --test-reporter=tap` report: each `not ok` that failed in its own code (`failureType:
 * 'testCodeFailure'`, or a hook's), never a suite failing only for its subtests. Its name is its titles joined, its unit the
 * file its `location` names, or the `# Subtest:` file at the top.
 */
export function readNodeTap(tap, unit = null) {
  const lines = String(tap).replace(/\r\n/g, '\n').split('\n');
  const failures = [];
  const titles = [];
  for (let at = 0; at < lines.length; at += 1) {
    const subtest = /^(\s*)# Subtest: (.*)$/.exec(lines[at]);
    if (subtest) {
      const depth = subtest[1].length / 4;
      titles.length = depth;
      titles[depth] = subtest[2];
      continue;
    }
    const result = /^(\s*)(not ok|ok) \d+ - (.*?)(?:\s+#.*)?$/.exec(lines[at]);
    if (!result) continue;
    const indent = result[1];
    const depth = indent.length / 4;
    const block = [];
    if (lines[at + 1]?.trim() === '---') {
      for (let next = at + 2; next < lines.length && lines[next].trim() !== '...'; next += 1) block.push(lines[next]);
    }
    if (result[2] === 'not ok') {
      const inner = `${indent}  `;
      const kind = yamlValue(block, 'failureType', inner);
      if (kind !== 'subtestsFailed') {
        const location = yamlValue(block, 'location', inner);
        const file = location ? location.replace(/:\d+:\d+$/, '').replace(/\\\\/g, '\\') : null;
        const error = yamlValue(block, 'error', inner) ?? '';
        const name = yamlValue(block, 'name', inner);
        const stack = yamlValue(block, 'stack', inner) ?? '';
        failures.push({
          runner: 'node', unit: unit ?? (file ? file : titles[0] ?? null),
          test: [...titles.slice(0, depth), result[3]].filter(Boolean).join(' > '),
          exception: exceptionOf(error, name), message: firstLine(error), frames: v8Frames(stack),
        });
      }
    }
    titles.length = depth;
  }
  return failures;
}

/** A report's failures, its kind told by its content: a trx, vitest's JSON, or `node --test`'s TAP. */
export function readReport(text, unit = null) {
  const trimmed = String(text).trimStart();
  if (trimmed.startsWith('<') && /<TestRun\b/.test(trimmed)) return readTrx(trimmed);
  if (trimmed.startsWith('{')) return readVitest(trimmed);
  if (/^(TAP version|# Subtest:|(not )?ok \d)/m.test(trimmed)) return readNodeTap(trimmed, unit);
  throw new Error('not a trx, a vitest JSON report or a node --test TAP report');
}

// ---------------------------------------------------------------------------------------------------
// The signature

/** The test's own file in a frame, as the failure names its unit: the class's simple name for a trx, the unit's path otherwise. */
function ownFrame(failure, root) {
  if (failure.runner === 'trx') {
    const unit = failure.unit.split('+')[0];
    return failure.frames.find((frame) => frame.file && (frame.method.startsWith(`${unit}.`) || frame.method.startsWith(`${failure.unit}.`)))
      ?? null;
  }
  const own = repositoryPath(failure.unit, root);
  return failure.frames.find((frame) => frame.file && own && repositoryPath(frame.file, root) === own) ?? null;
}

/** Where a method's body starts above `line` (1-based): the nearest line above that declares it, or the file's first. */
function methodStart(lines, line, method) {
  const name = method.split('.').at(-1).replace(/[<>]/g, '');
  if (!name || name === 'anonymous') return 0;
  const declares = new RegExp(`\\b${name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\s*[(<]`);
  for (let at = line - 1; at >= 0; at -= 1) if (declares.test(lines[at])) return at;
  return 0;
}

/**
 * A failure's signature: its test with its case, its exception type, and its location as `{ method, text, ordinal }`, the text
 * read from the tree the run used through `read(path)`; or, where no location can be read, its message line normalised.
 */
export function signatureOf(failure, read = null, root = null) {
  const frame = ownFrame(failure, root);
  const path = frame ? repositoryPath(frame.file, root) : null;
  const source = path && read ? read(path) : null;
  if (frame && frame.line && typeof source === 'string' && source) {
    const lines = source.replace(/\r\n/g, '\n').split('\n');
    const text = normalise(lines[frame.line - 1] ?? '');
    if (text) {
      const from = methodStart(lines, frame.line, frame.method);
      let ordinal = 0;
      for (let at = from; at < frame.line - 1; at += 1) if (normalise(lines[at]) === text) ordinal += 1;
      return { test: failure.test, exception: failure.exception, location: { method: frame.method, text, ordinal } };
    }
  }
  return { test: failure.test, exception: failure.exception, message: normalise(failure.message) };
}

/**
 * A gate that names no failing test, signed by its step: for a build, the set of its compiler errors' codes with their files
 * (`CS0103 src/…/X.cs`), sorted, since MSBuild prints each project's errors as its node finishes; otherwise the step's first
 * error line, normalised.
 */
export function gateSignature(gate, step, output, root = null) {
  const errors = new Set();
  for (const [, file, code] of String(output ?? '').matchAll(/^\s*(.+?)\(\d+,\d+\):\s+error\s+((?:CS|TS|MSB|NU)\d+)\b/gm)) {
    errors.add(`${code} ${repositoryPath(file.trim(), root) ?? file.trim()}`);
  }
  for (const [, file, code] of String(output ?? '').matchAll(/^(.+?\.tsx?)\(\d+,\d+\): error (TS\d+)/gm)) errors.add(`${code} ${repositoryPath(file.trim(), root) ?? file.trim()}`);
  if (step === 'build' && errors.size) return { gate, step, errors: [...errors].sort() };
  const first = String(output ?? '').split(/\r?\n/).find((line) => /\berror\b|\bfailed\b|\bError:/i.test(line)) ?? '';
  return { gate, step, message: normalise(first) };
}

/** Whether two signatures are one failure: every field equal, a build's errors as sets. */
export function sameSignature(a, b) {
  return signatureKey(a) === signatureKey(b);
}

/** A signature as one string, its fields in a fixed order. */
export function signatureKey(signature) {
  if (!signature) return '';
  const { test, exception, location, message, gate, step, errors } = signature;
  return JSON.stringify(gate !== undefined
    ? [gate, step, errors ? [...errors].sort() : null, message ?? null]
    : [test, exception, location ? [location.method, location.text, location.ordinal] : null, location ? null : message ?? null]);
}

// ---------------------------------------------------------------------------------------------------
// The tool

function main(argv) {
  if (!argv.length) {
    console.error('usage: node tools/test-failures.mjs <report>…');
    return 2;
  }
  const root = process.cwd();
  const read = (path) => (existsSync(join(root, path)) ? readFileSync(join(root, path), 'utf8') : null);
  for (const file of argv) {
    if (!existsSync(file)) throw new Error(`no such report: ${file}`);
    const failures = readReport(readFileSync(file, 'utf8'), file);
    console.log(`${file}: ${failures.length} failure${failures.length === 1 ? '' : 's'}`);
    for (const failure of failures) {
      const signature = signatureOf(failure, read, root);
      const where = signature.location ? `${signature.location.method}: ${signature.location.text}${signature.location.ordinal ? ` (#${signature.location.ordinal + 1})` : ''}` : `message: ${signature.message}`;
      console.log(`  ${failure.test}\n    ${failure.exception || '(no exception type)'} at ${where}`);
    }
  }
  return 0;
}

if (isMain(import.meta.url)) {
  try {
    process.exitCode = main(process.argv.slice(2));
  } catch (error) {
    console.error(`test-failures: ${error.message}`);
    process.exitCode = 2;
  }
}
