import type { CommandArgs } from '../platform/types.ts';
import type { Canon, CanonFile, Lock, Manifest } from './records.ts';
import type { ExitCode } from '../platform/errors.ts';
import { join } from 'node:path';
import { canonDigest, readCanon, resolveCanonRoot, resolveSelection } from './canon.ts';
import { readLock, readManifest } from './config.ts';
import { inspect } from './drift.ts';
import { canonFileDigest, holds, keepStates } from './keep.ts';
import { refuseNewerLock } from './lockversion.ts';
import { planChanges } from './materialize.ts';
import { INSTRUCTION_LIMIT, lockLayout } from '../layout/layout.ts';
import { DaorisError } from '../platform/errors.ts';

/** One difference the brief asks a decision on. */
export interface ReconcileItem {
  /** The canonical file it is about, or the repository's own file a set-up gap names. */
  subject: string;
  kind: 'changed' | 'added' | 'retired' | 'edited' | 'reopened' | 'dropped' | 'gap';
  /** The sentence the brief says it in, after the subject. */
  says: string;
  /** Where the canon's text is, for the items that have one. */
  canonText?: string;
  /** The keep line `daoris.json` takes for it, for the items a keep can answer. */
  keepLine?: string;
}

/**
 * What `daoris reconcile` lists (D230): only the differences. What a sync would update, add or retire (`planChanges`, the
 * update `status` reports); the copies edited here (D19's drift, the disk against the lock); a keep whose canon file moved
 * since it was judged, or that the canon no longer ships; and a set-up gap `check` already reports. Offline: it reads the
 * repository and the canon this tool carries, and writes nothing.
 */
export function reconcileItems(
  { root, manifest, lock, canon }: { root: string; manifest: Manifest; lock: Lock; canon: Canon },
): ReconcileItem[] {
  const selection = resolveSelection(canon, manifest.packs, manifest.switchedOff ?? {});
  const byTarget = new Map<string, CanonFile>(selection.files.map((file) => [file.target, file]));
  const textOf = (target: string) => {
    const file = byTarget.get(target);
    return file ? join(canon.root, file.source) : undefined;
  };
  const keepLine = (target: string) => {
    const file = byTarget.get(target);
    return file ? `"${target}": { "reason": "<why>", "canon": "${canonFileDigest(canon, file)}" }` : undefined;
  };
  const item = (subject: string, kind: ReconcileItem['kind'], says: string, keeps = true): ReconcileItem => {
    const canonText = textOf(subject);
    const line = keeps ? keepLine(subject) : undefined;
    return { subject, kind, says, ...(canonText ? { canonText } : {}), ...(line ? { keepLine: line } : {}) };
  };

  const items: ReconcileItem[] = [];
  const changes = planChanges({ root, manifest, canon, lock });
  for (const target of changes.changed) items.push(item(target, 'changed', 'the canon changed it, and the copy here is untouched.'));
  for (const target of changes.added) items.push(item(target, 'added', 'the canon added it.'));
  for (const target of changes.retired) {
    items.push(item(target, 'retired', 'the canon no longer ships it, and a sync deletes it: adopt that, or copy it aside as '
      + "this repository's own document first.", false));
  }

  const report = inspect({ root, manifest, lock });
  for (const target of report.drifted) {
    items.push(item(target, 'edited', "edited here: this repository's copy differs from what the tool last wrote."));
  }

  const was = lockLayout(root, lock, manifest);
  const keeps = keepStates(manifest, canon, selection.files, (file) => holds(root, manifest.harnessDescriptor, was.target, file));
  for (const keep of keeps.kept) {
    if (keep.reopened) {
      items.push(item(keep.target, 'reopened', `kept here (${keep.reason}), and the canon's file moved since it was kept.`));
    } else if (keep.absentCore) {
      items.push(item(keep.target, 'reopened', `kept here (${keep.reason}) with no text: a core file is kept only as a text, `
        + 'so write it, or adopt the canon\'s.'));
    }
  }
  for (const target of keeps.dropped) {
    items.push(item(target, 'dropped', "kept in `daoris.json`, and the canon no longer ships it here: take the keep out.", false));
  }

  // The set-up gaps `check` reports and never fails on, and the facts it fails on that no sync mends: this repository's own.
  if (report.agentsBytes > INSTRUCTION_LIMIT) {
    items.push(item('AGENTS.md', 'gap', `is ${report.agentsBytes} bytes, and one agent reads ${INSTRUCTION_LIMIT} bytes of a `
      + "repository's instruction files and cuts the rest, the doctrine's last rules among it: shorten this repository's own "
      + 'part, or move a long section to a document read on demand.', false));
  }
  if (report.overBudget) {
    items.push(item(manifest.target, 'gap', `holds ${report.coreBytes} bytes of always-loaded doctrine, over the `
      + `${manifest.coreBudgetBytes} that \`daoris.json\` allows: split detail to the on-demand tier, or raise the budget `
      + 'deliberately.', false));
  }
  for (const path of report.roomsWithoutInstructions) {
    items.push(item(path, 'gap', 'is a room `daoris.json` declares, with no instructions of its own: write them, or take '
      + 'the room out.', false));
  }
  for (const doc of report.documentsMissing) {
    items.push(item(doc.path, 'gap', `is declared in \`daoris.json\` as the ${doc.role}, and absent.`, false));
  }
  for (const doc of report.unlisted) items.push(item(doc.path, 'gap', `is this repository's own, and no index lists it: ${doc.move}.`, false));
  for (const path of report.readAlone) {
    items.push(item(path, 'gap', "is this repository's own skill, where one agent alone reads it: move it where every "
      + 'agent reads it.', false));
  }
  return items;
}

/** The brief, in the canon's words: no decision number, no product, nothing of the tool's own history. */
export function composeReconcile(
  { lock, canon, items }: { lock: Lock; canon: Canon; items: readonly ReconcileItem[] },
): string {
  const lines: string[] = ['Reconcile this repository with the shared doctrine', ''];

  const carried = canon.version;
  const direction = lock.canonVersion !== carried
    ? `This repository was synced from canon ${lock.canonVersion}, older than the canon ${carried} this tool carries, so `
      + "the canon's text is the newer."
    : lock.canonDigest !== canonDigest(canon)
      ? `This repository was synced from canon ${carried}, and this tool carries canon ${carried} too: both are canon `
        + `${carried}, and the two differ: which is newer cannot be told from here. Adopting writes this tool's text, so `
        + "adopt an item only where the canon's text reads as the newer, and where it reads as the older, keep yours and say so."
      : `This repository was synced from the canon this tool carries, canon ${carried}.`;
  lines.push(`${direction} Each item below is a difference between the canon and this repository, or a gap in this `
    + "repository's set-up. Decide each here, in this repository's own session, on its own branch: nothing is written into "
    + 'this repository from outside it.', '');

  lines.push('**The items**', '');
  items.forEach((each, at) => {
    lines.push(`${at + 1}. \`${each.subject}\` ${each.kind === 'gap' ? '' : '— '}${each.says}`);
    if (each.canonText) lines.push(`   The canon's text: ${each.canonText}`);
    if (each.keepLine) lines.push(`   To keep it: ${each.keepLine}`);
  });
  lines.push('');

  lines.push(
    '**How to decide each**',
    '',
    "- **Adopt**: take the canon's text. Once every item is decided, run `daoris sync`: it writes each adopted file and "
      + 'passes over every kept one. Where an adopted file was edited here, `daoris sync` refuses to discard the edit, and '
      + '`daoris sync --force` discards it, still passing over every kept file.',
    "- **Keep, with its reason**: this repository's own text stays. Add the item's line to `daoris.json`'s `kept`, with "
      + "the reason in this repository's words. The digest is the canon's file it was judged against, so a later change "
      + 'to that file brings the item back. A core file is kept only as a text, never as its absence.',
    "- **Upstream**: propose this repository's text to the repository that keeps the shared doctrine. `daoris upstream "
      + "<file>` composes the request's words for one file, and `daoris upstream --all` for every edited one; outside that "
      + "repository they write nothing. Hand the words to the person to send, in your close or your report, and keep the "
      + 'file meanwhile, with the reason that it is proposed.',
    '',
    "A set-up gap is no canon file: mend it in this repository's own files, or leave it and say why in your close.",
    '',
    "**Keep the checks green.** Run this repository's checks before the first change, and again at the close, "
      + '`daoris check` among them. A check green before and red after means the reconcile does not close done; where its '
      + 'fix is outside these items, stop and say so.',
    '',
    "**Close**, or report, with each item and what was decided: adopted; kept, with its reason; or proposed upstream, "
      + "with the request's words.",
  );
  return `${lines.join('\n')}\n`;
}

export function commandReconcile({ root, argv, write, packageRoot }: CommandArgs): ExitCode {
  const manifest = readManifest(root);
  const lock = readLock(root);
  if (!lock) {
    throw new DaorisError("this repository has not synced the doctrine, so there is nothing to reconcile: its set-up "
      + "comes first, with 'daoris init' and 'daoris sync'", 1);
  }
  const canon = readCanon(resolveCanonRoot(packageRoot));
  // An older canon never judges a newer lock's text (WSSETUP4): its brief would offer the older text as an update.
  refuseNewerLock(lock, canon.version, ['reconcile', ...argv]);

  const items = reconcileItems({ root, manifest, lock, canon });
  if (!items.length) {
    write("daoris: nothing to reconcile — this repository holds the canon's text, keeps what it keeps, and check reports "
      + 'no set-up gap');
    return 0;
  }
  write(composeReconcile({ lock, canon, items }).trimEnd());
  return 0;
}
