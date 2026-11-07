import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CHANGE_SKILL, onDemandExtras, renderIndex, renderTier, tierRuleBody } from '../../src/doctrine/tierrender.ts';
import type { CanonFile } from '../../src/doctrine/records.ts';

/**
 * What goes INSIDE the region — the always-loaded tier, rendered once, for the file
 * every harness reads.
 *
 * Four content decisions live here, and each is a consequence of the move rather than a preference:
 *
 * 1. **Frontmatter is stripped.** Frontmatter is only frontmatter at byte 0; eight
 *    rules concatenated would put seven `---` blocks mid-document, where they render as horizontal
 *    rules and stray key-value prose. What it carries — `applies_when` and `enforces` — becomes the
 *    table.
 * 2. **Provenance stays.** The per-rule header is what keeps drift and `upstream` per RULE inside one
 *    region, which design §4 rests on.
 * 3. **The rules rows stay in the table** — cheap, and they carry the `applies_when`/`enforces` the
 *    stripped frontmatter held. The knowledge and skills rows live in `<target>/INDEX.md`, which the
 *    region names: they grow with the repository, and would push the rules down.
 * 4. **Only the rules tier is in full.** A knowledge document marked `rule: true` is a
 *    row linked to its file, and its body is read when it applies.
 */

const file = (name: string): CanonFile => ({
  pack: 'core',
  source: `core/rules/${name}.md`,
  target: `rules/${name}.md`,
});

const rule = (name: string, applies: string, enforces: string, body: string) => ({
  file: file(name),
  text: `---\nname: ${name}\napplies_when: ${applies}\nenforces: ${enforces}\n---\n\n${body}\n`,
});

const two = [
  rule('sensitive-info', 'writing any tracked file', 'no machine paths', '# Sensitive info\n\nKeep it out.'),
  rule('task-lifecycle', 'finishing a task', 'the backlog holds open work', '# Task lifecycle\n\nMove it.'),
];

test('each rule arrives with its frontmatter gone and its provenance kept', () => {
  const region = renderTier({ rules: two, version: '0.0.1' });

  // The prose is there…
  assert.match(region, /# Sensitive info/);
  assert.match(region, /Keep it out\./);
  // …the provenance line with it…
  assert.match(region, /<!-- daoris: core\/core\/rules\/sensitive-info\.md @ 0\.0\.1/);
  // …and no frontmatter fence anywhere, which would render as a horizontal rule mid-document.
  assert.doesNotMatch(region, /^---$/m);
  assert.doesNotMatch(region, /^applies_when:/m);
});

test('the table carries what the stripped frontmatter held', () => {
  const region = renderTier({ rules: two, version: '0.0.1' });

  assert.match(region, /\| `sensitive-info` \| writing any tracked file \| no machine paths \|/);
});

/**
 * 🔴 No `---` line anywhere. It is indistinguishable from the frontmatter fence this render exists to
 * remove — the first version used one as a separator and its own test caught it — and in markdown a
 * `---` under a line of text silently turns that line into a heading.
 */
test('nothing in the region can be mistaken for a frontmatter fence', () => {
  const region = renderTier({ rules: two, version: '0.0.1' });

  assert.doesNotMatch(region, /^---\s*$/m);
});

/**
 * 🔴 The region's own headings sit at the SAME level the rule bodies use, and the bodies are not
 * touched. Rendering the real canon and reading it settled this: nesting under a `## Doctrine`
 * wrapper leaves eight `#` headings jumping back to top level inside it, and fixing that means
 * demoting every heading in every rule — a transformation that has to be exactly reversible over
 * arbitrary markdown, fenced blocks included, or a rule is corrupted on its way back out.
 */
test('the region is flat, because a body rewritten on the way in cannot be compared on the way out', () => {
  const region = renderTier({ rules: two, version: '0.0.1' });

  assert.match(region, /^# Doctrine$/m);
  // The rules' own headings arrive exactly as the canon wrote them.
  assert.match(region, /^# Sensitive info$/m);
  assert.match(region, /^# Task lifecycle$/m);
});

/**
 * The tiers that are NOT loaded are the reason an index exists at all: an agent cannot discover
 * `.claude/knowledge/` by reading a file that does not mention it. The region names the index, and the
 * index names every document.
 */
test('the region points at the index, and the index lists knowledge and skills by their paths', () => {
  const region = renderTier({ rules: [], version: '0.0.1' });
  const index = renderIndex({
    knowledge: [{
      file: { pack: 'core', source: 'core/knowledge/reaching-in.md', target: 'knowledge/reaching-in.md' },
      text: '---\nname: reaching-in\napplies_when: you have written into another repository\nenforces: stop and report\n---\n\nbody',
    }],
    skills: [{
      file: { pack: 'core', source: 'core/skills/fix-log/SKILL.md', target: 'skills/fix-log/SKILL.md' },
      text: '---\nname: fix-log\ndescription: Record a fix after landing it.\n---\n\nbody',
    }],
  });

  assert.match(region, /listed in \[\.claude\/INDEX\.md\]\(\.claude\/INDEX\.md\), generated from the files/);
  assert.doesNotMatch(region, /reaching-in|fix-log/);
  assert.match(index, /^\| `\.claude\/knowledge\/reaching-in\.md` \| you have written into another repository \| stop and report \|$/m);
  // A skill is named by its entry file: an agent whose skill roots hold nothing activates it by reading it.
  assert.match(index, /^\| `\.claude\/skills\/fix-log\/SKILL\.md` \| Record a fix after landing it\. \|$/m);
});

const onDemand = (name: string, applies: string, enforces: string, body: string) => ({
  file: { pack: 'core', source: `core/knowledge/${name}.md`, target: `knowledge/${name}.md` },
  text: `---\nname: ${name}\napplies_when: ${applies}\nenforces: ${enforces}\nrule: true\n---\n\n${body}\n`,
});

/**
 * A rule read on demand keeps its row in the always-loaded table, linked to the file that holds its body, and its
 * body stays out of the region: that is what keeps the region small while every rule's trigger is still loaded.
 */
test('a rule read on demand is a linked row, and its body stays in its file', () => {
  const region = renderTier({
    rules: two,
    onDemand: [onDemand('no-tmp-for-repo-files', 'composing a repository file', 'never OS temp', '# No temp\n\nScratch here.')],
    version: '0.0.1',
  });

  assert.match(region, /^\| \[`no-tmp-for-repo-files`\]\(\.claude\/knowledge\/no-tmp-for-repo-files\.md\) \| composing a repository file \| never OS temp \|$/m);
  assert.doesNotMatch(region, /Scratch here\.|# No temp/);
  // The rules in full come first, and the on-demand rows after them, all in the one table.
  assert.ok(region.indexOf('| `task-lifecycle` |') < region.indexOf('[`no-tmp-for-repo-files`]'));
  assert.ok(region.indexOf('[`no-tmp-for-repo-files`]') < region.indexOf('## Read on demand'));
});

test('the region sends every change through the change skill only where that skill is installed', () => {
  assert.match(renderTier({ rules: two, change: true, version: '0.0.1' }), /^Make every change through the `change` skill\.$/m);
  assert.doesNotMatch(renderTier({ rules: two, version: '0.0.1' }), /`change` skill/);
});

/**
 * What a sync writes decides the extras: only the canon's knowledge files marked `rule: true` become rows (a span, a
 * skill or an unmarked document never does), and the `change` line follows the skill's own target.
 */
test('the extras come from the canon writes: the marked knowledge files, and the change skill', () => {
  const marked = onDemand('task-lifecycle', 'finishing a task', 'open work only', '# Task lifecycle');
  const writes = [
    { ...marked.file, content: marked.text },
    { pack: 'core', source: 'core/knowledge/reaching-in.md', target: 'knowledge/reaching-in.md', content: '---\nname: reaching-in\napplies_when: w\nenforces: e\n---\n\nbody\n' },
    { pack: 'core', source: 'core/rules/sensitive-info.md', target: 'rules/sensitive-info.md', content: 'body', in: 'AGENTS.md' },
    { pack: 'core', source: `core/${CHANGE_SKILL}`, target: CHANGE_SKILL, content: '---\nname: change\ndescription: d\nrule: true\n---\n' },
  ];

  const extras = onDemandExtras(writes);
  assert.deepEqual(extras.onDemand!.map((document) => document.file.target), ['knowledge/task-lifecycle.md']);
  assert.equal(extras.change, true);
  assert.equal(onDemandExtras(writes.slice(0, 3)).change, false);
});

/** Deterministic, or every sync is a diff and every drift check is a lie. */
test('the same input renders the same bytes', () => {
  const once = renderTier({ rules: two, version: '0.0.1' });
  const twice = renderTier({ rules: [...two], version: '0.0.1' });

  assert.equal(once, twice);
});

/**
 * 🔴 One rule's body, addressable on its own — this is what keeps drift and `upstream` per rule
 * inside one region. Without it the whole region is one artefact, a person who improves one sentence
 * has drifted all eight rules, and `upstream <file>` has nothing to extract.
 */
test('a single rule can be read back out of a rendered region', () => {
  const region = renderTier({ rules: two, version: '0.0.1' });

  assert.equal(tierRuleBody(region, 'core/core/rules/sensitive-info.md'), '# Sensitive info\n\nKeep it out.');
  assert.equal(tierRuleBody(region, 'core/core/rules/task-lifecycle.md'), '# Task lifecycle\n\nMove it.');
  assert.equal(tierRuleBody(region, 'core/core/rules/never-shipped.md'), null);
});

/**
 * The body read back must be what the canon would render, byte for byte — otherwise the first `check`
 * after a clean `sync` reports drift on a file nobody touched, which the
 * body-comparison in `planSync` guards against.
 */
test('what is rendered is what reads back, for every rule', () => {
  const region = renderTier({ rules: two, version: '0.0.1' });

  for (const { file: canonFile, text } of two) {
    assert.equal(tierRuleBody(region, `${canonFile.pack}/${canonFile.source}`), tierBodyOf(text));
  }
});

/** The body a rule contributes: its text with the frontmatter removed and the edges trimmed. */
function tierBodyOf(text: string): string {
  const end = text.indexOf('\n---\n', 3);
  return text.slice(end + 5).trim();
}
