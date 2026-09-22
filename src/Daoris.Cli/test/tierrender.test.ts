import { test } from 'node:test';
import assert from 'node:assert/strict';
import { renderTier, tierRuleBody } from '../src/tierrender.ts';
import type { CanonFile } from '../src/types.ts';

/**
 * What goes INSIDE the region (CANON8b/D59) — the always-loaded tier, rendered once, for the file
 * every harness reads.
 *
 * Three content decisions live here, and each is a consequence of the move rather than a preference:
 *
 * 1. **Frontmatter is stripped.** D14 already says frontmatter is only frontmatter at byte 0; eight
 *    rules concatenated would put seven `---` blocks mid-document, where they render as horizontal
 *    rules and stray key-value prose. What it carried — `applies_when` and `enforces` — becomes the
 *    table, which is where it was always being read from anyway.
 * 2. **Provenance stays.** The per-rule header is what keeps drift and `upstream` per RULE inside one
 *    region, which design §4 rests on.
 * 3. **The knowledge and skills rows stay in the table**, because those tiers are not loaded and the
 *    table is the only way an agent learns they exist. The rules rows stay too — cheap, and they
 *    carry the `applies_when`/`enforces` the stripped frontmatter held.
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
  const region = renderTier({ rules: two, knowledge: [], skills: [], version: '0.0.1' });

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
  const region = renderTier({ rules: two, knowledge: [], skills: [], version: '0.0.1' });

  assert.match(region, /\| `sensitive-info` \| writing any tracked file \| no machine paths \|/);
});

/**
 * 🔴 No `---` line anywhere. It is indistinguishable from the frontmatter fence this render exists to
 * remove — the first version used one as a separator and its own test caught it — and in markdown a
 * `---` under a line of text silently turns that line into a heading.
 */
test('nothing in the region can be mistaken for a frontmatter fence', () => {
  const region = renderTier({ rules: two, knowledge: [], skills: [], version: '0.0.1' });

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
  const region = renderTier({ rules: two, knowledge: [], skills: [], version: '0.0.1' });

  assert.match(region, /^# Doctrine$/m);
  // The rules' own headings arrive exactly as the canon wrote them.
  assert.match(region, /^# Sensitive info$/m);
  assert.match(region, /^# Task lifecycle$/m);
});

/**
 * The tiers that are NOT loaded are the reason the table exists at all: an agent cannot discover
 * `.claude/knowledge/` by reading a file that does not mention it.
 */
test('knowledge and skills are listed with their paths, because nothing loads them', () => {
  const region = renderTier({
    rules: [],
    knowledge: [{
      file: { pack: 'core', source: 'core/knowledge/reaching-in.md', target: 'knowledge/reaching-in.md' },
      text: '---\nname: reaching-in\napplies_when: you have written into another repository\nenforces: stop and report\n---\n\nbody',
    }],
    skills: [{
      file: { pack: 'core', source: 'core/skills/fix-log/SKILL.md', target: 'skills/fix-log/SKILL.md' },
      text: '---\nname: fix-log\ndescription: Record a fix after landing it.\n---\n\nbody',
    }],
    version: '0.0.1',
  });

  assert.match(region, /\.claude\/knowledge\/reaching-in\.md/);
  assert.match(region, /\.claude\/skills\/fix-log/);
  // A skill is named by its DIRECTORY; `SKILL.md` is an implementation detail no roster should show.
  assert.doesNotMatch(region, /fix-log\/SKILL\.md\)/);
});

/** Deterministic, or every sync is a diff and every drift check is a lie. */
test('the same input renders the same bytes', () => {
  const once = renderTier({ rules: two, knowledge: [], skills: [], version: '0.0.1' });
  const twice = renderTier({ rules: [...two], knowledge: [], skills: [], version: '0.0.1' });

  assert.equal(once, twice);
});

/**
 * 🔴 One rule's body, addressable on its own — this is what keeps drift and `upstream` per rule
 * inside one region. Without it the whole region is one artefact, a person who improves one sentence
 * has drifted all eight rules, and `upstream <file>` has nothing to extract.
 */
test('a single rule can be read back out of a rendered region', () => {
  const region = renderTier({ rules: two, knowledge: [], skills: [], version: '0.0.1' });

  assert.equal(tierRuleBody(region, 'core/core/rules/sensitive-info.md'), '# Sensitive info\n\nKeep it out.');
  assert.equal(tierRuleBody(region, 'core/core/rules/task-lifecycle.md'), '# Task lifecycle\n\nMove it.');
  assert.equal(tierRuleBody(region, 'core/core/rules/never-shipped.md'), null);
});

/**
 * The body read back must be what the canon would render, byte for byte — otherwise the first `check`
 * after a clean `sync` reports drift on a file nobody touched, which is the failure D13 and the
 * body-comparison in `planSync` were both written from.
 */
test('what is rendered is what reads back, for every rule', () => {
  const region = renderTier({ rules: two, knowledge: [], skills: [], version: '0.0.1' });

  for (const { file: canonFile, text } of two) {
    assert.equal(tierRuleBody(region, `${canonFile.pack}/${canonFile.source}`), tierBodyOf(text));
  }
});

/** The body a rule contributes: its text with the frontmatter removed and the edges trimmed. */
function tierBodyOf(text: string): string {
  const end = text.indexOf('\n---\n', 3);
  return text.slice(end + 5).trim();
}
