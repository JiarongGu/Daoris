import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { Fixture } from './_fixture.ts';
import { makeFixture } from './_fixture.ts';
import { readCanon } from '../src/canon.ts';
import { readManifest, readLock } from '../src/config.ts';
import { planSync, applySync } from '../src/materialize.ts';
import { findTwins, commandDoctor } from '../src/twins.ts';

// Deliberately the real case: the canonical rule and the repo's own rule say
// the same thing under different names.
const CANONICAL = `---
name: file-tool-discipline
applies_when: inspecting files, or running a destructive command
enforces: use the dedicated read and search tools rather than shell equivalents
---

# Use the dedicated file tools

Inspect files with the dedicated read, search, and find tools rather than their
shell equivalents. Reserve the shell for genuine shell work. The dedicated tools
integrate with the approval system and never prompt for a read that was never
risky. Destructive commands deserve care precisely when they stop prompting.
`;

const LOCAL_TWIN = `# Minimise shell prompts — prefer the dedicated file tools

Inspect files with the dedicated read and search tools, not shell equivalents.
Reserve shell for genuine shell work. The dedicated tools integrate with the
approval system and never prompt for a read that was never risky. Destructive
commands deserve care precisely when they stop prompting.
`;

/**
 * The band that actually matters. Measured against real sibling documents, a
 * near-verbatim twin scores ~70% and is easy; genuine twins that were REWRITTEN
 * rather than copied land around 35-45%, while unrelated documents sit at
 * 8-16%. A threshold above that middle band misses the twins most worth finding
 * — the ones nobody recognises as duplicates precisely because the wording
 * diverged.
 */
const LOCAL_PARAPHRASE = `# Inspection belongs in the dedicated tools, not the shell

Reach for the dedicated read and search tools whenever you are inspecting files.
Keep the shell for the work that genuinely needs a shell: builds, suites, version
control. Because those tools are wired into the approval system, a read that
carried no risk does not interrupt anyone. And a destructive command earns the
most caution at the exact moment it stops asking.
`;

/**
 * A generic skill: "find the exemplar to mirror". It names module, service, handler, test,
 * registration and naming — the vocabulary of EVERY architecture document, which is why it matched
 * three unrelated ones on the second real adoption.
 */
const GENERIC_SKILL = `# Find the exemplar to mirror

Name the shape you are adding: a module, a service, a handler, a test, a migration. Search this
codebase for one already of that shape, read it end to end, and extract its registration, its naming,
its error handling and the wiring chain across files. New code should read like the code around it.
`;

const UNRELATED = `# Play queue ordering

The queue preserves insertion order except when shuffle is enabled, in which
case a seeded permutation decides playback sequence. Repeat modes interact with
crossfade windows at track boundaries.
`;

function seeded() {
  const canonFx = makeFixture('doctor-canon');
  canonFx.write('canon.json', '{"version":"0.1.0"}');
  canonFx.write('core/rules/file-tool-discipline.md', CANONICAL);

  const repoFx = makeFixture('doctor-repo');
  repoFx.write('daoris.json', JSON.stringify({ source: 's', packs: [] }));
  const canon = readCanon(canonFx.root);
  const manifest = readManifest(repoFx.root);
  applySync({
    root: repoFx.root,
    manifest,
    canonVersion: canon.version,
    force: false,
    plan: planSync({ root: repoFx.root, manifest, canon, lock: null }),
  });
  return { canonFx, repoFx };
}

const look = (repoFx: Fixture) =>
  findTwins({ root: repoFx.root, manifest: readManifest(repoFx.root), lock: readLock(repoFx.root) });

test('a local rule that duplicates a canonical one under another name is reported', () => {
  const { canonFx, repoFx } = seeded();
  repoFx.write('.claude/rules/minimise-bash-prompts.md', LOCAL_TWIN);

  const twins = look(repoFx);
  assert.equal(twins.length, 1);
  assert.equal(twins[0]!.local, 'rules/minimise-bash-prompts.md');
  assert.equal(twins[0]!.canonical, 'rules/file-tool-discipline.md');
  assert.ok(twins[0]!.score > 0.5, `score was ${twins[0]!.score}`);

  canonFx.cleanup();
  repoFx.cleanup();
});

test('a REWRITTEN twin is reported, not just a near-verbatim one', () => {
  const { canonFx, repoFx } = seeded();
  repoFx.write('.claude/rules/inspection-hygiene.md', LOCAL_PARAPHRASE);

  const twins = look(repoFx);
  assert.equal(twins.length, 1, 'a paraphrased twin is the case worth catching');
  assert.equal(twins[0]!.canonical, 'rules/file-tool-discipline.md');

  canonFx.cleanup();
  repoFx.cleanup();
});

test("a repo's own skill is checked against canonical skills too", () => {
  // The canonical skill has to exist BEFORE the sync, or it is never locked and never a comparison
  // target. This test used to pass by matching a canonical RULE across tiers, which was the noise.
  const canonFx = makeFixture('doctor-skill-canon');
  canonFx.write('canon.json', '{"version":"0.1.0"}');
  canonFx.write('core/skills/inspect/SKILL.md', CANONICAL);

  const repoFx = makeFixture('doctor-skill-repo');
  repoFx.write('daoris.json', JSON.stringify({ source: 's', packs: [] }));
  const canon = readCanon(canonFx.root);
  const manifest = readManifest(repoFx.root);
  applySync({
    root: repoFx.root,
    manifest,
    canonVersion: canon.version,
    force: false,
    plan: planSync({ root: repoFx.root, manifest, canon, lock: null }),
  });

  repoFx.write('.claude/skills/look-around/SKILL.md', LOCAL_TWIN);

  const twins = look(repoFx);

  assert.ok(
    twins.some((t) => t.local === 'skills/look-around/SKILL.md' && t.canonical === 'skills/inspect/SKILL.md'),
    `skills were not compared: ${JSON.stringify(twins)}`,
  );

  canonFx.cleanup();
  repoFx.cleanup();
});

test('an unrelated local document is not reported', () => {
  const { canonFx, repoFx } = seeded();
  repoFx.write('.claude/knowledge/play-queue.md', UNRELATED);
  assert.deepEqual(look(repoFx), []);
  canonFx.cleanup();
  repoFx.cleanup();
});

test('doctor is advisory — it reports suspects and still exits 0', () => {
  const { canonFx, repoFx } = seeded();
  repoFx.write('.claude/rules/minimise-bash-prompts.md', LOCAL_TWIN);

  const out: string[] = [];
  assert.equal(commandDoctor({ root: repoFx.root, write: (s: string) => out.push(s) }), 0);
  const text = out.join('\n');
  assert.match(text, /minimise-bash-prompts/);
  assert.match(text, /file-tool-discipline/);
  assert.match(text, /suspect|advisory/i);

  canonFx.cleanup();
  repoFx.cleanup();
});

test('doctor on a repo with no local documents says so and exits 0', () => {
  const { canonFx, repoFx } = seeded();
  const out: string[] = [];
  assert.equal(commandDoctor({ root: repoFx.root, write: (s: string) => out.push(s) }), 0);
  assert.match(out.join('\n'), /no suspected/i);
  canonFx.cleanup();
  repoFx.cleanup();
});
