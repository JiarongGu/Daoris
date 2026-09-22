import type {
  AnalysisReport, Canon, CommandArgs, Lock, LockEntry, PackSuggestion, Survey, Twin,
} from './types.ts';
import type { ExitCode } from './errors.ts';
import { existsSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { listFiles, listMarkdown, readText, sha256 } from './fsx.ts';
import { parseFrontmatter, renderCanonFile } from './document.ts';
import { findRegion } from './region.ts';
import { tierRuleBody } from './tierrender.ts';
import { readCanon, resolveCanonRoot, selectFiles } from './canon.ts';
import { DEFAULT_CORE_BUDGET_BYTES, lockIndex, readLock, readManifest } from './config.ts';
import { significantTokens, containment } from './twins.ts';
import {
  DEFAULT_HARNESS, HARNESSES, harnessVerdict, tierNames, verifyHarnessContract,
} from './harness.ts';

// Pre-adoption there is no manifest to resolve a harness from, so this command reads the DEFAULT
// harness's descriptor — one definition, not a constant quietly asserting one harness's conventions
// as universal (the exact scatter the descriptor seam retired).
const CLAUDE = HARNESSES[DEFAULT_HARNESS]!;
const DEFAULT_TARGET = CLAUDE.defaultTarget;
const TIERS = tierNames(CLAUDE);
// A repository that adopted before D59 has a generated roster in its rules directory. `analyze` runs
// on repositories that never adopted AND on ones that did, so it still skips that name rather than
// counting a generated file as doctrine somebody wrote.
const INDEX_FILE = 'RULES_INDEX.md';

/**
 * What a repository already has, before daoris touches it.
 *
 * Deliberately independent of the lock and the manifest: this runs on a repository that has never
 * adopted, which is the whole point.
 */
function survey(root: string, target: string): Survey {
  const found: Survey = { rules: [], knowledge: [], skills: [] };
  for (const tier of ['rules', 'knowledge'] as const) {
    for (const file of listMarkdown(join(root, target, tier))) {
      if (file === INDEX_FILE) continue;
      found[tier].push({ target: `${tier}/${file}`, bytes: statSync(join(root, target, tier, file)).size });
    }
  }
  for (const file of listFiles(join(root, target, 'skills'))) {
    if (!file.endsWith('/SKILL.md')) continue;
    found.skills.push({
      target: `skills/${file}`,
      bytes: statSync(join(root, target, 'skills', file)).size,
    });
  }
  return found;
}

/**
 * A crude first look for pack evidence — a hint, not the analysis.
 *
 * Keyword matching over filenames is exactly the kind of judgement an agent reading the repository
 * does better, and this command is meant to be run BY one: it supplies the facts that must be exact
 * (what collides, what duplicates, what it costs) and leaves the judgement to the reader. The split
 * is deliberate — an agent guessing at collisions would be wrong in a way that destroys files, and a
 * regex guessing at "is this a .NET library" is wrong in a way that costs a sentence.
 *
 * `init` refuses to guess packs at all, because a wrong guess installs the wrong always-loaded core
 * and every session pays for it. That reasoning still holds: this names what it saw and where, so a
 * reader can disagree with it.
 */
const PACK_EVIDENCE: Record<string, { look: (root: string) => string[]; why: string }> = {
  'dotnet-library': {
    look: (root: string) => [
      ...listFiles(root, (n) => n.endsWith('.csproj')).slice(0, 3),
      ...listFiles(root, (n) => n === 'Directory.Build.props').slice(0, 1),
    ],
    why: 'ships .NET projects',
  },
  'storage-sql': {
    look: (root: string) => [
      ...listFiles(root, (n) => n.endsWith('.sql')).slice(0, 3),
      ...listFiles(root, (n) => /migration/i.test(n) && n.endsWith('.cs')).slice(0, 2),
    ],
    why: 'has SQL or migrations',
  },
  'windows-machine': {
    look: (root: string) => listFiles(root, (n) => n.endsWith('.ps1')).slice(0, 3),
    why: 'has PowerShell scripts',
  },
};

function suggestPacks(root: string, canon: Canon): PackSuggestion[] {
  const suggestions: PackSuggestion[] = [];
  for (const [name, probe] of Object.entries(PACK_EVIDENCE)) {
    if (!canon.packs.has(name)) continue;
    let evidence: string[];
    try {
      // A huge tree makes this slow and a permission error makes it throw; neither should stop a
      // report whose other half is already useful.
      evidence = probe.look(root);
    } catch {
      evidence = [];
    }
    if (evidence.length) {
      suggestions.push({ name, why: probe.why, evidence: evidence.slice(0, 3) });
    }
  }
  return suggestions;
}

/**
 * What the canon would land on top of.
 *
 * A collision is a file the repository wrote itself at a path daoris would claim. Identical content
 * is not a collision — it has already adopted that text, whatever the reason.
 */
function findCollisions(
  root: string, target: string, canon: Canon, packs: readonly string[],
  canonVersion: string, locked: Map<string, LockEntry>,
): { collisions: string[]; updates: string[] } {
  const collisions: string[] = [];
  const updates: string[] = [];

  const regionTier = Object.entries(CLAUDE.tiers)
    .find(([, tier]) => tier.region)?.[1]?.region ?? null;
  let regionRead: string | null | undefined;
  const regionBody = (): string | null => {
    if (regionRead === undefined) {
      const abs = join(root, regionTier!.file);
      const held = existsSync(abs) ? findRegion(readText(abs), regionTier!.name) : null;
      regionRead = held && held.kind === 'present' ? held.body : null;
    }
    return regionRead;
  };
  for (const file of selectFiles(canon, packs)) {
    const body = readText(join(canon.root, file.source));

    // The always-loaded tier is a span (D59), so "what is already here" is a question about the
    // region. A repository that has no region yet simply has nothing there — which is the ordinary
    // pre-adoption case this command exists for.
    if (regionTier && file.target.startsWith(`${regionTier.name}/`)) {
      const held = regionBody();
      if (held === null) continue;
      const already = tierRuleBody(held, `${file.pack}/${file.source}`);
      if (already === null) continue;
      const { meta, body: prose } = parseFrontmatter(body, []);
      if (already === (meta ? prose : body).trim()) continue;
      (locked.has(file.target) ? updates : collisions).push(file.target);
      continue;
    }

    const abs = join(root, target, file.target);
    if (!existsSync(abs)) continue;

    const content = renderCanonFile(file, body, canonVersion);
    if (sha256(readText(abs)) === sha256(content)) continue;

    // Provenance decides which of the two this is (D12). In the lock, daoris wrote it, so a
    // difference is an UPDATE to install. Absent from the lock, the repository wrote it first, and
    // overwriting would destroy work the tool never had a claim to.
    (locked.has(file.target) ? updates : collisions).push(file.target);
  }
  return { collisions, updates };
}

/**
 * Documents that restate a canonical one under a different name.
 *
 * `doctor` answers this too, but only for a repository that has already synced — it compares against
 * the LOCK. Before adoption there is no lock, which is exactly when the answer is most useful: a twin
 * found now is a decision made deliberately, and one found afterwards is a duplicate already living
 * in the tree. Compared within a tier, for the reason recorded in D17.
 */
function findTwinsAgainstCanon(
  root: string, target: string, canon: Canon, packs: readonly string[], threshold = 0.3,
): Twin[] {
  const canonical = selectFiles(canon, packs)
    .filter((file) => file.target.endsWith('.md'))
    .map((file) => ({
      tier: file.target.split('/')[0],
      target: file.target,
      tokens: significantTokens(readText(join(canon.root, file.source))),
    }));

  const twins: Twin[] = [];
  for (const tier of TIERS) {
    const dir = join(root, target, tier);
    for (const file of listMarkdown(dir)) {
      if (file === INDEX_FILE) continue;
      const local = `${tier}/${file}`;
      // A file at a canonical path is a collision, which is reported separately and more precisely.
      if (canonical.some((c) => c.target === local)) continue;

      const tokens = significantTokens(readText(join(dir, file)));
      let best: Twin | null = null;
      for (const known of canonical) {
        if (known.tier !== tier) continue;
        const score = containment(tokens, known.tokens);
        if (score >= threshold && (!best || score > best.score)) {
          best = { local, canonical: known.target, score };
        }
      }
      if (best) twins.push(best);
    }
  }
  return twins.sort((a, b) => b.score - a.score);
}

/** Bytes of always-loaded context after adopting — the number that is paid every session. */
function projectBudget(
  root: string, target: string, canon: Canon, packs: readonly string[],
  existing: Survey, collisions: readonly string[],
): { current: number; projected: number } {
  const current = existing.rules.reduce((sum, f) => sum + f.bytes, 0);
  const collided = new Set(collisions);

  let added = 0;
  for (const file of selectFiles(canon, packs)) {
    if (!file.target.startsWith('rules/')) continue;
    const abs = join(root, target, file.target);
    // A collision replaces rather than adds; an existing identical file changes nothing.
    if (existsSync(abs) && !collided.has(file.target)) continue;
    added += Buffer.byteLength(readText(join(canon.root, file.source)), 'utf8');
    if (existsSync(abs)) added -= statSync(abs).size;
  }

  return { current, projected: current + added };
}

export function analyze(
  { root, canon, packs, target, budgetLimit, lock = null }:
  { root: string; canon: Canon; packs: readonly string[]; target: string; budgetLimit: number; lock?: Lock | null },
): AnalysisReport {
  const existing = survey(root, target);
  const locked = lockIndex(lock);
  const { collisions, updates } = findCollisions(root, target, canon, packs, canon.version, locked);
  return {
    target,
    harness: harnessVerdict(root),
    contract: verifyHarnessContract(root, target),
    existing,
    suggested: suggestPacks(root, canon),
    collisions,
    updates,
    twins: findTwinsAgainstCanon(root, target, canon, packs),
    budget: { ...projectBudget(root, target, canon, packs, existing, collisions), limit: budgetLimit },
  };
}

/**
 * Report only. Nothing is written and the exit code is always 0 — this is the command someone runs to
 * decide whether to adopt, and a decision aid that can fail a build is a decision aid nobody runs.
 */
export function commandAnalyze({ root, argv, write, packageRoot }: CommandArgs): ExitCode {
  const canon = readCanon(resolveCanonRoot(packageRoot));

  // Works with or without a manifest: before adoption there is none, which is the point.
  let manifest = null;
  try {
    manifest = readManifest(root);
  } catch {
    manifest = null;
  }

  const target = manifest?.target ?? DEFAULT_TARGET;
  const budgetLimit = manifest?.coreBudgetBytes ?? DEFAULT_CORE_BUDGET_BYTES;
  const requested = argv.filter((arg) => !arg.startsWith('--'));
  const packs = requested.length ? requested : (manifest?.packs ?? []);

  const report = analyze({ root, canon, packs, target, budgetLimit, lock: readLock(root) });

  // For the agent driving an adoption: the exact facts, in a shape it can act on rather than parse
  // out of prose.
  if (argv.includes('--json')) {
    write(JSON.stringify({ canonVersion: canon.version, packs, ...report }, null, 2));
    return 0;
  }

  const totalExisting =
    report.existing.rules.length + report.existing.knowledge.length + report.existing.skills.length;

  write(`daoris: analysing '${root}' against canon ${canon.version}`);
  write('');

  // Which harness this repository is for, before anything about what would be installed. Daoris
  // targets one, and a tree installed for a different one loads nothing — silently, with every file
  // present and correct.
  const { supported, others } = report.harness;
  if (supported.length) {
    write(`  harness         ${supported.map((h) => h.name).join(', ')} — supported`);
  } else {
    write('  harness         none detected — daoris installs the Claude Code layout');
  }
  if (others.length) {
    write(`  ALSO SEEN       ${others.map((h) => `${h.name} (${h.evidence.join(', ')})`).join('; ')}`);
    // 🔴 D59 reversed half of this. The always-loaded tier is a region in `AGENTS.md`, so a
    // repository already on that convention is not blind to what daoris writes — that file is
    // exactly where the largest tier lands. Only the on-demand tiers stay out of its reach, and
    // saying so is the difference between a warning and a wrong one.
    const region = CLAUDE.tiers.rules?.region?.file;
    if (region && others.some((h) => h.evidence.includes(region))) {
      write('                  daoris does not generate those layouts — but the always-loaded');
      write(`                  tier lands IN ${region} (D59), so that tier is already shared.`);
      write(`                  Only knowledge/ and skills/ stay unread, under ${target}/.`);
    } else {
      write('                  daoris does not generate those layouts. What it installs will be');
      write('                  invisible to them — present, correct, and never loaded.');
    }
  }
  if (report.contract.length) {
    write('');
    write('  contract problems — these fail SILENTLY, so they are worth fixing first:');
    for (const problem of report.contract) write(`    ${problem}`);
  }
  write('');
  write(`  already here    ${report.existing.rules.length} rule(s), ` +
        `${report.existing.knowledge.length} knowledge, ${report.existing.skills.length} skill(s)`);
  if (totalExisting === 0) write('                  (nothing yet — this is a fresh adoption)');

  if (report.suggested.length) {
    write('');
    write('  packs worth considering — evidence, not a recommendation:');
    for (const pack of report.suggested) {
      const chosen = packs.includes(pack.name) ? ' [selected]' : '';
      write(`    ${pack.name}${chosen} — ${pack.why}`);
      for (const file of pack.evidence) write(`        ${file}`);
    }
  }

  if (report.updates.length) {
    write('');
    write(`  ${report.updates.length} file(s) daoris already owns would be updated — no conflict.`);
  }

  if (report.collisions.length) {
    write('');
    write(`  ${report.collisions.length} collision(s) — this repo already owns these paths:`);
    for (const target of report.collisions) write(`    ${target}`);
    write('    sync refuses until each is moved aside or accepted with --force');
  }

  if (report.twins.length) {
    write('');
    write('  possible duplicates under another name — worth reading before adopting:');
    for (const twin of report.twins) {
      write(`    ${twin.local}`);
      write(`      looks like ${twin.canonical} (${Math.round(twin.score * 100)}% shared vocabulary)`);
    }
  }

  write('');
  const { current, projected, limit } = report.budget;
  // "OVER" used to imply the first `check` would fail; since D54 it does not, and a projection that
  // over-promises a failure is the same misleading sentence in the other direction.
  const verdict = projected > limit
    ? `OVER by ${projected - limit} — advisory, not a gate`
    : `${limit - projected} to spare`;
  write(`  always-loaded   ${current} bytes now -> ~${projected} after (limit ${limit}; ${verdict})`);
  write('');
  write(packs.length ? `  then: daoris init && daoris sync` : `  then: daoris init  (choose packs first)`);
  return 0;
}
