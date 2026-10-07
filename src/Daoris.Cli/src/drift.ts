import type { CommandArgs, DriftReport, Lock, Manifest } from './types.ts';
import type { ExitCode } from './errors.ts';
import { existsSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { digestBytes, listMarkdown, readText, sha256 } from './fsx.ts';
import { readLock, readManifest } from './config.ts';
import { indexInput, rosterFromDisk } from './indexgen.ts';
import { findRegion, hasImport, removeRegion } from './region.ts';
import { indexPath, renderIndex, spanBody, WHERE_HEADING, withoutFrontmatter as bare } from './tierrender.ts';
import { declaredPaths, documentLinks, present, RECORD_ROLES, shortDocumentLink, wordCount } from './documents.ts';
import { HARNESSES, DEFAULT_HARNESS, alwaysLoadedTiers } from './harness.ts';
import { folderTiers, INSTRUCTION_LIMIT, isFile, lockLayout, unlistedDocuments } from './layout.ts';
import { canonicalOnDisk, mirrorDigest, mirrorSources } from './mirror.ts';
import { linkProblems, shortLink } from './links.ts';

/**
 * The roster's on-demand half — everything from the pointer to the index to the first rule.
 *
 * @remarks
 * Comparing only this half is what keeps the staleness check offline and canon-free. The rules rows
 * above it are built from frontmatter the span strips, so nothing without the canon can say what they
 * ought to be; the rules themselves are covered per rule by the lock.
 */
function onDemandHalf(text: string): string {
  const start = text.indexOf('\n## Read on demand');
  if (start === -1) return '';
  const end = text.indexOf('\n<!-- daoris: ', start);
  return (end === -1 ? text.slice(start) : text.slice(start, end)).trim();
}

/**
 * The on-demand half split at the *Where things are* table, its last section (D122 §2.8): the tiers'
 * tables are rebuilt from disk, the records' table from the manifest, and a stale one says which.
 */
function splitWhere(half: string): { tiers: string; where: string } {
  const at = half.indexOf(`\n${WHERE_HEADING}\n`);
  return at === -1 ? { tiers: half, where: '' } : { tiers: half.slice(0, at).trim(), where: half.slice(at).trim() };
}

/**
 * Pure local hashing against the lock — no network, no canon, no package
 * resolution. That is what lets `check` sit inside a build gate in a repo that
 * has no node dependencies of its own and may be building offline.
 */
export function inspect(
  { root, manifest, lock }: { root: string; manifest: Manifest; lock: Lock | null },
): DriftReport {
  const drifted: string[] = [];
  const missing: string[] = [];
  // 🔴 Read where the lock says the files are (D117 §5.1). Between a manifest's flip and the sync
  // that moves them, reading at the manifest's root would call every file missing — a lie, and the
  // wrong advice: the fact is the move not made yet, and it is named below as one.
  const manifestHarness = manifest.harnessDescriptor ?? HARNESSES[DEFAULT_HARNESS]!;
  const was = lockLayout(root, lock, { ...manifest, harnessDescriptor: manifestHarness });
  const harness = was.harness;
  const target = was.target;

  // A file holding a span is read once, however many rules live in it.
  const files = new Map<string, string | null>();
  const fileText = (file: string): string | null => {
    if (!files.has(file)) {
      const abs = join(root, file);
      files.set(file, existsSync(abs) ? readText(abs) : null);
    }
    return files.get(file)!;
  };
  const regionBody = (file: string, name: string): string | null => {
    const text = fileText(file);
    const held = text === null ? null : findRegion(text, name);
    return held?.kind === 'present' ? held.body : null;
  };

  for (const entry of lock?.entries ?? []) {
    if (entry.in) {
      // A span inside a file the repository owns (D59). Its identity is still `rules/<name>.md`, and
      // what is hashed is the rule's BODY — which is what keeps drift per rule inside one region.
      const body = spanBody(root, harness, entry, fileText);
      if (body === null) missing.push(entry.target);
      else if (sha256(body) !== entry.sha256) drifted.push(entry.target);
      continue;
    }

    const abs = join(root, target, entry.target);
    if (!existsSync(abs)) missing.push(entry.target);
    else if (sha256(readText(abs)) !== entry.sha256) drifted.push(entry.target);
  }

  const staleLayout = lock && (was.harness.id !== manifestHarness.id || was.target !== manifest.target)
    ? `daoris.json names ${manifestHarness.id} (${manifest.target}/), and the files were written under `
      + `${was.harness.id} (${target}/)`
    : null;

  // The mirror, measured against the lock (D117 §3.2, D13) and against its source as it stands: offline
  // and canon-free, since a mirror is a copy of files already on disk.
  const mirrored = new Map((lock?.mirrors ?? []).map((entry) => [entry.path, entry]));
  const mirrorsDrifted: { path: string; of: string }[] = [];
  const mirrorsMissing: string[] = [];
  const mirrorsBehind: string[] = [];
  const locked = new Set((lock?.entries ?? []).map((entry) => entry.target));
  const sources = mirrorSources({
    root, harness, target,
    canonical: canonicalOnDisk(root, target, lock?.entries ?? []),
    owned: (tierPath) => locked.has(tierPath),
  });
  for (const entry of lock?.mirrors ?? []) {
    const abs = join(root, entry.path);
    if (!isFile(abs)) mirrorsMissing.push(entry.path);
    else if (digestBytes(readFileSync(abs)) !== entry.sha256) mirrorsDrifted.push({ path: entry.path, of: entry.of });
  }
  for (const source of sources) {
    const entry = mirrored.get(source.path);
    if (!entry) mirrorsMissing.push(source.path);
    else if (mirrorDigest(source.content) !== entry.sha256 && !mirrorsDrifted.some((m) => m.path === source.path)
      && !mirrorsMissing.includes(source.path)) {
      mirrorsBehind.push(source.path);
    }
  }
  // A mirror whose skill went is behind too: `sync` retires it.
  for (const entry of lock?.mirrors ?? []) {
    if (!sources.some((source) => source.path === entry.path) && isFile(join(root, entry.path))
      && !mirrorsDrifted.some((m) => m.path === entry.path)) {
      mirrorsBehind.push(entry.path);
    }
  }

  // Rooms (D117 §2.2): each declared one has instructions of its own, and its pointer imports them.
  const roomsWithoutInstructions: string[] = [];
  const roomPointersMissing: string[] = [];
  const pointer = manifestHarness.pointer;
  const instructions = pointer?.imports ?? 'AGENTS.md';
  for (const room of manifest.rooms ?? []) {
    if (!isFile(join(root, room, instructions))) {
      roomsWithoutInstructions.push(`${room}/${instructions}`);
      continue;
    }
    if (!pointer) continue;
    const at = join(root, room, pointer.file);
    if (!isFile(at) || !hasImport(readText(at), pointer.imports)) roomPointersMissing.push(`${room}/${pointer.file}`);
  }

  const links = linkProblems(root, {
    files: [
      ...(lock?.entries ?? []).filter((entry) => !entry.in).map((entry) => `${target}/${entry.target}`),
      ...(lock?.mirrors ?? []).map((entry) => entry.path),
    ],
    folders: [
      target,
      ...folderTiers(harness).map(([, tier]) => `${target}/${tier.dir}`),
      ...(harness.mirror ? [harness.mirror.root] : []),
    ],
    pairs: [
      ...new Set(Object.values(harness.tiers).flatMap((tier) => (tier.region ? [tier.region.file] : []))),
      ...(pointer ? [pointer.file] : []),
      ...(manifest.rooms ?? []).flatMap((room) => [`${room}/${instructions}`, ...(pointer ? [`${room}/${pointer.file}`] : [])]),
      indexPath(target),
    ],
  });

  // Reported, never failed on (D54): the root file against the smallest limit an agent reads, and what
  // the index no longer lists. Not while a move is pending — the move names them, and moves them.
  const agentsFile = Object.values(harness.tiers).find((tier) => tier.region)?.region?.file ?? 'AGENTS.md';
  const agentsBytes = isFile(join(root, agentsFile)) ? statSync(join(root, agentsFile)).size : 0;
  const { unlisted, readAlone } = staleLayout
    ? { unlisted: [], readAlone: [] }
    : unlistedDocuments({ root, harness, target, mirrors: new Set(mirrored.keys()) });

  // Offline staleness: a pack the manifest asks for that the lock has never seen.
  const syncedPacks = new Set((lock?.entries ?? []).map((entry) => entry.pack));
  const stalePacks = manifest.packs.filter((pack) => !syncedPacks.has(pack));

  // The same kind of fact for a switched-off core row (D71): the manifest and the lock disagree about
  // it — confirmed and never synced, or withdrawn and never synced. Named from the lock, so offline.
  const switchedOff = lock?.switchedOff ?? [];
  const confirmed = Object.entries(manifest.switchedOff ?? {});
  const staleSwitches = [
    ...confirmed
      .filter(([target, by]) => !switchedOff.some((row) => row.target === target && row.by === by))
      .map(([target, by]) => `${target} is switched off by '${by}' in the manifest and still on here`),
    ...switchedOff
      .filter((row) => !confirmed.some(([target, by]) => row.target === target && row.by === by))
      .map((row) => `${row.target} was switched off by '${row.by}' here and is no longer confirmed`),
  ];

  // The tier is the LOCATION, so the always-loaded footprint is still measurable — a region has a
  // byte count exactly as a directory did, which is the half of D7 that survives D59. Which tiers
  // those are is the harness's answer, not a constant here.
  let coreBytes = 0;
  for (const name of alwaysLoadedTiers(harness)) {
    const tier = harness.tiers[name]!;
    if (tier.region) {
      const body = regionBody(tier.region.file, tier.region.name);
      coreBytes += body === null ? 0 : Buffer.byteLength(body, 'utf8');
    } else if (tier.dir) {
      const dir = join(root, target, tier.dir);
      coreBytes += listMarkdown(dir).reduce((sum, file) => sum + statSync(join(dir, file)).size, 0);
    }
  }
  const overBudget = coreBytes > manifest.coreBudgetBytes;

  // 🔴 The KNOWLEDGE and SKILLS rows, rebuilt from disk and compared with `<target>/INDEX.md` (WSSETUP14a,
  // D128 §2.4). Offline and canon-free, which is what `check` inside a build gate requires (D8). What
  // this catches is the case that actually happens — a local document added and the index never
  // re-synced. A lock that does not name the index has not had one written yet (D128 §2.4).
  const index = indexPath(target);
  const indexText = isFile(join(root, index)) ? readText(join(root, index)) : null;
  const tiers = indexInput({ root, target, lock, harness });
  const indexStale = lock?.index !== index || indexText !== renderIndex(tiers);
  // Reported, never failed on (D54, D128 §3.2): the repository's own knowledge the index lists by its
  // heading. Its own only, since a canonical document without frontmatter is the canon's defect, and
  // the canon's own tests hold it.
  const withoutFrontmatter = tiers.knowledge.filter((document) => document.local && bare(document)).length;

  // The region's half, two kinds of table compared apart (D122 §2.8): the pointer, the mirror sentence
  // and the rooms rebuilt from the disk, and the records' table from the manifest, so a stale one is
  // named as itself. The rules rows are deliberately not checked: they come from frontmatter the span
  // strips, so nothing offline can rebuild them, and a canon change is `status`'s report.
  const documents = manifest.documents ?? [];
  let rosterStale = false;
  let documentsStale = false;
  for (const name of alwaysLoadedTiers(harness)) {
    const tier = harness.tiers[name]!;
    if (!tier.region) continue;
    const body = regionBody(tier.region.file, tier.region.name);
    if (body === null) {
      rosterStale = true;
      continue;
    }
    const held = splitWhere(onDemandHalf(body));
    const wanted = splitWhere(onDemandHalf(rosterFromDisk({ root, target, harness, rooms: manifest.rooms ?? [], documents })));
    if (held.tiers !== wanted.tiers) rosterStale = true;
    if (held.where !== wanted.where) documentsStale = true;
  }

  // The declared documents (D122 §2.8). Facts: one absent, or a link, or a link held as text, which is
  // never read through. Judgements, reported: a document over its ceiling in words, and the canon's
  // most-named records declared nowhere. A repository that declares nothing hears none of it.
  const documentLinksFound = documentLinks(root, documents);
  const linked = new Set(documentLinksFound.map((link) => link.declared));
  const documentsMissing = declaredPaths(documents).filter((doc) => !linked.has(doc.path) && present(root, doc.path) === null);
  const documentsOver: { label: string; words: number; ceiling: number }[] = [];
  const ceilingsUnmeasured: { role: string; path: string; ceiling: number }[] = [];
  const measure = (label: string, text: string, ceiling: number) => {
    const words = wordCount(text);
    if (words > ceiling) documentsOver.push({ label, words, ceiling });
  };
  for (const doc of documents) {
    if (doc.words === null) continue;
    if (doc.role === 'brief') {
      // The repository's own part of the root instruction file: everything outside Daoris's region.
      const region = Object.values(harness.tiers).find((tier) => tier.region)?.region;
      const text = region ? fileText(region.file) : null;
      if (region && text !== null && !links.some((problem) => problem.path === region.file)) {
        measure(`the brief (${region.file}, outside the region)`, removeRegion(text, region.name) ?? '', doc.words);
      }
    } else if (doc.role === 'room') {
      for (const room of manifest.rooms ?? []) {
        const path = `${room}/${instructions}`;
        if (isFile(join(root, path)) && !links.some((problem) => problem.path === path)) {
          measure(`${path} (room)`, readText(join(root, path)), doc.words);
        }
      }
    } else if (doc.path !== null && !linked.has(doc.path)) {
      const kind = present(root, doc.path);
      if (kind === 'file') measure(`${doc.path} (${doc.role})`, readText(join(root, doc.path)), doc.words);
      else if (kind === 'folder') ceilingsUnmeasured.push({ role: doc.role, path: doc.path, ceiling: doc.words });
    }
  }
  const recordsUndeclared = documents.length
    ? RECORD_ROLES.filter((role) => !documents.some((doc) => doc.role === role && doc.path !== null))
    : [];

  // The budget REPORTS; it does not fail (D54, the owner's call). Everything else here is a FACT the
  // tool established — a file drifted, one is missing, a pack was never synced, the index is behind.
  // Size is a JUDGEMENT: one byte over a number somebody chose is not wrong, and a gate that stops a
  // build over a judgement gets its number raised rather than read — which is the failure D28
  // predicted in its own words about noise. The number stays and is stated on every run, because a
  // signal nobody can see is not a signal.
  const ok = !drifted.length && !missing.length && !stalePacks.length && !indexStale && !rosterStale
    && !staleSwitches.length
    && !staleLayout && !mirrorsDrifted.length && !mirrorsMissing.length && !mirrorsBehind.length
    && !roomsWithoutInstructions.length && !roomPointersMissing.length && !links.length
    && !documentsStale && !documentsMissing.length && !documentLinksFound.length;
  return {
    drifted, missing, stalePacks, coreBytes, overBudget, index, indexStale, rosterStale, switchedOff, staleSwitches,
    staleLayout, mirrorsDrifted, mirrorsMissing, mirrorsBehind, roomsWithoutInstructions, roomPointersMissing,
    links, agentsBytes, unlisted, readAlone, withoutFrontmatter,
    documentsStale, documentsMissing, documentLinks: documentLinksFound, documentsOver, ceilingsUnmeasured, recordsUndeclared,
    ok,
  };
}

export function commandCheck({ root, write }: Pick<CommandArgs, 'root' | 'write'>): ExitCode {
  const manifest = readManifest(root);
  const report = inspect({ root, manifest, lock: readLock(root) });

  for (const target of report.drifted) write(`  drifted   ${target}`);
  for (const target of report.missing) write(`  missing   ${target}`);
  for (const pack of report.stalePacks) {
    write(`  stale     pack '${pack}' is in the manifest but not the lock`);
  }
  for (const stale of report.staleSwitches) write(`  stale     ${stale} — run 'daoris sync'`);
  // Every run, whatever else it finds (D71): a core row that is off is never a surprise, and it is not
  // a failure either — it is a decision the manifest records.
  for (const row of report.switchedOff) write(`  off       ${row.target} (switched off by pack '${row.by}')`);
  if (report.overBudget) {
    write(
      `  budget    ${manifest.target} always-loaded is ${report.coreBytes} of ` +
        `${manifest.coreBudgetBytes} declared bytes — advisory, not a gate: split principle from ` +
        'detail into the on-demand tier, or raise it deliberately in daoris.json',
    );
  }
  if (report.indexStale) {
    write(`  index     ${report.index} is ${isFile(join(root, report.index)) ? 'out of date' : 'absent'} — run 'daoris sync'`);
  }
  if (report.rosterStale) {
    write("  roster    the doctrine region's pointer or rooms are out of date — run 'daoris sync'");
  }
  // The layout's facts (D117 §5.2): each fails, and each says which file to open.
  if (report.staleLayout) write(`  stale     layout: ${report.staleLayout} — run 'daoris sync' to move them`);
  for (const mirror of report.mirrorsDrifted) {
    write(`  drifted   ${mirror.path} — a mirror of ${mirror.of}, edited here`);
  }
  for (const path of report.mirrorsMissing) write(`  missing   ${path} — a mirror; run 'daoris sync'`);
  for (const path of report.mirrorsBehind) write(`  behind    ${path} — its source moved on; run 'daoris sync'`);
  for (const path of report.roomsWithoutInstructions) {
    write(`  room      ${path} is absent — the room is declared in daoris.json, and its instructions are this repo's to write`);
  }
  for (const path of report.roomPointersMissing) write(`  pointer   ${path} does not import AGENTS.md — run 'daoris sync'`);
  for (const problem of report.links) write(`  LINK      ${problem.path} — ${shortLink(problem)}`);
  // Reported, never failed on (D54).
  if (report.agentsBytes > INSTRUCTION_LIMIT) {
    write(`  size      AGENTS.md is ${report.agentsBytes} bytes; codex reads ${INSTRUCTION_LIMIT} of a repository's `
      + 'instruction files and cuts the rest, which is the region\'s last rules — advisory, not a gate');
  }
  // The declared documents (D122 §2.8): the facts first, each failing, then the judgements, never failing.
  for (const doc of report.documentsMissing) write(`  document  ${doc.path} (${doc.role}) is declared in daoris.json, and absent`);
  for (const link of report.documentLinks) write(`  LINK      ${link.declared} (${link.role}) — ${shortDocumentLink(link)}`);
  if (report.documentsStale) {
    write("  where     the region's Where things are table differs from daoris.json's documents — run 'daoris sync'");
  }
  for (const over of report.documentsOver) {
    write(`  words     ${over.label} is ${over.words} words of ${over.ceiling} — over by ${over.words - over.ceiling}; `
      + 'relocate, condense, then raise — advisory, not a gate');
  }
  for (const doc of report.ceilingsUnmeasured) {
    write(`  ceiling   ${doc.path} (${doc.role}) is a folder; its ceiling of ${doc.ceiling} words measures nothing — advisory`);
  }
  if (report.recordsUndeclared.length) {
    write(`  records   ${report.recordsUndeclared.map((role) => `no ${role}`).join(' and ')} declared in daoris.json's `
      + "documents — the canon's records have nowhere to point here (advisory)");
  }
  if (report.withoutFrontmatter) {
    const one = report.withoutFrontmatter === 1;
    write(`  frontmatter  ${report.withoutFrontmatter} knowledge ${one ? 'document has' : 'documents have'} none; `
      + `the index lists ${one ? 'it by its' : 'them by their'} first heading — advisory`);
  }
  for (const doc of report.unlisted) write(`  unlisted  ${doc.path} — no index lists it; ${doc.move}`);
  for (const path of report.readAlone) {
    write(`  alone     ${path} — this repo's own skill, read by ${manifest.harnessDescriptor.mirror?.reader ?? 'one agent'} alone`);
  }

  if (report.ok) {
    // "clean" would be the wrong word while the core is over its own declared budget, even though
    // nothing here failed — so the over case says what is true instead of reusing the happy sentence.
    write(report.overBudget
      ? `daoris: no drift — always-loaded core is ${report.coreBytes} of `
        + `${manifest.coreBudgetBytes} declared bytes, over by ${report.coreBytes - manifest.coreBudgetBytes}`
      : `daoris: clean — ${report.coreBytes} of ${manifest.coreBudgetBytes} bytes of always-loaded core`);
    return 0;
  }
  write("daoris: run 'daoris sync' to reconcile, or 'daoris upstream <file>' to keep a local edit");
  return 1;
}
