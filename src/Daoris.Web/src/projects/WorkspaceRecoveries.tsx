import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { moment, sentence } from '../locales/format';
import { Button, Inline } from '../design/ui';
import { InlineConfirm } from '../work/InlineConfirm';
import { entryName, type MilestoneEntry, type RecoveryDropGroup, type RecoveryDropResult } from '../work/milestone';

/** The command the driver says deletes a recovery ref nothing holds (D102's CLEANUP1 note): no new verb. */
const DELETE = 'git update-ref -d';

/** A recovery ref nothing of the person's holds: kept at any age, until they delete it. */
const unheld = (entry: MilestoneEntry) => entry.kind === 'recovery-ref' && !entry.until && entry.detail.includes(DELETE);

/** A folder's own name, never its path: the folder is the machine's (D47 §4). */
const folderName = (path: string) => path.split(/[\\/]/).filter(Boolean).pop() ?? path;

/** What keeps it, in the reader's language where the page has the words, and the driver's own clause where it does not. */
function keeps(t: TFunction, entry: MilestoneEntry): string {
  if (entry.kind === 'moved-aside' && entry.disposition === 'retained' && entry.until) {
    return t('projects.recoveries.folderUntil', { when: moment(entry.until) });
  }
  if (entry.kind === 'recovery-ref' && entry.until) return t('projects.recoveries.heldUntil', { when: moment(entry.until) });
  if (unheld(entry)) return t('projects.recoveries.unheld');
  return entry.detail;
}

function Copy({ command }: { command: string }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(command);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      // No clipboard here: the command is on the page to select by hand.
    }
  };
  return (
    <Button variant="ghost" className="text-small" aria-label={t('projects.recoveries.copy')} onClick={() => void copy()}>
      {copied ? t('projects.recoveries.copied') : t('projects.recoveries.copyShort')}
    </Button>
  );
}

/** The refs a press would delete, by repository, each with the commit the page shows: only a ref nothing holds that names its commit. */
function dropGroups(rows: readonly MilestoneEntry[]): RecoveryDropGroup[] {
  const groups = new Map<string, RecoveryDropGroup>();
  for (const entry of rows) {
    if (!unheld(entry) || !entry.commit || !entry.repository) continue;
    const group = groups.get(entry.repository) ?? { repository: entry.repository, refs: [] };
    group.refs.push({ ref: entry.resource, commit: entry.commit });
    groups.set(entry.repository, group);
  }

  return [...groups.values()];
}

/**
 * The delete of the ones nothing holds, asked once (RECOVERYDROP1, D102's CLEANUP1 note): it lists the refs and says plainly that
 * their commits may then be lost from this machine, once git's garbage collection runs, and that a git remote may still have the
 * work. The driver deletes each only if it still names the commit shown and nothing of the person's holds it by then.
 */
function DropAsk({ groups, onDrop, onClose, onDone }: {
  groups: RecoveryDropGroup[];
  onDrop: (groups: RecoveryDropGroup[]) => Promise<RecoveryDropResult>;
  onClose: () => void;
  onDone: (result: RecoveryDropResult) => void;
}) {
  const { t } = useTranslation();
  const count = groups.reduce((sum, group) => sum + group.refs.length, 0);
  return (
    <InlineConfirm
      block
      label={t('projects.recoveries.dropTitle')}
      says={(
        <div className="grid min-w-0 gap-2">
          <Inline text={t('projects.recoveries.dropSays', { count })} />
          <ul className="m-0 grid list-none gap-0.5 p-0">
            {groups.flatMap((group) => group.refs.map((each) => (
              <li key={`${group.repository}\n${each.ref}`} className="font-mono text-small [overflow-wrap:anywhere]">{`${group.repository} · ${each.ref}`}</li>
            )))}
          </ul>
          <Inline text={t('projects.recoveries.dropLoss')} />
        </div>
      )}
      meanIt={t('projects.recoveries.dropMeanIt')}
      onConfirm={(answered) => {
        onDrop(groups).then((result) => {
          // Nothing deleted and a whole press refused: the driver's own sentence, said inside the ask, which stays open.
          if (result.dropped.length === 0 && result.refused.length > 0) answered.refused(result.refused.join(' '));
          else {
            onDone(result);
            answered.done();
          }
        }, (error: unknown) => answered.refused(sentence(error)));
      }}
      onClose={onClose}
    />
  );
}

/**
 * **What is kept for recovery** (RECOVERYVIEW1, CLEANUP1): Daoris's own recoveries no unit of work names, on the workspace's
 * Branches: a folder set aside, kept until its date, and a recovery ref, kept by what holds its commit or, where nothing of the
 * person's does, until they delete it, with the command to do that, copyable. Nothing is drawn where there are none.
 *
 * @remarks
 * **A molecule**: the entries arrive as a prop, read from `WORK_STATE.recovery` by the holder; a browser and a shell older than the
 * field pass none. They are the machine's, so only the workspace's own repositories' are listed, by name in any case. A folder is
 * named, never its path; a clause the page has no words for passes through as the driver recorded it.
 */
export function WorkspaceRecoveries({ recoveries, repositories, onDrop }: {
  recoveries: readonly MilestoneEntry[] | null | undefined;
  /** The workspace's repositories, by name: another's recoveries are not this page's. */
  repositories: readonly string[];
  /**
   * The person's press to delete the refs nothing holds (RECOVERYDROP1): sent each repository's refs with the commits shown, it
   * answers what went and what was kept. None, and a shell older than the commit, offer the copyable commands alone.
   */
  onDrop?: (groups: RecoveryDropGroup[]) => Promise<RecoveryDropResult>;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const [result, setResult] = useState<RecoveryDropResult | null>(null);
  const mine = new Set(repositories.map((name) => name.toLowerCase()));
  const rows = (recoveries ?? []).filter((entry) => entry.repository && mine.has(entry.repository.toLowerCase()));
  const groups = onDrop ? dropGroups(rows) : [];
  if (rows.length === 0 && !result) return null;

  return (
    <section className="grid min-w-0 gap-2 border-t border-line pt-4">
      <div className="flex min-w-0 flex-wrap items-center justify-between gap-2">
        <h2 className="m-0 text-body font-semibold text-ink">{t('projects.recoveries.title')}</h2>
        {groups.length > 0 && !asking && (
          <Button variant="ghost" className="text-small" onClick={() => { setResult(null); setAsking(true); }}>{t('projects.recoveries.drop')}</Button>
        )}
      </div>
      {asking && onDrop && groups.length > 0 && (
        <DropAsk groups={groups} onDrop={onDrop} onClose={() => setAsking(false)} onDone={setResult} />
      )}
      {result && (
        <div role="status" className="grid min-w-0 gap-0.5 text-small text-ink-soft">
          <span>{t('projects.recoveries.deleted', { count: result.dropped.length })}</span>
          {result.skipped.map((each) => (
            <span key={`${each.repository}\n${each.ref}`} className="[overflow-wrap:anywhere]">{t('projects.recoveries.kept', { ref: each.ref, why: each.why })}</span>
          ))}
        </div>
      )}
      <ul className="m-0 grid list-none gap-3 p-0">
        {rows.map((entry) => (
          <li key={`${entry.kind}\n${entry.repository}\n${entry.resource}`} className="grid min-w-0 gap-0.5">
            <span className="font-mono text-small text-ink [overflow-wrap:anywhere]">
              {entryName(t, entry.kind === 'moved-aside' ? { ...entry, resource: folderName(entry.resource) } : entry)}
            </span>
            <span className="text-small text-ink-soft">{[entry.repository, keeps(t, entry)].join(' · ')}</span>
            {unheld(entry) && (
              <span className="flex min-w-0 items-center gap-2">
                <code className="min-w-0 font-mono text-small text-ink [overflow-wrap:anywhere]">{`${DELETE} ${entry.resource}`}</code>
                <Copy command={`${DELETE} ${entry.resource}`} />
              </span>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
