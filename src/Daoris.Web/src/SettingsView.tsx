import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { figure } from './format';
import { cn } from './lib/cn';
import { useRegistry, useStatus, useWorkspaceHoldings } from './queries';
import { useScope } from './scope';
import { useHarnessRun, WithHarnessRuns } from './harnessRuns';
import {
  useAddFavorite, useBrowserSettings, useDriver, useHarnessAction, useHarnesses, useLines,
  usePluginAction, usePlugins, useRefreshHarnesses, useRemotes, useRemoveFavorite, useRuleAction, useRuleProposal, useRules,
  useSetAgentSettings, useSetBrowser, useSetExtensions, useSetHelper, useSetIntake, useSetLanding, useSetLine, useSetNotify, useSweep, useSweepPlan, useSetStrikes, useStarts, useUnwireRemote, useUsage,
  useWireRemote,
} from './shell';
import { AccountSettingsForm, AccountSettingsSummary } from './settings/AccountSettings';
import { AgentRules } from './settings/AgentRules';
import { proposalChange } from './settings/proposals';
import { StartWiringList } from './map/StartWiring';
import { SessionConsole } from './SessionConsole';
import { AiJobs, type SearchTier } from './settings/AiJobs';
import { LandingList } from './settings/Landings';
import { LineList } from './settings/Lines';
import { SweepList } from './settings/Sweep';
import { SignIn } from './SignIn';
import { byTool, doorLabel, type ToolDoor } from './tools';
import {
  Button, Card, CheckField, Chip, failure, Icon, Inline, type Notify, PageHeader, PathText, Pill, Prose,
  SectionTitle, Segmented, SelectField, SettingRow, Tip, useErrorNotify,
} from './ui';
import { useThemeChoice } from './theme';
import { workspacesOf } from './workspaces';

/**
 * Settings (D66): the application's own — how it looks, which language it speaks — and, on the
 * desktop, everything that is true about THIS machine beneath them.
 *
 * @remarks
 * 🔴 There was no settings page, so no way to change the theme. The machine's page was the only settings there were, reachable from a sliders
 * icon labelled *Machine*, and the theme was the OS's alone. Appearance leads because it is the part
 * everyone has; a browser shows only that part, because a browser may learn nothing of a machine
 * (D47 §4) — the machine's half is absent there, never disabled.
 */
/** Settings' domains, in the order its list shows them (D75 §2). */
export type SettingsSection =
  | 'appearance' | 'ai' | 'workspace' | 'driver' | 'agents' | 'permissions' | 'plugins' | 'browser';

/** A part of a domain a menu item is named for (UX5 U72), found by the id `settings-<anchor>`. */
export type SettingsAnchor = 'usage' | 'proposals' | 'wiring' | 'lines';

/** Which domains need this machine: a browser is never offered one (D47 §4). */
const SECTIONS: readonly { id: SettingsSection; machine: boolean }[] = [
  { id: 'appearance', machine: false },
  { id: 'ai', machine: false },
  // Its list of workspaces is for everyone; its wiring is the machine's, and only a shell sees that.
  { id: 'workspace', machine: false },
  { id: 'driver', machine: true },
  { id: 'agents', machine: true },
  { id: 'permissions', machine: true },
  { id: 'plugins', machine: true },
  { id: 'browser', machine: true },
];

/**
 * Settings (D66, as amended by D75): one page, its domains in a list at its left, one shown at a time,
 * as an IDE's settings are.
 *
 * @remarks
 * 🔴 **It was one long page**, and every way in (three menu items, the status bar's driver, remote and
 * tier, a waiting proposal's row) opened it at its top. A domain is now reachable by name, and the
 * one chosen is the caller's to hold, so a menu can open *Permissions* rather than the page.
 *
 * Every domain is cards the page already held. The two doors are unchanged (D50): each row is still
 * the file a terminal edits.
 */
export function SettingsView({ notify, section = 'appearance', onSection, anchor = null, onAnchored }: {
  notify: Notify;
  section?: SettingsSection;
  onSection?: (section: SettingsSection) => void;
  /**
   * The part of the domain a menu item named, brought into view once it is drawn (UX5 U72): the
   * Agents menu's *Usage* opened its domain at the top, a screen above the usage.
   */
  anchor?: SettingsAnchor | null;
  /** Told once the part is in view, so a later visit opens at the domain's top again. */
  onAnchored?: () => void;
}) {
  const { t } = useTranslation();
  // Watched for until it exists: a part is drawn by the card holding it when that card's query
  // answers, which re-renders the card and not this page, so a check after this page's renders
  // missed it.
  const anchored = useRef(onAnchored);
  anchored.current = onAnchored;
  useEffect(() => {
    if (!anchor) return undefined;
    const bring = () => {
      const part = document.getElementById(`settings-${anchor}`);
      if (!part) return false;
      part.scrollIntoView({ block: 'start' });
      anchored.current?.();
      return true;
    };
    if (bring()) return undefined;
    const watch = new MutationObserver(() => { if (bring()) watch.disconnect(); });
    watch.observe(document.body, { childList: true, subtree: true });
    return () => watch.disconnect();
  }, [anchor]);
  // The same "is a shell here" answer every control uses — one detection path, not two that drift.
  const attached = useDriver().data !== undefined;
  const offered = SECTIONS.filter((domain) => attached || !domain.machine);
  // A domain this window cannot show opens on Appearance: a machine's domain in a browser, or one a
  // shell remembered.
  const shown = offered.some((domain) => domain.id === section) ? section : 'appearance';

  return (
    // The application holds a tool's running action above every view (SIGNIN1); rendered alone, this
    // page holds its own, so it still works where nothing above does.
    <WithHarnessRuns notify={notify}>
    <section>
      <PageHeader
        title={t('settings.title')}
        description={t(attached ? 'settings.description' : 'settings.descriptionBrowser')}
      />
      <div className="grid items-start gap-x-6 gap-y-3 md:grid-cols-[11rem_minmax(0,1fr)]">
        {/* It stays put while a long domain scrolls, as an IDE's settings list does. */}
        <nav aria-label={t('settings.domains')} className="md:sticky md:top-0">
          <ul className="m-0 list-none p-0">
            {offered.map(({ id }) => (
              <li key={id}>
                <button
                  type="button"
                  aria-current={id === shown ? 'page' : undefined}
                  onClick={() => onSection?.(id)}
                  className={cn(
                    'w-full rounded-control border-l-2 px-3 py-1.5 text-left text-body transition-colors duration-(--speed)',
                    id === shown
                      ? 'border-l-accent bg-accent-soft font-medium text-ink'
                      : 'border-l-transparent text-ink-soft hover:bg-accent-soft/50',
                  )}
                >
                  {t(`settings.domain.${id}`)}
                </button>
              </li>
            ))}
          </ul>
        </nav>
        {/* A card stacked under another keeps its own top margin; the first in a domain does not. */}
        <div className="min-w-0 [&>*:first-child]:mt-0">
          {shown === 'appearance' && <Appearance />}
          {shown === 'ai' && <OwnAi attached={attached} notify={notify} />}
          {shown === 'workspace' && (
            <>
              <WorkspaceList />
              {attached && <WiringSettings notify={notify} />}
              {attached && <Starts notify={notify} />}
              {attached && <LineSettings notify={notify} />}
              {attached && <LandingSettings notify={notify} />}
              {attached && <SweepSettings notify={notify} />}
            </>
          )}
          {shown === 'driver' && <DriverSettings notify={notify} />}
          {shown === 'agents' && <HarnessRoster notify={notify} />}
          {shown === 'permissions' && <Rules notify={notify} />}
          {shown === 'plugins' && <Plugins notify={notify} />}
          {shown === 'browser' && <BrowserDomain notify={notify} />}
        </div>
      </div>
    </section>
    </WithHarnessRuns>
  );
}

/**
 * Daoris's own AI (AGT6): the jobs it may use a model for, the tier answering each, and how to
 * change it — between Appearance and the machine, because it is for everyone.
 *
 * @remarks
 * Which tier answers search is the service's answer over HTTP, the one every browser is given and
 * the status bar already states (D24), so a browser sees that job too. The intake is this machine's
 * `driver.json`, so its half — and every query it needs — exists only with a shell (D47 §4): a
 * browser is given no intake at all, never a disabled one.
 */
function OwnAi({ attached, notify }: { attached: boolean; notify: Notify }) {
  const status = useStatus();
  const search: SearchTier | undefined = status.data
    ? { tier: status.data.tier, note: status.data.note, semantic: status.data.semantic }
    : undefined;

  return attached ? <MachineAi search={search} notify={notify} /> : <AiJobs search={search} />;
}

function MachineAi({ search, notify }: { search?: SearchTier; notify: Notify }) {
  const { t } = useTranslation();
  const driver = useDriver();
  const roster = useHarnesses();
  const registry = useRegistry('machine');
  const setIntake = useSetIntake();
  const setHelper = useSetHelper();
  // The circles *What a start runs on* names, spelled the same way — so both cards read one answer.
  const workspaces = workspacesOf(registry.data ?? []);
  const answer = useStarts(workspaces);

  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  // An agent a person can name here is a way in this machine HAS: one not installed would hold every
  // intake, and a choice whose only outcome is a hold is worse than none.
  const agents = harnesses
    .filter((door) => door.present)
    .map((door) => ({ value: door.harness, label: doorLabel(t, door) }));
  const starts = Array.isArray(answer.data?.starts) ? answer.data.starts : [];

  return (
    <AiJobs
      search={search}
      // An older shell has never heard of the intake: its STATE carries no field, and it gets no row.
      // Off is "" rather than null, because the bridge leaves a null out — which read as older.
      intake={driver.data && 'intakeAdapter' in driver.data
        ? {
          adapter: driver.data.intakeAdapter || null,
          agents,
          starts: starts.filter((start) => start.job === 'intake'),
          nameOf: namer(t, harnesses),
          busy: setIntake.isPending,
          onChange: (adapter) => setIntake.mutate({ adapter }, {
            onSuccess: () => notify(adapter
              ? t('settings.ai.intake.named', { agent: adapter })
              : t('settings.ai.intake.cleared')),
            onError: failure(notify),
          }),
        }
        : undefined}
      // Ask Daoris's own agent (D89): absent on a shell older than it, "" off, as the intake's.
      helper={driver.data && 'helperAdapter' in driver.data
        ? {
          adapter: driver.data.helperAdapter || null,
          agents,
          busy: setHelper.isPending,
          onChange: (adapter) => setHelper.mutate({ adapter }, {
            onSuccess: () => notify(adapter
              ? t('settings.ai.helper.named', { agent: adapter })
              : t('settings.ai.helper.cleared')),
            onError: failure(notify),
          }),
        }
        : undefined}
    />
  );
}

/**
 * What a person calls an account — the roster's own rule (who signed in, a key's handle, the name),
 * looked up on the TOOL, because a door's accounts are its owner's (AGT7). One copy, for every card
 * that names an account from the driver's answer.
 */
function namer(t: ReturnType<typeof useTranslation>['t'], harnesses: ToolDoor[]) {
  const tools = byTool(harnesses);
  // By the tool, or by a door onto it: usage is counted per door, and a door's accounts are its
  // owner's (AGT7).
  const toolFor = (owner: string) =>
    tools.find((tool) => tool.name === owner) ?? tools.find((tool) => tool.doors.some((door) => door.harness === owner));
  return (owner: string, profile?: string | null) => {
    const tool = toolFor(owner);
    // The tool's own home is named as its row names it: who signed in, else this machine's own. It
    // was "the agent's own sign-in" in one card and "its own home" in another (UX5 U53).
    if (!profile) return tool?.ownAccount ?? t('harness.own');
    const row = tool?.accounts.find((account) => account.name === profile);
    return row?.account ?? (row?.key ? t('harness.profile.keyName', { handle: row.key }) : profile);
  };
}

/**
 * How this window looks and speaks — per viewer, remembered by this window's profile like the scope,
 * never machine wiring and never a file (D66). Both settings apply at once; nothing to save.
 */
function Appearance() {
  const { t, i18n } = useTranslation();
  const [theme, setTheme] = useThemeChoice();
  const language = i18n.language.startsWith('zh') ? 'zh' : 'en';

  return (
    // No title of its own: the domain list names it, and a card alone in its domain would say it twice.
    <Card>
      <SettingRow
        label={t('settings.theme.label')}
        hint={t('settings.theme.hint')}
        control={(
          <Segmented
            label={t('settings.theme.label')}
            value={theme}
            onChange={setTheme}
            options={[
              { value: 'system', label: t('settings.theme.system') },
              { value: 'light', label: t('settings.theme.light') },
              { value: 'dark', label: t('settings.theme.dark') },
            ]}
          />
        )}
      />
      <SettingRow
        label={t('settings.language.label')}
        hint={t('settings.language.hint')}
        control={(
          <Segmented
            label={t('settings.language.label')}
            value={language}
            onChange={(next) => void i18n.changeLanguage(next)}
            // Each language named in itself — a person who cannot read the current one can still
            // find their own.
            options={[
              { value: 'en', label: t('language.en') },
              { value: 'zh', label: t('language.zh') },
            ]}
          />
        )}
      />
    </Card>
  );
}

/**
 * The Driver domain (D75): where this machine's Daoris lives, and the driver's two dials over
 * `driver.json`, the same file `daoris driver` edits (D50). The file is the truth and this is an
 * editor over it: hand-editing keeps working, and neither surface is the only way to say anything.
 *
 * **Shell-only, like every machine domain.** A browser over a keyed remote must never read where a
 * machine lives or syncs, and never re-point it, so the state lives behind the shell's bridge and the
 * service has no route onto it at all. For the wiring, the key goes in and never comes out: what is
 * rendered is the audit prefix a deployment's own `keys list` prints (`WiringSettings`).
 */
function DriverSettings({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  // Whether this machine interrupts the person (SURF5b) — the same `driver.json` field
  // `daoris driver notify on|off` edits, which is what makes this a door rather than the door.
  const driver = useDriver();
  const setNotify = useSetNotify();
  const setStrikesMutation = useSetStrikes();

  // Held as text while it is being typed: a number input mid-edit passes through the empty string
  // and through "0", and writing either straight to the config would park nothing while the person
  // was still reaching for the second digit.
  const [strikes, setStrikes] = useState<string | null>(null);
  const onError = failure(notify);

  return (
    <>
      {/* 🔴 A setting is a ROW. Every card here used to open with a
          paragraph and put its one control beneath it, so the first checkbox sat 580px below the
          title and the next dial a screen further down. `SettingRow` carries the shape now — the
          label leads, the hint is one line, the control is at the right, the paragraph is on the
          glyph — and the cards are the sections of one settings page rather than five essays. */}

      {/* The driver's two dials, in one card: that one asks to be TOLD when a driver stops, this one
          bounds what it spends before anyone is told (D58). The notification switch leads because it
          is the setting a person is most likely to have come here to change. */}
      <Card>
        {/* Where this machine's Daoris lives (D63): the card's first row since D75, where it had
            floated above the card with no label once the page lost its "This machine" heading. The
            notice is the shell's own sentence about what the start did, carried in the state rather
            than only raised: a toast raised before the page subscribed reached nobody. */}
        {driver.data?.home && (
          <SettingRow
            label={t('settings.home.label')}
            why={t('settings.home.hint')}
            control={<PathText path={driver.data.home} className="text-small text-ink-soft" />}
          >
            {driver.data.homeNotice && (
              <p className="max-w-prose border-l-[3px] border-accent bg-page/60 px-3.5 py-2 text-body text-ink-soft">
                {driver.data.homeNotice}
              </p>
            )}
            {/* The host this window adopted serves another install's page (case study 4d). A
                standing fact, so a standing line: the toast that carried it fired before this page
                existed to hear it, which is how the second deployment showed a new window, an old
                page, and no surface saying so. */}
            {driver.data.hostNotice && (
              <p className="mt-2 max-w-prose border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
                {driver.data.hostNotice}
              </p>
            )}
          </SettingRow>
        )}
        <SettingRow
          label={t('settings.notify.label')}
          hint={t('settings.notify.terminal')}
          why={t('settings.notify.body')}
          control={(
            <CheckField
              hideLabel
              checked={driver.data?.notify ?? true}
              onChange={(on) => setNotify.mutate({ notify: on }, {
                onSuccess: () => notify(t(on ? 'settings.notify.on' : 'settings.notify.off')),
                onError,
              })}
              label={t('settings.notify.label')}
            />
          )}
        />

        <SettingRow
          label={t('settings.strikes.label')}
          hint={t('settings.strikes.terminal')}
          why={t('settings.strikes.body')}
          control={(
            /* Sized to what it HOLDS, which is one or two digits. A full-width box for a number reads
               as a text field somebody forgot. */
            <input
              type="number"
              min={0}
              max={99}
              aria-label={t('settings.strikes.label')}
              value={strikes ?? String(driver.data?.strikes ?? 3)}
              onChange={(event) => setStrikes(event.target.value)}
              onBlur={() => {
                // 🔴 Passing through is not a write (REV3): `Number(null)` and `Number('')` are both 0,
                // the one value that means "never park". Nothing typed, a cleared box, or the value it
                // already holds all leave the config alone and put the held value back.
                const held = driver.data?.strikes ?? 3;
                const value = strikes === null || strikes.trim() === '' ? held : Number(strikes);
                if (!Number.isInteger(value) || value < 0 || value === held) {
                  setStrikes(null);
                  return;
                }

                setStrikesMutation.mutate({ strikes: value }, {
                  onSuccess: () => notify(t(value === 0 ? 'settings.strikes.never' : 'settings.strikes.set', { count: value })),
                  onError,
                });
              }}
              className="w-[4.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink"
            />
          )}
        >
          {/* Stated where the zero is, because zero is the one value whose consequence is invisible. */}
          {Number(strikes ?? driver.data?.strikes ?? 3) === 0 && (
            <p className="max-w-prose border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
              {t('settings.strikes.zero')}
            </p>
          )}
        </SettingRow>
      </Card>
    </>
  );
}

/**
 * Each repository's line and each workspace's default (WSR2): the driver's own resolution, and the
 * screen's half of `daoris driver line` (D50). Shell-only, because the guess is read off a checkout.
 */
function LineSettings({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const answer = useLines();
  const driver = useDriver();
  const setLine = useSetLine();
  useErrorNotify(answer.error, notify);

  return (
    <LineList
      lines={Array.isArray(answer.data?.lines) ? answer.data.lines : []}
      workspaceLines={driver.data?.workspaceLines ?? []}
      busy={setLine.isPending}
      onSet={(change) => setLine.mutate(change, {
        onSuccess: () => {
          const name = change.repository ?? change.workspace ?? '';
          notify(change.branch
            ? t('settings.lines.saved', { name, branch: change.branch })
            : t('settings.lines.cleared', { name }));
        },
        onError: failure(notify),
      })}
    />
  );
}

/**
 * How work lands in each repository and each workspace (WSR1, D87): the driver's own choice, and the
 * screen's half of `daoris driver landing` (D50). Shell-only, beside the lines it lands on.
 */
function LandingSettings({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const answer = useLines();
  const driver = useDriver();
  const setLanding = useSetLanding();

  return (
    <LandingList
      landings={Array.isArray(answer.data?.landings) ? answer.data.landings : []}
      workspaceLandings={driver.data?.workspaceLandings ?? []}
      busy={setLanding.isPending}
      onSet={(change) => setLanding.mutate(change, {
        onSuccess: () => {
          const name = change.repository ?? change.workspace ?? '';
          notify(!change.form
            ? t('settings.landing.cleared', { name })
            : change.form === 'branch'
              ? t('settings.landing.savedBranch', { name, pattern: change.pattern })
              : t('settings.landing.savedMerge', { name }));
        },
        onError: failure(notify),
      })}
    />
  );
}

/**
 * The clean-up (WSR3, D88): the driver's list of every session branch here, and the press. Shell-only,
 * because it is read off this machine's checkouts.
 */
function SweepSettings({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const plan = useSweepPlan();
  const sweep = useSweep();
  useErrorNotify(plan.error, notify);

  return (
    <SweepList
      branches={Array.isArray(plan.data?.branches) ? plan.data.branches : undefined}
      busy={sweep.isPending || plan.isFetching}
      onLook={() => void plan.refetch()}
      onClean={(only) => sweep.mutate(only, {
        onSuccess: (done) => notify(t('settings.sweep.done', { removed: done.removed, count: only.length })),
        onError: failure(notify),
      })}
    />
  );
}

/**
 * Which deployment serves each workspace here (D48 §5), over the home's `remotes.json`: the same
 * file `daoris remote` edits and the sync loop reads. A domain of its own since D75, under Workspace.
 */
function WiringSettings({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const wiring = useRemotes();
  const wire = useWireRemote();
  const unwire = useUnwireRemote();
  useErrorNotify(wiring.error, notify);

  const [workspace, setWorkspace] = useState('');
  const [url, setUrl] = useState('');
  const [key, setKey] = useState('');
  // The wiring form is one press away, not open on every visit: it is done once per machine per
  // deployment, and open it was the largest thing on a page most people come to for a checkbox.
  const [wiringOpen, setWiringOpen] = useState(false);

  const remotes = wiring.data?.remotes ?? [];
  const onError = failure(notify);

  const add = () => wire.mutate(
    { workspace: workspace.trim(), url: url.trim(), key: key.trim() },
    {
      onSuccess: (state) => {
        // "Wired" and "in effect" are two different things, and only here do they come apart: the
        // edit always lands in the FILE, but with the environment pair set no loader reads that file
        // (D48 §5). Saying only "wired" while the new row does not appear reads as an edit that
        // failed — so the sentence says what actually happened, using the answer's own flag rather
        // than this form's idea of the machine.
        notify(t(
          state.fromEnvironment ? 'settings.wiring.wiredButOverridden' : 'settings.wiring.wired',
          { workspace: workspace.trim() || 'default' }));
        // The key never lingers in a form's state once it has landed in the file.
        setWorkspace('');
        setUrl('');
        setKey('');
        setWiringOpen(false);
      },
      onError,
    });

  return (
      <Card id="settings-wiring" className="mt-3.5 scroll-mt-3">
        <SectionTitle>{t('settings.wiring.title')}</SectionTitle>
        <SettingRow
          label={t('settings.wiring.label')}
          hint={t('settings.wiring.hint')}
          why={t('settings.wiring.body')}
          control={wiring.data && (
            <PathText path={wiring.data.path} className="text-small text-ink-faint" />
          )}
        />

        {wiring.data?.fromEnvironment && (
          /* With the environment pair set, no loader reads the file — so the rows below are what is
             actually in effect, and saying which source decided is the difference between reporting
             the wiring and reporting this surface's own last edit. */
          <p className="mt-3 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
            {t('settings.wiring.fromEnvironment')}
          </p>
        )}

        {remotes.length === 0 ? (
          <Prose className="mt-3">{t('settings.wiring.none')}</Prose>
        ) : (
          <ul className="m-0 mt-3 list-none p-0">
            {remotes.map((remote) => (
              <li
                key={remote.workspace}
                className="flex flex-wrap items-baseline gap-3 border-t border-line py-2 first:border-t-0"
              >
                <Chip accent>{remote.workspace}</Chip>
                <PathText path={remote.url} className="text-small" />
                <Tip content={t('settings.wiring.keyTip')}>
                  <span className="font-mono text-small text-ink-faint">{remote.key}</span>
                </Tip>
                <Button
                  variant="ghost"
                  className="ml-auto"
                  disabled={unwire.isPending}
                  onClick={() => unwire.mutate({ workspace: remote.workspace }, {
                    onSuccess: () => notify(t('settings.wiring.unwired', { workspace: remote.workspace })),
                    onError,
                  })}
                >
                  {t('settings.wiring.remove')}
                </Button>
              </li>
            ))}
          </ul>
        )}

        {!wiringOpen ? (
          <Button
            variant="ghost"
            className="mt-3"
            disabled={wire.isPending}
            onClick={() => setWiringOpen(true)}
          >
            <Icon name="plus" size={13} />
            {t('settings.wiring.addTitle')}
          </Button>
        ) : (
        <div className="mt-3 border-t border-line pt-3">
          <SectionTitle level={3}>{t('settings.wiring.addTitle')}</SectionTitle>
          {/* Sized to what the fields HOLD, not to the column they sit in. Three equal thirds of a
              72rem card gave a 570px box to the word "default"; a workspace name is short, a
              deployment URL is long, and a key is in between — so the widths say so. */}
          <div className="mt-2 grid max-w-[48rem] gap-2 md:grid-cols-[10rem_minmax(0,1fr)_12rem]">
            <label className="grid gap-1 text-small text-ink-faint">
              {t('settings.wiring.workspace')}
              <input
                value={workspace}
                onChange={(event) => setWorkspace(event.target.value)}
                placeholder={t('settings.wiring.workspacePlaceholder')}
                className="rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
              />
            </label>
            <label className="grid gap-1 text-small text-ink-faint">
              {t('settings.wiring.url')}
              <input
                value={url}
                onChange={(event) => setUrl(event.target.value)}
                placeholder="https://…"
                className="rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-body text-ink"
              />
            </label>
            <label className="grid gap-1 text-small text-ink-faint">
              {t('settings.wiring.key')}
              <input
                value={key}
                type="password"
                onChange={(event) => setKey(event.target.value)}
                placeholder="dk_…"
                className="rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-body text-ink"
              />
            </label>
          </div>
          <Prose className="mt-2 text-small">{t('settings.wiring.keyBody')}</Prose>
          <div className="mt-3 flex gap-2">
            <Button
              variant="primary"
              disabled={!url.trim() || !key.trim() || wire.isPending}
              onClick={add}
            >
              {t('settings.wiring.add')}
            </Button>
            <Button variant="ghost" onClick={() => setWiringOpen(false)}>{t('settings.wiring.cancel')}</Button>
          </div>
        </div>
        )}
      </Card>
  );
}

/**
 * Daoris's browser (CHR5, CHR7): its favorites, which it shows in a Daoris folder on its bookmarks
 * bar, and whether other software's Chrome extensions are offered or refused. The same two files
 * `daoris browser` edits (D50), which `daoris-browser` reads each time it starts, so the page says
 * that an edit shows at the next start rather than implying it shows now.
 */
function BrowserDomain({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const state = useBrowserSettings();
  const add = useAddFavorite();
  const remove = useRemoveFavorite();
  const setExtensions = useSetExtensions();
  const setBrowser = useSetBrowser();
  useErrorNotify(state.error, notify);
  const onError = failure(notify);

  const [address, setAddress] = useState('');
  const [title, setTitle] = useState('');
  const data = state.data;

  const keep = () => add.mutate(
    { address: address.trim(), ...(title.trim() ? { title: title.trim() } : {}) },
    {
      onSuccess: () => {
        notify(t('settings.browser.favorites.added', { title: title.trim() || address.trim() }));
        setAddress('');
        setTitle('');
      },
      onError,
    });

  return (
    <>
      <Prose className="mb-3">{t('settings.browser.nextStart')}</Prose>

      {/* Which browser (BRW12): Daoris's own, or the person's Edge on a profile of Daoris's. */}
      <Card id="settings-which-browser" className="scroll-mt-3">
        <SectionTitle>{t('settings.browser.which.title')}</SectionTitle>
        <SettingRow
          label={t('settings.browser.which.label')}
          hint={data?.browser === 'edge'
            ? t('settings.browser.which.hintEdge', { profile: data.edgeProfile })
            : t('settings.browser.which.hintDaoris')}
          control={data && (
            <Segmented
              label={t('settings.browser.which.label')}
              value={data.browser}
              options={[
                { value: 'daoris', label: t('settings.browser.which.daoris') },
                { value: 'edge', label: t('settings.browser.which.edge') },
              ]}
              onChange={(browser) => setBrowser.mutate({ browser }, {
                onSuccess: () => notify(t('settings.browser.which.set', {
                  choice: t(browser === 'edge' ? 'settings.browser.which.edge' : 'settings.browser.which.daoris'),
                })),
                onError,
              })}
            />
          )}
        />
        {data && !data.edgeFound && (
          <p className="mt-3 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
            {t('settings.browser.which.noEdge')}
          </p>
        )}
        {data?.browser === 'edge' && (
          <Prose className="mt-3 text-small">{t('settings.browser.ownOnly')}</Prose>
        )}
      </Card>

      <Card id="settings-favorites" className="mt-3.5 scroll-mt-3">
        <SectionTitle>{t('settings.browser.favorites.title')}</SectionTitle>
        <SettingRow
          label={t('settings.browser.favorites.label')}
          hint={t('settings.browser.favorites.hint')}
          control={data && <PathText path={data.favoritesPath} className="text-small text-ink-faint" />}
        />
        {data?.favoritesProblem && (
          <p className="mt-3 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
            {t('settings.browser.unreadable', { file: data.favoritesPath, problem: data.favoritesProblem })}
          </p>
        )}
        {data && data.favorites.length === 0 && !data.favoritesProblem && (
          <Prose className="mt-3">{t('settings.browser.favorites.none')}</Prose>
        )}
        {data && data.favorites.length > 0 && (
          <ul className="m-0 mt-3 list-none p-0">
            {data.favorites.map((favorite) => (
              <li
                key={favorite.url}
                className="flex flex-wrap items-baseline gap-3 border-t border-line py-2 first:border-t-0"
              >
                <span className="text-body text-ink">{favorite.title}</span>
                <PathText path={favorite.url} className="text-small text-ink-faint" />
                <Button
                  variant="ghost"
                  className="ml-auto"
                  disabled={remove.isPending}
                  onClick={() => remove.mutate({ address: favorite.url }, {
                    onSuccess: () => notify(t('settings.browser.favorites.removed', { url: favorite.url })),
                    onError,
                  })}
                >
                  {t('settings.browser.favorites.remove')}
                </Button>
              </li>
            ))}
          </ul>
        )}
        {/* The rule runs the card's width, as the list's rows do; the fields are sized to what they
            hold, an address long and a title short. */}
        <div className="mt-3 border-t border-line pt-3">
        <div className="grid max-w-[48rem] items-end gap-2 md:grid-cols-[minmax(0,1fr)_12rem_auto]">
          <label className="grid gap-1 text-small text-ink-faint">
            {t('settings.browser.favorites.address')}
            <input
              value={address}
              onChange={(event) => setAddress(event.target.value)}
              placeholder="https://…"
              className="rounded-control border border-line-strong bg-raised px-2.5 py-1.5 font-mono text-body text-ink"
            />
          </label>
          <label className="grid gap-1 text-small text-ink-faint">
            {t('settings.browser.favorites.titleField')}
            <input
              value={title}
              onChange={(event) => setTitle(event.target.value)}
              placeholder={t('settings.browser.favorites.titlePlaceholder')}
              className="rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
            />
          </label>
          <Button variant="primary" disabled={!address.trim() || add.isPending} onClick={keep}>
            <Icon name="plus" size={13} />
            {t('settings.browser.favorites.add')}
          </Button>
        </div>
        </div>
      </Card>

      <Card id="settings-extensions" className="mt-3.5 scroll-mt-3">
        <SectionTitle>{t('settings.browser.extensions.title')}</SectionTitle>
        <SettingRow
          label={t('settings.browser.extensions.label')}
          hint={t('settings.browser.extensions.hint')}
          control={data && (
            <Segmented
              label={t('settings.browser.extensions.label')}
              value={data.extensions}
              options={[
                { value: 'offer', label: t('settings.browser.extensions.offer') },
                { value: 'refuse', label: t('settings.browser.extensions.refuse') },
              ]}
              onChange={(extensions) => setExtensions.mutate({ extensions }, {
                onSuccess: () => notify(t('settings.browser.extensions.set', {
                  choice: t(extensions === 'refuse' ? 'settings.browser.extensions.refused' : 'settings.browser.extensions.offered'),
                })),
                onError,
              })}
            />
          )}
        />
        {data?.settingsProblem && (
          <p className="mt-3 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
            {t('settings.browser.unreadable', { file: data.settingsPath, problem: data.settingsProblem })}
          </p>
        )}
      </Card>
    </>
  );
}

/**
 * What an agent Daoris starts may do (PERM1, D72) — the machine's `permissions.json`, the file the
 * driver composes each spawn's rules from and `daoris agent rules` edits (D50).
 *
 * **The scopes a rule can reach are the registry's**: its circles and its repositories, by the names
 * the driver composes against. A refusal is the driver's sentence, verbatim.
 */
function Rules({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const answer = useRules();
  const act = useRuleAction();
  const settle = useRuleProposal();
  const registry = useRegistry('machine');
  useErrorNotify(answer.error, notify);

  // An older shell has never heard of the question: the card is absent rather than the page blank.
  const rules = answer.data && Array.isArray(answer.data.defaults) && Array.isArray(answer.data.scopes) ? answer.data : null;
  if (!rules) return null;

  const rows = registry.data ?? [];
  const circles = workspacesOf(rows);
  const repositories = rows.map((row) => row.repository).sort();
  const where = (scope: string, name: string | undefined) => scope === 'machine'
    ? t('settings.rules.scopeMachine')
    : t(scope === 'workspace' ? 'settings.rules.scopeWorkspace' : 'settings.rules.scopeRepository', { name: name ?? '' });
  const failed = failure(notify);

  return (
    <AgentRules
      rules={rules}
      circles={circles}
      repositories={repositories}
      busy={act.isPending || settle.isPending}
      onAnswer={(id, accept) => {
        const proposal = rules.proposals?.find((one) => one.id === id);
        settle.mutate({ id, accept }, {
          onSuccess: () => notify(t(accept ? 'settings.rules.proposals.accepted' : 'settings.rules.proposals.declined', {
            change: proposal ? proposalChange(proposal) : `#${id}`,
          })),
          onError: failed,
        });
      }}
      onSwitchDefault={(id, on) => act.mutate({ action: 'default', id, on }, {
        onSuccess: () => notify(t('settings.rules.switched', { id, state: t(on ? 'settings.rules.on' : 'settings.rules.off') })),
        onError: failed,
      })}
      onRemove={({ scope, name, rule }) => act.mutate({ action: 'remove', rule, scope, name }, {
        onSuccess: () => notify(t('settings.rules.removed', { rule, where: where(scope, name) })),
        onError: failed,
      })}
      onAdd={({ list, rule, scope, name }, added) => act.mutate({ action: 'add', list, rule, scope, name }, {
        onSuccess: () => {
          notify(t('settings.rules.added', { rule, list: t(`settings.rules.list.${list}`), where: where(scope, name) }));
          added();
        },
        onError: failed,
      })}
    />
  );
}

/**
 * What a start in each workspace would run on (MAP1b) — beside the agents, because it is their
 * accounts and pins resolved: the workspace's default, then the machine's, then the agent's own.
 *
 * **Read from the driver, never recomputed here.** The answer is `SelectAsync`'s, so the page cannot
 * show an account the loop would not take; this organism only names the circles and the accounts.
 */
/**
 * Every workspace and what it holds (D75 §3), from the same unscoped registry answer the Workspace
 * menu reads, so the two cannot disagree. For everyone: which repositories share a workspace is what
 * a browser is told too, and no machine path is in it.
 */
function WorkspaceList() {
  const { t } = useTranslation();
  const { workspace: scoped } = useScope();
  const holdings = useWorkspaceHoldings();
  const list = holdings.data ?? [];

  return (
    <Card>
      <SectionTitle>{t('settings.workspaces.title', { count: list.length })}</SectionTitle>
      {holdings.data && list.length === 0 && (
        <Prose className="text-small">{t('settings.workspaces.none')}</Prose>
      )}
      <ul className="m-0 list-none p-0">
        {list.map((workspace) => (
          <li key={workspace.name} aria-label={workspace.name} className="border-t border-line py-2 first:border-t-0">
            <div className="flex flex-wrap items-baseline gap-2">
              <span className="text-body font-medium text-ink">{workspace.name}</span>
              {scoped === workspace.name && <Chip accent>{t('settings.workspaces.inView')}</Chip>}
              <span className="text-small text-ink-faint">
                {t('settings.workspaces.repositories', { count: workspace.repositories })}
              </span>
            </div>
            <p title={workspace.members.join(' · ')} className="m-0 mt-0.5 truncate text-small text-ink-soft">
              {workspace.members.join(' · ')}
            </p>
          </li>
        ))}
      </ul>
    </Card>
  );
}

function Starts({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const registry = useRegistry('machine');
  const roster = useHarnesses();
  const workspaces = workspacesOf(registry.data ?? []);
  const answer = useStarts(workspaces);
  useErrorNotify(answer.error, notify);

  // An older shell has never heard of the question: the card is absent rather than the page blank.
  const starts = Array.isArray(answer.data?.starts) ? answer.data.starts : null;
  if (!starts || starts.length === 0) return null;

  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];

  return (
    <Card className="mt-3.5">
      <SectionTitle>{t('wiring.title')}</SectionTitle>
      <Prose className="mb-3 mt-0 text-small text-ink-soft">{t('wiring.body')}</Prose>
      <StartWiringList starts={starts} nameOf={namer(t, harnesses)} />
    </Card>
  );
}

/**
 * This machine's plugins (D64): one row per folder under the home's `plugins/` — what it declares,
 * what it speaks on, whether it is running, and why it contributes nothing when it does not.
 *
 * **Two doors, one folder** (D50): the switch is a row in `plugins.json` that `daoris plugin
 * enable|disable` edits too, and Remove takes the install folder while naming what the plugin kept.
 * **No plugin code runs in this page** — a plugin's word reaches the console under `plugin:<id>`.
 */
function Plugins({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const catalog = usePlugins();
  const act = usePluginAction();
  useErrorNotify(catalog.error, notify);

  // Defensive about the shape, for SES1's reason: a shell older than this surface answers something
  // else entirely to a question it has never heard, and the page must not go blank for it.
  const plugins = Array.isArray(catalog.data?.plugins) ? catalog.data.plugins : null;
  if (!catalog.data || !plugins) return null;

  const run = (id: string, action: 'enable' | 'disable' | 'remove') => act.mutate({ id, action }, {
    onSuccess: (result) => notify(t(
      action === 'remove'
        ? (result.data ? 'plugin.removedKept' : 'plugin.removed')
        : action === 'enable' ? 'plugin.enabled' : 'plugin.disabled',
      { id, data: result.data ?? '' })),
    onError: failure(notify),
  });

  const what = (plugin: (typeof plugins)[number]) => {
    const parts = [
      plugin.harnesses.length > 0 ? t('plugin.declares', { harnesses: plugin.harnesses.join(', ') }) : null,
      plugin.points.length > 0 ? t('plugin.speaks', { points: plugin.points.join(', ') }) : null,
    ].filter(Boolean);
    return parts.length > 0 ? parts.join('; ') : t('plugin.quiet');
  };

  return (
    <Card className="mt-3.5">
      <SettingRow
        label={t('plugin.folder')}
        hint={t('plugin.terminal')}
        why={t('plugin.body')}
        control={<PathText path={catalog.data.folder} className="text-small text-ink-faint" />}
      />

      {plugins.length === 0 ? (
        <Prose className="mt-3 text-small"><Inline text={t('plugin.none')} /></Prose>
      ) : plugins.map((plugin) => (
        <SettingRow
          key={plugin.id}
          label={(
            <span className="flex flex-wrap items-center gap-2">
              <span>{plugin.name}</span>
              {plugin.version && <span className="font-mono text-small text-ink-faint">{plugin.version}</span>}
              {plugin.running && <Pill tone="done">{t('plugin.running')}</Pill>}
              {!plugin.enabled && <Pill tone="neutral">{t('plugin.off')}</Pill>}
            </span>
          )}
          hint={(
            <span className="flex flex-col gap-0.5">
              {/* A refused plugin declares nothing BECAUSE it was refused — saying "declares nothing"
                  above the sentence that says why would be the same fact twice, the second time
                  wrong. Its sentence stands alone beneath. */}
              {!plugin.problem && <span>{what(plugin)}{plugin.description ? ` — ${plugin.description}` : ''}</span>}
              <span className="truncate font-mono text-meta">{plugin.folder}</span>
            </span>
          )}
          control={(
            <>
              <Button
                variant="ghost"
                disabled={act.isPending}
                onClick={() => run(plugin.id, plugin.enabled ? 'disable' : 'enable')}
              >
                {t(plugin.enabled ? 'plugin.disable' : 'plugin.enable')}
              </Button>
              <Tip content={t('plugin.forgetTip')}>
                <Button variant="ghost" disabled={act.isPending} onClick={() => run(plugin.id, 'remove')}>
                  {t('plugin.forget')}
                </Button>
              </Tip>
            </>
          )}
        >
          {/* The driver's own sentence, verbatim — a version this build does not speak, a
              conflict naming both sides, a manifest that would not parse. Content, not chrome. */}
          {plugin.problem && (
            <p className="max-w-prose border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
              <Inline text={plugin.problem} />
            </p>
          )}
        </SettingRow>
      ))}
    </Card>
  );
}

/**
 * The toolchain roster (D49 §4): which harnesses this machine has, and which accounts they hold.
 *
 * **Detection is free; acting is the person's click.** The versions and login states below come from
 * asking each tool its own reporting commands — read-only, no account, no network of Daoris's own.
 * Installing, updating and logging in each run that harness's OWN mechanism, only when pressed, and
 * never mid-session: a tool that changed under a running loop is a moving target nobody diffed.
 *
 * **A sign-in stays the tool's.** A profile is an isolated configuration home whose LOCATION Daoris
 * owns; the credential inside it is put there by the harness's own login flow and stays in the
 * harness's own store, under the person's OS account. Nothing on this surface reads one, and there
 * is nowhere to type one. The single field for a secret is an API key's (D67 §1), behind a press,
 * sent once and shown back only as its last four characters.
 */
/** What a DOOR does, and so what streams under it. */
const DOOR_ACTIONS = ['install', 'update', 'pin', 'unpin'] as const;

function HarnessRoster({ notify }: { notify: Notify }) {
  // The account a Remove has been pressed on once, by its directory — the second press is what
  // deletes it (D66 §3), and only on the row that asked.
  const [removing, setRemoving] = useState<string | null>(null);
  // The account whose own model and effort are open to change (AGT6), by its directory — one at a
  // time, closed until asked for, like every rare form here.
  const [tuning, setTuning] = useState<string | null>(null);
  const { t } = useTranslation();
  const roster = useHarnesses();
  const refresh = useRefreshHarnesses();
  const act = useHarnessAction();
  const tune = useSetAgentSettings();
  // What each account has carried (TOOL3). Beside the roster because it is about the same accounts.
  const usage = useUsage();
  // The circles this machine has, so an account can be chosen for one (D49 §4) — the terminal
  // could already do it (`daoris agent profile default … --workspace`), and the screen could not.
  const registry = useRegistry('machine');
  // Every circle, the unnamed `default` included: the CLI sets that circle's account too.
  const workspaces = workspacesOf(registry.data ?? []);
  useErrorNotify(roster.error, notify);

  // Which action is running, so its console can be shown under the harness that is doing it — one at
  // a time by construction, two installers racing over one PATH being nothing to make easy. Which
  // ACCOUNT a sign-in is for, so it lands on that row (2026-09-23), and which tool a sign-in to
  // another account runs for (D66 §3). Held above every view (SIGNIN1), so a sign-in outlives
  // leaving this domain: its panel is here on the way back, and its end is said wherever you are.
  const { running, runningProfile, signingInNew, busy: acting, run } = useHarnessRun();
  const busy = acting || act.isPending;
  // Which tool has its API-key field open, and what is typed in it (AGT3, D67 §1). The draft lives
  // here only until it is sent, and is dropped the moment it is — sent or taken back.
  const [keying, setKeying] = useState<string | null>(null);
  const [keyDraft, setKeyDraft] = useState('');
  // What a person calls an account: who signed in, else the key's handle, else the directory's name.
  const named = (profile: { name: string; account?: string | null; key?: string | null }) =>
    profile.account ?? (profile.key ? t('harness.profile.keyName', { handle: profile.key }) : profile.name);
  const closeKey = () => {
    setKeying(null);
    setKeyDraft('');
  };
  const addKey = (harness: string) => {
    const key = keyDraft.trim();
    if (!key) return;
    closeKey();
    act.mutate({ harness, action: 'key-add', key }, {
      onSuccess: (result) => notify(t('harness.profile.keyAdded', { profile: result.profile, handle: result.key })),
      onError: failure(notify),
    });
  };
  // The version being typed per harness (TOOL2). Local to the form: a pin only exists once the
  // install behind it succeeded, so there is nothing to remember until then.
  const [pinning, setPinning] = useState<Record<string, string>>({});

  /**
   * Whether a harness has its version form open.
   *
   * @remarks
   * 🔴 Accounts were hard to read, and the screenshot said why: **the rare forms were
   * always open, on every harness** — five harnesses meant five empty boxes and five copies of the
   * same paragraph, and the accounts, the thing a person came for, were a thin row between them.
   */
  const [opened, setOpened] = useState<Record<string, 'pin' | null>>({});
  const open = (harness: string, which: 'pin') =>
    setOpened((held) => ({ ...held, [harness]: held[harness] === which ? null : which }));

  // Defensive about the shape, deliberately, and for the reason SES1 wrote down: a shell older than
  // this surface answers something else entirely to a request it has never heard of. A machine's
  // settings page must not go blank because one card asked a question the host cannot answer.
  const answered = roster.data;
  const harnesses = Array.isArray(answered?.harnesses) ? answered.harnesses : null;
  // Same defensiveness, and the same reason: an older shell has never heard of this question.
  const accounts = Array.isArray(usage.data?.accounts) ? usage.data.accounts : [];
  if (!answered || !harnesses) return null;
  const nameOf = namer(t, harnesses);

  return (
    <Card className="mt-3.5">
      <SectionTitle>{t('harness.title')}</SectionTitle>
      {/* One line each, and the rest on the glyph: what a tool is here, and what an account is —
          said ONCE, where the second used to be repeated under every harness's add form. */}
      <SettingRow
        label={t('harness.body')}
        hint={t('harness.secrets')}
        why={t('harness.profile.note')}
        control={<PathText path={answered.settingsPath} className="text-small text-ink-faint" />}
      />

      {/* 🔴 A card per TOOL, and the adapters are its ways in: "harness account" and `*-acp` hid
          what the page was for. The reference project is a reference — take its design and its
          logic, not its words.

          The surface had been listing four adapters as four things to have opinions about. A person
          has one Claude Code and one account for it; whether Daoris holds the session over a pipe or
          over the protocol is Daoris's business, not a second tool. `byTool` reads that off
          `accountOf` and `wire`, both of which have said it all along. */}
      {byTool(harnesses).map((tool) => (
        <div
          key={tool.name}
          className="mt-3 rounded-card border border-line bg-page/60 p-3 first:mt-3.5"
        >
          <header className="flex flex-wrap items-center gap-2">
            {/* What a person calls it, and whose it is (AGT1) — `dsh` meant nothing to the owner
                until it said DeepSeek. The id a terminal types is on each door below. */}
            <span className="text-body font-semibold text-ink">{tool.product ?? tool.name}</span>
            {tool.maker && <span className="text-small text-ink-faint">{tool.maker}</span>}
            {tool.present
              ? <Pill tone="done">{t('harness.installed')}</Pill>
              : <Pill tone="neutral">{t('harness.absent')}</Pill>}
            {tool.doors.some((door) => door.harness === answered.adapter)
              && <Chip accent>{t('harness.spawns')}</Chip>}
          </header>

          {/* The ACCOUNTS, at the tool where they belong. They are the reason a person opened this
              card, and they had been a flat baseline row per adapter — so one account read as two
              whenever a tool had two doors, and the widest thing on the row was a seventy-character
              directory. The name leads now, its state is beside it, where the sessions actually go
              is stated rather than implied, and the directory is one truncated line underneath. */}
          <p className="mt-3 text-small font-semibold text-ink-soft">{t('harness.accounts')}</p>
          <ul className="m-0 mt-1 list-none p-0">
            {/* 🔴 The account a person actually HAS leads: the tool's own configuration home. A
                machine with no named profile read "No accounts" while its owner was logged in —
                and nothing said that sessions were running as that login. The state is the
                tool's own answer about its own home, read-only; Daoris never logs into it, so
                there is no button for that here, and the row says whose business it is. */}
            <li className="flex flex-wrap items-center gap-x-3 gap-y-1 py-2">
              <span className="flex min-w-0 flex-1 basis-56 flex-col gap-0.5">
                <span className="flex flex-wrap items-center gap-2">
                  <Icon name="account" size={13} className="text-ink-faint" />
                  {/* Who, when the tool says (D66 §3) — a person knows an account by who it is. */}
                  <span className="text-body font-medium text-ink">{tool.ownAccount ?? t('harness.own')}</span>
                  {/* A name that repeats says which it is: signed in as the same person here and
                      in an account made in Daoris, the two rows read as one fact stated twice. */}
                  {tool.ownAccount && tool.accounts.some((profile) => named(profile) === tool.ownAccount) && (
                    <span className="text-meta text-ink-faint">{t('harness.own')}</span>
                  )}
                  {tool.present && tool.ownLogin !== 'unknown' && (
                    <Pill tone={tool.ownLogin === 'in' ? 'done' : 'neutral'}>
                      {t(`harness.login.${tool.ownLogin}`)}
                    </Pill>
                  )}
                  {tool.machineDefault === null && (
                    <Chip accent>{t('harness.profile.sessionsUse')}</Chip>
                  )}
                </span>
                <span className="text-meta text-ink-faint">{t('harness.ownHome')}</span>
              </span>
              <div className="ml-auto flex shrink-0 items-center gap-1">
                {/* Naming NO profile clears the default — "use the tool's own home again". */}
                {tool.machineDefault !== null && (
                  <Button
                    variant="ghost"
                    disabled={busy}
                    onClick={() => run(tool.doors[0]!.harness, 'profile-default')}
                  >
                    {t('harness.profile.use')}
                  </Button>
                )}
                {workspaces.length > 0 && (
                  <SelectField
                    value=""
                    onChange={(workspace) =>
                      run(tool.doors[0]!.harness, 'profile-default', undefined, undefined, workspace)}
                    options={workspaces.map((workspace) => ({ value: workspace, label: workspace }))}
                    placeholder={t('harness.profile.useForPlaceholder')}
                    ariaLabel={t('harness.profile.useFor', { profile: t('harness.own') })}
                  />
                )}
              </div>
            </li>
            {tool.accounts.map((profile) => (
                <li
                  key={profile.home}
                  className="flex flex-wrap items-center gap-x-3 gap-y-1 border-t border-line py-2"
                >
                  <span className="flex min-w-0 flex-1 basis-56 flex-col gap-0.5">
                    <span className="flex flex-wrap items-center gap-2">
                      <Icon name="account" size={13} className="text-ink-faint" />
                      {/* Who is signed in, when the tool says (D66 §3): an account made by signing
                          in is `account-2` on disk, and nobody knows it by that. The directory's
                          name is still on the line below, inside its path, for a terminal. */}
                      <span className="text-body font-medium text-ink">{named(profile)}</span>
                      {/* 🔴 A key account is never shown as "logged in" (AGT3). Measured: the tool
                          says logged in for ANY key, a wrong one included, and the first request is
                          where a bad key is refused. The pill says what is known. */}
                      {profile.key ? (
                        <Tip content={t('harness.login.keyedTip')}>
                          <Pill tone="neutral">{t('harness.login.keyed')}</Pill>
                        </Tip>
                      ) : (
                        <Pill tone={profile.login === 'in' ? 'done' : 'neutral'}>
                          {t(`harness.login.${profile.login}`)}
                        </Pill>
                      )}
                      {/* States the CONSEQUENCE, not the setting: "this machine's default" is a fact
                          about a config file, and what a person wants is which account the next
                          session runs as — the same fact worded as an answer. */}
                      {tool.machineDefault === profile.name && (
                        <Chip accent>{t('harness.profile.sessionsUse')}</Chip>
                      )}
                      {/* And which circles run as it (D49 §4) — one chip per circle, the same fact
                          worded the same way. */}
                      {tool.workspaceDefaults
                        .filter((circle) => circle.profile === profile.name)
                        .map((circle) => (
                          <Chip accent key={circle.workspace}>
                            {t('harness.profile.workspaceUses', { workspace: circle.workspace })}
                          </Chip>
                        ))}
                    </span>
                    <Tip content={t('harness.homeTip')}>
                      <span className="truncate font-mono text-meta text-ink-faint">{profile.home}</span>
                    </Tip>
                    {/* What the account runs on, by the tool's own file under it (AGT6), where Daoris
                        knows that file. The owner could see a session's model only inside the session. */}
                    {profile.settings && <AccountSettingsSummary settings={profile.settings} />}
                    {/* 🔴 What the next step IS and what it will do, on the row that needs it. After
                        "Add" there was a name, a "not logged in" pill and a button, and nothing
                        about the browser window about to open or where the output would go. */}
                    {profile.login !== 'in' && (
                      <span className="text-meta text-ink-faint">{t('harness.login.hint')}</span>
                    )}
                  </span>
                  <div className="ml-auto flex shrink-0 items-center gap-1">
                    {/* Logging in is the one thing here that is a step in a task rather than a
                        preference, so it is the one that looks like a button. It runs against the
                        account-owning door, because that is the tool that HAS the login flow. */}
                    {/* A key account is signed in by its key (AGT3): there is no sign-in to offer. */}
                    {tool.doors[0]!.signsIn !== false && !profile.key && (
                      <Button
                        variant={profile.login === 'in' ? 'ghost' : 'default'}
                        disabled={busy || !tool.present}
                        onClick={() => run(tool.doors[0]!.harness, 'login', profile.name)}
                      >
                        {t(profile.login === 'in' ? 'harness.login.again' : 'harness.login.action')}
                      </Button>
                    )}
                    {tool.machineDefault !== profile.name && (
                      <Button
                        variant="ghost"
                        disabled={busy}
                        onClick={() => run(tool.doors[0]!.harness, 'profile-default', profile.name)}
                      >
                        {t('harness.profile.use')}
                      </Button>
                    )}
                    {/* The account's own model and effort (AGT6, D98) — offered only where the tool's
                        settings are known, which is the rule every control on this row follows. */}
                    {tool.settingsChoices && profile.settings && tuning !== profile.home && (
                      <Button variant="ghost" disabled={busy} onClick={() => setTuning(profile.home)}>
                        {t('harness.settings.open')}
                      </Button>
                    )}
                    {/* 🔴 Remove REMOVES (D66 §3). "Forget" un-pointed the account and kept any
                        directory the tool would not call signed out, so a removed account stayed
                        listed and signed in — the owner's report. It deletes the account's
                        directory, sign-in and all, so the first press only asks. */}
                    {removing !== profile.home && (
                      <Button
                        variant="ghost"
                        disabled={busy}
                        onClick={() => setRemoving(profile.home)}
                      >
                        <Icon name="remove" size={13} />
                        {t('harness.profile.remove')}
                      </Button>
                    )}
                    {workspaces.length > 0 && (
                      <SelectField
                        value=""
                        onChange={(workspace) =>
                          run(tool.doors[0]!.harness, 'profile-default', profile.name, undefined, workspace)}
                        options={workspaces.map((workspace) => ({ value: workspace, label: workspace }))}
                        placeholder={t('harness.profile.useForPlaceholder')}
                        ariaLabel={t('harness.profile.useFor', { profile: named(profile) })}
                      />
                    )}
                  </div>
                  {removing === profile.home && (
                    <div
                      role="group"
                      aria-label={t('harness.profile.removeTitle', { account: named(profile) })}
                      className="flex basis-full flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
                    >
                      <span className="min-w-0 flex-1 basis-64 text-small text-ink-soft">
                        {t('harness.profile.removeConfirm')}
                      </span>
                      <Button
                        variant="danger"
                        disabled={busy}
                        onClick={() => {
                          setRemoving(null);
                          run(tool.doors[0]!.harness, 'profile-remove', profile.name);
                        }}
                      >
                        {t('harness.profile.removeMeanIt')}
                      </Button>
                      <Button variant="ghost" onClick={() => setRemoving(null)}>
                        {t('common.cancel')}
                      </Button>
                    </div>
                  )}
                  {tuning === profile.home && tool.settingsChoices && profile.settings && (
                    <AccountSettingsForm
                      harness={tool.doors[0]!.harness}
                      account={profile.name}
                      accountLabel={named(profile)}
                      settings={profile.settings}
                      choices={tool.settingsChoices}
                      busy={tune.isPending}
                      onSave={(change) => tune.mutate({ harness: tool.doors[0]!.harness, profile: profile.name, ...change }, {
                        onSuccess: () => {
                          setTuning(null);
                          notify(t('harness.settings.saved', { account: named(profile) }));
                        },
                        onError: failure(notify),
                      })}
                      onCancel={() => setTuning(null)}
                    />
                  )}
                  {/* Signing in happens HERE, on the account it is for. */}
                  {running === `${tool.doors[0]!.harness}:login` && runningProfile === profile.name && (
                    <SignIn id={running} harness={tool.doors[0]!.harness} profile={named(profile)} />
                  )}
                </li>
              ))}
            </ul>
          {/* A tool whose own settings Daoris does not know is offered none, and says so once (AGT6):
              inventing its keys would be a guess written into somebody else's file. */}
          {tool.settingsChoices === null && (
            <p className="m-0 mt-1 text-meta text-ink-faint">
              {t('harness.settings.unknown', { tool: tool.product ?? tool.name })}
            </p>
          )}

          {/* 🔴 An account is made by SIGNING IN (D66 §3). There was a name box first — a name
              typed before anyone knew whose account it was — then a login as a second step. Now
              one press runs the tool's own sign-in into a fresh account, which is kept only if the
              sign-in finishes and is listed by who signed in. */}
          {signingInNew === tool.doors[0]!.harness && (
            <SignIn
              id={`${tool.doors[0]!.harness}:login-new`}
              harness={tool.doors[0]!.harness}
              action="login-new"
              tool={tool.product ?? tool.name}
            />
          )}

          {/* 🔴 An account that is an API key (AGT3, D67 §1). One field,
              behind a press, only on an agent that takes a key. The draft is dropped the moment it
              is sent, and the page is told back only the key's last four characters. */}
          {keying === tool.name && (
            <form
              className="mt-2 flex flex-wrap items-center gap-2"
              onSubmit={(event) => {
                event.preventDefault();
                addKey(tool.doors[0]!.harness);
              }}
            >
              <input
                autoFocus
                type="password"
                autoComplete="off"
                spellCheck={false}
                value={keyDraft}
                onChange={(event) => setKeyDraft(event.target.value)}
                aria-label={t('harness.profile.keyLabel', { tool: tool.product ?? tool.name })}
                placeholder={t('harness.profile.keyPlaceholder')}
                className="min-w-72 flex-1 rounded-control border border-line-strong bg-sunken px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
              />
              <Button type="submit" variant="primary" disabled={busy || !keyDraft.trim()}>
                {t('harness.profile.keySave')}
              </Button>
              <Button variant="ghost" onClick={closeKey}>{t('common.cancel')}</Button>
              <span className="basis-full text-meta text-ink-faint">{t('harness.profile.keyHint')}</span>
            </form>
          )}

          {signingInNew !== tool.doors[0]!.harness && keying !== tool.name && (
            <div className="mt-2 flex flex-wrap items-center gap-1">
              {tool.doors[0]!.signsIn !== false && (
                <Button
                  variant="ghost"
                  disabled={busy || !tool.present}
                  onClick={() => run(tool.doors[0]!.harness, 'login-new')}
                >
                  <Icon name="plus" size={13} />
                  {t('harness.profile.signInNew')}
                </Button>
              )}
              {tool.doors[0]!.takesKey && (
                <Button
                  variant="ghost"
                  disabled={busy || !tool.present}
                  onClick={() => setKeying(tool.name)}
                >
                  <Icon name="account" size={13} />
                  {t('harness.profile.addKey')}
                </Button>
              )}
            </div>
          )}

          {/* 🔴 The ways in, beneath the tool rather than beside it. Each is installed, versioned
              and pinned separately — they are different packages — which is exactly why they had
              looked like different tools. Named `harness` below because that is what `driver.json`
              calls this and what `daoris driver adapter` takes. */}
          <p className="mt-3 text-small font-semibold text-ink-soft">{t('harness.doors')}</p>
          {tool.doors.map((harness) => (
            <div key={harness.harness} className="mt-1 border-t border-line pt-2">
              <header className="flex flex-wrap items-center gap-2">
                <Pill tone="neutral">
                  {t(harness.wire === 'acp' ? 'harness.wire.acp' : 'harness.wire.pipe')}
                </Pill>
                <span className="font-mono text-small text-ink">{harness.harness}</span>
                {harness.present
                  ? <span className="font-mono text-small text-ink-faint">{harness.version}</span>
                  : <span className="text-small text-ink-faint">{t('harness.absent')}</span>}
                {/* Where a declared door came from (D64): the plugin's folder is where its command
                    and its posture live, and a person asking "why is this here" is asking that. */}
                {harness.plugin && <Chip>{t('harness.declaredBy', { plugin: harness.plugin })}</Chip>}

                <span className="ml-auto flex gap-2">
                  {!harness.present && (
                    <Button disabled={busy} onClick={() => run(harness.harness, 'install')}>
                      {t('harness.install')}
                    </Button>
                  )}
                  {/* 🔴 USE1a: offered only where it does something, and saying which it does.
                      It was offered on every door, and on a pinned one it could only be refused. */}
                  {harness.present && (harness.updates === 'pin' || harness.updates === 'tool') && (
                    <Tip content={t(harness.updates === 'pin' ? 'harness.update.pinTip' : 'harness.update.toolTip')}>
                      <Button variant="ghost" disabled={busy} onClick={() => run(harness.harness, 'update')}>
                        {t('harness.update')}
                      </Button>
                    </Tip>
                  )}
                </span>
              </header>

          {/* The absence names what it is, rather than leaving a person to guess at a blank row.
              🔴 CLAMPED, with the whole of it one hover away. What the host hands over here ends in
              the platform's own exception text and a machine path — true, occasionally the thing you
              need, and four lines of a five-line card when it is not. The useful sentence is the
              first one, and clamping keeps it first without parsing somebody else's wording. */}
          {harness.problem && (
            <Tip content={harness.problem}>
              <p className="mt-1.5 line-clamp-2 text-small text-ink-soft"><Inline text={harness.problem} /></p>
            </Tip>
          )}

          {/* The managed toolchain (TOOL2/D57). Absent means PATH, which is the usual case and is
              stated rather than left blank — "Daoris manages this" and "the machine happens to have
              one" are different facts about the same working session.

              Absent entirely where the harness declares no package: a control whose only outcome is
              a refusal is worse than none, and the WHY lives once in the card's body rather than
              beside every row (measured — repeated per harness it was two long lines each). */}
          {harness.pinnable && (
          <div className="mt-2 flex flex-wrap items-center gap-2 text-small">
            {harness.pinned ? (
              <>
                <Pill tone={harness.managed ? 'done' : 'declined'}>
                  {t(harness.managed ? 'harness.pin.pinned' : 'harness.pin.missing',
                    { version: harness.pinned })}
                </Pill>
                {harness.managed && (
                  <Tip content={t('harness.pin.managedTip')}>
                    <span className="min-w-0 flex-1 truncate font-mono text-meta text-ink-faint">
                      {harness.managed}
                    </span>
                  </Tip>
                )}
                <Button
                  variant="ghost"
                  className="ml-auto"
                  disabled={busy}
                  onClick={() => run(harness.harness, 'unpin')}
                >
                  {t('harness.pin.unpin')}
                </Button>
              </>
            ) : (
              <>
                {/* A way in that is not there does not run from PATH yet: it read "runs from PATH"
                    under "not on this machine's PATH" (UX5 U56). */}
                <span className="text-ink-faint">{t(harness.present ? 'harness.pin.fromPath' : 'harness.pin.fromPathAbsent')}</span>
                {/* 🔴 Behind a press, not always open. An always-open `1.2.3` box on every harness
                    is five inputs offering an action almost nobody takes, and they were the widest
                    thing on the surface. */}
                <Button
                  variant="ghost"
                  className="ml-auto"
                  aria-expanded={opened[harness.harness] === 'pin'}
                  onClick={() => open(harness.harness, 'pin')}
                >
                  {t('harness.pin.open')}
                </Button>
              </>
            )}
          </div>
          )}

          {harness.pinnable && !harness.pinned && opened[harness.harness] === 'pin' && (
            <form
              className="mt-2 flex flex-wrap items-center gap-2"
              onSubmit={(event) => {
                event.preventDefault();
                const version = (pinning[harness.harness] ?? '').trim();
                if (!version) return;
                run(harness.harness, 'pin', undefined, version);
              }}
            >
              <input
                autoFocus
                aria-label={t('harness.pin.version', { harness: harness.harness })}
                value={pinning[harness.harness] ?? ''}
                onChange={(event) => setPinning(
                  (held) => ({ ...held, [harness.harness]: event.target.value }))}
                placeholder={t('harness.pin.placeholder')}
                className="w-32 rounded-control border border-line-strong bg-sunken px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
              />
              <Button
                type="submit"
                disabled={busy || !(pinning[harness.harness] ?? '').trim()}
              >
                {t('harness.pin.action')}
              </Button>
            </form>
          )}

              {/* A tool action is a process like any other, so it streams through the same console
                  (D49 §2). An install that printed nothing until it finished is indistinguishable
                  from one that hung. It belongs to the DOOR that is doing it — and only what a door
                  does: a sign-in streams inside its own panel above, and an account edit ends in
                  its sentence, so neither is shown here under "Ways in" (D66). */}
              {DOOR_ACTIONS.some((action) => running === `${harness.harness}:${action}`)
                && <SessionConsole id={running!} />}
            </div>
          ))}
        </div>
      ))}

      {/* What each account has carried (TOOL3/D57 §4) — the question "multiple accounts with usage
          management" actually asks. Derived from the sessions, so the two can never disagree, and
          absent entirely on a machine that has measured nothing rather than a row of zeroes. */}
      {accounts.length > 0 && (
        <div id="settings-usage" className="mt-4 scroll-mt-3 border-t border-line pt-3.5">
          <SectionTitle>{t('usage.title')}</SectionTitle>
          <Prose className="mt-1.5 text-small">{t('usage.body')}</Prose>
          <ul className="m-0 mt-2 list-none p-0">
            {accounts.map((account) => {
              // Named as the list above names it: who signed in, a key's handle, else the directory,
              // and the tool's own home as its row says it (UX5 U53: "its own home", and a named
              // account by its directory in the accent). An own home whose name another account on
              // this door also carries says which it is, as the list does.
              const called = nameOf(account.harness, account.profile);
              const repeated = !account.profile && accounts.some((other) =>
                other.harness === account.harness && other.profile && nameOf(other.harness, other.profile) === called);
              return (
                <li
                  key={`${account.harness}:${account.profile ?? ''}`}
                  className="flex flex-wrap items-baseline gap-3 border-t border-line py-1.5 first:border-t-0"
                >
                  <span className="font-mono text-small">{account.harness}</span>
                  <Chip>{called}</Chip>
                  {repeated && <span className="text-meta text-ink-faint">{t('harness.own')}</span>}
                  <span className="text-small text-ink-soft">
                    {t('usage.sessions', { count: account.sessions })}
                  </span>
                  {/* The unit is named, and it is "context" rather than "tokens": the number is in
                      the harness's own units, and calling them tokens would be a claim Daoris cannot
                      make. A bare figure in a column is unreadable without it (measured by looking). */}
                  <Tip content={t('usage.contextTip')}>
                    <span className="ml-auto font-mono text-small text-ink-faint">
                      {t('usage.context', { used: figure(account.used) })}
                    </span>
                  </Tip>
                </li>
              );
            })}
          </ul>
          <p className="mt-2 max-w-prose text-meta text-ink-faint">{t('usage.note')}</p>
        </div>
      )}

      <Button className="mt-4" disabled={refresh.isPending} onClick={() => refresh.mutate()}>
        {t('harness.refresh')}
      </Button>
    </Card>
  );
}
