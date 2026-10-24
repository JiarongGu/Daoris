#!/usr/bin/env node
// GITHUBDOCS1 (D211): the links a reader is handed resolve, the agent rules are one text in three files,
// and the README the CLI package ships reads right on its registry.
// Mirrors doc-system.mjs: a pure audit over a file inventory, an import-safe runner, no dependency and no
// file writes. The universal `links` gate resolves a relative link's FILE on disk; this adds what it leaves:
// the letter case GitHub sees, the #anchor, the absolute links into this repository, and the registry.
import { readFileSync } from 'node:fs';
import { dirname, join, posix } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, repositoryFiles } from './fsx.mjs';

/** The repository the absolute links name: `origin` on GitHub, where the README is read first. */
export const REPOSITORY = 'JiarongGu/Daoris';
/** The branch an absolute link must name; a commit or a tag is history and is left alone. */
const BRANCH = 'main';

/** The README a package ships, its copies of the agent rules, and the rule limit one of them enforces. */
export const SHIPPED_README = 'README.md';
export const AGENT_FILES = ['llms.txt', 'context7.json'];
// Context7 skips a rule over 255 characters, said only in its own parse log.
export const RULE_LIMIT = 255;

// History by definition: an entry records what was true when it was written, links included, and is
// never edited to follow a later move. Fixtures quote documents rather than being them.
const HISTORY = /^(docs\/archive\/|docs\/decisions\/|docs\/task-archive\.md$|docs\/FIX-LOG\.md$|CHANGELOG\.md$|canon\/CHANGELOG\.md$)/;
const FIXTURE = /(^|\/)(golden|[^/]*-fixtures|_fixtures)\//;

const REPO_URL = new RegExp(String.raw`https://(github\.com/${REPOSITORY}/(?:blob|tree)|raw\.githubusercontent\.com/${REPOSITORY})/([^/\s)]+)/([^\s)"'\`<>#?]+)(?:\?[^\s)"'\`<>#]*)?(?:#([^\s)"'\`<>]*))?`, 'g');
const MD_LINK = /!?\[[^\]]*\]\(\s*<?([^)\s>]+)>?(?:\s+"[^"]*")?\s*\)/g;
const LINK_DEFINITION = /^ {0,3}\[[^\]]+\]:\s*<?([^\s>]+)>?/;
const LINE_ANCHOR = /^L\d+(C\d+)?(-L\d+(C\d+)?)?$/;
const RULES_BLOCK = /<!-- agent-rules:begin[^\n]*\n([\s\S]*?)<!-- agent-rules:end -->/;

const decode = (s) => { try { return decodeURIComponent(s); } catch { return null; } };

/** Each line with its code spans blanked and fenced blocks emptied: neither holds a link a reader follows. */
export function proseLines(text) {
  let fence = null;
  return text.split(/\r?\n/).map((line) => {
    const f = line.match(/^\s*(`{3,}|~{3,})/);
    if (f) { if (!fence) fence = f[1][0]; else if (f[1][0] === fence) fence = null; return ''; }
    return fence ? '' : line.replace(/`[^`]*`/g, (s) => ' '.repeat(s.length));
  });
}

/** GitHub's id for a heading: github-slugger over its rendered text, a code span's text kept verbatim. */
export function headingSlug(text) {
  const rendered = text.split(/(`+[^`]*`+)/).map((part, i) => (i % 2 === 1 ? part.replace(/`/g, '') : part
    .replace(/!\[[^\]]*\]\([^)]*\)/g, '')            // an image's alt is not the heading's text
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/<[^>]+>/g, '')
    .replace(/\*/g, '')
    .replace(/(^|[^\p{L}\p{N}_])_+|_+(?=[^\p{L}\p{N}_]|$)/gu, '$1'))).join('');
  return rendered.trim().toLowerCase().replace(/[^\p{L}\p{N}\p{M} _-]/gu, '').replace(/ /g, '-');
}

/** Every heading id in a markdown text, with GitHub's `-1`, `-2` for a repeated heading. */
export function anchorsOf(text) {
  const ids = new Set();
  const seen = new Map();
  const add = (heading) => {
    const base = headingSlug(heading);
    const n = seen.get(base) ?? 0;
    seen.set(base, n + 1);
    ids.add(n === 0 ? base : `${base}-${n}`);
  };
  let fence = null;
  let previous = '';
  for (const line of text.split(/\r?\n/)) {
    const f = line.match(/^\s*(`{3,}|~{3,})/);
    if (f) { if (!fence) fence = f[1][0]; else if (f[1][0] === fence) fence = null; previous = ''; continue; }
    if (fence) continue;
    for (const m of line.matchAll(/<a\s+(?:id|name)="([^"]+)"/g)) ids.add(m[1]);
    const atx = line.match(/^ {0,3}(?:>\s*)*#{1,6}(?:\s+(.*?))?\s*#*\s*$/);
    if (atx) { add(atx[1] ?? ''); previous = ''; continue; }
    // A setext heading: a paragraph line underlined with = or -.
    if (/^ {0,3}(=+|-+)\s*$/.test(line) && previous.trim() !== '' && !/^\s*([|>#*+-]|\d+\.)/.test(previous)) {
      add(previous.trim());
      previous = '';
      continue;
    }
    previous = line;
  }
  return ids;
}

/** The rules a file carries: README's and llms.txt's marked block, one `- ` rule per line, or context7.json's `rules`. */
function rulesOf(file, text, findings) {
  if (file.endsWith('.json')) {
    let rules;
    try { rules = JSON.parse(text).rules; } catch (error) { findings.push(`${file}: not valid JSON (${error.message})`); return null; }
    if (!Array.isArray(rules)) { findings.push(`${file}: has no "rules" array, so it carries no copy of the agent rules`); return null; }
    return rules;
  }
  const block = RULES_BLOCK.exec(text);
  if (!block) { findings.push(`${file}: no <!-- agent-rules:begin --> … <!-- agent-rules:end --> block`); return null; }
  const lines = block[1].split(/\r?\n/).filter((line) => line.trim() !== '');
  const stray = lines.find((line) => !line.startsWith('- '));
  if (stray) findings.push(`${file}: a line in the agent-rules block is not a "- " rule, so no copy is compared with it; keep each rule on one line: ${stray.trim().slice(0, 100)}`);
  return lines.filter((line) => line.startsWith('- ')).map((line) => line.slice(2));
}

/** README's agent-rules block is canonical; each agent file repeats it, rule for rule, and none is over the limit. */
export function agentRuleFindings(present, read) {
  const findings = [];
  if (!present.has(SHIPPED_README)) return [`${SHIPPED_README}: missing, so the agent rules have no canonical copy`];
  const canonical = rulesOf(SHIPPED_README, read(SHIPPED_README), findings);
  if (!canonical) return findings;
  if (canonical.length === 0) findings.push(`${SHIPPED_README}: the agent-rules block holds no "- " rule`);
  canonical.forEach((rule, i) => {
    if (rule.length > RULE_LIMIT) {
      findings.push(`${SHIPPED_README}: agent rule ${i + 1} is ${rule.length} characters, and Context7 drops a rule over ${RULE_LIMIT}; split it: ${rule.slice(0, 80)}`);
    }
  });
  for (const file of AGENT_FILES) {
    if (!present.has(file)) { findings.push(`${file}: missing, so its copy of the agent rules is kept by nothing`); continue; }
    const copy = rulesOf(file, read(file), findings);
    if (!copy) continue;
    const at = canonical.findIndex((rule, i) => copy[i] !== rule);
    if (at < 0 && copy.length === canonical.length) continue;
    const i = at < 0 ? canonical.length : at;
    findings.push(`${file}: its agent rules differ from ${SHIPPED_README}'s at rule ${i + 1}; edit ${SHIPPED_README}'s block, then copy it\n`
      + `      ${SHIPPED_README}: ${(canonical[i] ?? '(none)').slice(0, 100)}\n      ${file}: ${(copy[i] ?? '(none)').slice(0, 100)}`);
  }
  return findings;
}

/** Every link in the inventory's documents resolves: its path in the letter case git holds, and its #anchor. */
export function linkFindings(files, read) {
  const present = new Set(files);
  const dirs = new Set(['.']);
  for (const file of files) for (let d = posix.dirname(file); d !== '.'; d = posix.dirname(d)) dirs.add(d);
  const anchorCache = new Map();
  const findings = [];
  const counts = { absolute: 0, relative: 0, anchors: 0 };

  const unresolved = (target, anchor, { raw = false } = {}) => {
    const isFile = present.has(target);
    if (!isFile && !dirs.has(target)) return `"${target}" is not in a clone of this repository (or not in that letter case)`;
    if (raw && !isFile) return `"${target}" is a directory, and raw.githubusercontent.com serves files only`;
    if (!anchor || LINE_ANCHOR.test(anchor)) return null;
    counts.anchors++;
    if (!target.endsWith('.md')) return `"#${anchor}" points into "${target}", which has no headings`;
    const id = decode(anchor);
    if (id === null) return `"#${anchor}" is not valid percent-encoding`;
    if (!anchorCache.has(target)) anchorCache.set(target, anchorsOf(read(target)));
    return anchorCache.get(target).has(id) ? null : `"${target}" has no heading "#${anchor}"`;
  };

  for (const file of files) {
    const markdown = file.endsWith('.md');
    if (!markdown && !AGENT_FILES.includes(file)) continue;
    if (HISTORY.test(file) || FIXTURE.test(file)) continue;
    const text = read(file);
    const say = (i, why, line) => findings.push(`${file}:${i + 1}: a link does not resolve: ${why}\n      ${line.trim().slice(0, 110)}`);
    // Absolute links, from the raw text: a URL in a code span is still one a reader follows.
    text.split(/\r?\n/).forEach((line, i) => {
      for (const m of line.matchAll(REPO_URL)) {
        counts.absolute++;
        const [, host, ref, p, anchor] = m;
        if (/^([0-9a-f]{7,40}|v\d.*)$/.test(ref)) continue;
        if (ref !== BRANCH) { say(i, `it names the branch "${ref}", and this repository's is ${BRANCH}`, line); continue; }
        const target = decode(p);
        if (target === null) { say(i, `"${p}" is not valid percent-encoding`, line); continue; }
        const why = unresolved(target.replace(/\/$/, ''), anchor, { raw: host.startsWith('raw.') });
        if (why) say(i, why, line);
      }
    });
    if (!markdown) continue;
    // Relative links, reference definitions and in-document #anchors.
    proseLines(text).forEach((line, i) => {
      const hrefs = [...line.matchAll(MD_LINK)].map((m) => m[1]);
      const definition = LINK_DEFINITION.exec(line);
      if (definition) hrefs.push(definition[1]);
      for (const href of hrefs) {
        if (/^[a-z][a-z0-9+.-]*:/i.test(href) || href.startsWith('/')) continue;
        counts.relative++;
        const [p, anchor] = href.split('#');
        const local = decode(p);
        if (local === null) { say(i, `"${p}" is not valid percent-encoding`, line); continue; }
        const target = local === '' ? file : posix.normalize(posix.join(posix.dirname(file), local)).replace(/\/$/, '') || '.';
        // Private context is gitignored by design (AGENTS.md's layout); a clone never holds it.
        if (target === 'local' || target.startsWith('local/')) continue;
        if (target.startsWith('../')) { say(i, `"${href}" leaves the repository`, line); continue; }
        const why = unresolved(target, anchor);
        if (why) say(i, why, line);
      }
    });
  }
  return { findings, counts };
}

/**
 * How a registry reads a README a package ships: away from the repository, so only an absolute link has a
 * target; npm's sanitizer drops raw HTML, and a registry that shows it shows it as text. An in-page #anchor
 * works on GitHub and npm alike, and a comment outside a fence is hidden by both, which is where the
 * agent-rules markers live. A comment INSIDE a fence is shown as code on GitHub and npm, and stripped by
 * nuget.org, so a sample never carries one.
 */
export function shippedReadmeFindings(text, file = SHIPPED_README) {
  const findings = [];
  const shown = text.replace(/<!--[\s\S]*?-->/g, (c) => c.replace(/[^\n]/g, ' '));
  let fence = null;
  let fenceComment = false;
  text.split(/\r?\n/).forEach((line, i) => {
    const f = line.match(/^\s*(`{3,}|~{3,})/);
    if (f) { if (!fence) fence = f[1][0]; else if (f[1][0] === fence) fence = null; return; }
    if (fence && line.includes('<!--') && !fenceComment) {
      fenceComment = true;
      findings.push(`${file}:${i + 1}: an HTML comment inside a code block, which a registry may strip from the sample; write the note as prose`);
    }
  });
  proseLines(shown).forEach((line, i) => {
    const hrefs = [...line.matchAll(MD_LINK)].map((m) => m[1]);
    const definition = LINK_DEFINITION.exec(line);
    if (definition) hrefs.push(definition[1]);
    for (const href of hrefs) {
      if (/^([a-z][a-z0-9+.-]*:|#)/i.test(href)) continue;
      findings.push(`${file}:${i + 1}: links "${href}" relatively, and the package's README is read on its registry, where only an absolute link has a target; write https://github.com/${REPOSITORY}/blob/${BRANCH}/…`);
    }
    if (/<\/?[A-Za-z][A-Za-z0-9-]*(\s[^>]*)?\/?>/.test(line)) {
      findings.push(`${file}:${i + 1}: raw HTML, which a registry drops or shows as text; write it as markdown\n      ${line.trim().slice(0, 110)}`);
    }
  });
  return findings;
}

/** The whole audit over an inventory and its reads, so a fixture needs no checkout. */
export function auditLinks(files, read) {
  const present = new Set(files);
  const { findings, counts } = linkFindings(files, read);
  findings.push(...agentRuleFindings(present, read));
  if (present.has(SHIPPED_README)) findings.push(...shippedReadmeFindings(read(SHIPPED_README)));
  return { findings, counts };
}

if (isMain(import.meta.url)) {
  try {
    const args = process.argv.slice(2);
    if (args.length > 1 || (args.length === 1 && args[0] !== '--json')) throw new Error('usage: node tools/doc-links.mjs [--json]');
    const root = dirname(dirname(fileURLToPath(import.meta.url)));
    // git's own list, so a link in the wrong letter case fails here as it would on GitHub, NTFS or not.
    const files = repositoryFiles(root);
    const result = auditLinks(files, (file) => readFileSync(join(root, file), 'utf8'));
    if (args[0] === '--json') console.log(JSON.stringify(result, null, 2));
    else {
      const { absolute, relative, anchors } = result.counts;
      if (result.findings.length) console.error(result.findings.join('\n'));
      else console.log(`doc-links: ${absolute} absolute and ${relative} relative links resolve, ${anchors} anchors by GitHub's slug rule; `
        + `the agent rules agree in ${[SHIPPED_README, ...AGENT_FILES].join(', ')}; the shipped README reads right on its registry`);
    }
    process.exitCode = result.findings.length ? 1 : 0;
  } catch (error) {
    console.error(`doc-links: ${error.message}`);
    process.exitCode = 2;
  }
}
