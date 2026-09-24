import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { CheckField } from '../ui';

/**
 * A repository's standing choices for THIS machine's driver (D46 §6): drive it, hold it, give each
 * session its own tree (D51). The same file `daoris driver drive|hold|trees` edits — two doors, one
 * truth (D50).
 *
 * @remarks
 * One row for both kinds of repository that can be driven here: an adopter's card, and since INT3c
 * an unadopted repository with a root on this machine, which D70 makes drivable over the protocol
 * door. The planner treats the two alike once past that door, so the screen does too.
 *
 * **A hold appears only once the repository is driven** — a hold on nothing is noise. A `note` is the
 * view's fact about what driving here does, said under the choices; an `action` ends the row.
 *
 * Props only, no hook from the query layer or the shell (components §2).
 */
export function DriverChoices({
  drivable, held, ownTree, onDrive, onHold, onTrees, note, action, className,
}: {
  drivable: boolean;
  held: boolean;
  ownTree: boolean;
  onDrive: (drivable: boolean) => void;
  onHold: (held: boolean) => void;
  onTrees: (ownTree: boolean) => void;
  note?: string;
  action?: ReactNode;
  className?: string;
}) {
  const { t } = useTranslation();
  return (
    <div className={className}>
      <p className="m-0 flex flex-wrap items-center gap-4">
        <span className="min-w-12 text-meta text-ink-faint">{t('projects.driver.label')}</span>
        <CheckField checked={drivable} onChange={onDrive} label={t('projects.driver.drive')} />
        {drivable && <CheckField checked={held} onChange={onHold} label={t('projects.driver.hold')} />}
        {/* Session trees (D51): this repository's sessions open their own worktree, so the person's
            uncommitted work in the checkout stops holding the driver. */}
        <CheckField checked={ownTree} onChange={onTrees} label={t('projects.driver.trees')} />
        {action && <span className="ml-auto">{action}</span>}
      </p>
      {note && <p className="mt-1.5 mb-0 text-small text-ink-soft">{note}</p>}
    </div>
  );
}
