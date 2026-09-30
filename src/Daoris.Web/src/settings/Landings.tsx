import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Button, Card, CheckField, Chip, Inline, Prose, SectionTitle, Segmented, SelectField, SettingRow } from '../ui';

/**
 * How a session's work lands (WSR1, D87): merged into the line, or put on a branch the pattern names —
 * whether its tree and branch go once a press lands it (D88), and for a branch the plugin that pushes
 * it and opens the pull request (WSR4, D100).
 */
export type LandingRule = { form: string; pattern?: string; tidy?: boolean; plugin?: string };

/** Where a repository's rule came from: set for it, set for its workspace, or the default merge. */
export type LandingSource = 'repository' | 'workspace' | 'default';

/** One repository's rule on this machine, as the driver chose it. */
export type RepositoryLanding = { repository: string; workspace: string; source: LandingSource } & LandingRule;

/** A change to a rule: a repository's or a workspace's, cleared when it names no form. */
export type LandingChange = { repository?: string; workspace?: string; form?: string; pattern?: string; tidy?: boolean; plugin?: string };

const MERGE: LandingRule = { form: 'merge' };

/** The example a pattern field shows, and what a new branch rule starts from. */
const EXAMPLE = 'feature/{quest}-{slug}';

/** The chooser's value for "no plugin": never a plugin's id, which starts with a letter or a digit. */
const NO_PLUGIN = '-';

const same = (a?: LandingRule, b?: LandingRule) =>
  a?.form === b?.form && (a?.form !== 'branch' || (a?.pattern === b?.pattern && a?.plugin === b?.plugin))
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
 * landing` (D50).
 */
export function LandingList({ landings, workspaceLandings, landers = [], busy, onSet }: {
  landings: RepositoryLanding[];
  /** What each workspace sets, by name. */
  workspaceLandings: ({ workspace: string } & LandingRule)[];
  /** The plugins here that land work: installed, switched on, sound, and speaking on `work/land`. */
  landers?: string[];
  busy?: boolean;
  onSet: (change: LandingChange) => void;
}) {
  const { t } = useTranslation();
  const circles = [...new Set([...landings.map((l) => l.workspace), ...workspaceLandings.map((w) => w.workspace)])]
    .sort((a, b) => a.localeCompare(b));

  const says = (landing: RepositoryLanding) => {
    if (landing.source === 'default') return t('settings.landing.from.default');
    const rule = landing.form === 'branch' ? 'branch' : 'merge';
    const said = t(`settings.landing.from.${landing.source}.${rule}`, { pattern: landing.pattern, workspace: landing.workspace });
    return landing.form === 'branch' && landing.plugin ? `${said} ${t('settings.landing.byPlugin', { plugin: landing.plugin })}` : said;
  };

  return (
    <Card id="settings-landing" className="mt-3.5 scroll-mt-3">
      <SectionTitle>{t('settings.landing.title')}</SectionTitle>
      <Prose className="mt-1 text-small text-ink-soft"><Inline text={t('settings.landing.body')} /></Prose>

      {circles.length === 0 && <Prose className="mt-3">{t('settings.lines.none')}</Prose>}

      {circles.map((workspace) => {
        const shared = workspaceLandings.find((w) => w.workspace === workspace);
        return (
          <section key={workspace} aria-label={workspace} className="mt-3 border-t border-line pt-3">
            <SettingRow
              label={<Chip accent>{workspace}</Chip>}
              hint={t('settings.landing.workspaceHint')}
              control={(
                <LandingField
                  name={workspace}
                  set={shared}
                  inherited={MERGE}
                  landers={landers}
                  busy={busy}
                  onSave={(rule) => onSet({ workspace, ...rule })}
                />
              )}
            />
            {landings.filter((landing) => landing.workspace === workspace).map((landing) => (
              <SettingRow
                key={landing.repository}
                label={landing.repository}
                hint={says(landing)}
                control={(
                  <LandingField
                    name={landing.repository}
                    set={landing.source === 'repository' ? landing : undefined}
                    inherited={landing.source === 'repository' ? shared ?? MERGE : landing}
                    landers={landers}
                    busy={busy}
                    onSave={(rule) => onSet({ repository: landing.repository, ...rule })}
                  />
                )}
              />
            ))}
          </section>
        );
      })}
    </Card>
  );
}

/**
 * One rule's control: merge or branch, the pattern and who pushes it when it is a branch, and a clear
 * only where a rule is set — the row keeps the clear's room either way, so every row's control sits in
 * one column.
 */
function LandingField({ name, set, inherited, landers, busy, onSave }: {
  name: string;
  set?: LandingRule;
  /** What stands without this row's own rule — what the control starts from when none is set. */
  inherited: LandingRule;
  landers: string[];
  busy?: boolean;
  onSave: (rule?: LandingRule) => void;
}) {
  const { t } = useTranslation();
  const start = set ?? inherited;
  const [form, setForm] = useState(start.form);
  const [tidy, setTidy] = useState(Boolean(start.tidy));
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
  }, [start.form, start.tidy, start.plugin, set?.pattern]);

  // What an empty field means: the pattern this row inherits, else the example.
  const fallback = inherited.form === 'branch' && inherited.pattern ? inherited.pattern : EXAMPLE;
  const draft: LandingRule = {
    ...(form === 'branch' ? { form, pattern: pattern.trim() || fallback } : MERGE),
    ...(form === 'branch' && plugin !== NO_PLUGIN ? { plugin } : {}),
    ...(tidy ? { tidy: true } : {}),
  };
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
      <CheckField
        label={t('settings.landing.tidy')}
        checked={tidy}
        onChange={setTidy}
        disabled={busy}
        className="text-small"
      />
      <Button type="submit" disabled={busy || !changed}>{t('settings.landing.set')}</Button>
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
    </form>
  );
}
