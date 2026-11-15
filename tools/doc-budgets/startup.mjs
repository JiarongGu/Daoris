/**
 * The startup set: what this repository makes a session pay before its first step. The brief, `CLAUDE.md`, every skill's
 * description and every agent's description are loaded into each session; a dispatched agent pays its own body too, so the
 * largest body is counted. Tokens are estimated as bytes / 4 of what a harness hands over (HTML comments stripped), with
 * the raw file said beside it for the harness that reads it whole; the measured figures are Stage 0's.
 *
 * The local instruction file and the assistant's memory index load too, but they are untracked and not this repository's to
 * hold, so they are reported when present and never failed on.
 */
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';

/** The ceilings the startup set is held to. */
export const STARTUP = { tokens: 3200, description: 30, body: 350 };

const words = (text) => text.split(/\s+/).filter(Boolean).length;
const bytes = (text) => Buffer.byteLength(text, 'utf8');
const tokensOf = (count) => Math.ceil(count / 4);
const stripComments = (text) => text.replace(/<!--[\s\S]*?-->/g, '');
const read = (file) => (existsSync(file) ? readFileSync(file, 'utf8') : null);

/** A document's frontmatter description and its body, or null when it has no frontmatter. */
function parts(text) {
  const match = /^---\r?\n([\s\S]*?)\r?\n---\r?\n?([\s\S]*)$/.exec(text);
  if (!match) return null;
  const line = /^description:[ \t]*(.*)$/m.exec(match[1]);
  return { description: (line?.[1] ?? '').trim().replace(/^(['"])(.*)\1$/, '$2'), body: match[2] };
}

/** The `.md` documents a tier holds: each skill's `SKILL.md`, or each agent's file. */
function documents(root, dir, skills) {
  const at = join(root, dir);
  if (!existsSync(at)) return [];
  return readdirSync(at, { withFileTypes: true })
    .filter((entry) => (skills ? entry.isDirectory() && existsSync(join(at, entry.name, 'SKILL.md')) : entry.isFile() && entry.name.endsWith('.md')))
    .map((entry) => `${dir}/${skills ? `${entry.name}/SKILL.md` : entry.name}`)
    .sort();
}

/** Where Claude Code keeps this checkout's memory index: its home's projects folder, named for the checkout's path. */
export const memoryIndexOf = (root) => join(homedir(), '.claude', 'projects', root.replace(/[^A-Za-z0-9]/g, '-'), 'memory', 'MEMORY.md');

/**
 * The startup set of the checkout at `root`: each source's estimated tokens, the total, what fails, and what is reported.
 *
 * @param options.memoryIndex The memory index to report, when it is not where Claude Code keeps this checkout's.
 */
export function startupSet(root, { memoryIndex = memoryIndexOf(root) } = {}) {
  const target = JSON.parse(read(join(root, 'daoris.json')) ?? '{}').target ?? '.claude';
  const sources = [];
  const failing = [];

  for (const name of ['AGENTS.md', 'CLAUDE.md']) {
    const text = read(join(root, name));
    if (text !== null) sources.push({ name, tokens: tokensOf(bytes(stripComments(text))), rawTokens: tokensOf(bytes(text)) });
  }

  const described = (paths, kind) => {
    let total = 0;
    for (const path of paths) {
      const held = parts(readFileSync(join(root, path), 'utf8'));
      if (!held) continue;
      total += bytes(held.description);
      const count = words(held.description);
      if (count > STARTUP.description) failing.push({ what: `${path} description: ${count} words of ${STARTUP.description}` });
      if (kind === 'agent') {
        const body = words(held.body);
        if (body > STARTUP.body) failing.push({ what: `${path} body: ${body} words of ${STARTUP.body}` });
      }
    }
    return total;
  };

  const skills = documents(root, `${target}/skills`, true);
  sources.push({ name: 'skill descriptions', count: skills.length, tokens: tokensOf(described(skills, 'skill')) });
  const agents = documents(root, `${target}/agents`, false);
  sources.push({ name: 'agent descriptions', count: agents.length, tokens: tokensOf(described(agents, 'agent')) });
  const bodies = agents.map((path) => ({ path, body: parts(readFileSync(join(root, path), 'utf8'))?.body ?? '' }))
    .sort((a, b) => bytes(b.body) - bytes(a.body));
  if (bodies.length) {
    sources.push({ name: 'agent body (largest)', label: bodies[0].path, words: words(bodies[0].body), tokens: tokensOf(bytes(bodies[0].body)) });
  }

  const total = sources.reduce((sum, source) => sum + source.tokens, 0);
  if (total > STARTUP.tokens) failing.push({ what: `the startup set: ${total} tokens of ${STARTUP.tokens}` });

  const reported = [];
  for (const [name, file] of [['local/CLAUDE.local.md', join(root, 'local', 'CLAUDE.local.md')], ['memory index', memoryIndex]]) {
    const text = read(file);
    if (text !== null) reported.push({ name, words: words(text), tokens: tokensOf(bytes(text)) });
  }
  return { sources, total, failing, reported };
}
