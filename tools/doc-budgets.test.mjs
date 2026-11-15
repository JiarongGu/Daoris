/**
 * The startup set: what this repository makes every session pay before its first step, against its ceilings.
 *
 *   node --test tools/doc-budgets.test.mjs
 *
 * Each case builds a small checkout in a gitignored scratch folder and measures it.
 */
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { startupSet } from './doc-budgets/startup.mjs';

const scratch = join(dirname(fileURLToPath(import.meta.url)), '..', '_fixtures', 'doc-budgets-test');
after(() => rmSync(scratch, { recursive: true, force: true }));

let made = 0;
/** A checkout holding `files`, each path relative to its root. */
function checkout(files) {
  const root = join(scratch, `c${made += 1}`);
  rmSync(root, { recursive: true, force: true });
  for (const [path, text] of Object.entries(files)) {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), text);
  }
  return root;
}

const skill = (name, words) => `---\nname: ${name}\ndescription: ${'word '.repeat(words).trim()}\n---\n\n# ${name}\n\n${'body '.repeat(500)}\n`;
const agent = (name, words, body) => `---\nname: ${name}\ndescription: ${'word '.repeat(words).trim()}\ntools: Read\n---\n\n${'word '.repeat(body)}\n`;

test('the startup set counts the brief without its comments, the descriptions and the largest agent body', () => {
  const root = checkout({
    'AGENTS.md': `<!-- a comment a harness strips -->\n${'x'.repeat(396)}\n`,
    'CLAUDE.md': '<!-- generated -->\n@AGENTS.md\n',
    '.claude/skills/one/SKILL.md': skill('one', 10),
    '.claude/agents/builder.md': agent('builder', 10, 300),
    '.claude/agents/scout.md': agent('scout', 10, 100),
  });
  const set = startupSet(root);
  const by = Object.fromEntries(set.sources.map((source) => [source.name, source]));

  assert.equal(by['AGENTS.md'].tokens, 100, 'bytes / 4 of the file a harness hands over, its comments stripped');
  assert.ok(by['AGENTS.md'].rawTokens > by['AGENTS.md'].tokens, 'the raw file, as another harness reads it, is said beside');
  assert.equal(by['CLAUDE.md'].tokens, 3);
  assert.equal(by['skill descriptions'].count, 1);
  assert.equal(by['agent descriptions'].count, 2);
  assert.equal(by['agent body (largest)'].label, '.claude/agents/builder.md');
  assert.equal(set.total, set.sources.reduce((sum, source) => sum + source.tokens, 0));
  assert.deepEqual(set.failing, []);
});

test('a description over thirty words, an agent body over 350 words and a total over the ceiling each fail', () => {
  const root = checkout({
    'AGENTS.md': `${'x'.repeat(14000)}\n`,
    '.claude/skills/long/SKILL.md': skill('long', 31),
    '.claude/agents/builder.md': agent('builder', 10, 351),
  });
  const set = startupSet(root);

  assert.deepEqual(set.failing.map((failing) => failing.what), [
    '.claude/skills/long/SKILL.md description: 31 words of 30',
    '.claude/agents/builder.md body: 351 words of 350',
    `the startup set: ${set.total} tokens of 3200`,
  ]);
});

/** Untracked and not this repository's to hold: said when present, so a reader sees the whole start, and never failed on. */
test('the local file and the memory index are reported when present, and never fail', () => {
  const root = checkout({
    'AGENTS.md': 'short\n',
    'local/CLAUDE.local.md': `${'word '.repeat(5000)}\n`,
  });
  const memory = join(root, 'memory', 'MEMORY.md');
  mkdirSync(dirname(memory), { recursive: true });
  writeFileSync(memory, `${'entry '.repeat(400)}\n`);
  const set = startupSet(root, { memoryIndex: memory });

  assert.deepEqual(set.reported.map((each) => `${each.name} ${each.words}`), ['local/CLAUDE.local.md 5000', 'memory index 400']);
  assert.deepEqual(set.failing, []);
  assert.deepEqual(startupSet(root, { memoryIndex: join(root, 'nowhere.md') }).reported.map((each) => each.name), ['local/CLAUDE.local.md']);
});
