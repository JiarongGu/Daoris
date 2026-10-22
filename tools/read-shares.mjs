/**
 * READSHARE1: no plain read of a file the driver's AtomicFile writes (FIX-LOG 2026-10-22, BINDRACE1; `AtomicFile.ReadText`).
 *
 *   node tools/read-shares.mjs        check the driver's sources (exit 0 clean, 1 breach, 2 tool error)
 *
 * Windows' move keeps the file it moves open for deletion for a moment after the file lands, so a read that does not share deletion
 * (`File.ReadAllText`, `File.ReadAllBytes`, `File.ReadLines`, `File.OpenRead`, a `FileStream` opened to read without
 * `FileShare.Delete`) is refused then with a sharing violation. A file `AtomicFile` writes is read through `AtomicFile.ReadText` or
 * `AtomicFile.ReadBytes`, which share it. This census reads the driver's C# (tests, `bin/` and `obj/` skipped) in two steps:
 *
 *  - a writer: a source that writes through `AtomicFile` (`WriteText`, `WriteBytes`, `TryCreateText`) or through one of the driver's
 *    writers that ends in it (`JsonFile.Write`, `JsonKeeping.Save`, `RecordList<…>`, `PluginFolders.WriteFile`). The types it
 *    declares are writer types, and their members whose names say where they keep a file (`PathOf`, `FilePath`, `FolderOf`, any name
 *    holding `Path` or `Folder`) are the places those writes land, as are the home's entries a writer names (`HomeEntry.Remotes`);
 *  - a breach: a plain read in a writer, or in a source that names one of those places (`WorkflowRunBindings.PathOf(`,
 *    `HomeEntry.Remotes`).
 *
 * The census works per source, not per path, so a writer that also reads a file nothing moves into place (a git `HEAD`, a transcript
 * appended to) marks that read's line `// read-shares: <what it reads>`, and the clean line counts the marks. A read inside a comment
 * or a string is not a read. A reader handed its path by its caller, naming no place, is not found: the census is a net under the
 * readers, and `AtomicFile.ReadText`'s own remarks are the rule. There is no .NET build.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';
import { blank } from './partials.mjs';

export const DRIVER = 'src/Daoris.Desktop/Daoris.Desktop.Driver';
const SKIPPED = new Set(['bin', 'obj', 'node_modules']);
const MARK = '// read-shares:';

const WRITES = /\bAtomicFile\.(?:WriteText|WriteBytes|TryCreateText)\s*\(|\bJsonFile\.Write\s*\(|\bJsonKeeping\.(?:Save|SaveHolding)\s*\(|\bRecordList\s*<|\bPluginFolders\.WriteFile\s*\(/;
const TYPES = /\b(?:class|record|struct|interface)\s+([A-Za-z_]\w*)/g;
const PLACE = /^[A-Z]\w*(?:Path|Folder)\w*$|^(?:Path|Folder)\w*$/;
const MEMBERS = /\b(?:public|internal)\s+(?:static\s+)?(?:readonly\s+)?string\??\s+([A-Za-z_]\w*)\s*(?:\(|=>|\{|=|;)/g;
const PLAIN = /(?<![.\w])(?:System\.IO\.)?File\.(ReadAllText|ReadAllBytes|ReadAllLines|ReadLines|OpenRead|OpenText)(?:Async)?\s*\(/g;
const STREAM = /\bnew\s+FileStream\s*\(/g;

function* sources(dir) {
  let entries;
  try {
    entries = readdirSync(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const entry of entries) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!SKIPPED.has(entry.name)) yield* sources(full);
    } else if (entry.name.endsWith('.cs')) {
      yield full;
    }
  }
}

/** The text of the parenthesised arguments that open at `open` (the index of `(`) in blanked source. */
function argumentsAt(text, open) {
  let depth = 0;
  for (let i = open; i < text.length; i++) {
    if (text[i] === '(') depth++;
    else if (text[i] === ')' && --depth === 0) return text.slice(open + 1, i);
  }
  return text.slice(open + 1);
}

/** Each plain read in blanked `text`: `{ index, what }`. A FileStream that reads without sharing deletion is one; a claim is not. */
export function plainReads(text) {
  const found = [];
  for (const m of text.matchAll(PLAIN)) found.push({ index: m.index, what: `File.${m[1]}` });
  for (const m of text.matchAll(STREAM)) {
    const args = argumentsAt(text, m.index + m[0].length - 1);
    const reads = /\bFileAccess\.Read\b/.test(args) || (!/\bFileAccess\./.test(args) && /\bFileMode\.Open\b/.test(args));
    if (reads && !/\bFileShare\.(?:Delete|ReadWrite\s*\|\s*FileShare\.Delete)\b/.test(args) && !/\bDeleteOnClose\b/.test(args)) {
      found.push({ index: m.index, what: 'a FileStream that does not share deletion' });
    }
  }
  return found.sort((a, b) => a.index - b.index);
}

/** The driver's sources under `root`, each `{ rel, raw, text }` with `text` blanked of comments and strings. */
function read(root, dir) {
  return [...sources(join(root, dir))]
    .filter((file) => !file.slice(root.length + 1).replaceAll('\\', '/').split('/').some((part) => part.endsWith('.Tests')))
    .map((file) => {
      const raw = readFileSync(file, 'utf8');
      return { rel: file.slice(root.length + 1).replaceAll('\\', '/'), raw, text: blank(raw) };
    });
}

/** The places writes land: `Type.Member` for each path-naming member of a type a writer declares, and each `HomeEntry.Name` a writer names. */
export function places(files) {
  const found = new Set();
  for (const { text } of files.filter((f) => WRITES.test(f.text))) {
    const members = [...text.matchAll(MEMBERS)].map((m) => m[1]).filter((name) => PLACE.test(name));
    for (const type of text.matchAll(TYPES)) for (const member of members) found.add(`${type[1]}.${member}`);
    for (const entry of text.matchAll(/\bHomeEntry\s*\.\s*([A-Za-z_]\w*)/g)) found.add(`HomeEntry.${entry[1]}`);
  }
  return found;
}

/** The breaches under `root` (`{ problems, marked }`): a plain read in a writer, or in a source naming where a writer keeps a file. */
export function checkReadShares(root, dir = DRIVER) {
  const files = read(root, dir);
  const where = places(files);
  const problems = [];
  let marked = 0;
  for (const { rel, raw, text } of files) {
    const writer = WRITES.test(text);
    const named = writer ? null : [...where].find((place) => new RegExp(`\\b${place.replace('.', '\\s*\\.\\s*')}\\b`).test(text));
    if (!writer && !named) continue;
    const lines = raw.split('\n');
    for (const { index, what } of plainReads(text)) {
      const line = text.slice(0, index).split('\n').length;
      if (lines[line - 1].includes(MARK)) {
        marked++;
        continue;
      }
      const why = writer ? 'which writes through AtomicFile' : `which names ${named}, where AtomicFile writes`;
      problems.push(`${rel}:${line}: ${what} in a source ${why}: read it through AtomicFile.ReadText or ReadBytes, or mark the line \`${MARK} <what it reads>\``);
    }
  }
  return { problems, marked };
}

function main() {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  try {
    const { problems, marked } = checkReadShares(root);
    for (const p of problems) console.error(`read-shares: ${p}`);
    if (problems.length) return 1;
    console.log(`read-shares: clean (${marked} marked reads of files nothing moves into place)`);
    return 0;
  } catch (e) {
    console.error(`read-shares: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main();
