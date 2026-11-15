import { test } from 'node:test';
import assert from 'node:assert/strict';
import { renderIndex, renderTier, tierRuleBody } from '../../src/doctrine/tierrender.ts';
import type { CanonFile } from '../../src/doctrine/records.ts';

/**
 * What goes INSIDE the region — the always-loaded tier, rendered once, for the file
 * every harness reads.
 *
 * 1. **Frontmatter is stripped.** Frontmatter is only frontmatter at byte 0; concatenated rules would
 *    put `---` blocks mid-document, where they render as horizontal rules and stray key-value prose.
 * 2. **Provenance stays.** The per-file header is what keeps drift and `upstream` per rule file inside
 *    one region.
 * 3. **The region lists places, then the rules in full.** No table of rules: every session pays for the
 *    region, so it holds where the records are, one line naming `<target>/INDEX.md`, and the rules.
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

test('the region carries the rules in full and no table of them, nor a line sending changes to a skill', () => {
  const region = renderTier({ rules: two, version: '0.0.1' });

  assert.doesNotMatch(region, /\| `sensitive-info` \||## Rules\n|## Read on demand|`change` skill/);
  assert.ok(region.indexOf('## Where things are') < region.indexOf('# Sensitive info'));
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

  assert.match(region, /^- knowledge and skills, each with when it applies: `\.claude\/INDEX\.md`, searched for your kind of change$/m);
  assert.doesNotMatch(region, /before a non-trivial task/);
  assert.doesNotMatch(region, /reaching-in|fix-log/);
  assert.match(index, /^\| `\.claude\/knowledge\/reaching-in\.md` \| you have written into another repository \| stop and report \|$/m);
  // A skill is named by its entry file: an agent whose skill roots hold nothing activates it by reading it.
  assert.match(index, /^\| `\.claude\/skills\/fix-log\/SKILL\.md` \| Record a fix after landing it\. \|$/m);
});

/**
 * The places are a list, a role and its path per line: the roles read by lookup share one line named the library,
 * the gates say what they hold, and the index line is last whether anything is declared or not.
 */
test('the places list groups the library, notes the gates, and ends at the index', () => {
  const documents = [
    { role: 'router', path: 'docs/README.md' },
    { role: 'decisions', path: 'docs/decisions' },
    { role: 'backlog', path: 'TASKS.md' },
    { role: 'archive', path: 'docs/task-archive.md' },
    { role: 'fixes', path: 'docs/FIX-LOG.md' },
    { role: 'gates', path: 'daoris.gates.json' },
  ];
  const region = renderTier({ rules: two, documents, version: '0.0.1' });

  assert.ok(region.includes([
    '## Where things are',
    '',
    'Declared in `daoris.json`; look here before searching.',
    '',
    '- router `docs/README.md`',
    '- backlog `TASKS.md`',
    '- decisions `docs/decisions`, archive `docs/task-archive.md`, fixes `docs/FIX-LOG.md`: the library',
    '- gates `daoris.gates.json`: the checks, and what may run unasked',
    '- knowledge and skills, each with when it applies: `.claude/INDEX.md`, searched for your kind of change',
  ].join('\n')), region);

  const bare = renderTier({ rules: two, version: '0.0.1' });
  assert.ok(bare.includes('## Where things are\n\n- knowledge and skills, each with when it applies:'), bare);
  assert.doesNotMatch(bare, /Declared in/);
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
