import { fenced } from '../doc-duplicates.mjs';
import { indentOf, isComment, truncate } from './common.mjs';
import { trusted } from './inside.mjs';

/** A line that only closes: `}`, `});`, `]`, `} as const;`. Any other line that starts with a closer goes on. */
const PURE_CLOSER = /^[}\])][}\])]*(?:\s*(?:as const|satisfies\s+[^;]+))?[;,)]*\s*$/;

/**
 * The last line of a declaration that starts at `start` with `indent`: its own closing line at that indent, or the
 * last line before the next thing at that indent or shallower. Comments and blank lines in between are left to what
 * follows, so a range never ends on the next declaration's doc comment.
 */
function endOf(lines, inside, start, indent) {
  let last = start;
  for (let i = start + 1; i < lines.length; i += 1) {
    if (inside[i]) {
      last = i;
      continue;
    }
    const trimmed = lines[i].trim();
    if (!trimmed || isComment(trimmed)) continue;
    const at = indentOf(lines[i]);
    if (at > indent) {
      last = i;
      continue;
    }
    if (at < indent) break;
    if (trimmed === '{') {
      last = i;
      continue;
    }
    if (PURE_CLOSER.test(trimmed)) return i;
    if (/^[}\])]/.test(trimmed)) {
      last = i;
      continue;
    }
    break;
  }
  return last;
}

/**
 * Depth by containment: an entry inside an earlier one's range, and deeper than it, is under it. Lines come back
 * 1-based; whatever else an entry carries is kept, for the fixtures.
 */
function nest(entries) {
  entries.sort((a, b) => a.start - b.start || b.end - a.end);
  const open = [];
  for (const entry of entries) {
    while (open.length && (open.at(-1).end < entry.start || open.at(-1).indent >= entry.indent)) open.pop();
    entry.depth = open.length;
    open.push(entry);
  }
  return entries.map((entry) => ({ ...entry, start: entry.start + 1, end: entry.end + 1 }));
}

const CS_KEYWORDS = new Set([
  'if', 'for', 'foreach', 'while', 'switch', 'catch', 'using', 'lock', 'return', 'nameof', 'typeof', 'sizeof', 'default',
  'base', 'this', 'new', 'when', 'get', 'set', 'init', 'add', 'remove', 'await', 'throw', 'yield', 'else', 'case', 'goto',
  'in', 'is', 'as', 'not', 'and', 'or', 'var', 'do', 'try', 'fixed', 'checked', 'unchecked', 'stackalloc', 'out', 'ref',
]);
const CS_TYPE = /^\s*((?:(?:public|private|protected|internal|static|sealed|abstract|partial|readonly|ref|unsafe|new|file)\s+)*)(record\s+struct|record\s+class|record|class|struct|interface|enum)\s+(@?[A-Za-z_]\w*)/;
export const CS_ROUTE = /\.Map(Get|Post|Put|Delete|Patch)\(\s*"([^"]+)"/;
const ATTRIBUTES = /^\s*(?:\[(?:[^[\]]|\[[^\]]*\])*\]\s*)+/;
const CS_MODIFIER_END = /\b(?:public|private|protected|internal|static|async|override|virtual|abstract|sealed|extern|unsafe|new|partial|readonly|file|required|ref)$/;

/**
 * A C# member declared on `line`: a method, a constructor, an operator or a local function, by the name before its
 * first parenthesis. A field, a property and a statement are not one: an initialiser's `=` stands before the
 * parenthesis, and a call has no type before its name. Null when the line declares none.
 */
export function csMember(line, { inType }) {
  const code = line.replace(ATTRIBUTES, '').replace(/\/\/.*$/, '');
  // The parameters' parenthesis, past a tuple type: `public (string? Value, ChoiceFrom From) ResolveFrom(`.
  let paren = -1;
  for (let k = 0; k < code.length; k += 1) {
    if (code[k] !== '(') continue;
    const before = code.slice(0, k).trim();
    if (before === '' || /[<,]$/.test(before) || CS_MODIFIER_END.test(before)) {
      for (let depth = 0; k < code.length; k += 1) {
        if (code[k] === '(') depth += 1;
        else if (code[k] === ')' && --depth === 0) break;
      }
      continue;
    }
    paren = k;
    break;
  }
  if (paren < 0) return null;
  const head = code.slice(0, paren);
  if (/=(?!>)/.test(head) || /["'$@]/.test(head)) return null;
  const named = /(operator\s*[^\s(]+|@?[A-Za-z_]\w*)\s*(?:<[^()]*>)?\s*$/.exec(head);
  if (!named) return null;
  const prefix = head.slice(0, named.index).trim();
  const name = named[1];
  // `x.Foo(` and `x?.Foo(` are calls; `object? Foo(` is a nullable return type.
  if (CS_KEYWORDS.has(name) || prefix.endsWith('.')) return null;
  const first = prefix.split(/\s+/)[0] ?? '';
  if (CS_KEYWORDS.has(first) && !['new', 'ref'].includes(first)) return null;
  // Outside a type a call can stand at any indent: a local function names a type before it, and a statement that
  // ends at its line has no body, unless it is the arrow of an expression-bodied function.
  if (!inType && (!prefix || (code.trimEnd().endsWith(';') && !code.slice(paren).includes('=>')))) return null;
  return { name: name.replace(/\s+/g, ' '), modifiers: prefix };
}

/**
 * A C# file's types, members and, in top-level statements, local functions and mapped routes, each with its lines.
 * Members are read at the indent of a type's body only, so a statement inside a method is never taken for one.
 */
export function outlineCSharp(text) {
  const lines = text.split('\n');
  const inside = trusted(lines, 'cs');
  const entries = [];
  const stack = [];
  for (let i = 0; i < lines.length; i += 1) {
    if (inside[i]) continue;
    const trimmed = lines[i].trim();
    if (!trimmed || isComment(trimmed) || trimmed.startsWith('#')) continue;
    const indent = indentOf(lines[i]);
    while (stack.length && indent <= stack.at(-1).indent) {
      if (indent === stack.at(-1).indent && /^[{}]/.test(trimmed)) break;
      stack.pop();
    }
    if (/^[{}\])]/.test(trimmed) || /^\[[^\]]*\]$/.test(trimmed)) continue;
    const container = stack.at(-1) ?? null;
    if (container) {
      container.body ??= indent;
      if (indent !== container.body) continue;
    }
    if (/^namespace\s+[\w.]+\s*$/.test(trimmed)) {
      stack.push({ indent, body: null, namespace: true });
      continue;
    }
    const type = CS_TYPE.exec(lines[i].replace(ATTRIBUTES, ''));
    if (type) {
      const kind = type[2].split(/\s+/)[0];
      entries.push({ start: i, indent, name: `${kind} ${type[3]}`, kind: 'type', modifiers: type[1].trim(), type: type[3] });
      stack.push({ indent, body: null });
      continue;
    }
    if (!container || container.namespace) {
      const route = CS_ROUTE.exec(lines[i]);
      if (route) {
        entries.push({ start: i, indent, name: `${route[1].toUpperCase()} ${route[2]}`, kind: 'route' });
        continue;
      }
    }
    const member = csMember(lines[i], { inType: container !== null && !container.namespace });
    if (member) entries.push({ start: i, indent, name: `${member.name}()`, kind: 'member', modifiers: member.modifiers, member: member.name });
  }
  for (const entry of entries) entry.end = endOf(lines, inside, entry.start, entry.indent);
  return nest(entries);
}

const TS_TOP = [
  [/^export\s+default\s+(?:async\s+)?function\s*\*?\s*([A-Za-z_$][\w$]*)?/, 'function'],
  [/^(?:export\s+)?(?:async\s+)?function\s*\*?\s*([A-Za-z_$][\w$]*)/, 'function'],
  [/^(?:export\s+)?(?:default\s+)?(?:abstract\s+)?class\s+([A-Za-z_$][\w$]*)/, 'class'],
  [/^export\s+(?:declare\s+)?(?:const\s+)?enum\s+([A-Za-z_$][\w$]*)/, 'enum'],
  [/^export\s+(?:declare\s+)?interface\s+([A-Za-z_$][\w$]*)/, 'interface'],
  [/^export\s+(?:declare\s+)?type\s+([A-Za-z_$][\w$]*)/, 'type'],
  [/^export\s+(?:const|let|var)\s+([A-Za-z_$][\w$]*)/, 'const'],
  [/^(?:const|let)\s+([A-Za-z_$][\w$]*)\s*(?::[^=]+)?=\s*(?:async\s+)?(?:function\b|\(|<|[A-Za-z_$][\w$]*\s*=>|memo\(|forwardRef\()/, 'const'],
];
const TS_MEMBER = /^\s+(?:(?:static|async|get|set|private|public|protected|readonly|override|abstract)\s+)*(#?[A-Za-z_$][\w$]*)\s*(?:<[^()]*>)?\s*\(/;
const TS_KEYWORDS = new Set(['if', 'for', 'while', 'switch', 'catch', 'return', 'function', 'await', 'typeof', 'new', 'super', 'throw']);
const TEST_BLOCK = /^\s*(describe|test|it)(?:\.(?:only|skip|todo|concurrent|each\([^)]*\)))*\s*\(\s*(['"`])((?:\\.|(?!\2).)*)\2/;
const SECTION = /^\s*section\(\s*(['"`])((?:\\.|(?!\1).)*)\1/;
const isTestFile = (path) => /\.(test|spec)\.[cm]?[jt]sx?$/.test(path);
/**
 * Whether a `const x = (…` declares a function: its parenthesis closes on an arrow or a return type, or stays open past
 * the line (parameters that wrap). `(a ?? b).reduce(…)` is an expression.
 */
function declaresFunction(line) {
  const m = /=\s*(?:async\s+)?(?:<[^=]*?>\s*)?\(/.exec(line);
  if (!m) return true;
  let depth = 0;
  for (let k = m.index + m[0].length - 1; k < line.length; k += 1) {
    if (line[k] === '(') depth += 1;
    else if (line[k] === ')' && --depth === 0) return /^\s*(?:=>|:)/.test(line.slice(k + 1));
  }
  return true;
}

/** A function this many lines long or more gets its body outlined too. */
const LONG_BODY = 150;
const BODY_FUNCTION = /^\s+(const|let|function)\s+([A-Za-z_$][\w$]*)\s*(?:(?::[^=]+)?=\s*(?:async\s+)?(?:\(|function\b|[A-Za-z_$][\w$]*\s*=>|useCallback\(|useMemo\()|\()/;
const BODY_EFFECT = /^\s+(useEffect|useLayoutEffect)\(/;
const BODY_RETURN = /^\s+return\s*(?:\(\s*)?(?:<.*)?$/;

/**
 * A script's top-level declarations, a class's members, a test file's groups and cases, and a tool's phases
 * (`section('…')`), each with its lines. `exported` is kept for the fixtures.
 */
export function outlineScript(text, path) {
  const lines = text.split('\n');
  const inside = trusted(lines, 'ts');
  const testFile = isTestFile(path);
  const entries = [];
  const sections = [];
  let cls = null;
  for (let i = 0; i < lines.length; i += 1) {
    if (inside[i]) continue;
    const line = lines[i];
    const trimmed = line.trim();
    if (!trimmed || isComment(trimmed)) continue;
    const indent = indentOf(line);
    if (indent === 0) {
      cls = null;
      for (const [pattern, kind] of TS_TOP) {
        const m = pattern.exec(line);
        if (!m || (kind === 'const' && !line.startsWith('export') && !declaresFunction(line))) continue;
        entries.push({ start: i, indent, name: `${kind} ${m[1] ?? 'default'}`, exported: line.startsWith('export'), id: m[1] ?? 'default' });
        if (kind === 'class') cls = { body: null };
        break;
      }
    } else if (cls && !/^[}\])]/.test(trimmed)) {
      cls.body ??= indent;
      const m = indent === cls.body ? TS_MEMBER.exec(line) : null;
      if (m && !TS_KEYWORDS.has(m[1])) entries.push({ start: i, indent, name: `${m[1]}()` });
    }
    if (testFile) {
      const m = TEST_BLOCK.exec(line);
      if (m) entries.push({ start: i, indent, name: `${m[1]} '${truncate(m[3], 64)}'` });
    } else {
      const m = SECTION.exec(line);
      if (m) sections.push({ start: i, indent: indent + 1, name: `§ ${truncate(m[2], 80)}` });
    }
  }
  for (const entry of entries) entry.end = endOf(lines, inside, entry.start, entry.indent);
  // A long function (a page's component, a tool's main) is outlined inside too: its own functions, effects and return.
  for (const entry of entries.filter((top) => top.indent === 0 && /^(function|const) /.test(top.name) && top.end - top.start >= LONG_BODY)) {
    let body = null;
    for (let i = entry.start + 1; i < entry.end; i += 1) {
      const trimmed = lines[i].trim();
      if (inside[i] || !trimmed || isComment(trimmed)) continue;
      body ??= indentOf(lines[i]);
      if (indentOf(lines[i]) !== body) continue;
      const m = BODY_FUNCTION.exec(lines[i]);
      const name = m && declaresFunction(lines[i]) ? `${m[1]} ${m[2]}` : BODY_EFFECT.exec(lines[i])?.[1] ?? (BODY_RETURN.test(lines[i]) ? 'return' : null);
      if (name) entries.push({ start: i, indent: body, name, end: endOf(lines, inside, i, body) });
    }
  }
  // A phase runs to the next phase, or to the end of the declaration it is written in.
  sections.forEach((section, k) => {
    const within = entries.filter((entry) => entry.start < section.start && entry.end >= section.start).map((entry) => entry.end);
    let end = Math.min(k + 1 < sections.length ? sections[k + 1].start - 1 : lines.length - 1, ...within);
    while (end > section.start && !lines[end].trim()) end -= 1;
    section.end = end;
  });
  return nest([...entries, ...sections]);
}

/** A markdown file's headings to the third level, outside fences, each to the next heading of its level or above. */
export function outlineMarkdown(text) {
  const lines = text.split('\n');
  const inFence = fenced(lines);
  const heads = [];
  lines.forEach((line, i) => {
    const m = !inFence[i] && /^(#{1,3})\s+(.+?)\s*#*\s*$/.exec(line);
    if (m) heads.push({ start: i, level: m[1].length, name: truncate(m[2], 90) });
  });
  const top = Math.min(...heads.map((head) => head.level));
  return heads.map((head, k) => {
    const next = heads.slice(k + 1).find((other) => other.level <= head.level);
    let end = (next ? next.start : lines.length) - 1;
    while (end > head.start && !lines[end].trim()) end -= 1;
    return { start: head.start + 1, end: end + 1, depth: head.level - top, name: head.name };
  });
}

export const CODE = /\.(cs|ts|tsx|mts|cts|mjs|cjs|js)$/;

/** The outline of one file by its kind, or null for a kind with none. */
export function outlineOf(path, text) {
  if (path.endsWith('.cs')) return outlineCSharp(text);
  if (CODE.test(path)) return outlineScript(text, path);
  if (path.endsWith('.md')) return outlineMarkdown(text);
  return null;
}

// ---------------------------------------------------------------------------------------------------
// The files a session reads whole, and their outlines
