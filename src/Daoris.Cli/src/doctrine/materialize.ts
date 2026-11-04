import type { CommandArgs } from '../platform/types.ts';
import type { SyncPlan } from './records.ts';
import type { ExitCode } from '../platform/errors.ts';
import { canonDigest, isSwitchedOff, readCanon, resolveCanonRoot } from './canon.ts';
import { lockIndex, readLock, readManifest } from './config.ts';
import { refuseNewerLock } from './lockversion.ts';
import { shortLink } from '../layout/links.ts';
import { shortDocumentLink } from '../layout/documents.ts';
import { planSync } from './syncplan.ts';
import { applySync, refuseWhatForceCannot } from './syncapply.ts';
export { planSync } from './syncplan.ts';
export { planChanges } from './syncchanges.ts';
export { applySync } from './syncapply.ts';

export function commandSync({ root, argv, write, packageRoot }: CommandArgs): ExitCode {
  const manifest = readManifest(root);
  const canon = readCanon(resolveCanonRoot(packageRoot));
  const lock = readLock(root);
  // Before anything is planned, for every mode (WSSETUP4, D124 §1.4): the state space D19 enumerates assumes
  // the canon is not older than the lock, and a dry run is how a person asks whether this would refuse.
  refuseNewerLock(lock, canon.version, ['sync', ...argv]);
  const plan = planSync({ root, manifest, canon, lock });

  // A rename is reported in place of the delete and the add it is made of,
  // because "these two are the same rule" is the part a reader cannot recover.
  const renamedFrom = new Set(plan.renames.map((rename) => rename.from));
  const renamedTo = new Set(plan.renames.map((rename) => rename.to));

  // A switched-off row's files leave by the same delete a retirement uses, and are named apart from
  // one (D71): what happened is a decision the manifest records, not the canon letting go of a file.
  const lockEntries = lockIndex(readLock(root));
  const offDeletes = new Set(plan.deletes.filter((target) => {
    const entry = lockEntries.get(target);
    return entry !== undefined && isSwitchedOff(plan.switchedOff, entry);
  }));

  // 🔴 Never silent (D71): every sync names what is off and every offer still waiting, so neither a
  // missing core rule nor a pack's pending switch can be a surprise to the person reading the output.
  const sayWhatIsOff = () => {
    for (const row of plan.switchedOff) write(`  off       ${row.target} (switched off by pack '${row.by}')`);
    for (const offer of plan.offers) {
      write(`  offered   ${offer.target} — pack '${offer.by}' would switch it off: ${offer.because}`);
      write(`            it stays on; to confirm, add to daoris.json: "switchedOff": { "${offer.target}": "${offer.by}" }`);
    }
  };
  // Never silent either (D230): each keep this sync passed over, `--force` included, and each it dropped.
  const sayWhatIsKept = () => {
    for (const keep of plan.kept) {
      write(`  kept      ${keep.target} — this repository's own text: ${keep.reason}`
        + (keep.reopened ? "; the canon's file moved since it was kept, and 'daoris reconcile' lists it" : ''));
    }
    for (const target of plan.keepsDropped) {
      write(`  dropped   the keep of ${target} — the canon no longer ships it here; take it out of daoris.json's kept`);
    }
  };

  // What a move and the layout do, said beside what D19 does (D117 §5.2): each move with both paths,
  // each pointer, and every old `rules/` file that stays where only one agent reads it.
  const moved = new Set(plan.moves.map((move) => move.target));
  const sayTheLayout = (dry: boolean) => {
    for (const move of plan.moves) write(`  moved     ${move.from} -> ${move.to}`);
    if (plan.index.old !== null) write(`  moved     ${plan.index.old} -> ${plan.index.path}`);
    for (const pointer of plan.rooms.pointers) {
      if (pointer.state !== 'unchanged') write(`  pointer   ${pointer.path}`);
    }
    for (const pointer of plan.rooms.unpoint) write(`  pointer   ${pointer.path} (removed: no longer a room)`);
    for (const path of plan.keptRules) {
      write(`  kept      ${path} — read by Claude Code alone; its text could go in AGENTS.md, above the region`);
    }
    // Said, never refused (D122 §2.8): an absent record is `check`'s fact, and the table still names it.
    for (const doc of plan.documents.missing) {
      write(`  document  ${doc.path} (${doc.role}) is declared in daoris.json, and absent — 'daoris check' fails until it is there`);
    }
    if (!dry) return;
    for (const mirror of plan.mirrors.writes) {
      if (mirror.state !== 'unchanged') write(`  mirror    ${mirror.path}`);
    }
    for (const path of plan.mirrors.retire) write(`  retire    ${path}`);
  };

  if (argv.includes('--dry-run')) {
    for (const rename of plan.renames) write(`  renamed   ${rename.from} -> ${rename.to}`);
    for (const entry of plan.writes) {
      if (entry.state !== 'unchanged' && !renamedTo.has(entry.target) && !moved.has(entry.target)) {
        write(`  ${entry.state.padEnd(9)} ${entry.target}`);
      }
    }
    for (const target of plan.deletes) {
      if (!renamedFrom.has(target) && !offDeletes.has(target)) write(`  retire    ${target}`);
    }
    if (plan.index.state !== 'unchanged' && plan.index.old === null && !plan.index.collision) {
      write(`  ${plan.index.state.padEnd(9)} ${plan.index.path}`);
    }
    sayTheLayout(true);
    sayWhatIsOff();
    sayWhatIsKept();
    for (const keep of plan.kept.filter((each) => each.absentCore)) write(`  ABSENT    ${keep.target} (a core file kept, with no text here)`);
    for (const problem of plan.links) write(`  LINK      ${problem.path} — ${shortLink(problem)}`);
    for (const link of plan.documents.links) write(`  LINK      ${link.declared} (${link.role}) — ${shortDocumentLink(link)}`);
    for (const doc of plan.leftBehind) write(`  LEFT BEHIND ${doc.path} — ${doc.move}`);
    for (const pair of plan.bothRoots) write(`  TWO COPIES ${pair.old} and ${pair.neu}`);
    for (const path of plan.rooms.missing) write(`  NO ROOM   ${path} — declared in daoris.json, and absent`);
    for (const target of plan.drifted) write(`  DRIFTED   ${target}`);
    for (const mirror of plan.mirrors.edited) write(`  DRIFTED   ${mirror.path} — a mirror of ${mirror.of}, edited here`);
    for (const target of plan.collisions) write(`  COLLIDES  ${target} (this repo's own)`);
    for (const collision of plan.moveCollisions) write(`  COLLIDES  ${collision.to} (this repo's own)`);
    for (const path of plan.mirrors.collisions) write(`  COLLIDES  ${path} (this repo's own, at a mirror path)`);
    if (plan.index.collision) write(`  COLLIDES  ${plan.index.path} (this repo's own)`);
    for (const target of plan.editedRetirements) write(`  AT RISK   ${target} (retired, but edited here)`);
    for (const target of plan.editedSwitchedOff) write(`  AT RISK   ${target} (switched off, but edited here)`);
    for (const path of plan.mirrors.editedGone) write(`  AT RISK   ${path} (a mirror of a skill that went, edited here)`);
    write(`daoris: ${plan.writes.length} file(s) selected, ${plan.deletes.length - offDeletes.size} to retire`);
    return refuses(plan) ? 1 : 0;
  }

  const force = argv.includes('--force');
  // Before the lines --force prints: none of these is an edit it could discard.
  refuseWhatForceCannot(plan);

  // --force is the only way to lose work with this tool. A refusal names the
  // file it is protecting; the override that overrules that refusal has to name
  // it too, or nothing anywhere records what was destroyed.
  if (force) {
    for (const target of plan.drifted) write(`  overwrote ${target} (local edit discarded)`);
    for (const target of plan.collisions) write(`  overwrote ${target} (this repo's own file)`);
    for (const collision of plan.moveCollisions) write(`  overwrote ${collision.to} (this repo's own file)`);
    for (const mirror of plan.mirrors.edited) write(`  overwrote ${mirror.path} (edit to a mirror discarded)`);
    for (const path of plan.mirrors.collisions) write(`  overwrote ${path} (this repo's own file)`);
    if (plan.index.collision) write(`  overwrote ${plan.index.path} (this repo's own file)`);
    for (const target of plan.editedRetirements) write(`  discarded ${target} (retired, edited here)`);
    for (const target of plan.editedSwitchedOff) write(`  discarded ${target} (switched off, edited here)`);
    for (const path of plan.mirrors.editedGone) write(`  discarded ${path} (a mirror of a skill that went, edited here)`);
  }

  applySync({ root, manifest, plan, canonVersion: canon.version, canonDigest: canonDigest(canon), force });
  for (const rename of plan.renames) write(`  renamed   ${rename.from} -> ${rename.to}`);
  sayTheLayout(false);
  sayWhatIsOff();
  sayWhatIsKept();
  const retired = plan.deletes.length - plan.renames.length - offDeletes.size;
  const mirrored = plan.mirrors.writes.length ? `; mirrored ${plan.mirrors.writes.length}` : '';
  write(`daoris: synced ${plan.writes.length} file(s); retired ${retired}${mirrored}`);
  return 0;
}

/** Whether applying this plan would refuse without `--force` — the dry run's exit code. */
function refuses(plan: SyncPlan): boolean {
  return plan.index.collision || plan.kept.some((keep) => keep.absentCore) || [
    plan.links, plan.documents.links, plan.leftBehind, plan.bothRoots, plan.rooms.missing, plan.drifted, plan.collisions,
    plan.moveCollisions, plan.editedRetirements, plan.editedSwitchedOff,
    plan.mirrors.edited, plan.mirrors.collisions, plan.mirrors.editedGone,
  ].some((list) => list.length > 0);
}
