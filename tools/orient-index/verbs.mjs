import { join } from 'node:path';
import { CLI_TABLE, DESKTOP, DESKTOP_TOOL, DEVKIT, DRIVER_CODE, DRIVER_HOST, GENERATED, PACKAGES, driverFileIn, indentOf, truncate } from './common.mjs';
import { csMember } from './outline.mjs';

/** The line a C# type is declared on, in the driver's code, by its name. */
function findClass(ctx, name) {
  const declares = new RegExp(`^\\s*(?:(?:public|internal|static|sealed|abstract|partial|file)\\s+)*class\\s+${name}\\b`);
  for (const file of ctx.files.filter((path) => DRIVER_CODE.some((folder) => path.startsWith(folder)) && path.endsWith('.cs'))) {
    const at = ctx.lines(file).findIndex((line) => declares.test(line));
    if (at >= 0) return { file, line: at + 1 };
  }
  return null;
}

function cliVerbs(ctx) {
  if (!ctx.has(CLI_TABLE)) return [];
  const table = ctx.read(CLI_TABLE);
  const modules = new Map([...table.matchAll(/import \{ command as (\w+) \} from '\.\/cli\/([\w-]+)\.ts'/g)].map((m) => [m[1], `src/Daoris.Cli/src/cli/${m[2]}.ts`]));
  const order = /COMMANDS[^=]*=\s*\[([\s\S]*?)\];/.exec(table)?.[1].match(/\w+/g) ?? [];
  return order.filter((id) => modules.has(id) && ctx.has(modules.get(id))).map((id) => {
    const file = modules.get(id);
    const source = ctx.read(file);
    const at = source.split('\n').findIndex((line) => /^\s*name:\s*'/.test(line)) + 1;
    // `run: commandSync`, or `run: (args) => commandTool(args, …)`: the function the row hands its words to.
    const run = /\brun:\s*(?:(?:async\s*)?\([^)]*\)\s*=>\s*)?([A-Za-z_$][\w$]*)/.exec(source)?.[1];
    let handler = '';
    if (run) {
      // The handler's module, at the sources' top or in a module folder: `../drift.ts`, `../doctrine/drift.ts`.
      const from = new RegExp(`import \\{[^}]*\\b${run}\\b[^}]*\\} from '\\.\\./([\\w/-]+)\\.ts'`).exec(source)?.[1];
      const target = from ? `src/Daoris.Cli/src/${from}.ts` : file;
      const line = ctx.has(target) ? ctx.lines(target).findIndex((text) => new RegExp(`^export\\s+(?:async\\s+)?(?:function\\s+|const\\s+)${run}\\b`).test(text)) : -1;
      if (line >= 0) handler = `\`${target.slice('src/Daoris.Cli/src/'.length)}:${line + 1}\` ${run}`;
    }
    const usage = /usage:\s*\[\s*(['"])((?:\\.|(?!\1).)*)\1/.exec(source)?.[2] ?? '';
    return {
      verb: /name:\s*'([^']+)'/.exec(source)?.[1] ?? id,
      kind: /kind:\s*'([^']+)'/.exec(source)?.[1] ?? '',
      at: `\`${file.slice('src/Daoris.Cli/src/'.length)}:${at}\``,
      handler,
      says: truncate(usage.trim().replace(/^\S+\s*/, '').replace(/\s{2,}/g, ' '), 70),
    };
  });
}

/**
 * The sub-verbs `DriverCommand.Usage` names after each verb: a form's second word, and each word after a `|` of a
 * group. A form line stands at the usage's form indent, or continues a form with `|`; a description is deeper, and its
 * words are never read as verbs (`sync brings each repository…` describes `trees sync`).
 */
function driverSubVerbs(ctx, verbs) {
  const subs = new Map(verbs.map((verb) => [verb, []]));
  const command = driverFileIn(ctx, 'DriverCommand.cs');
  if (!command) return subs;
  const usage = (/Usage\s*=\s*"""([\s\S]*?)"""/.exec(ctx.read(command))?.[1] ?? '').split('\n');
  const formIndent = Math.min(...usage.filter((line) => subs.has(line.trim().split(/\s+/)[0])).map(indentOf));
  let current = null;
  const add = (word) => {
    const clean = current && /^\[?([a-z][a-z-]*(?:\|[a-z][a-z-]*)*)\]?$/.exec(word)?.[1];
    if (clean) for (const part of clean.split('|')) if (!subs.get(current).includes(part)) subs.get(current).push(part);
  };
  for (const line of usage) {
    const trimmed = line.trim();
    const continues = trimmed.startsWith('|') && indentOf(line) > formIndent;
    if (!trimmed || (indentOf(line) !== formIndent && !continues)) continue;
    for (const form of continues ? [trimmed] : trimmed.split('·')) {
      const tokens = form.trim().split(/\s+/);
      if (!continues) {
        current = subs.has(tokens[0]) ? tokens[0] : null;
        if (tokens[1]) add(tokens[1]);
      }
      tokens.forEach((token, k) => {
        if (token === '|' && tokens[k + 1]) add(tokens[k + 1]);
        else if (/^\|[a-z]/.test(token)) add(token.slice(1));
      });
    }
  }
  return subs;
}

function driverVerbs(ctx) {
  if (!ctx.has(DRIVER_HOST)) return [];
  const lines = ctx.lines(DRIVER_HOST);
  const verbs = [];
  lines.forEach((line, i) => {
    const m = /^\s*if \(args is \[((?:"[a-z-]+",\s*)+)/.exec(line);
    if (!m) return;
    const words = [...m[1].matchAll(/"([a-z-]+)"/g)].map((word) => word[1]);
    const verb = words.join(' ');
    // The class the verb is handed to, by the call that runs it; a predicate such as `WorkCommand.Asks` is not one.
    let handler = null;
    for (let j = i; j < Math.min(lines.length, i + 30); j += 1) {
      if (j > i && /^\s*if \(args is \[/.test(lines[j])) break;
      const call = /\b(\w+(?:Console|Command))\.(Run\w*|Install)\(/.exec(lines[j]);
      if (call) {
        handler = call[1];
        break;
      }
    }
    const entry = verbs.find((known) => known.verb === verb) ?? (verbs.push({ verb, routed: i + 1, handlers: [], files: [] }), verbs.at(-1));
    const where = handler ? findClass(ctx, handler) : null;
    const cell = where ? `\`${where.file.slice(DESKTOP.length)}:${where.line}\` ${handler}` : `\`Program.cs:${i + 1}\` (inline)`;
    if (!entry.handlers.includes(cell)) entry.handlers.push(cell);
    // A console usually hands its words to the library's command of the same name, where the sub-verbs are read.
    const library = handler?.endsWith('Console') ? findClass(ctx, handler.replace(/Console$/, 'Command')) : null;
    for (const file of [where?.file, library?.file]) if (file && !entry.files.includes(file)) entry.files.push(file);
  });
  const command = driverFileIn(ctx, 'DriverCommand.cs');
  if (command) {
    const at = ctx.lines(command).findIndex((line) => /args\[0\] == "drive"/.test(line));
    verbs.push({ verb: 'drive', routed: lines.findIndex((line) => /DriverCommand\.Read\(/.test(line)) + 1, handlers: [`\`${command.slice(DESKTOP.length)}:${at + 1}\` DriverCommand.Read`], files: [] });
  }
  const subs = driverSubVerbs(ctx, verbs.map((verb) => verb.verb.split(' ')[0]));
  for (const verb of verbs) {
    const words = verb.verb.includes(' ') ? [] : subs.get(verb.verb) ?? [];
    verb.subs = words.map((word) => {
      for (const file of [...verb.files, DRIVER_HOST]) {
        const at = ctx.lines(file).findIndex((line) => line.includes(`"${word}"`));
        if (at >= 0) return `${word} \`${file.slice(file.lastIndexOf('/') + 1)}:${at + 1}\``;
      }
      return word;
    });
  }
  return verbs;
}

function devkitVerbs(ctx) {
  if (!ctx.has(DEVKIT)) return [];
  const lines = ctx.lines(DEVKIT);
  return lines.flatMap((line, i) => {
    const m = /^\s*"([a-z][a-z-]*)"(?:\s+or\s+"[^"]+")*\s*=>\s*(\w+)\(/.exec(line);
    if (!m) return [];
    const at = lines.findIndex((other, k) => k !== i && csMember(other, { inType: false })?.name === m[2]);
    return [{ verb: m[1], at: i + 1, handler: at >= 0 ? `\`Program.cs:${at + 1}\` ${m[2]}` : m[2] }];
  });
}

function desktopVerbs(ctx) {
  if (!ctx.has(DESKTOP_TOOL)) return [];
  return ctx.lines(DESKTOP_TOOL).flatMap((line, i) => {
    const m = /^\s*case\s+'([a-z][a-z-]*)':/.exec(line);
    return m ? [{ verb: m[1], at: i + 1 }] : [];
  });
}

function npmScripts(ctx) {
  return PACKAGES.filter((file) => ctx.has(file)).map((file) => {
    const lines = ctx.lines(file);
    const scripts = JSON.parse(ctx.read(file)).scripts ?? {};
    const from = lines.findIndex((line) => /^\s*"scripts"\s*:/.test(line));
    return {
      file,
      scripts: Object.entries(scripts).map(([name, command]) => ({
        name,
        at: lines.findIndex((line, k) => k > from && line.trimStart().startsWith(`${JSON.stringify(name)}:`)) + 1,
        command: truncate(String(command), 70),
      })),
    };
  });
}

const cell = (text) => text.replace(/\|/g, '\\|');

export function verbsPage(ctx) {
  const out = ['# Terminal verbs', '', `${GENERATED} Where each terminal verb is routed and handled, by program.`];
  const cli = cliVerbs(ctx);
  if (cli.length) {
    out.push('', '## `daoris` (the CLI)', '', 'Each verb is a module of `src/Daoris.Cli/src/cli/`, its row in `COMMANDS` (`src/Daoris.Cli/src/cli.ts`); paths below are under `src/Daoris.Cli/src/`.', '',
      '| Verb | Class | Row | Handler | Usage |', '|---|---|---|---|---|');
    for (const verb of cli) out.push(`| \`${verb.verb}\` | ${verb.kind} | ${verb.at} | ${verb.handler || '—'} | ${cell(verb.says)} |`);
  }
  const driver = driverVerbs(ctx);
  if (driver.length) {
    out.push('', '## `daoris-driver` (the driver\'s headless host)', '',
      `Routed in \`${DRIVER_HOST}\`, its usage \`DriverCommand.Usage\` in \`${driverFileIn(ctx, 'DriverCommand.cs') ?? 'DriverCommand.cs'}\`; handlers below are under \`${DESKTOP}\`. A sub-verb names where its word first appears: the handler, the library's command it hands to, or \`Program.cs\`.`, '',
      '| Verb | Routed at | Handled in | Sub-verbs |', '|---|---|---|---|');
    for (const verb of driver) out.push(`| \`${verb.verb}\` | \`Program.cs:${verb.routed}\` | ${verb.handlers.join('; ')} | ${verb.subs.join(' · ') || '—'} |`);
  }
  const devkit = devkitVerbs(ctx);
  if (devkit.length) {
    out.push('', '## `daoris-devkit`', '', `Routed in \`${DEVKIT}\`.`, '', '| Verb | Routed at | Handler |', '|---|---|---|');
    for (const verb of devkit) out.push(`| \`${verb.verb}\` | \`Program.cs:${verb.at}\` | ${verb.handler} |`);
  }
  const desktop = desktopVerbs(ctx);
  if (desktop.length) {
    out.push('', '## `npm run desktop -- <verb>`', '', `Each a \`case\` in \`${DESKTOP_TOOL}\`: ${desktop.map((verb) => `\`${verb.verb}\` ${verb.at}`).join(' · ')}.`);
  }
  for (const { file, scripts } of npmScripts(ctx)) {
    out.push('', `## npm scripts in \`${file}\``, '', '| Script | Line | Runs |', '|---|---|---|');
    for (const script of scripts) out.push(`| \`${script.name}\` | ${script.at} | ${cell(`\`${script.command.replace(/`/g, "'")}\``)} |`);
  }
  return `${out.join('\n')}\n`;
}

// ---------------------------------------------------------------------------------------------------
// Catalogues
