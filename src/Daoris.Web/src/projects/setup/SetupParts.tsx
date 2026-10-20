import type { TFunction } from 'i18next';
import { type ReactNode, useEffect, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { LandingRule } from '../../settings/repository/Landings';
import { Button, Icon, Inline, SettingList, SettingRow } from '../../design/ui';
import { Refused } from '../../work/InlineConfirm';

// The parts a Setup tab is made of (UX6f, UX6g; D150 §1 rule 4, §4.2, §4.3): a section that folds to the line naming its
// values, and a value that is the thing's own or stands from above. A repository's Setup and a workspace's share them, so
// the two read and fold alike. Molecules: every state is reached by props, and every press goes out.

/**
 * How a rule lands, in a folded line's words: into its line, or on a branch, who pushes it, with no press, and squashed to one
 * commit (DOORS1b).
 */
export function landingSays(t: TFunction, landing: LandingRule): string {
  if (landing.form !== 'branch') return t('projects.setup.summary.landsMerge');
  return [
    t('projects.setup.summary.landsBranch', { pattern: landing.pattern }),
    ...(landing.plugin ? [t('projects.setup.summary.pushedBy', { plugin: landing.plugin })] : []),
    ...(landing.autoAccept ? [t('projects.setup.summary.automatic')] : []),
    ...(landing.squash ? [t('projects.setup.summary.squashed')] : []),
  ].join(t('projects.setup.summary.clauseJoin'));
}

const PLUGIN_MARK = '\u0001';

/**
 * A landing rule's sentence for the row: its clauses joined, and, given `onOpenPlugin`, the plugin it names a door to that
 * plugin's page (PLUGUI1c2c, plugins-screen design §5). The name is cut out of `settings.landing.byPlugin` where it sits,
 * so the catalogue keeps one key; absent the opener, or a catalogue that does not hold it as code, it stays words. `after`
 * is what the rule says last: what finds its ticket and what its squash makes (DOORS1b, `landingProcessSays`).
 */
export function landingRowSays(
  t: TFunction, said: string, plugin: string | undefined, auto: boolean, onOpenPlugin?: (plugin: string) => void, after: string[] = [],
): ReactNode {
  const join = t('projects.setup.sentenceJoin');
  const autoSays = [...(auto ? [t('settings.landing.byAuto')] : []), ...after];
  if (!plugin) return [said, ...autoSays].join(join);
  const byPlugin = t('settings.landing.byPlugin', { plugin: PLUGIN_MARK });
  const parts = byPlugin.split(`\`${PLUGIN_MARK}\``);
  if (!onOpenPlugin || parts.length !== 2) return [said, t('settings.landing.byPlugin', { plugin }), ...autoSays].join(join);
  return (
    <>
      <Inline text={`${said}${join}${parts[0]}`} />
      <Button variant="ghost" className="text-small" onClick={() => onOpenPlugin(plugin)}>{plugin}</Button>
      <Inline text={[parts[1]!, ...autoSays].join(join)} />
    </>
  );
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
      {/* The section's rows on one label track (R1): each value beside its words, the values one column. */}
      {open && <SettingList id={`${id}-body`} className="pb-3 pl-3">{children}</SettingList>}
    </section>
  );
}

/**
 * A value that is the thing's own or stands from above (§4.2): what it is and what said so, beneath its name; and at the
 * right, its control with *Clear* where it is its own, or *Set for this …* (`setHere`), which opens the control in place
 * with *Never mind* beside it. A `wide` control, the landing rule's several parts, is laid out beneath the row's words at
 * the row's width, keeping only *Clear* or *Never mind* at the right. A change the driver refused is said under the row, whole,
 * with the editor still open (`refusal`; the UI/UX design R3), as `HeavyGates` says one under its *Save*.
 */
export function Inheritable({ label, twin, why, says, own, busy, wide = false, setHere, onClear, editor, refusal = null, children }: {
  label: string;
  twin: string;
  why?: string;
  says: ReactNode;
  own: boolean;
  busy: boolean;
  wide?: boolean;
  /** The press that sets a value here, naming whose it is: *Set for this repository*, *Set for this workspace*. */
  setHere: string;
  onClear: () => void;
  editor: () => ReactNode;
  /** The driver's sentence for a change refused here, said under the row until the next press. */
  refusal?: string | null;
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
    // The row's own wrapper lays the control out (R1): a wrapper of its own here once kept `justify-end`, so the value
    // drifted to the pane's far edge after the row stopped sending it there (the design §9's trap for UIFIT1b).
    <SettingRow label={label} hint={twin} why={why} control={control}>
      <p className="m-0 text-small text-ink-soft">{typeof says === 'string' ? <Inline text={says} /> : says}</p>
      {open && wide && <div className="mt-2">{editor()}</div>}
      {refusal && <Refused className="mt-2" sentence={refusal} />}
      {children}
    </SettingRow>
  );
}
