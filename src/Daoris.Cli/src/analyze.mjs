import { existsSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { listFiles, listMarkdown, readText, sha256 } from './fsx.mjs';
import { withHeader, makeHeader } from './document.mjs';
import { readCanon, resolveCanonRoot, selectFiles } from './canon.mjs';
import { lockIndex, readLock, readManifest } from './config.mjs';
import { significantTokens, containment } from './twins.mjs';

const DEFAULT_TARGET = '.claude';
const TIERS = ['rules', 'knowledge', 'skills'];

/**
 * What a repository already has, before daoris touches it.
 *
 * Deliberately independent of the lock and the manifest: this runs on a repository that has never
 * adopted, which is the whole point.
 */
function survey(root, target) {
  const found = { rules: [], knowledge: [], skills: [] };
  for (const tier of ['rules', 'knowledge']) {
    for (const file of listMarkdown(join(root, target, tier))) {
      if (file === 'RULES_INDEX.md') continue;
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
const PACK_EVIDENCE = {
  'dotnet-library': {
    look: (root) => [
      ...listFiles(root, (n) => n.endsWith('.csproj')).slice(0, 3),
      ...listFiles(root, (n) => n === 'Directory.Build.props').slice(0, 1),
    ],
    why: 'ships .NET projects',
  },
  'storage-sql': {
    look: (root) => [
      ...listFiles(root, (n) => n.endsWith('.sql')).slice(0, 3),
      ...listFiles(root, (n) => /migration/i.test(n) && n.endsWith('.cs')).slice(0, 2),
    ],
    why: 'has SQL or migrations',
  },
  'windows-machine': {
    look: (root) => listFiles(root, (n) => n.endsWith('.ps1')).slice(0, 3),
    why: 'has PowerShell scripts',
  },
};

function suggestPacks(root, canon) {
  const suggestions = [];
  for (const [name, probe] of Object.entries(PACK_EVIDENCE)) {
    if (!canon.packs.has(name)) continue;
    let evidence;
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
function findCollisions(root, target, canon, packs, canonVersion, locked) {
  const collisions = [];
  const updates = [];
  for (const file of selectFiles(canon, packs)) {
    const abs = join(root, target, file.target);
    if (!existsSync(abs)) continue;

    const body = readText(join(canon.root, file.source));
    const content = file.target.endsWith('.md')
      ? withHeader(makeHeader(file.pack, file.source, canonVersion), body)
      : body;
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
function findTwinsAgainstCanon(root, target, canon, packs, threshold = 0.3) {
  const canonical = selectFiles(canon, packs)
    .filter((file) => file.target.endsWith('.md'))
    .map((file) => ({
      tier: file.target.split('/')[0],
      target: file.target,
      tokens: significantTokens(readText(join(canon.root, file.source))),
    }));

  const twins = [];
  for (const tier of TIERS) {
    const dir = join(root, target, tier);
    for (const file of listMarkdown(dir)) {
      if (file === 'RULES_INDEX.md') continue;
      const local = `${tier}/${file}`;
      // A file at a canonical path is a collision, which is reported separately and more precisely.
      if (canonical.some((c) => c.target === local)) continue;

      const tokens = significantTokens(readText(join(dir, file)));
      let best = null;
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
function projectBudget(root, target, canon, packs, existing, collisions) {
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

export function analyze({ root, canon, packs, target, budgetLimit, lock = null }) {
  const existing = survey(root, target);
  const locked = lockIndex(lock);
  const { collisions, updates } = findCollisions(root, target, canon, packs, canon.version, locked);
  return {
    target,
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
export function commandAnalyze({ root, argv, write, packageRoot }) {
  const canon = readCanon(resolveCanonRoot(packageRoot));

  // Works with or without a manifest: before adoption there is none, which is the point.
  let manifest = null;
  try {
    manifest = readManifest(root);
  } catch {
    manifest = null;
  }

  const target = manifest?.target ?? DEFAULT_TARGET;
  const budgetLimit = manifest?.coreBudgetBytes ?? 24000;
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
  const verdict = projected > limit ? `OVER by ${projected - limit}` : `${limit - projected} to spare`;
  write(`  always-loaded   ${current} bytes now -> ~${projected} after (limit ${limit}; ${verdict})`);
  write('');
  write(packs.length ? `  then: daoris init && daoris sync` : `  then: daoris init  (choose packs first)`);
  return 0;
}
