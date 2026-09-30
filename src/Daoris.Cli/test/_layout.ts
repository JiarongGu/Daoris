// The fixture the layout suites share (LAYOUT3, D117): a canon with one of each tier, a skill that
// carries a file beside its SKILL.md, and a repository on either descriptor. Each suite passes its
// own tag, because `node --test` runs the files at once and a shared folder would be two suites
// deleting each other's scratch.

import type { Fixture } from './_fixture.ts';
import type { Lock, SyncPlan } from '../src/types.ts';
import { makeFixture } from './_fixture.ts';
import { readCanon } from '../src/canon.ts';
import { readLock, readManifest } from '../src/config.ts';
import { applySync, planSync } from '../src/materialize.ts';
import { runCli } from '../src/cli.ts';

export const doc = (name: string, body = `Body of ${name}.`) =>
  `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n\n${body}\n`;

export const skill = (name: string, body = `Steps of ${name}.`) =>
  `---\nname: ${name}\ndescription: Use when ${name} is wanted.\n---\n\n${body}\n`;

export interface LayoutFixture {
  canonFx: Fixture;
  repoFx: Fixture;
  /** Replaces the manifest's fields, keeping `source`. */
  manifest(fields: Record<string, unknown>): void;
  /** Flips the manifest to a descriptor, and its target with it unless one is given. */
  flip(harness: string, target?: string): void;
  plan(): SyncPlan;
  sync(force?: boolean): Lock;
  lock(): Lock;
  /** The CLI through its dispatcher, pointed at the fixture canon. */
  cli(...argv: string[]): { code: number; out: string };
  cleanup(): void;
}

/**
 * A canon of one rule, one knowledge document and one skill (`finder`, with `run.sh` beside it),
 * and a repository that has adopted nothing yet.
 */
export function layoutFixture(tag: string, harness = 'claude-code'): LayoutFixture {
  const canonFx = makeFixture(`${tag}-canon`);
  canonFx.write('canon.json', '{"version":"0.1.0"}');
  canonFx.write('core/rules/sensitive-info.md', doc('sensitive-info'));
  canonFx.write('core/knowledge/storage.md', doc('storage'));
  canonFx.write('core/skills/finder/SKILL.md', skill('finder'));
  canonFx.write('core/skills/finder/run.sh', '#!/bin/sh\necho finder\n');

  const repoFx = makeFixture(`${tag}-repo`);
  let fields: Record<string, unknown> = harness === 'claude-code'
    ? { source: 's', packs: [] }
    : { source: 's', packs: [], harness, target: '.agents' };
  const writeManifest = () => repoFx.write('daoris.json', `${JSON.stringify(fields, null, 2)}\n`);
  writeManifest();

  const fx: LayoutFixture = {
    canonFx,
    repoFx,
    manifest(next) {
      fields = { source: 's', packs: [], ...next };
      writeManifest();
    },
    flip(to, target) {
      fields = { ...fields, harness: to, target: target ?? (to === 'agents' ? '.agents' : '.claude') };
      writeManifest();
    },
    plan() {
      const canon = readCanon(canonFx.root);
      return planSync({ root: repoFx.root, manifest: readManifest(repoFx.root), canon, lock: readLock(repoFx.root) });
    },
    sync(force = false) {
      const canon = readCanon(canonFx.root);
      const manifest = readManifest(repoFx.root);
      const plan = planSync({ root: repoFx.root, manifest, canon, lock: readLock(repoFx.root) });
      return applySync({ root: repoFx.root, manifest, plan, canonVersion: canon.version, force });
    },
    lock: () => readLock(repoFx.root)!,
    cli(...argv) {
      const previous = process.env.DAORIS_CANON;
      process.env.DAORIS_CANON = canonFx.root;
      const out: string[] = [];
      try {
        const code = runCli(argv, repoFx.root, (line) => out.push(line));
        if (code instanceof Promise) throw new Error('a doctrine command answered asynchronously');
        return { code, out: out.join('\n') };
      } finally {
        if (previous === undefined) delete process.env.DAORIS_CANON;
        else process.env.DAORIS_CANON = previous;
      }
    },
    cleanup() {
      canonFx.cleanup();
      repoFx.cleanup();
    },
  };
  return fx;
}
