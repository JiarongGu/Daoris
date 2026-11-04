import { join } from 'node:path';
import { BRIDGE, CONTRACTS, GENERATED, HTTP_HOST, MODULES, SHELL, WEB_SRC, compare, lineAt } from './common.mjs';
import { CS_ROUTE, csMember } from './outline.mjs';

const DIRECT = /'(DAORIS\.[A-Z]+)'\s*,\s*'([A-Z][A-Z_]*)'/g;
const FORWARDED = /'(DAORIS\.[A-Z]+)'\s*,\s*([A-Za-z_$][\w$]*)\s*[,)]/g;
const ROUTE_LITERAL = /'([A-Z][A-Z_]*)'/g;
const NAME = /^[A-Za-z_$][\w$]*$/;

/**
 * The first argument of a call whose name ends at `at`: past its type arguments, to the first comma or close at depth
 * zero, with where it starts. Null when what follows is not a call. As `DriverModuleRoutesTests.FirstArgument` reads it.
 */
export function firstArgument(text, at) {
  if (text[at] === '<') {
    let depth = 0;
    for (; at < text.length; at += 1) {
      if (text[at] === '<') depth += 1;
      else if (text[at] === '>' && text[at - 1] !== '=' && --depth === 0) break;
    }
    at += 1;
  }
  while (at < text.length && /\s/.test(text[at])) at += 1;
  if (text[at] !== '(') return null;
  const start = at + 1;
  let nesting = 0;
  let quote = null;
  for (at = start; at < text.length; at += 1) {
    const c = text[at];
    if (quote) {
      if (c === quote && text[at - 1] !== '\\') quote = null;
    } else if (c === "'" || c === '"' || c === '`') quote = c;
    else if ('([{'.includes(c)) nesting += 1;
    else if (')]}'.includes(c) && nesting > 0) nesting -= 1;
    else if ((c === ',' || c === ')') && nesting === 0) return { text: text.slice(start, at), start };
  }
  return null;
}

/** A script's top-level names by line, so a call is named by the declaration it is written in. */
function topLevelNames(text) {
  return text.split('\n').flatMap((line, i) => {
    const m = /^(?:export\s+)?(?:default\s+)?(?:async\s+)?(?:function\s*\*?\s*|const\s+|let\s+|class\s+)([A-Za-z_$][\w$]*)/.exec(line);
    return m ? [{ line: i + 1, name: m[1] }] : [];
  });
}

/** Each call of one of `senders` in `text`: the sender, the call's first argument, and where that starts. */
function sends(text, senders) {
  if (senders.size === 0) return [];
  const caller = new RegExp(`(?<![.\\w$])(${[...senders.keys()].map((name) => name.replace(/\$/g, '\\$')).join('|')})\\s*(?=[<(])`, 'g');
  return [...text.matchAll(caller)].flatMap((m) => {
    const argument = firstArgument(text, m.index + m[0].length);
    return argument ? [{ sender: m[1], ...argument }] : [];
  });
}

/**
 * Every route the page sends a desktop module, from the bridge's files: module, route, file, line, and the function it
 * is written in. A send names its route in a direct `invoke('DAORIS.X', 'ROUTE')`, or as the first argument of a helper
 * that forwards its own route argument to one: `call` and `callBrowser`, and a helper of a helper (`useWiringChange`).
 * Helpers are found, never listed, so a new one cannot hide its callers' routes (the modules' route test keeps a list,
 * `DriverModuleRoutesTests.Forwarders`, for the driver's alone).
 */
function pageCalls(ctx) {
  // The bridge is cut into scope folders, so a domain file sits at any depth under it.
  const files = ctx.files.filter((path) => path.startsWith(BRIDGE) && /\.ts$/.test(path.slice(BRIDGE.length)) && !path.endsWith('.test.ts'));
  for (const shell of ctx.files.filter((path) => path.startsWith(WEB_SRC) && path.endsWith(SHELL) && !files.includes(path))) files.push(shell);
  const sources = files.map((file) => {
    const text = ctx.read(file);
    const tops = topLevelNames(text);
    return { file, text, from: (offset) => tops.filter((top) => top.line <= lineAt(text, offset)).at(-1)?.name ?? '' };
  });
  // A declaration that passes a name, not a literal, as the route is a helper of that module; found until none is new.
  const senders = new Map();
  for (let grew = true; grew;) {
    grew = false;
    const learn = (name, module) => {
      if (name && !senders.has(name)) {
        senders.set(name, module);
        grew = true;
      }
    };
    for (const { text, from } of sources) {
      for (const m of text.matchAll(FORWARDED)) learn(from(m.index), m[1]);
      for (const send of sends(text, senders)) if (NAME.test(send.text.trim())) learn(from(send.start), senders.get(send.sender));
    }
  }
  const calls = [];
  for (const { file, text, from } of sources) {
    for (const m of text.matchAll(DIRECT)) calls.push({ module: m[1], route: m[2], file, line: lineAt(text, m.index), from: from(m.index) });
    for (const send of sends(text, senders)) {
      for (const literal of send.text.matchAll(ROUTE_LITERAL)) {
        const at = send.start + literal.index;
        calls.push({ module: senders.get(send.sender), route: literal[1], file, line: lineAt(text, at), from: from(at) });
      }
    }
  }
  return calls;
}

/**
 * Every route a module answers: the driver module's `[DriverRoute]` marks, each with the method under it, and every
 * other module's `case "ROUTE":` or `"ROUTE" =>` arms, in the class that names its module. The driver's marks are in its
 * domains' handler classes, `<Domain>Routes.cs`.
 */
function moduleHandlers(ctx) {
  const handlers = [];
  // The library keeps its sources in module folders: a handler class is found at any depth, by its file name.
  for (const file of ctx.files.filter((path) => path.startsWith(MODULES) && path.endsWith('.cs'))) {
    const lines = ctx.lines(file);
    if (/^[A-Z][A-Za-z]*Routes\.cs$/.test(file.slice(file.lastIndexOf('/') + 1))) {
      lines.forEach((line, i) => {
        const mark = /\[DriverRoute\("([A-Z][A-Z_]*)"[,)]/.exec(line);
        if (!mark) return;
        let j = i + 1;
        while (j < lines.length && /^\s*\[/.test(lines[j])) j += 1;
        const member = csMember(lines[j] ?? '', { inType: true });
        handlers.push({ module: 'DAORIS.DRIVER', route: mark[1], file, line: j + 1, method: member?.name ?? '' });
      });
      continue;
    }
    const name = /ModuleName\s*=>\s*"(DAORIS\.[A-Z]+)"/.exec(ctx.read(file))?.[1];
    if (!name) continue;
    lines.forEach((line, i) => {
      const arm = /^\s*case\s+"[A-Z][A-Z_]*"\s*:/.exec(line) ?? /^\s*"[A-Z][A-Z_]*"(?:\s+or\s+"[A-Z][A-Z_]*")*\s*=>/.exec(line);
      if (!arm) return;
      for (const route of arm[0].matchAll(/"([A-Z][A-Z_]*)"/g)) handlers.push({ module: name, route: route[1], file, line: i + 1, method: '' });
    });
  }
  return handlers;
}

export function routesPage(ctx) {
  const calls = pageCalls(ctx);
  const handlers = moduleHandlers(ctx);
  const out = [
    '# Bridge routes',
    '',
    `${GENERATED} Every route the page sends a desktop module, the handler that answers it (in \`${MODULES}\`) and the bridge function that sends it (in \`${WEB_SRC}\`). A route nobody answers, or that no bridge file sends, says \`—\`. The service's HTTP routes follow.`,
  ];
  const modules = [...new Set([...calls, ...handlers].map((entry) => entry.module))].sort(compare);
  for (const module of modules) {
    const routes = [...new Set([...calls, ...handlers].filter((entry) => entry.module === module).map((entry) => entry.route))].sort(compare);
    out.push('', `## ${module} (${routes.length})`, '', '| Route | Handler | Sent by |', '|---|---|---|');
    for (const route of routes) {
      const answered = handlers.filter((entry) => entry.module === module && entry.route === route)
        .map((entry) => `\`${entry.file.slice(MODULES.length)}:${entry.line}\`${entry.method ? ` ${entry.method}` : ''}`);
      const seen = new Set();
      const sent = calls.filter((entry) => entry.module === module && entry.route === route)
        .filter((entry) => !seen.has(`${entry.file}:${entry.from}`) && seen.add(`${entry.file}:${entry.from}`))
        .map((entry) => `\`${entry.file.slice(WEB_SRC.length)}:${entry.line}\`${entry.from ? ` ${entry.from}` : ''}`);
      out.push(`| \`${route}\` | ${answered.join('; ') || '—'} | ${sent.join(', ') || '—'} |`);
    }
  }
  if (modules.length === 0) out.push('', 'No bridge route was found.');
  // A declared route's row names its record, whose module in the host's `Routes/` maps it.
  const routes = [HTTP_HOST, ...ctx.files.filter((path) => path.startsWith(CONTRACTS) && path.endsWith('.cs'))].filter(ctx.has).flatMap((file) => {
    const lines = ctx.lines(file);
    return lines.flatMap((line, i) => {
      const m = file === HTTP_HOST ? CS_ROUTE.exec(line) : /static string Path => "([^"]+)"/.exec(line);
      if (!m || file === HTTP_HOST) return m ? [{ method: m[1].toUpperCase(), path: m[2], at: `Program.cs:${i + 1}` }] : [];
      const at = lines.slice(0, i).findLastIndex((text) => /\brecord \w+/.test(text));
      return [{ method: /Method => "(\w+)"/.exec(lines.slice(at, i + 1).join('\n'))?.[1] ?? '?', path: m[1], at: `${file.slice(CONTRACTS.length)}:${at + 1}` }];
    });
  }).sort((a, b) => compare(a.path, b.path) || compare(a.method, b.method));
  out.push('', `## The service's HTTP routes (${routes.length})`, '', `Mapped in \`${HTTP_HOST}\`, or declared in \`${CONTRACTS}\`.`, '', '| Route | Mapped at |', '|---|---|');
  for (const route of routes) out.push(`| ${route.method} ${route.path} | \`${route.at}\` |`);
  return `${out.join('\n')}\n`;
}

// ---------------------------------------------------------------------------------------------------
// Terminal verbs
