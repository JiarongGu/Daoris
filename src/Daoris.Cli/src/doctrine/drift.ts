import type { CommandArgs } from '../platform/types.ts';
import type { DriftReport, Lock, Manifest } from './records.ts';
import type { ExitCode } from '../platform/errors.ts';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { compareOrdinal, digestBytes, listMarkdown, readText, sha256 } from '../platform/fsx.ts';
import { readLock, readManifest } from './config.ts';
import { indexInput, rosterFromDisk } from './indexgen.ts';
import { findRegion, removeRegion } from './region.ts';
import { DECLARED_LINE, INDEX_ITEM, indexPath, renderIndex, spanBody, WHERE_HEADING, withoutFrontmatter as bare } from './tierrender.ts';
import { overCeilings } from './ceilings.ts';
import { declaredPaths, documentLinks, present, RECORD_ROLES, shortDocumentLink, wordCount } from '../layout/documents.ts';
import { HARNESSES, DEFAULT_HARNESS, alwaysLoadedTiers } from '../layout/harness.ts';
import { folderTiers, INSTRUCTION_LIMIT, isFile, lockLayout, unlistedDocuments } from '../layout/layout.ts';
import { canonicalOnDisk, mirrorDigest, mirrorSources } from '../layout/mirror.ts';
import { linkProblems, shortLink } from '../layout/links.ts';
import { planPointers, pointerPaths } from '../layout/pointers.ts';
import { planRooms, ROOM_FILE } from '../layout/rooms.ts';

/**
 * The roster's half `check` rebuilds: everything from the places heading to the first rule.
 *
 * @remarks
 * Comparing only this half is what keeps the staleness check offline and canon-free: the rules
 * themselves are covered per rule file by the lock, and the switched-off line above the heading needs the
 * lock's knowledge of who switched a row off.
 */
function rosterHalf(text: string): string {
  const start = text.indexOf(`\n${WHERE_HEADING}\n`);
  if (start === -1) return '';
  const end = text.indexOf('\n<!-- daoris: ', start);
  return (end === -1 ? text.slice(start) : text.slice(start, end)).trim();
}

/**
 * The region's largest rules by bytes, each measured from its provenance line to the next one.
 * The name is the rule's file name, which is what the canon and the lock call it.
 */
function largestRules(body: string, count: number): { name: string; bytes: number }[] {
  const lines = body.split('\n');
  const starts = lines.flatMap((line, at) => (line.startsWith('<!-- daoris: ') ? [at] : []));
  return starts
    .map((start, i) => {
      const source = /^<!-- daoris: (\S+) @/.exec(lines[start]!)?.[1] ?? 'rule';
      const name = source.slice(source.lastIndexOf('/') + 1).replace(/\.md$/, '');
      const text = lines.slice(start, starts[i + 1] ?? lines.length).join('\n');
      return { name, bytes: Buffer.byteLength(text, 'utf8') };
    })
    .sort((a, b) => b.bytes - a.bytes || compareOrdinal(a.name, b.name))
    .slice(0, count);
}

/**
 * The roster half split into the declared places, rebuilt from the manifest, and the rest (the index
 * line, the mirror sentence, the rooms), rebuilt from disk, so a stale one says which.
 */
function splitWhere(half: string): { tiers: string; where: string } {
  const lines = half.split('\n');
  const declared = (line: string) => line === DECLARED_LINE || (line.startsWith('- ') && !line.startsWith(INDEX_ITEM));
  return { tiers: lines.filter((line) => !declared(line)).join('\n'), where: lines.filter(declared).join('\n') };
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
  // 🔴 Read where the lock says the files are. Between a manifest's flip and the sync
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

  // A file the manifest keeps is this repository's own text: its difference from the lock is a decision recorded,
  // never drift, and it is named on every run as a switched-off row is.
  const keeps = manifest.kept ?? {};
  const kept = Object.entries(keeps).sort(([a], [b]) => compareOrdinal(a, b)).map(([target, keep]) => ({ target, reason: keep.reason }));

  for (const entry of lock?.entries ?? []) {
    if (entry.in) {
      // A span inside a file the repository owns. Its identity is still `rules/<name>.md`, and
      // what is hashed is the rule's BODY — which is what keeps drift per rule inside one region.
      const body = spanBody(root, harness, entry, fileText);
      if (body === null) missing.push(entry.target);
      else if (sha256(body) !== entry.sha256 && !keeps[entry.target]) drifted.push(entry.target);
      continue;
    }

    const abs = join(root, target, entry.target);
    if (!existsSync(abs)) missing.push(entry.target);
    else if (sha256(readText(abs)) !== entry.sha256 && !keeps[entry.target]) drifted.push(entry.target);
  }

  const staleLayout = lock && (was.harness.id !== manifestHarness.id || was.target !== manifest.target)
    ? `daoris.json names ${manifestHarness.id} (${manifest.target}/), and the files were written under `
      + `${was.harness.id} (${target}/)`
    : null;

  // The mirror, measured against the lock and against its source as it stands: offline
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

  // Rooms: each declared one has instructions of its own. A pointer an earlier sync wrote is one file too many.
  const roomsWithoutInstructions = planRooms({ root, manifest }).missing;
  const pointersRetired = planPointers({ root, manifest }).retire;

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
      ...pointerPaths(manifest),
      ...(manifest.rooms ?? []).map((room) => `${room}/${ROOM_FILE}`),
      indexPath(target),
    ],
  });

  // Reported, never failed on: the root file against the smallest limit an agent reads, and what
  // the index no longer lists. Not while a move is pending — the move names them, and moves them.
  const agentsFile = Object.values(harness.tiers).find((tier) => tier.region)?.region?.file ?? 'AGENTS.md';
  const agentsBytes = isFile(join(root, agentsFile)) ? statSync(join(root, agentsFile)).size : 0;
  // Only worked out over the limit: the own part is the file without the region, the rules are the
  // region's provenance-marked sections.
  let agentsOwnBytes = agentsBytes;
  let agentsRegionBytes = 0;
  let agentsLargestRules: { name: string; bytes: number }[] = [];
  const agentsRegion = Object.values(harness.tiers).find((tier) => tier.region)?.region;
  if (agentsBytes > INSTRUCTION_LIMIT && agentsRegion) {
    const text = fileText(agentsRegion.file);
    const held = text === null ? null : findRegion(text, agentsRegion.name);
    if (text !== null && held?.kind === 'present') {
      agentsOwnBytes = Buffer.byteLength(removeRegion(text, agentsRegion.name) ?? '', 'utf8');
      agentsRegionBytes = agentsBytes - agentsOwnBytes;
      agentsLargestRules = largestRules(held.body, 3);
    }
  }
  const { unlisted, readAlone } = staleLayout
    ? { unlisted: [], readAlone: [] }
    : unlistedDocuments({ root, harness, target, mirrors: new Set(mirrored.keys()) });

  // Offline staleness: a pack the manifest asks for that the lock has never seen.
  const syncedPacks = new Set((lock?.entries ?? []).map((entry) => entry.pack));
  const stalePacks = manifest.packs.filter((pack) => !syncedPacks.has(pack));

  // The same kind of fact for a switched-off core row: the manifest and the lock disagree about
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
  // word count exactly as a directory has. Which tiers those are is the harness's answer, not a constant here.
  let coreWords = 0;
  for (const name of alwaysLoadedTiers(harness)) {
    const tier = harness.tiers[name]!;
    if (tier.region) {
      const body = regionBody(tier.region.file, tier.region.name);
      coreWords += body === null ? 0 : wordCount(body);
    } else if (tier.dir) {
      const dir = join(root, target, tier.dir);
      coreWords += listMarkdown(dir).reduce((sum, file) => sum + wordCount(readText(join(dir, file))), 0);
    }
  }
  const overBudget = coreWords > manifest.coreBudget.words;

  // 🔴 The KNOWLEDGE and SKILLS rows, rebuilt from disk and compared with `<target>/INDEX.md`.
  // Offline and canon-free, which is what `check` inside a build gate requires. What
  // this catches is the case that actually happens — a local document added and the index never
  // re-synced. A lock that does not name the index has not had one written yet.
  const index = indexPath(target);
  const indexText = isFile(join(root, index)) ? readText(join(root, index)) : null;
  const tiers = indexInput({ root, target, lock, harness });
  const indexStale = lock?.index !== index || indexText !== renderIndex(tiers);
  // Reported, never failed on: the repository's own knowledge the index lists by its
  // heading. Its own only, since a canonical document without frontmatter is the canon's defect, and
  // the canon's own tests hold it.
  const withoutFrontmatter = tiers.knowledge.filter((document) => document.local && bare(document)).length;

  // The region's half, two kinds of table compared apart: the pointer, the mirror sentence
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
    const held = splitWhere(rosterHalf(body));
    const wanted = splitWhere(rosterHalf(rosterFromDisk({ root, target, harness, rooms: manifest.rooms ?? [], documents })));
    if (held.tiers !== wanted.tiers) rosterStale = true;
    if (held.where !== wanted.where) documentsStale = true;
  }

  // The declared documents. Facts: one absent, or a link, or a link held as text, which is
  // never read through. Judgements, reported unless declared failing: a document over a ceiling, and the
  // canon's most-named records declared nowhere. A repository that declares nothing hears none of it.
  const documentLinksFound = documentLinks(root, documents);
  const linked = new Set(documentLinksFound.map((link) => link.declared));
  const documentsMissing = declaredPaths(documents).filter((doc) => !linked.has(doc.path) && present(root, doc.path) === null);
  const documentsOver = overCeilings(documents, (each) => {
    if (each.role === 'brief') {
      // The whole root instruction file: the brief and the doctrine region, which every session pays for together.
      const region = Object.values(harness.tiers).find((tier) => tier.region)?.region;
      const text = region ? fileText(region.file) : null;
      return region && text !== null && !links.some((problem) => problem.path === region.file)
        ? [{ label: `the brief (${region.file})`, text }]
        : [];
    }
    if (each.role === 'room') {
      return (manifest.rooms ?? []).map((room) => `${room}/${ROOM_FILE}`)
        .filter((path) => isFile(join(root, path)) && !links.some((problem) => problem.path === path))
        .map((path) => ({ label: `${path} (room)`, text: readText(join(root, path)) }));
    }
    if (each.path === null || linked.has(each.path)) return [];
    const kind = present(root, each.path);
    if (kind === 'file') return [{ label: `${each.path} (${each.role})`, text: readText(join(root, each.path)) }];
    if (kind !== 'folder') return [];
    return readdirSync(join(root, each.path), { withFileTypes: true })
      .filter((entry) => entry.isFile() && entry.name.endsWith('.md')).map((entry) => entry.name).sort(compareOrdinal)
      .map((name) => ({ label: `${each.path}/${name} (${each.role})`, text: readText(join(root, each.path!, name)) }));
  });
  const recordsUndeclared = documents.length
    ? RECORD_ROLES.filter((role) => !documents.some((doc) => doc.role === role && doc.path !== null))
    : [];

  // A size fails only where the repository declared its ceiling failing: everything else here is a FACT the
  // tool established — a file drifted, one is missing, a pack is never synced, the index is behind. A
  // reporting ceiling stays and is stated on every run, because a signal nobody can see is not a signal.
  const factsOk = !drifted.length && !missing.length && !stalePacks.length && !indexStale && !rosterStale
    && !staleSwitches.length
    && !staleLayout && !mirrorsDrifted.length && !mirrorsMissing.length && !mirrorsBehind.length
    && !roomsWithoutInstructions.length && !pointersRetired.length && !links.length
    && !documentsStale && !documentsMissing.length && !documentLinksFound.length;
  const sizeFails = documentsOver.some((over) => over.fails) || (overBudget && manifest.coreBudget.fails);
  const ok = factsOk && !sizeFails;
  return {
    factsOk, sizeFails,
    drifted, missing, stalePacks, coreWords, overBudget, index, indexStale, rosterStale, switchedOff, staleSwitches, kept,
    staleLayout, mirrorsDrifted, mirrorsMissing, mirrorsBehind, roomsWithoutInstructions, pointersRetired,
    links, agentsBytes, agentsOwnBytes, agentsRegionBytes, agentsLargestRules, unlisted, readAlone, withoutFrontmatter,
    documentsStale, documentsMissing, documentLinks: documentLinksFound, documentsOver, recordsUndeclared,
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
  // Every run, whatever else it finds: a core row that is off is never a surprise, and it is not
  // a failure either — it is a decision the manifest records.
  for (const row of report.switchedOff) write(`  off       ${row.target} (switched off by pack '${row.by}')`);
  for (const keep of report.kept) write(`  kept      ${keep.target} — this repository's own text: ${keep.reason}`);
  if (report.overBudget) {
    write(
      `  budget    the always-loaded doctrine is ${report.coreWords} of ${manifest.coreBudget.words} declared words — `
        + `${manifest.coreBudget.fails ? 'fails, as daoris.json declares' : 'advisory, not a gate'}: split principle from `
        + 'detail into the on-demand tier, or raise it deliberately in daoris.json',
    );
  }
  if (report.indexStale) {
    write(`  index     ${report.index} is ${isFile(join(root, report.index)) ? 'out of date' : 'absent'} — run 'daoris sync'`);
  }
  if (report.rosterStale) {
    write("  roster    the doctrine region's pointer or rooms are out of date — run 'daoris sync'");
  }
  // The layout's facts: each fails, and each says which file to open.
  if (report.staleLayout) write(`  stale     layout: ${report.staleLayout} — run 'daoris sync' to move them`);
  for (const mirror of report.mirrorsDrifted) {
    write(`  drifted   ${mirror.path} — a mirror of ${mirror.of}, edited here`);
  }
  for (const path of report.mirrorsMissing) write(`  missing   ${path} — a mirror; run 'daoris sync'`);
  for (const path of report.mirrorsBehind) write(`  behind    ${path} — its source moved on; run 'daoris sync'`);
  for (const path of report.roomsWithoutInstructions) {
    write(`  room      ${path} is absent — the room is declared in daoris.json, and its instructions are this repo's to write`);
  }
  for (const path of report.pointersRetired) write(`  pointer   ${path} holds only the retired import of AGENTS.md — run 'daoris sync' to take it out`);
  for (const problem of report.links) write(`  LINK      ${problem.path} — ${shortLink(problem)}`);
  // Reported, never failed on.
  if (report.agentsBytes > INSTRUCTION_LIMIT) {
    const split = report.agentsRegionBytes
      ? `; own part ${report.agentsOwnBytes} bytes, doctrine region ${report.agentsRegionBytes} bytes`
        + (report.agentsLargestRules.length
          ? `; largest rules: ${report.agentsLargestRules.map((rule) => `${rule.name} (${rule.bytes})`).join(', ')}`
          : '')
        + ' — shorten the own part, or split a large rule to the on-demand tier in the canon'
      : '';
    write(`  size      AGENTS.md is ${report.agentsBytes} bytes; codex reads ${INSTRUCTION_LIMIT} of a repository's `
      + `instruction files and cuts the rest, which is the region's last rules — over by ${report.agentsBytes - INSTRUCTION_LIMIT}`
      + `${split} — advisory, not a gate`);
  }
  // The declared documents: the facts first, each failing, then the judgements, never failing.
  for (const doc of report.documentsMissing) write(`  document  ${doc.path} (${doc.role}) is declared in daoris.json, and absent`);
  for (const link of report.documentLinks) write(`  LINK      ${link.declared} (${link.role}) — ${shortDocumentLink(link)}`);
  if (report.documentsStale) {
    write("  where     the region's Where things are list differs from daoris.json's documents — run 'daoris sync'");
  }
  for (const over of report.documentsOver) {
    write(`  ${over.unit.padEnd(9)} ${over.label} is ${over.measured} ${over.unit} of ${over.ceiling} — over by `
      + `${over.measured - over.ceiling}; the read path has grown: move its history to its record, condense, then raise — `
      + `${over.fails ? 'fails, as daoris.json declares; at the ceiling a line in takes a line out' : 'advisory, not a gate'}`);
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
    const { words } = manifest.coreBudget;
    write(report.overBudget
      ? `daoris: no drift — always-loaded core is ${report.coreWords} of ${words} declared words, over by ${report.coreWords - words}`
      : `daoris: clean — ${report.coreWords} of ${words} words of always-loaded core`);
    return 0;
  }
  // A failing ceiling is no drift: the mend is this repository's own text, which no sync takes.
  if (report.sizeFails) write('daoris: over a ceiling daoris.json declares failing — cut the text named above');
  if (!report.factsOk) {
    write("daoris: run 'daoris sync' to take the canon's text, or 'daoris reconcile' to decide each difference");
  }
  return 1;
}
