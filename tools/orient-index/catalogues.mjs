import { join } from 'node:path';
import { GENERATED, GLOSSARY, LOCALES, compare } from './common.mjs';
import { CODE, outlineCSharp, outlineScript } from './outline.mjs';

export function cataloguesPage(ctx) {
  const out = [
    '# Catalogue areas',
    '',
    `${GENERATED} A key's home is the area named by its longest dotted prefix that has a file (\`${LOCALES}index.ts\`): area \`settings.rules\` is \`${LOCALES}en/settings.rules.json\` and its \`zh\` twin. Second segments with three keys or more are counted, most first.`,
    '',
    '| Area | Keys | Languages | Second segments (keys) |',
    '|---|---|---|---|',
  ];
  const areas = new Map();
  for (const path of ctx.files.filter((file) => file.startsWith(LOCALES) && /^[a-z]+\/[^/]+\.json$/.test(file.slice(LOCALES.length)))) {
    const [language, name] = path.slice(LOCALES.length).split('/');
    const area = name.slice(0, -'.json'.length);
    if (!areas.has(area)) areas.set(area, { languages: [], keys: null });
    const entry = areas.get(area);
    entry.languages.push(language);
    if (language === 'en' || entry.keys === null) entry.keys = Object.keys(JSON.parse(ctx.read(path)));
  }
  for (const area of [...areas.keys()].sort(compare)) {
    const { languages, keys } = areas.get(area);
    const counts = new Map();
    for (const key of keys) {
      const rest = key.startsWith(`${area}.`) ? key.slice(area.length + 1) : null;
      const segment = rest?.includes('.') ? rest.split('.')[0] : null;
      if (segment) counts.set(segment, (counts.get(segment) ?? 0) + 1);
    }
    const ranked = [...counts].filter(([, n]) => n >= 3).sort((a, b) => b[1] - a[1] || compare(a[0], b[0]));
    const shown = ranked.slice(0, 8).map(([segment, n]) => `\`${segment}\` ${n}`);
    if (ranked.length > 8) shown.push(`+${ranked.length - 8} more`);
    out.push(`| \`${area}\` | ${keys.length} | ${languages.sort(compare).join(', ')} | ${shown.join(', ')} |`);
  }
  if (ctx.has(GLOSSARY)) {
    const lines = ctx.lines(GLOSSARY);
    const sections = lines.flatMap((line, i) => {
      const m = /^ {2}"([^"]+)"\s*:/.exec(line);
      return m ? [{ name: m[1], start: i + 1 }] : [];
    });
    sections.forEach((section, k) => {
      section.end = (k + 1 < sections.length ? sections[k + 1].start : lines.length) - 1;
    });
    const kinds = lines.flatMap((line, i) => {
      const m = /^ {4}"([a-z][\w-]*)"\s*:\s*\{/.exec(line);
      const within = sections.find((section) => section.name === 'kinds');
      return m && within && i + 1 > within.start && i + 1 <= within.end ? [`\`${m[1]}\` ${i + 1}`] : [];
    });
    const terms = lines.flatMap((line, i) => {
      const m = /^\s*\{\s*"term"\s*:\s*"([^"]+)"/.exec(line);
      return m ? [`\`${m[1]}\` ${i + 1}`] : [];
    });
    out.push('', '## The glossary', '', `\`${GLOSSARY}\`, the names people and code use, by section: ${sections.map((section) => `\`${section.name}\` ${section.start}-${section.end}`).join(' · ')}.`);
    if (kinds.length) out.push('', `Kinds, by line: ${kinds.join(', ')}.`);
    if (terms.length) out.push('', `Terms, by line: ${terms.join(', ')}.`);
  }
  return `${out.join('\n')}\n`;
}

// ---------------------------------------------------------------------------------------------------
// Test fixtures, stand-ins and kits

const FIXTURE_NAME = /(Fixture|Harness|StandIn|Stub|Fake|Host|Scratch|World|Builder|Bridge)$/;
const SCRIPT_FIXTURES = [
  (path) => /^src\/Daoris\.Cli\/test\/_[^/]+\.ts$/.test(path),
  (path) => path.startsWith('src/Daoris.Web/src/test/') && CODE.test(path),
  (path) => /^src\/Daoris\.Web\/src\/(?:.+\/)?(?:fixtures|[A-Za-z]+Fixtures|storyIcons)\.tsx?$/.test(path),
  (path) => path.startsWith('src/Daoris.Web/.storybook/') && CODE.test(path),
  (path) => path === 'src/Daoris.Web/scripts/e2e-host.mjs',
  (path) => /^tools\/(?:[a-z-]+-kit|testbed)\.mjs$/.test(path),
];

/** The .NET suites' helpers: each helper file's types and their non-private members, and the shared types a test file declares. */
function dotnetFixtures(ctx) {
  const suites = new Map();
  for (const path of ctx.files.filter((file) => file.endsWith('.cs') && /\/[A-Za-z.]*Tests\//.test(file))) {
    const suite = /^(.*?[A-Za-z.]*Tests)\//.exec(path)[1];
    const base = path.slice(suite.length + 1);
    if (/(^|\/)(GlobalUsings|AssemblyInfo)\.cs$/.test(base) || base.includes('/obj/') || base.includes('/bin/')) continue;
    const entries = outlineCSharp(ctx.read(path));
    const own = base.split('/').pop().slice(0, -'.cs'.length);
    const helper = !base.endsWith('Tests.cs');
    const rows = [];
    for (const type of entries.filter((entry) => entry.kind === 'type')) {
      // A nested type is private unless it says otherwise; a top-level one is internal unless it says otherwise.
      const open = type.depth > 0 ? /\b(public|internal|protected)\b/.test(type.modifiers) : !/\bprivate\b/.test(type.modifiers);
      if (helper) {
        if (!open && type.depth > 0) continue;
        const members = entries.filter((entry) => entry.kind === 'member' && entry.start > type.start && entry.end <= type.end && entry.depth === type.depth + 1
          && /\b(public|internal|protected)\b/.test(entry.modifiers ?? '') && entry.member !== type.type);
        const names = [];
        for (const member of members) if (!names.some((name) => name.startsWith(`${member.member} `))) names.push(`${member.member} ${member.start}`);
        rows.push(`- \`${base}:${type.start}\` ${type.name}${names.length ? `: ${names.join(', ')}` : ''}`);
      } else if (type.type !== own && !/Tests$/.test(type.type) && (open || FIXTURE_NAME.test(type.type))) {
        rows.push(`- \`${base}:${type.start}\` ${type.name}${open ? '' : ' (private)'}`);
      }
    }
    if (!rows.length) continue;
    if (!suites.has(suite)) suites.set(suite, []);
    suites.get(suite).push(...rows);
  }
  return suites;
}

export function fixturesPage(ctx) {
  const out = ['# Test fixtures, stand-ins and kits', '', `${GENERATED} By suite: each helper file's types and their non-private members, each test file's shared or stand-in types, and the scripts' fixtures and kits by export, each with its line.`];
  for (const [suite, rows] of [...dotnetFixtures(ctx)].sort((a, b) => compare(a[0], b[0]))) out.push('', `## \`${suite}/\``, '', ...rows);
  const scripts = ctx.files.filter((path) => SCRIPT_FIXTURES.some((matches) => matches(path)));
  const groups = new Map();
  for (const path of scripts) {
    const folder = path.slice(0, path.lastIndexOf('/') + 1);
    const exports = outlineScript(ctx.read(path), path).filter((entry) => entry.exported && entry.indent === 0);
    if (!groups.has(folder)) groups.set(folder, []);
    const names = [];
    for (const entry of exports) if (!names.some((name) => name.startsWith(`${entry.id} `))) names.push(`${entry.id} ${entry.start}`);
    groups.get(folder).push(`- \`${path.slice(folder.length)}\`: ${names.join(', ') || '(no export)'}`);
  }
  for (const folder of [...groups.keys()].sort(compare)) out.push('', `## \`${folder}\``, '', ...groups.get(folder));
  return `${out.join('\n')}\n`;
}

// ---------------------------------------------------------------------------------------------------
// The decisions digest
