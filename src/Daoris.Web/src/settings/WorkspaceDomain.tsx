import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { StartWiringList } from '../map/StartWiring';
import { useRegistry, useWorkspaceHoldings } from '../queries';
import { useScope } from '../scope';
import {
  useDriver, useHarnesses, useLines, usePlugins, useRemotes, useSetLanding, useSetLine, useStarts, useSweep,
  useSweepPlan, useTreesSync, useTreesSyncPlan, useUnwireRemote, useWireRemote,
} from '../shell';
import {
  Button, Card, Chip, failure, Icon, type Notify, PathText, Prose, SectionTitle, SettingRow, Tip, useErrorNotify,
} from '../ui';
import { workspacesOf } from '../workspaces';
import { LandingList } from './Landings';
import { LineList } from './Lines';
import { namer } from './namer';
import { SweepList } from './Sweep';
import { SyncSection } from './Sync';

/**
 * The Workspace domain (D75 §3): every workspace and what it holds, which is for everyone, and on a
 * desktop the machine's half beneath it: which deployment serves each workspace, what a start runs on,
 * each repository's line, how work lands, and the clean-up. A browser is given the list alone.
 */
export function WorkspaceDomain({ attached, notify }: { attached: boolean; notify: Notify }) {
  return (
    <>
      <WorkspaceList />
      {attached && <WiringSettings notify={notify} />}
      {attached && <Starts notify={notify} />}
      {attached && <LineSettings notify={notify} />}
      {attached && <LandingSettings notify={notify} />}
      {attached && <SweepSettings notify={notify} />}
    </>
  );
}

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
 * What a start in each workspace would run on (MAP1b) — beside the agents, because it is their
 * accounts and pins resolved: the workspace's default, then the machine's, then the agent's own.
 *
 * **Read from the driver, never recomputed here.** The answer is `SelectAsync`'s, so the page cannot
 * show an account the loop would not take; this organism only names the circles and the accounts.
 */
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
  // The plugins a branch rule may hand its branch to (D100): on, sound, and speaking on `work/land`.
  const catalog = usePlugins();
  const landers = (catalog.data?.plugins ?? [])
    .filter((plugin) => plugin.enabled && !plugin.problem && plugin.points.includes('work/land'))
    .map((plugin) => plugin.id);

  return (
    <LandingList
      landings={Array.isArray(answer.data?.landings) ? answer.data.landings : []}
      workspaceLandings={driver.data?.workspaceLandings ?? []}
      landers={landers}
      busy={setLanding.isPending}
      onSet={(change) => setLanding.mutate(change, {
        onSuccess: () => {
          const name = change.repository ?? change.workspace ?? '';
          notify(!change.form
            ? t('settings.landing.cleared', { name })
            : change.form === 'branch'
              ? change.plugin
                ? t('settings.landing.savedBranchPlugin', { name, pattern: change.pattern, plugin: change.plugin })
                : t('settings.landing.savedBranch', { name, pattern: change.pattern })
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
  // Bringing up to date (WSR6): asked for by its own press, since looking fetches.
  const syncPlan = useTreesSyncPlan();
  const sync = useTreesSync();
  useErrorNotify(plan.error, notify);
  useErrorNotify(syncPlan.error, notify);
  const looked = syncPlan.data;

  return (
    <SweepList
      branches={Array.isArray(plan.data?.branches) ? plan.data.branches : undefined}
      landed={Array.isArray(plan.data?.landed) ? plan.data.landed : undefined}
      busy={sweep.isPending || plan.isFetching}
      onLook={() => void plan.refetch()}
      onClean={(only) => sweep.mutate(only, {
        onSuccess: (done) => notify(t('settings.sweep.done', { removed: done.removed, count: only.length })),
        onError: failure(notify),
      })}
      sync={(
        <SyncSection
          plan={looked && Array.isArray(looked.lines) && Array.isArray(looked.rebases) && Array.isArray(looked.deletes) ? looked : undefined}
          busy={sync.isPending || syncPlan.isFetching}
          onLook={() => void syncPlan.refetch()}
          onSync={(only) => sync.mutate(only, {
            onSuccess: (done) => {
              notify(t('settings.sync.done', { changed: done.changed, count: only.length }));
              // Look again, so the list shows what is left, with why.
              void syncPlan.refetch();
            },
            onError: failure(notify),
          })}
        />
      )}
    />
  );
}
