import type { TFunction } from 'i18next';
import { type ReactNode, useEffect, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { LandingRule } from '../settings/Landings';
import { Button, Icon, Inline, SettingRow } from '../ui';

// The parts a Setup tab is made of (UX6f, UX6g; D150 §1 rule 4, §4.2, §4.3): a section that folds to the line naming its
// values, and a value that is the thing's own or stands from above. A repository's Setup and a workspace's share them, so
// the two read and fold alike. Molecules: every state is reached by props, and every press goes out.

/** How a rule lands, in a folded line's words: into its line, or on a branch, who pushes it, and with no press. */
export function landingSays(t: TFunction, landing: LandingRule): string {
  if (landing.form !== 'branch') return t('projects.setup.summary.landsMerge');
  return [
    t('projects.setup.summary.landsBranch', { pattern: landing.pattern }),
    ...(landing.plugin ? [t('projects.setup.summary.pushedBy', { plugin: landing.plugin })] : []),
    ...(landing.autoAccept ? [t('projects.setup.summary.automatic')] : []),
  ].join(t('projects.setup.summary.clauseJoin'));
}

/**
 * One section of a Setup: its name and, folded, the line naming its values; a press opens it to its rows. Its name is
 * the press's name, and the line its description, so a reader hears the section and then what it holds. A section a
 * door asks for after it was drawn opens then (UX6g: a door into a workspace's remote, its page already on Setup).
 */
export function SetupSection({ title, summary, startsOpen, children }: {
  title: string; summary: string; startsOpen: boolean; children: ReactNode;
}) {
  const [open, setOpen] = useState(startsOpen);
  useEffect(() => { if (startsOpen) setOpen(true); }, [startsOpen]);
  const id = useId();
  return (
    <section aria-labelledby={`${id}-title`} className="border-t border-line first:border-t-0">
      <h2 className="m-0 text-body font-normal">
        <button
          type="button"
          aria-expanded={open}
          aria-controls={`${id}-body`}
          aria-labelledby={`${id}-title`}
          aria-describedby={open ? undefined : `${id}-summary`}
          onClick={() => setOpen(!open)}
          className="flex w-full flex-wrap items-baseline gap-x-4 gap-y-0.5 py-2.5 text-left"
        >
          <span id={`${id}-title`} className="w-32 shrink-0 font-medium text-ink">{title}</span>
          {!open && (
            <span id={`${id}-summary`} className="min-w-[12rem] flex-1 text-small text-ink-soft">
              <Inline text={summary} />
            </span>
          )}
          <span className="ml-auto self-center text-ink-faint" aria-hidden>
            <Icon name={open ? 'chevronDown' : 'chevronRight'} size={14} />
          </span>
        </button>
      </h2>
      {open && <div id={`${id}-body`} className="pb-3 pl-3">{children}</div>}
    </section>
  );
}

/**
 * A value that is the thing's own or stands from above (§4.2): what it is and what said so, beneath its name; and at the
 * right, its control with *Clear* where it is its own, or *Set for this …* (`setHere`), which opens the control in place
 * with *Never mind* beside it. A `wide` control, the landing rule's several parts, is laid out beneath the row's words at
 * the row's width, keeping only *Clear* or *Never mind* at the right.
 */
export function Inheritable({ label, twin, why, says, own, busy, wide = false, setHere, onClear, editor, children }: {
  label: string;
  twin: string;
  why?: string;
  says: string;
  own: boolean;
  busy: boolean;
  wide?: boolean;
  /** The press that sets a value here, naming whose it is: *Set for this repository*, *Set for this workspace*. */
  setHere: string;
  onClear: () => void;
  editor: () => ReactNode;
  children?: ReactNode;
}) {
  const { t } = useTranslation();
  const [editing, setEditing] = useState(false);
  // Set or cleared by either door, the row starts again from what stands.
  useEffect(() => setEditing(false), [own]);
  const open = own || editing;
  const close = own
    ? <Button variant="ghost" disabled={busy} onClick={onClear}>{t('projects.setup.clear')}</Button>
    : <Button variant="ghost" onClick={() => setEditing(false)}>{t('common.cancel')}</Button>;
  const control = !open
    ? <Button disabled={busy} onClick={() => setEditing(true)}>{setHere}</Button>
    : wide ? close : <>{editor()}{close}</>;
  return (
    <SettingRow label={label} hint={twin} why={why} control={<div className="flex min-w-0 flex-wrap items-center justify-end gap-2 @max-[26rem]:justify-start">{control}</div>}>
      <p className="m-0 text-small text-ink-soft"><Inline text={says} /></p>
      {open && wide && <div className="mt-2">{editor()}</div>}
      {children}
    </SettingRow>
  );
}
