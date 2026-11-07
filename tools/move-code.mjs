#!/usr/bin/env node
/**
 * Named top-level statements moved from one module to others, with their comments, and the imports each side then needs.
 *
 *   node tools/move-code.mjs --moves <file.json> [--plan] [--root <dir>]
 *
 * A large file split by hand is retyped through an editor, and retyping is where a move goes wrong: a line dropped, a
 * comment left behind, an import that still names the old place. This moves the text itself and asserts what it moved.
 *
 * The plan: `{ "from": "tools/a.mjs", "moves": [{ "to": "tools/a/b.mjs", "names": ["x"], "span": ["first", "last"],
 * "expect": 3, "header": "What b does." }] }`, its paths from the root (default: the current folder). A statement is named
 * by what it declares (a function, class, variable, interface, type or enum) or, for a call such as `test('title', …)`, by
 * its first argument's text. `names` takes each named statement, `span` every statement from the first through the last,
 * and `expect` is how many statements the move takes. A statement moves with the comments directly above it, byte for
 * byte, in the order it had; a comment a blank line sets apart stays. `header` opens a module the move creates.
 *
 * Each side is read with the TypeScript compiler, so a name is followed to its binding rather than matched as text. A
 * target gets the imports its statements read, respelled from where it stands, and `export` on what the others read of
 * it; the module moved from imports what it still reads, re-exports what it exported, so every caller is unchanged, and
 * drops each import it no longer reads.
 *
 * Refused before anything is written: a count that differs, a name found no or two times, an unnamed statement in a span,
 * a statement two moves take, a statement that reads one left behind (which would make the new module import the old),
 * one that reads `import.meta` (whose meaning moves with the file), a name a target already binds, and two targets that
 * would import each other. After the plan, every statement is read again where it lands: each name it read from the top
 * level must still be bound there, and nothing else, or nothing is written.
 *
 * `--plan` prints the plan and changes nothing. Exit 0 clean, 1 a refused plan, 2 a tool error (usage, unreadable input).
 */
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, isAbsolute, join, posix, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, writeAtomic } from './fsx.mjs';

// The compiler is the CLI workspace's dependency; the root's install hoists it.
const requireFromCli = createRequire(join(dirname(fileURLToPath(import.meta.url)), '..', 'src', 'Daoris.Cli', 'package.json'));
let compiler = null;
const typescript = () => (compiler ??= requireFromCli('typescript'));

/** A plan the mover will not carry out, for exit 1. */
export class MoveRefused extends Error {}

const WIDTH = 140;
const toPosix = (path) => path.replace(/\\/g, '/');

const lineStart = (text, at) => text.lastIndexOf('\n', at - 1) + 1;
const lineEnd = (text, at) => {
  const next = text.indexOf('\n', at);
  return next < 0 ? text.length : next + 1;
};

/** The names a binding pattern declares. */
const bound = (ts, name) => (ts.isIdentifier(name)
  ? [name.text]
  : name.elements.flatMap((element) => (ts.isOmittedExpression(element) ? [] : bound(ts, element.name))));

/** What a statement declares at the top level, as bindings. */
function bindingsOf(ts, node) {
  const declares = ts.isFunctionDeclaration(node) || ts.isClassDeclaration(node) || ts.isInterfaceDeclaration(node)
    || ts.isTypeAliasDeclaration(node) || ts.isEnumDeclaration(node);
  if (declares) return node.name ? [node.name.text] : [];
  if (ts.isVariableStatement(node)) return node.declarationList.declarations.flatMap((declaration) => bound(ts, declaration.name));
  return [];
}

/** A call statement's title: `test('title', …)`, awaited or not. */
function titleOf(ts, node) {
  if (!ts.isExpressionStatement(node)) return null;
  let expression = node.expression;
  if (ts.isAwaitExpression(expression)) expression = expression.expression;
  if (!ts.isCallExpression(expression)) return null;
  const first = expression.arguments[0];
  return first && (ts.isStringLiteral(first) || ts.isNoSubstitutionTemplateLiteral(first)) ? first.text : null;
}

/** An import declaration's bindings: `{ local, imported, type }`, `imported` being a name, `default` or `*`. */
function importBindings(ts, node) {
  const clause = node.importClause;
  if (!clause) return [];
  const all = clause.isTypeOnly;
  const found = [];
  if (clause.name) found.push({ local: clause.name.text, imported: 'default', type: all });
  const named = clause.namedBindings;
  if (named && ts.isNamespaceImport(named)) found.push({ local: named.name.text, imported: '*', type: all });
  if (named && ts.isNamedImports(named)) {
    for (const element of named.elements) {
      found.push({ local: element.name.text, imported: (element.propertyName ?? element.name).text, type: all || element.isTypeOnly });
    }
  }
  return found;
}

const isTypeDeclaration = (ts, node) => ts.isInterfaceDeclaration(node) || ts.isTypeAliasDeclaration(node);

/** A module read for its statements, each with the span its text and comments take. */
export function analyse(path, text) {
  const ts = typescript();
  const typed = /\.[cm]?tsx?$/.test(path);
  const name = typed ? '/module.ts' : '/module.js';
  const file = ts.createSourceFile(name, text, ts.ScriptTarget.ESNext, true, typed ? ts.ScriptKind.TS : ts.ScriptKind.JS);
  const host = {
    getSourceFile: (asked) => (asked === name ? file : undefined),
    getDefaultLibFileName: () => '/lib.d.ts',
    writeFile: () => {},
    getCurrentDirectory: () => '/',
    getCanonicalFileName: (asked) => asked,
    useCaseSensitiveFileNames: () => true,
    getNewLine: () => '\n',
    fileExists: (asked) => asked === name,
    readFile: (asked) => (asked === name ? text : undefined),
  };
  const options = { allowJs: true, noResolve: true, noLib: true, types: [], noEmit: true, target: ts.ScriptTarget.ESNext, module: ts.ModuleKind.ESNext };
  const program = ts.createProgram({ rootNames: [name], options, host });
  const syntax = program.getSyntacticDiagnostics(file);
  if (syntax.length) throw new MoveRefused(`${path} does not parse: ${ts.flattenDiagnosticMessageText(syntax[0].messageText, ' ')}`);
  const line = (at) => file.getLineAndCharacterOfPosition(at).line + 1;
  const statements = file.statements.map((node, index) => {
    // A comment is the statement's when no blank line parts them: one set apart is the file's or a section's, and stays.
    const comments = ts.getLeadingCommentRanges(text, node.pos) ?? [];
    let first = node.getStart(file);
    for (let i = comments.length - 1; i >= 0 && (text.slice(comments[i].end, first).match(/\n/g) ?? []).length <= 1; i -= 1) first = comments[i].pos;
    const start = lineStart(text, first);
    const trailing = ts.getTrailingCommentRanges(text, node.end) ?? [];
    const last = trailing.length ? trailing.at(-1).end : node.end;
    const end = lineEnd(text, last);
    let removeFrom = start;
    while (removeFrom > 0) {
      const above = lineStart(text, removeFrom - 1);
      if (text.slice(above, removeFrom).trim()) break;
      removeFrom = above;
    }
    const binds = bindingsOf(ts, node);
    const title = titleOf(ts, node);
    const modifiers = ts.canHaveModifiers(node) ? ts.getModifiers(node) ?? [] : [];
    return {
      node, index, binds, names: title === null ? binds : [title], start, end, removeFrom, lines: [line(start), line(last)],
      shared: text.slice(last, end).trim() !== '',
      exported: modifiers.some((modifier) => modifier.kind === ts.SyntaxKind.ExportKeyword),
      defaulted: modifiers.some((modifier) => modifier.kind === ts.SyntaxKind.DefaultKeyword),
      isImport: ts.isImportDeclaration(node),
      imports: ts.isImportDeclaration(node) ? importBindings(ts, node) : [],
      isType: isTypeDeclaration(ts, node),
    };
  });
  return { path, text, eol: text.includes('\r\n') ? '\r\n' : '\n', ts, file, checker: program.getTypeChecker(), statements };
}

/** The top-level statement a declaration binds a name for, or null when it binds none there (a local, a property). */
function topBinding(ts, file, declaration) {
  let node = declaration;
  if (ts.isImportSpecifier(node)) node = node.parent.parent.parent;
  else if (ts.isNamespaceImport(node)) node = node.parent.parent;
  else if (ts.isImportClause(node)) node = node.parent;
  else if (ts.isBindingElement(node) || ts.isVariableDeclaration(node)) {
    while (ts.isBindingElement(node) || ts.isObjectBindingPattern(node) || ts.isArrayBindingPattern(node)) node = node.parent;
    if (!ts.isVariableDeclaration(node)) return null;
    node = node.parent.parent;
  } else if (!(ts.isFunctionDeclaration(node) || ts.isClassDeclaration(node) || ts.isInterfaceDeclaration(node)
    || ts.isTypeAliasDeclaration(node) || ts.isEnumDeclaration(node))) return null;
  return node.parent === file ? node : null;
}

/**
 * What a statement reads from the top level: `Map<name, statement index>`, where the statement is the one declaring or
 * importing the name. Its own names are left out; `meta` says whether it reads `import.meta`, `dynamic` lists the relative
 * specifiers of the `import('…')` calls it makes.
 */
export function readsOf(module, statement) {
  const { ts, checker, file } = module;
  const indexOf = new Map(module.statements.map((entry) => [entry.node, entry.index]));
  const reads = new Map();
  let meta = false;
  const dynamic = [];
  const visit = (node) => {
    if (ts.isMetaProperty(node) && node.keywordToken === ts.SyntaxKind.ImportKeyword) meta = true;
    if (ts.isCallExpression(node) && node.expression.kind === ts.SyntaxKind.ImportKeyword) {
      const [first] = node.arguments;
      if (first && ts.isStringLiteralLike(first) && /^\.\.?\//.test(first.text)) dynamic.push(first);
    }
    if (ts.isIdentifier(node)) {
      const parent = node.parent;
      const symbol = ts.isShorthandPropertyAssignment(parent) && parent.name === node
        ? checker.getShorthandAssignmentValueSymbol(parent)
        : ts.isExportSpecifier(parent) ? checker.getExportSpecifierLocalTargetSymbol(parent) : checker.getSymbolAtLocation(node);
      for (const declaration of symbol?.declarations ?? []) {
        const top = topBinding(ts, file, declaration);
        const at = top ? indexOf.get(top) : undefined;
        if (at !== undefined && at !== statement.index) reads.set(node.text, at);
      }
    }
    ts.forEachChild(node, visit);
  };
  visit(statement.node);
  return { reads, meta, dynamic };
}

/** A relative specifier, written from `fromFile`'s folder to `toFile`, both from the root. */
function specifierFor(fromFile, toFile) {
  const spelled = posix.relative(posix.dirname(fromFile), toFile);
  return spelled.startsWith('.') ? spelled : `./${spelled}`;
}

/** A specifier of `fromFile` (from the root) read again from `toFile`; a bare one is unchanged. */
function rebase(specifier, fromFile, toFile) {
  if (!/^\.\.?(\/|$)/.test(specifier)) return specifier;
  return specifierFor(toFile, posix.normalize(posix.join(posix.dirname(fromFile), specifier)));
}

/** Where a relative specifier of `file` lands, from the root; null for a bare one. */
const landing = (specifier, file) => (/^\.\.?(\/|$)/.test(specifier) ? posix.normalize(posix.join(posix.dirname(file), specifier)) : null);

function clauseOf(bindings, keyword) {
  const parts = [];
  const def = bindings.find((binding) => binding.imported === 'default' && keyword === 'import');
  const star = bindings.find((binding) => binding.imported === '*');
  const named = bindings.filter((binding) => binding !== def && binding !== star);
  const spell = (binding) => `${binding.type ? 'type ' : ''}${binding.imported === binding.local ? binding.local : `${binding.imported} as ${binding.local}`}`;
  return { def, star, named: named.map(spell) };
}

/** One import or re-export statement's text, wrapped as the repository wraps a long one. */
export function importStatement(keyword, specifier, bindings, eol = '\n') {
  const { def, star, named } = clauseOf(bindings, keyword);
  const head = [def?.local, star ? `* as ${star.local}` : null].filter(Boolean);
  const tail = ` from '${specifier}';`;
  const single = `${keyword} ${[...head, ...(named.length ? [`{ ${named.join(', ')} }`] : [])].join(', ')}${tail}`;
  if (single.length <= WIDTH || !named.length) return single;
  const rows = [];
  let row = '';
  for (const name of named) {
    const next = row ? `${row} ${name},` : `  ${name},`;
    if (next.length > WIDTH && row) {
      rows.push(row);
      row = `  ${name},`;
    } else row = next;
  }
  rows.push(row);
  return [`${keyword} ${head.length ? `${head.join(', ')}, ` : ''}{`, ...rows, `}${tail}`].join(eol);
}

function selectStatements(source, move, at) {
  const named = new Map();
  for (const statement of source.statements) {
    if (statement.isImport) continue;
    for (const name of statement.names) named.set(name, [...(named.get(name) ?? []), statement]);
  }
  const one = (name) => {
    const found = named.get(name) ?? [];
    if (found.length !== 1) throw new MoveRefused(`move ${at}: '${name}' names ${found.length} statements of ${source.path}, not one`);
    return found[0];
  };
  const chosen = new Set((move.names ?? []).map(one));
  if (move.span) {
    if (!Array.isArray(move.span) || move.span.length !== 2) throw new MoveRefused(`move ${at}: a span is [first, last]`);
    const [first, last] = move.span.map(one);
    if (first.index > last.index) throw new MoveRefused(`move ${at}: '${move.span[0]}' comes after '${move.span[1]}'`);
    for (const statement of source.statements.slice(first.index, last.index + 1)) {
      if (!statement.names.length || statement.isImport) {
        throw new MoveRefused(`move ${at}: line ${statement.lines[0]} of ${source.path}, inside the span, names nothing`);
      }
      chosen.add(statement);
    }
  }
  return [...chosen].sort((a, b) => a.index - b.index);
}

function readMoves(root, plan) {
  if (typeof plan?.from !== 'string' || !Array.isArray(plan.moves) || !plan.moves.length) {
    throw new Error('a plan is { "from": "<file>", "moves": [{ "to": "<file>", "names" | "span", "expect": <n> }] }');
  }
  for (const [i, move] of plan.moves.entries()) {
    if (typeof move?.to !== 'string') throw new Error(`move ${i + 1} names no 'to'`);
    if (!Number.isInteger(move.expect)) throw new Error(`move ${i + 1} gives no 'expect', the count of statements it takes`);
    if (!move.names && !move.span) throw new Error(`move ${i + 1} names no statement: give 'names' or 'span'`);
  }
  const from = toPosix(plan.from);
  if (!existsSync(join(root, from))) throw new Error(`${from} does not exist`);
  return { from, moves: plan.moves.map((move) => ({ ...move, to: toPosix(move.to) })) };
}

/** Each text edit applied from the end, so earlier offsets hold. */
function applyEdits(text, edits) {
  let out = text;
  for (const edit of [...edits].sort((a, b) => b.start - a.start || b.end - a.end)) out = out.slice(0, edit.start) + edit.text + out.slice(edit.end);
  return out;
}

/** The imports a module gains: `Map<specifier, bindings[]>`, merged into its existing ones or written after its last. */
function importEdits(module, filePath, gains, keyword) {
  const edits = [];
  const fresh = [];
  for (const [specifier, bindings] of gains) {
    if (!bindings.length) continue;
    const existing = module?.statements.find((statement) => {
      const node = statement.node;
      if (keyword === 'import') return statement.isImport && node.moduleSpecifier.text === specifier && node.importClause && !node.importClause.isTypeOnly && !node.attributes;
      return module.ts.isExportDeclaration(node) && node.moduleSpecifier?.text === specifier && !node.isTypeOnly && node.exportClause && module.ts.isNamedExports(node.exportClause);
    });
    if (existing) {
      const node = existing.node;
      const had = keyword === 'import'
        ? existing.imports
        : node.exportClause.elements.map((element) => ({ local: element.name.text, imported: (element.propertyName ?? element.name).text, type: element.isTypeOnly }));
      const merged = [...had, ...bindings.filter((binding) => !had.some((old) => old.local === binding.local))];
      edits.push({ start: node.getStart(module.file), end: node.end, text: importStatement(keyword, specifier, merged, module.eol) });
    } else fresh.push(importStatement(keyword, specifier, bindings, module?.eol ?? '\n'));
  }
  return { edits, fresh };
}

function addNamed(gains, specifier, binding) {
  const list = gains.get(specifier) ?? [];
  if (!list.some((old) => old.local === binding.local)) list.push(binding);
  gains.set(specifier, list);
}

/** The statements that are code, in order: an import or a re-export is what a move writes, not what it moves. */
const code = (module) => module.statements.filter((statement) => !statement.isImport
  && !(module.ts.isExportDeclaration(statement.node) && statement.node.moduleSpecifier));
const label = (statement) => statement.names[0] ?? `line ${statement.lines[0]}`;

/** The plan: each target's text, the source's, and what each gained, without writing. */
export function planMoves(root, planned) {
  const { from, moves } = readMoves(root, planned);
  const source = analyse(from, readFileSync(join(root, from), 'utf8'));
  const { eol } = source;
  const targets = new Map();
  const owner = new Map();
  for (const [i, move] of moves.entries()) {
    const at = i + 1;
    if (move.to === from) throw new MoveRefused(`move ${at}: ${move.to} is the module moved from`);
    const chosen = selectStatements(source, move, at);
    if (chosen.length !== move.expect) {
      const found = chosen.map((statement) => `${statement.names[0]} (${statement.lines.join('-')})`).join(', ');
      throw new MoveRefused(`move ${at} to ${move.to} takes ${chosen.length} statement(s), not the ${move.expect} expected: ${found}`);
    }
    for (const statement of chosen) {
      if (owner.has(statement.index)) throw new MoveRefused(`'${statement.names[0]}' is taken by two moves`);
      if (statement.shared) throw new MoveRefused(`'${statement.names[0]}' shares line ${statement.lines[1]} with other code`);
      if (statement.defaulted) throw new MoveRefused(`'${statement.names[0]}' is a default export, which a re-export would rename`);
      owner.set(statement.index, move.to);
    }
    const target = targets.get(move.to) ?? { to: move.to, statements: [], header: null };
    target.statements.push(...chosen);
    target.header ??= move.header ?? null;
    targets.set(move.to, target);
  }
  const readsBy = new Map(source.statements.map((statement) => [statement.index, readsOf(source, statement)]));

  // What each target reads, and what each target must export for the others and the source.
  const exportsOf = new Map([...targets.keys()].map((to) => [to, new Set()]));
  const behind = [];
  for (const target of targets.values()) {
    target.statements.sort((a, b) => a.index - b.index);
    target.gains = new Map();
    target.from = new Set();
    for (const statement of target.statements) {
      const { reads, meta } = readsBy.get(statement.index);
      if (meta) throw new MoveRefused(`'${statement.names[0]}' reads import.meta, whose meaning moves with the file: move it by hand`);
      for (const [name, at] of reads) {
        const read = source.statements[at];
        if (read.isImport) {
          const binding = read.imports.find((entry) => entry.local === name);
          const specifier = rebase(read.node.moduleSpecifier.text, from, target.to);
          if (landing(specifier, target.to) === target.to) continue;
          if (read.node.attributes) throw new MoveRefused(`'${statement.names[0]}' reads ${name}, imported with attributes: move it by hand`);
          addNamed(target.gains, specifier, binding);
        } else if (owner.get(at) === target.to) continue;
        else if (owner.has(at)) {
          addNamed(target.gains, specifierFor(target.to, owner.get(at)), { local: name, imported: name, type: read.isType });
          exportsOf.get(owner.get(at)).add(name);
          target.from.add(owner.get(at));
        } else behind.push(`'${statement.names[0]}' reads '${name}' (line ${read.lines[0]}), which stays in ${from}`);
      }
    }
  }
  if (behind.length) throw new MoveRefused(`a moved statement reads one left behind; move it too:\n  ${behind.join('\n  ')}`);
  for (const target of targets.values()) {
    for (const other of target.from) {
      if (targets.get(other).from.has(target.to)) throw new MoveRefused(`${target.to} and ${other} would import each other`);
    }
  }

  // The source: what it still reads of the moved names, what it re-exports, which imports it no longer reads.
  const remaining = source.statements.filter((statement) => !owner.has(statement.index));
  const sourceGains = new Map();
  const reexports = new Map();
  const stillRead = new Set();
  for (const statement of remaining) {
    for (const [name, at] of readsBy.get(statement.index).reads) {
      if (source.statements[at].isImport) stillRead.add(name);
      else if (owner.has(at)) {
        const to = owner.get(at);
        addNamed(sourceGains, specifierFor(from, to), { local: name, imported: name, type: source.statements[at].isType });
        exportsOf.get(to).add(name);
      }
    }
  }
  for (const target of targets.values()) {
    for (const statement of target.statements) {
      if (!statement.exported) continue;
      for (const name of statement.binds) addNamed(reexports, specifierFor(from, target.to), { local: name, imported: name, type: statement.isType });
    }
  }
  const sourceEdits = [];
  const dropped = [];
  const importsNow = new Map(source.statements.filter((entry) => entry.isImport && entry.imports.length)
    .map((statement) => [statement, statement.imports.filter((binding) => stillRead.has(binding.local))]));
  const fresh = [];
  for (const [specifier, bindings] of sourceGains) {
    const into = [...importsNow.keys()].find((statement) => statement.node.moduleSpecifier.text === specifier
      && !statement.node.importClause.isTypeOnly && !statement.node.attributes);
    if (into) importsNow.get(into).push(...bindings.filter((binding) => !importsNow.get(into).some((old) => old.local === binding.local)));
    else fresh.push(importStatement('import', specifier, bindings, eol));
  }
  for (const [statement, now] of importsNow) {
    const spelled = (list) => list.map((binding) => `${binding.type}:${binding.imported}:${binding.local}`).join(',');
    if (spelled(now) === spelled(statement.imports)) continue;
    dropped.push(...statement.imports.filter((binding) => !now.some((kept) => kept.local === binding.local))
      .map((binding) => `${binding.local} (${statement.node.moduleSpecifier.text})`));
    if (statement.node.attributes) throw new MoveRefused(`${from} would rewrite an import with attributes, line ${statement.lines[0]}: move it by hand`);
    sourceEdits.push(now.length
      ? { start: statement.node.getStart(source.file), end: statement.node.end, text: importStatement('import', statement.node.moduleSpecifier.text, now, eol) }
      : { start: statement.removeFrom, end: statement.end, text: '' });
  }
  for (const index of owner.keys()) {
    const statement = source.statements[index];
    sourceEdits.push({ start: statement.removeFrom, end: statement.end, text: '' });
  }
  const exported = importEdits(source, from, reexports, 'export');
  sourceEdits.push(...exported.edits);
  fresh.push(...exported.fresh);
  if (fresh.length) {
    // The line after the last import, which a removal of that import ends at and never spans.
    const lastImport = source.statements.filter((statement) => statement.isImport).at(-1);
    const after = lastImport ? lastImport.end : lineStart(source.text, source.statements[0]?.node.getStart(source.file) ?? 0);
    sourceEdits.push({ start: after, end: after, text: `${fresh.map((line) => `${line}${eol}`).join('')}${lastImport ? '' : eol}` });
  }
  const sourceText = applyEdits(source.text, sourceEdits);

  // Each target's text: its statements byte for byte, with `export` where another module reads a name it did not export.
  const outputs = [];
  for (const target of targets.values()) {
    const path = join(root, target.to);
    const existing = existsSync(path) ? analyse(target.to, readFileSync(path, 'utf8')) : null;
    const bound = new Set(existing?.statements.flatMap((statement) => [...statement.binds, ...statement.imports.map((binding) => binding.local)]) ?? []);
    for (const statement of target.statements) {
      for (const name of statement.binds) if (bound.has(name)) throw new MoveRefused(`${target.to} already binds '${name}'`);
    }
    for (const [specifier, bindings] of target.gains) {
      target.gains.set(specifier, bindings.filter((binding) => {
        const had = existing?.statements.find((statement) => statement.imports.some((old) => old.local === binding.local));
        if (!had && bound.has(binding.local)) throw new MoveRefused(`${target.to} already binds '${binding.local}'`);
        if (!had) return true;
        const same = had.node.moduleSpecifier.text === specifier && had.imports.some((old) => old.local === binding.local && old.imported === binding.imported);
        if (!same) throw new MoveRefused(`${target.to} already binds '${binding.local}' from ${had.node.moduleSpecifier.text}`);
        return false;
      }));
    }
    const needed = exportsOf.get(target.to);
    target.exportAdded = [];
    const texts = target.statements.map((statement) => {
      const { dynamic } = readsBy.get(statement.index);
      const edits = dynamic.map((literal) => {
        const quote = source.text[literal.getStart(source.file)];
        return { start: literal.getStart(source.file) - statement.start, end: literal.end - statement.start, text: `${quote}${rebase(literal.text, from, target.to)}${quote}` };
      });
      if (!statement.exported && statement.binds.some((name) => needed.has(name))) {
        const at = statement.node.getStart(source.file) - statement.start;
        edits.push({ start: at, end: at, text: 'export ' });
        target.exportAdded.push(...statement.binds);
      }
      return applyEdits(source.text.slice(statement.start, statement.end), edits);
    });
    // Statements that were adjacent stay adjacent; any others are parted by one blank line.
    const body = texts.map((text, i) => (i && target.statements[i - 1].end !== target.statements[i].start ? `${eol}${text}` : text)).join('');
    let text;
    if (existing) {
      const { edits, fresh: lines } = importEdits(existing, target.to, target.gains, 'import');
      if (lines.length) {
        const lastImport = existing.statements.filter((statement) => statement.isImport).at(-1);
        const after = lastImport ? lastImport.end : 0;
        edits.push({ start: after, end: after, text: `${lines.map((line) => `${line}${eol}`).join('')}${lastImport ? '' : eol}` });
      }
      const kept = applyEdits(existing.text, edits).replace(/(\r?\n)*$/, eol);
      text = `${kept}${eol}${body}`;
    } else {
      const header = target.header ? `/**${eol}${target.header.split('\n').map((line) => ` *${line ? ` ${line}` : ''}`).join(eol)}${eol} */${eol}` : '';
      // The platform's and the packages' modules first, then the repository's, each in the order first read.
      const order = [...target.gains.keys()].sort((a, b) => Number(/^\.\.?\//.test(a)) - Number(/^\.\.?\//.test(b)));
      const lines = order.map((specifier) => importStatement('import', specifier, target.gains.get(specifier), eol));
      text = `${header}${lines.length ? `${lines.join(eol)}${eol}${eol}` : ''}${body}`;
    }
    outputs.push({ to: target.to, text, prior: existing ? code(existing).length : 0, target });
  }

  // Read every statement again where it lands: what it read from the top level must be bound there, and nothing else.
  const check = (path, text, statements, prior = 0) => {
    const landed = analyse(path, text);
    const twins = code(landed).slice(prior);
    if (twins.length !== statements.length) throw new MoveRefused(`after the move, ${path} holds ${twins.length} moved statement(s) of code, not ${statements.length}`);
    for (const [i, statement] of statements.entries()) {
      const twin = twins[i];
      if (twin.names.join('\0') !== statement.names.join('\0') || twin.node.kind !== statement.node.kind) {
        throw new MoveRefused(`after the move, ${path} has ${label(twin)} where ${label(statement)} belongs`);
      }
      const before = [...readsBy.get(statement.index).reads.keys()].sort().join(', ');
      const after = [...readsOf(landed, twin).reads.keys()].sort().join(', ');
      if (before !== after) throw new MoveRefused(`after the move, ${label(statement)} in ${path} reads [${after}] from the top level, not [${before}]`);
    }
  };
  check(from, sourceText, code(source).filter((statement) => !owner.has(statement.index)));
  for (const output of outputs) check(output.to, output.text, output.target.statements, output.prior);

  return {
    from, sourceText, outputs,
    report: { sourceGains, reexports, dropped, targets: [...targets.values()] },
  };
}

const bindingList = (bindings) => bindings.map((binding) => binding.local).join(', ');

/** The plan in lines, as `--plan` prints it and a run says what it did. */
export function planLines(result) {
  const out = [];
  for (const target of result.report.targets) {
    const first = target.statements[0].lines[0];
    const last = target.statements.at(-1).lines[1];
    out.push(`${result.from} → ${target.to}: ${target.statements.length} statement(s), lines ${first}-${last}`);
    out.push(`  ${target.statements.map((statement) => `${statement.names[0]} ${statement.lines.join('-')}`).join(', ')}`);
    for (const [specifier, bindings] of target.gains) if (bindings.length) out.push(`  imports from ${specifier}: ${bindingList(bindings)}`);
    if (target.exportAdded.length) out.push(`  exported for the others: ${target.exportAdded.join(', ')}`);
  }
  for (const [specifier, bindings] of result.report.sourceGains) out.push(`${result.from} imports from ${specifier}: ${bindingList(bindings)}`);
  for (const [specifier, bindings] of result.report.reexports) out.push(`${result.from} re-exports from ${specifier}: ${bindingList(bindings)}`);
  if (result.report.dropped.length) out.push(`${result.from} drops the imports it no longer reads: ${result.report.dropped.join(', ')}`);
  return out;
}

/** Write what `planMoves` planned: the targets first, then the module moved from. */
export function applyMoves(root, result) {
  for (const output of result.outputs) {
    const path = join(root, output.to);
    mkdirSync(dirname(path), { recursive: true });
    writeAtomic(path, output.text);
  }
  writeAtomic(join(root, result.from), result.sourceText);
}

if (isMain(import.meta.url)) {
  const args = process.argv.slice(2);
  const valueOf = (flag) => {
    const at = args.indexOf(flag);
    return at >= 0 ? args[at + 1] : undefined;
  };
  const known = new Set(['--plan', '--moves', '--root']);
  const unknown = args.filter((arg, i) => arg.startsWith('--') ? !known.has(arg) : !['--moves', '--root'].includes(args[i - 1]));
  const movesFile = valueOf('--moves');
  if (unknown.length || !movesFile) {
    console.error('usage: node tools/move-code.mjs --moves <file.json> [--plan] [--root <dir>]');
    process.exit(2);
  }
  const root = resolve(valueOf('--root') ?? process.cwd());
  try {
    const plan = JSON.parse(readFileSync(isAbsolute(movesFile) ? movesFile : join(root, movesFile), 'utf8'));
    const result = planMoves(root, plan);
    for (const line of planLines(result)) console.log(line);
    if (args.includes('--plan')) console.log('move-code: planned, nothing written');
    else {
      applyMoves(root, result);
      console.log(`move-code: wrote ${result.outputs.length + 1} file(s)`);
    }
  } catch (error) {
    console.error(`move-code: ${error.message}`);
    process.exit(error instanceof MoveRefused ? 1 : 2);
  }
}
