import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from './format';
import { useRegistry } from './queries';
import {
  useDriver, useHarnessAction, useHarnesses, usePluginAction, usePlugins, useRefreshHarnesses,
  useRemotes, useSetNotify, useSetStrikes,
  useUnwireRemote, useUsage, useWireRemote,
} from './shell';
import { SessionConsole } from './SessionConsole';
import { byTool, type ToolDoor } from './tools';
import {
  Button, Card, CheckField, Chip, Icon, type Notify, PageHeader, Pill, Prose, SectionTitle,
  SelectField, SettingRow, Tip, useErrorNotify,
} from './ui';

/**
 * The machine's own settings (D50): what is true about THIS computer rather than about the family.
 *
 * Today that is the wiring — which deployment serves each workspace here (D48 §5) — over
 * the home's `remotes.json`, the same file `daoris remote` edits and the sync loop reads. The file is
 * the truth and this is an editor over it, exactly as the driver's controls are editors over
 * `driver.json`: hand-editing keeps working, and neither surface is the only way to say anything.
 *
 * **Shell-only, and more strictly than the other controls.** A browser over a keyed remote must never
 * read where a machine syncs, and never re-point it — so the state lives behind the shell's bridge and
 * the service has no route onto it at all. The key goes in and never comes out: what is rendered is
 * the audit prefix a deployment's own `keys list` prints.
 */
export function SettingsView({ notify }: { notify: Notify }) {
  const { t } = useTranslation();
  const wiring = useRemotes();
  const wire = useWireRemote();
  const unwire = useUnwireRemote();
  useErrorNotify(wiring.error, notify);

  // Whether this machine interrupts the person (SURF5b) — the same `driver.json` field
  // `daoris driver notify on|off` edits, which is what makes this a door rather than the door.
  const driver = useDriver();
  const setNotify = useSetNotify();
  const setStrikesMutation = useSetStrikes();

  // Held as text while it is being typed: a number input mid-edit passes through the empty string
  // and through "0", and writing either straight to the config would park nothing while the person
  // was still reaching for the second digit.
  const [strikes, setStrikes] = useState<string | null>(null);

  const [workspace, setWorkspace] = useState('');
  const [url, setUrl] = useState('');
  const [key, setKey] = useState('');
  // The wiring form is one press away, not open on every visit: it is done once per machine per
  // deployment, and open it was the largest thing on a page most people come to for a checkbox.
  const [wiringOpen, setWiringOpen] = useState(false);

  const remotes = wiring.data?.remotes ?? [];
  const onError = (error: unknown) => notify(sentence(error), 'error');

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
    <section>
      <PageHeader title={t('settings.title')} description={t('settings.description')} />

      {/* 🔴 A setting is a ROW (owner, 2026-09-23: *"you have this really long list of setup (this
          machine), which probably can be improved ui/ux"*). Every card here used to open with a
          paragraph and put its one control beneath it, so the first checkbox sat 580px below the
          title and the next dial a screen further down. `SettingRow` carries the shape now — the
          label leads, the hint is one line, the control is at the right, the paragraph is on the
          glyph — and the cards are the sections of one settings page rather than five essays. */}

      {/* Where this machine's Daoris lives (D63) — under the header, because the header's sentence
          is about it and every path below is under it. A card of its own cost 200px of the page for
          one line of fact (measured), and pushed the first control below the fold's first third. The
          notice is the shell's own sentence about what the start did, carried in the state rather
          than only raised: a toast raised before the page subscribed reached nobody. */}
      {driver.data?.home && (
        <div className="-mt-3.5 mb-5">
          <Tip content={t('settings.home.hint')}>
            <p className="m-0 inline-block max-w-full cursor-help break-all font-mono text-small text-ink-soft">
              {driver.data.home}
            </p>
          </Tip>
          {driver.data.homeNotice && (
            <p className="mt-2 max-w-prose border-l-[3px] border-accent bg-raised px-3.5 py-2 text-body text-ink-soft">
              {driver.data.homeNotice}
            </p>
          )}
          {/* The host this window adopted serves another install's page (case study 4d). A
              standing fact, so a standing line: the toast that carried it fired before this page
              existed to hear it, which is how the second deployment showed a new window, an old
              page, and no surface saying so. */}
          {driver.data.hostNotice && (
            <p className="mt-2 max-w-prose border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
              {driver.data.hostNotice}
            </p>
          )}
        </div>
      )}

      {/* The driver's two dials, in one card: that one asks to be TOLD when a driver stops, this one
          bounds what it spends before anyone is told (D58). The notification switch leads because it
          is the setting a person is most likely to have come here to change. */}
      <Card>
        <SectionTitle>{t('settings.driver.title')}</SectionTitle>
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
                const value = Number(strikes);
                if (!Number.isInteger(value) || value < 0) {
                  setStrikes(String(driver.data?.strikes ?? 3));
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

      <Card className="mt-3.5">
        <SectionTitle>{t('settings.wiring.title')}</SectionTitle>
        <SettingRow
          label={t('settings.wiring.label')}
          hint={t('settings.wiring.hint')}
          why={t('settings.wiring.body')}
          control={wiring.data && (
            <span className="break-all font-mono text-small text-ink-faint">{wiring.data.path}</span>
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
                <span className="break-all font-mono text-small">{remote.url}</span>
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

      <HarnessRoster notify={notify} />
      <Plugins notify={notify} />
    </section>
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
    onError: (error: unknown) => notify(sentence(error), 'error'),
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
      <SectionTitle>{t('plugin.title')}</SectionTitle>
      <SettingRow
        label={t('plugin.title')}
        hint={t('plugin.terminal')}
        why={t('plugin.body')}
        control={<span className="break-all font-mono text-small text-ink-faint">{catalog.data.folder}</span>}
      />

      {plugins.length === 0 ? (
        <Prose className="mt-3 text-small">{t('plugin.none')}</Prose>
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
              {plugin.problem}
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
 * **Daoris manages directories and names, never secrets.** A profile is an isolated configuration
 * home whose LOCATION Daoris owns; the credential inside it is put there by the harness's own login
 * flow and stays in the harness's own store, under the person's OS account. Nothing on this surface
 * reads one, and there is deliberately nowhere for one to be typed.
 */
function HarnessRoster({ notify }: { notify: Notify }) {
  // The profile name being typed, per harness. A draft, so it lives with the roster that renders the
  // form rather than with the view above it — and keyed by harness because two rosters are on screen
  // at once and one shared string would type into both.
  const [newProfile, setNewProfile] = useState<Record<string, string>>({});
  const { t } = useTranslation();
  const roster = useHarnesses();
  const refresh = useRefreshHarnesses();
  const act = useHarnessAction();
  // What each account has carried (TOOL3). Beside the roster because it is about the same accounts.
  const usage = useUsage();
  // The circles this machine has, so an account can be chosen for one (D49 §4) — the terminal
  // could already do it (`daoris harness profile default … --workspace`), and the screen could not.
  const registry = useRegistry();
  const workspaces = [...new Set((registry.data ?? []).map((r) => r.workspace).filter(Boolean))]
    .sort() as string[];
  useErrorNotify(roster.error, notify);

  // Which action is running, so its console can be shown under the harness that is doing it. One at
  // a time by construction: two installers racing over one PATH is not a thing to make easy.
  const [running, setRunning] = useState<string | null>(null);
  // The version being typed per harness (TOOL2). Local to the form: a pin only exists once the
  // install behind it succeeded, so there is nothing to remember until then.
  const [pinning, setPinning] = useState<Record<string, string>>({});

  /**
   * Which rare control a harness has open — `'account'`, `'pin'`, or nothing.
   *
   * @remarks
   * 🔴 Written from the owner's *"the crediental managment / account login still not really looking
   * nice and easy to understand"* (2026-09-22), and the screenshot said why: **both rare forms were
   * always open, on every harness.** Five harnesses meant five empty name boxes, five version
   * boxes and five copies of the same paragraph, so the surface was mostly controls nobody was
   * using and the accounts — the thing a person actually came for — were a thin row between them.
   *
   * One at a time per harness, because the two are alternatives in practice and two open forms is
   * the crowding this removes coming back.
   */
  const [opened, setOpened] = useState<Record<string, 'account' | 'pin' | null>>({});
  const open = (harness: string, which: 'account' | 'pin') =>
    setOpened((held) => ({ ...held, [harness]: held[harness] === which ? null : which }));

  const run = (
    harness: string,
    action: 'install' | 'update' | 'login' | 'pin' | 'unpin'
      | 'profile-add' | 'profile-remove' | 'profile-default',
    profile?: string,
    version?: string,
    workspace?: string,
  ) => {
    setRunning(`${harness}:${action}`);
    act.mutate({ harness, action, profile, version, workspace }, {
      // The harness's own exit code decides which it was: Daoris ran somebody else's tool and reports
      // what it did, rather than deciding on its behalf that it went well.
      onSuccess: (result) => (result.exitCode === 0
        ? notify(t('harness.done', { harness, action: t(`harness.${action}`) }))
        : notify(
          t('harness.failed', { harness, action: t(`harness.${action}`), code: result.exitCode }),
          'error')),
      onError: (error: unknown) => notify(sentence(error), 'error'),
    });
  };

  // Defensive about the shape, deliberately, and for the reason SES1 wrote down: a shell older than
  // this surface answers something else entirely to a request it has never heard of. A machine's
  // settings page must not go blank because one card asked a question the host cannot answer.
  const answered = roster.data;
  const harnesses = Array.isArray(answered?.harnesses) ? answered.harnesses : null;
  // Same defensiveness, and the same reason: an older shell has never heard of this question.
  const accounts = Array.isArray(usage.data?.accounts) ? usage.data.accounts : [];
  if (!answered || !harnesses) return null;

  return (
    <Card className="mt-3.5">
      <SectionTitle>{t('harness.title')}</SectionTitle>
      {/* One line each, and the rest on the glyph: what a tool is here, and what an account is —
          said ONCE, where the second used to be repeated under every harness's add form. */}
      <SettingRow
        label={t('harness.body')}
        hint={t('harness.secrets')}
        why={t('harness.profile.note')}
        control={<span className="break-all font-mono text-small text-ink-faint">{answered.settingsPath}</span>}
      />

      {/* 🔴 A card per TOOL, and the adapters are its ways in (owner, 2026-09-22: *"'harness
          account'? and `*-acp` really confusing of the scope of this project"*, with the correction
          that the reference project is a reference — take its design and its logic, not its words).

          The surface had been listing four adapters as four things to have opinions about. A person
          has one Claude Code and one account for it; whether Daoris holds the session over a pipe or
          over the protocol is Daoris's business, not a second tool. `byTool` reads that off
          `accountOf` and `wire`, both of which have said it all along. */}
      {byTool(harnesses as ToolDoor[]).map((tool) => (
        <div
          key={tool.name}
          className="mt-3 rounded-card border border-line bg-page/60 p-3 first:mt-3.5"
        >
          <header className="flex flex-wrap items-center gap-2">
            <span className="text-body font-semibold text-ink">{tool.name}</span>
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
                  <span className="text-body font-medium text-ink">{t('harness.own')}</span>
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
                    disabled={act.isPending}
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
                      <span className="text-body font-medium text-ink">{profile.name}</span>
                      <Pill tone={profile.login === 'in' ? 'done' : 'neutral'}>
                        {t(`harness.login.${profile.login}`)}
                      </Pill>
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
                    <Button
                      variant={profile.login === 'in' ? 'ghost' : 'default'}
                      disabled={act.isPending || !tool.present}
                      onClick={() => run(tool.doors[0]!.harness, 'login', profile.name)}
                    >
                      {t(profile.login === 'in' ? 'harness.login.again' : 'harness.login.action')}
                    </Button>
                    {tool.machineDefault !== profile.name && (
                      <Button
                        variant="ghost"
                        disabled={act.isPending}
                        onClick={() => run(tool.doors[0]!.harness, 'profile-default', profile.name)}
                      >
                        {t('harness.profile.use')}
                      </Button>
                    )}
                    {/* 🔴 "Forget", not "delete". It stops this machine pointing at the account and
                        never deletes a credential — a button that quietly destroyed one would be the
                        irreversible act this family never does silently. The directory goes only on
                        the tool's own word that the account is signed out (or when it is empty);
                        "deletes nothing" had left a forgotten account on the list forever, because
                        the directory IS the account. The word on the button is the word for what
                        happens, and the console says which it was. */}
                    <Tip content={t('harness.profile.forgetTip')}>
                      <Button
                        variant="ghost"
                        disabled={act.isPending}
                        onClick={() => run(tool.doors[0]!.harness, 'profile-remove', profile.name)}
                      >
                        {t('harness.profile.forget')}
                      </Button>
                    </Tip>
                    {workspaces.length > 0 && (
                      <SelectField
                        value=""
                        onChange={(workspace) =>
                          run(tool.doors[0]!.harness, 'profile-default', profile.name, undefined, workspace)}
                        options={workspaces.map((workspace) => ({ value: workspace, label: workspace }))}
                        placeholder={t('harness.profile.useForPlaceholder')}
                        ariaLabel={t('harness.profile.useFor', { profile: profile.name })}
                      />
                    )}
                  </div>
                </li>
              ))}
            </ul>

          {opened[tool.name] === 'account' ? (
            <form
              className="mt-2 flex flex-wrap items-center gap-2"
              onSubmit={(event) => {
                event.preventDefault();
                const name = newProfile[tool.name]?.trim();
                if (!name) return;
                run(tool.doors[0]!.harness, 'profile-add', name);
                setNewProfile((held) => ({ ...held, [tool.name]: '' }));
                setOpened((held) => ({ ...held, [tool.name]: null }));
              }}
            >
              <input
                autoFocus
                value={newProfile[tool.name] ?? ''}
                onChange={(event) =>
                  setNewProfile((held) => ({ ...held, [tool.name]: event.target.value }))}
                placeholder={t('harness.profile.placeholder')}
                aria-label={t('harness.profile.add', { harness: tool.name })}
                className="min-w-40 rounded-control border border-line-strong bg-sunken px-2.5 py-1 text-body text-ink outline-none placeholder:text-ink-faint"
              />
              <Button type="submit" disabled={act.isPending || !(newProfile[tool.name] ?? '').trim()}>
                {t('harness.profile.addAction')}
              </Button>
            </form>
          ) : (
            /* 🔴 The thing that did not exist (DEPLOY3) — a screen could list accounts and log into
               one and never MAKE one. It still exists; it is one press away instead of an open box
               on every row, and the paragraph explaining what an account IS moved to the card's
               body, where it is read once rather than once per tool. */
            <Button
              variant="ghost"
              className="mt-2"
              disabled={act.isPending}
              onClick={() => open(tool.name, 'account')}
            >
              <Icon name="plus" size={13} />
              {t('harness.profile.addOpen')}
            </Button>
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
                    <Button disabled={act.isPending} onClick={() => run(harness.harness, 'install')}>
                      {t('harness.install')}
                    </Button>
                  )}
                  {harness.present && (
                    <Button variant="ghost" disabled={act.isPending} onClick={() => run(harness.harness, 'update')}>
                      {t('harness.update')}
                    </Button>
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
              <p className="mt-1.5 line-clamp-2 text-small text-ink-soft">{harness.problem}</p>
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
                  disabled={act.isPending}
                  onClick={() => run(harness.harness, 'unpin')}
                >
                  {t('harness.pin.unpin')}
                </Button>
              </>
            ) : (
              <>
                <span className="text-ink-faint">{t('harness.pin.fromPath')}</span>
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
                disabled={act.isPending || !(pinning[harness.harness] ?? '').trim()}
              >
                {t('harness.pin.action')}
              </Button>
            </form>
          )}

              {/* A tool action is a process like any other, so it streams through the same console
                  (D49 §2). An install that printed nothing until it finished is indistinguishable
                  from one that hung. It belongs to the DOOR that is doing it. */}
              {running?.startsWith(`${harness.harness}:`) && <SessionConsole id={running} />}
            </div>
          ))}
        </div>
      ))}

      {/* What each account has carried (TOOL3/D57 §4) — the question "multiple accounts with usage
          management" actually asks. Derived from the sessions, so the two can never disagree, and
          absent entirely on a machine that has measured nothing rather than a row of zeroes. */}
      {accounts.length > 0 && (
        <div className="mt-4 border-t border-line pt-3.5">
          <SectionTitle>{t('usage.title')}</SectionTitle>
          <Prose className="mt-1.5 text-small">{t('usage.body')}</Prose>
          <ul className="m-0 mt-2 list-none p-0">
            {accounts.map((account) => (
              <li
                key={`${account.harness}:${account.profile ?? ''}`}
                className="flex flex-wrap items-baseline gap-3 border-t border-line py-1.5 first:border-t-0"
              >
                <span className="font-mono text-small">{account.harness}</span>
                <Chip accent={Boolean(account.profile)}>
                  {account.profile ?? t('usage.ownHome')}
                </Chip>
                <span className="text-small text-ink-soft">
                  {t('usage.sessions', { count: account.sessions })}
                </span>
                {/* The unit is named, and it is "context" rather than "tokens": the number is in
                    the harness's own units, and calling them tokens would be a claim Daoris cannot
                    make. A bare figure in a column is unreadable without it (measured by looking). */}
                <Tip content={t('usage.contextTip')}>
                  <span className="ml-auto font-mono text-small text-ink-faint">
                    {t('usage.context', { used: account.used.toLocaleString() })}
                  </span>
                </Tip>
              </li>
            ))}
          </ul>
          <p className="mt-2 text-meta text-ink-faint">{t('usage.note')}</p>
        </div>
      )}

      <Button className="mt-4" disabled={refresh.isPending} onClick={() => refresh.mutate()}>
        {t('harness.refresh')}
      </Button>
    </Card>
  );
}
