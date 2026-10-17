import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { moment } from '../locales/format';
import { Button } from '../design/ui';
import { entryName, type MilestoneEntry } from '../work/milestone';

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
export function WorkspaceRecoveries({ recoveries, repositories }: {
  recoveries: readonly MilestoneEntry[] | null | undefined;
  /** The workspace's repositories, by name: another's recoveries are not this page's. */
  repositories: readonly string[];
}) {
  const { t } = useTranslation();
  const mine = new Set(repositories.map((name) => name.toLowerCase()));
  const rows = (recoveries ?? []).filter((entry) => entry.repository && mine.has(entry.repository.toLowerCase()));
  if (rows.length === 0) return null;

  return (
    <section className="grid min-w-0 gap-2 border-t border-line pt-4">
      <h2 className="m-0 text-body font-semibold text-ink">{t('projects.recoveries.title')}</h2>
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
