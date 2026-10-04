import { type ReactNode, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Button, Card, CheckField, Chip, Inline, Prose, SectionTitle, Segmented, SelectField, SettingRow } from '../ui';
import { OnItsPage } from './OnItsPage';

/**
 * How a session's work lands (WSR1, D87): merged into the line, or put on a branch the pattern names —
 * whether its tree and branch go once a press lands it (D88), for a branch the plugin that pushes
 * it and opens the pull request (WSR4, D100), and whether a quest's done lands it with no press (LAND2a, D145).
 */
export type LandingRule = { form: string; pattern?: string; tidy?: boolean; plugin?: string; autoAccept?: boolean };

/** Where a repository's rule came from: set for it, set for its workspace, or the default merge. */
export type LandingSource = 'repository' | 'workspace' | 'default';

/** One repository's rule on this machine, as the driver chose it. */
export type RepositoryLanding = { repository: string; workspace: string; source: LandingSource } & LandingRule;

/** A change to a rule: a repository's or a workspace's, cleared when it names no form. */
export type LandingChange = {
  repository?: string; workspace?: string; form?: string; pattern?: string; tidy?: boolean; plugin?: string; autoAccept?: boolean;
};

const MERGE: LandingRule = { form: 'merge' };

/** The example a pattern field shows, and what a new branch rule starts from. */
const EXAMPLE = 'feature/{quest}-{slug}';

/** The chooser's value for "no plugin": never a plugin's id, which starts with a letter or a digit. */
const NO_PLUGIN = '-';

const same = (a?: LandingRule, b?: LandingRule) =>
  a?.form === b?.form
  && (a?.form !== 'branch' || (a?.pattern === b?.pattern && a?.plugin === b?.plugin && Boolean(a?.autoAccept) === Boolean(b?.autoAccept)))
  && Boolean(a?.tidy) === Boolean(b?.tidy);

/**
 * How work lands (WSR1, D87): what accepting a session does, per workspace, with a repository's
 * override.
 *
 * @remarks
 * Written from the owner's first real workspace, where a session's work was merged on one machine into
 * a branch the team takes through review. **Daoris itself never pushes** (D87): the branch form stops at
 * a branch for the person to push — or, where the rule names one, for a plugin installed here to push
 * and open the pull request (WSR4, D100). Only the plugins that land work are offered, and only on a
 * branch. What each row shows is the driver's own choice, read rather than recomputed, and a rule the
 * driver refuses comes back as its own sentence. A row's control is the screen's half of `daoris driver
 * landing --workspace` (D50).
 *
 * **A repository's own rule has one home, its Setup** (UX6f, D150 §1, §3.1): its row left this list, which keeps a line
 * naming the repositories that set their own, each a door to its Setup, until UX6g moves the workspace's rule too.
 */
export function LandingList({ landings, workspaceLandings, landers = [], busy, onSet, onOpen }: {
  landings: RepositoryLanding[];
  /** What each workspace sets, by name. */
  workspaceLandings: ({ workspace: string } & LandingRule)[];
  /** The plugins here that land work: installed, switched on, sound, and speaking on `work/land`. */
  landers?: string[];
  busy?: boolean;
  onSet: (change: LandingChange) => void;
  /** Open a repository's page at Setup, or with null Repositories at Setup. */
  onOpen?: (repository: string | null) => void;
}) {
  const { t } = useTranslation();
  const circles = [...new Set([...landings.map((l) => l.workspace), ...workspaceLandings.map((w) => w.workspace)])]
    .sort((a, b) => a.localeCompare(b));

  return (
    <Card id="settings-landing" className="mt-3.5 scroll-mt-3">
      <SectionTitle>{t('settings.landing.title')}</SectionTitle>
      <Prose className="mt-1 text-small text-ink-soft"><Inline text={t('settings.landing.body')} /></Prose>

      {circles.length === 0 && <Prose className="mt-3">{t('settings.lines.none')}</Prose>}

      {circles.map((workspace) => {
        const shared = workspaceLandings.find((w) => w.workspace === workspace);
        return (
          <section key={workspace} aria-label={workspace} className="mt-3 border-t border-line pt-3">
            <LandingRow
              label={<Chip accent>{workspace}</Chip>}
              hint={t('settings.landing.workspaceHint')}
              name={workspace}
              set={shared}
              inherited={MERGE}
              landers={landers}
              busy={busy}
              onSave={(rule) => onSet({ workspace, ...rule })}
            />
            <OnItsPage
              own={landings.filter((landing) => landing.workspace === workspace && landing.source === 'repository').map((landing) => landing.repository)}
              onOpen={onOpen}
            />
          </section>
        );
      })}
    </Card>
  );
}

/** What a draft that accepts automatically does: the plugin that pushes it, or none. Null where it does not. */
export type Accepting = { plugin?: string } | null;

type FieldProps = {
  name: string;
  set?: LandingRule;
  /** What stands without this row's own rule — what the control starts from when none is set. */
  inherited: LandingRule;
  landers: string[];
  busy?: boolean;
  /** Whether the control carries its own *Clear*: a repository's Setup gives each value one beside it (UX6f). */
  clearable?: boolean;
  onSave: (rule?: LandingRule) => void;
};

/**
 * While a rule's control accepts automatically, the sentence that says what that gives (LAND2a, D145 point 5): the
 * person's standing say-so for a push with no press is said where it is given, and with no plugin, that nothing leaves
 * this machine.
 */
export function AcceptingNote({ accepting }: { accepting: Accepting }) {
  const { t } = useTranslation();
  if (!accepting) return null;
  return (
    <p className={cn('m-0 text-small', accepting.plugin ? 'text-ink-soft' : 'text-warn')}>
      <Inline text={accepting.plugin
        ? t('settings.landing.autoAcceptSays', { plugin: accepting.plugin })
        : t('settings.landing.autoAcceptAlone')}
      />
    </p>
  );
}

/** One rule's row: its control, and beneath it what accepting automatically gives while the control says so. */
function LandingRow({ label, hint, ...field }: { label: ReactNode; hint: string } & FieldProps) {
  const [accepting, setAccepting] = useState<Accepting>(null);
  return (
    <SettingRow label={label} hint={hint} control={<LandingField {...field} onAccepting={setAccepting} />}>
      <AcceptingNote accepting={accepting} />
    </SettingRow>
  );
}

/**
 * One rule's control: merge or branch, the pattern, who pushes it and whether it accepts automatically when it is a
 * branch, and a clear only where a rule is set — the row keeps the clear's room either way, so every row's control sits
 * in one column.
 */
export function LandingField({ name, set, inherited, landers, busy, clearable = true, onSave, onAccepting }: FieldProps & {
  /** What the draft accepts automatically through, said beneath the row by its owner. */
  onAccepting: (accepting: Accepting) => void;
}) {
  const { t } = useTranslation();
  const start = set ?? inherited;
  const [form, setForm] = useState(start.form);
  const [tidy, setTidy] = useState(Boolean(start.tidy));
  // A choice like the tidy, which starts from what stands, set here or above (LAND2a).
  const [auto, setAuto] = useState(Boolean(start.autoAccept));
  // Only a pattern SET on this row is a value; an inherited one is the placeholder, and must not read
  // as set — in dark the two looked alike on the lines card (seen on the window).
  const [pattern, setPattern] = useState(set?.pattern ?? '');
  // Who pushes is a choice like the form, so it starts from what stands, set here or above (D100).
  const [plugin, setPlugin] = useState(start.plugin ?? NO_PLUGIN);
  // The answer moves when either door edits the file, and the control follows it.
  useEffect(() => {
    setForm(start.form);
    setTidy(Boolean(start.tidy));
    setPattern(set?.pattern ?? '');
    setPlugin(start.plugin ?? NO_PLUGIN);
    setAuto(Boolean(start.autoAccept));
  }, [start.form, start.tidy, start.plugin, start.autoAccept, set?.pattern]);

  // What an empty field means: the pattern this row inherits, else the example.
  const fallback = inherited.form === 'branch' && inherited.pattern ? inherited.pattern : EXAMPLE;
  // Only a branch accepts automatically (D145 point 1): a merge would write into the checkout with no press.
  const accepts = form === 'branch' && auto;
  const draft: LandingRule = {
    ...(form === 'branch' ? { form, pattern: pattern.trim() || fallback } : MERGE),
    ...(form === 'branch' && plugin !== NO_PLUGIN ? { plugin } : {}),
    ...(tidy ? { tidy: true } : {}),
    ...(accepts ? { autoAccept: true } : {}),
  };
  const through = accepts ? (plugin !== NO_PLUGIN ? plugin : '') : null;
  useEffect(() => {
    onAccepting(through === null ? null : through ? { plugin: through } : {});
  }, [through, onAccepting]);
  const changed = !same(draft, set ?? inherited);
  // The plugins here that land work, and the one the rule names even where it no longer does — the
  // driver's sentence says why, and the chooser must still show what stands.
  const choices = [...new Set([...landers, ...(start.plugin ? [start.plugin] : [])])];

  return (
    <form
      className="flex min-w-0 flex-wrap items-center justify-end gap-2"
      onSubmit={(event) => {
        event.preventDefault();
        if (changed) onSave(draft);
      }}
    >
      <Segmented
        label={t('settings.landing.field', { name })}
        value={form}
        options={[
          { value: 'merge', label: t('settings.landing.form.merge') },
          { value: 'branch', label: t('settings.landing.form.branch') },
        ]}
        onChange={setForm}
      />
      {form === 'branch' && (
        // Gives way to its column: with the side bar open the row's control column was 130px, and a
        // fixed 14rem field spilled left over the row's words (WSR4, seen on the window).
        <input
          aria-label={t('settings.landing.pattern', { name })}
          value={pattern}
          onChange={(event) => setPattern(event.target.value)}
          placeholder={fallback}
          spellCheck={false}
          className="w-56 min-w-0 max-w-full rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint"
        />
      )}
      {form === 'branch' && choices.length > 0 && (
        <SelectField
          ariaLabel={t('settings.landing.plugin', { name })}
          value={plugin}
          onChange={setPlugin}
          disabled={busy}
          options={[
            { value: NO_PLUGIN, label: t('settings.landing.pluginNone') },
            ...choices.map((id) => ({ value: id, label: t('settings.landing.pluginNamed', { plugin: id }) })),
          ]}
        />
      )}
      {form === 'branch' && (
        <CheckField
          label={t('settings.landing.autoAccept')}
          checked={auto}
          onChange={setAuto}
          disabled={busy}
          className="text-small"
        />
      )}
      <CheckField
        label={t('settings.landing.tidy')}
        checked={tidy}
        onChange={setTidy}
        disabled={busy}
        className="text-small"
      />
      <Button type="submit" disabled={busy || !changed}>{t('settings.landing.set')}</Button>
      {clearable && (
        <Button
          variant="ghost"
          disabled={busy || set === undefined}
          aria-hidden={set === undefined}
          tabIndex={set === undefined ? -1 : undefined}
          className={cn(set === undefined && 'invisible @max-[26rem]:hidden')}
          onClick={() => onSave(undefined)}
        >
          {t('settings.landing.clear')}
        </Button>
      )}
    </form>
  );
}
