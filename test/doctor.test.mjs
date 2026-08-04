import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture } from './_fixture.mjs';
import { readCanon } from '../src/canon.mjs';
import { readManifest, readLock } from '../src/config.mjs';
import { planSync, applySync } from '../src/materialize.mjs';
import { findTwins, commandDoctor } from '../src/twins.mjs';

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

const look = (repoFx) =>
  findTwins({ root: repoFx.root, manifest: readManifest(repoFx.root), lock: readLock(repoFx.root) });

test('a local rule that duplicates a canonical one under another name is reported', () => {
  const { canonFx, repoFx } = seeded();
  repoFx.write('.claude/rules/minimise-bash-prompts.md', LOCAL_TWIN);

  const twins = look(repoFx);
  assert.equal(twins.length, 1);
  assert.equal(twins[0].local, 'rules/minimise-bash-prompts.md');
  assert.equal(twins[0].canonical, 'rules/file-tool-discipline.md');
  assert.ok(twins[0].score > 0.5, `score was ${twins[0].score}`);

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

  const out = [];
  assert.equal(commandDoctor({ root: repoFx.root, write: (s) => out.push(s) }), 0);
  const text = out.join('\n');
  assert.match(text, /minimise-bash-prompts/);
  assert.match(text, /file-tool-discipline/);
  assert.match(text, /suspect|advisory/i);

  canonFx.cleanup();
  repoFx.cleanup();
});

test('doctor on a repo with no local documents says so and exits 0', () => {
  const { canonFx, repoFx } = seeded();
  const out = [];
  assert.equal(commandDoctor({ root: repoFx.root, write: (s) => out.push(s) }), 0);
  assert.match(out.join('\n'), /no suspected/i);
  canonFx.cleanup();
  repoFx.cleanup();
});
