import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from './format';
import {
  useHarnessAction, useHarnesses, useRefreshHarnesses, useRemotes, useUnwireRemote, useWireRemote,
} from './shell';
import { SessionConsole } from './SessionConsole';
import { Button, Card, Chip, type Notify, PageHeader, Pill, SectionTitle, Tip, useErrorNotify } from './ui';

/**
 * The machine's own settings (D50): what is true about THIS computer rather than about the family.
 *
 * Today that is the wiring — which deployment serves each workspace here (D48 §5) — over
 * `~/.daoris/remotes.json`, the same file `daoris remote` edits and the sync loop reads. The file is
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

  const [workspace, setWorkspace] = useState('');
  const [url, setUrl] = useState('');
  const [key, setKey] = useState('');

  const remotes = wiring.data?.remotes ?? [];
  const onError = (error: unknown) => notify(sentence(error), 'error');

  const add = () => wire.mutate(
    { workspace: workspace.trim(), url: url.trim(), key: key.trim() },
    {
      onSuccess: () => {
        notify(t('settings.wiring.wired', { workspace: workspace.trim() || 'default' }));
        // The key never lingers in a form's state once it has landed in the file.
        setWorkspace('');
        setUrl('');
        setKey('');
      },
      onError,
    });

  return (
    <section>
      <PageHeader title={t('settings.title')} description={t('settings.description')} />

      <Card>
        <SectionTitle>{t('settings.wiring.title')}</SectionTitle>
        <p className="mt-1.5 text-[0.85rem] text-ink-soft">{t('settings.wiring.body')}</p>

        {wiring.data && (
          <p className="mt-2 break-all font-mono text-[0.78rem] text-ink-faint">{wiring.data.path}</p>
        )}

        {wiring.data?.fromEnvironment && (
          /* With the environment pair set, no loader reads the file — so the rows below are what is
             actually in effect, and saying which source decided is the difference between reporting
             the wiring and reporting this surface's own last edit. */
          <p className="mt-3 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-[0.85rem] text-ink-soft">
            {t('settings.wiring.fromEnvironment')}
          </p>
        )}

        {remotes.length === 0 ? (
          <p className="mt-3 text-[0.85rem] text-ink-soft">{t('settings.wiring.none')}</p>
        ) : (
          <ul className="m-0 mt-3 list-none p-0">
            {remotes.map((remote) => (
              <li
                key={remote.workspace}
                className="flex flex-wrap items-baseline gap-3 border-t border-line py-2 first:border-t-0"
              >
                <Chip accent>{remote.workspace}</Chip>
                <span className="break-all font-mono text-[0.8rem]">{remote.url}</span>
                <Tip content={t('settings.wiring.keyTip')}>
                  <span className="font-mono text-[0.78rem] text-ink-faint">{remote.key}</span>
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

        <div className="mt-4 border-t border-line pt-3.5">
          <SectionTitle>{t('settings.wiring.addTitle')}</SectionTitle>
          <div className="mt-2 grid gap-2 md:grid-cols-3">
            <label className="grid gap-1 text-[0.78rem] text-ink-faint">
              {t('settings.wiring.workspace')}
              <input
                value={workspace}
                onChange={(event) => setWorkspace(event.target.value)}
                placeholder={t('settings.wiring.workspacePlaceholder')}
                className="rounded-control border border-line bg-raised px-2.5 py-1.5 text-[0.85rem] text-ink"
              />
            </label>
            <label className="grid gap-1 text-[0.78rem] text-ink-faint">
              {t('settings.wiring.url')}
              <input
                value={url}
                onChange={(event) => setUrl(event.target.value)}
                placeholder="https://…"
                className="rounded-control border border-line bg-raised px-2.5 py-1.5 font-mono text-[0.85rem] text-ink"
              />
            </label>
            <label className="grid gap-1 text-[0.78rem] text-ink-faint">
              {t('settings.wiring.key')}
              <input
                value={key}
                type="password"
                onChange={(event) => setKey(event.target.value)}
                placeholder="dk_…"
                className="rounded-control border border-line bg-raised px-2.5 py-1.5 font-mono text-[0.85rem] text-ink"
              />
            </label>
          </div>
          <p className="mt-2 text-[0.8rem] text-ink-soft">{t('settings.wiring.keyBody')}</p>
          <Button
            variant="primary"
            className="mt-3"
            disabled={!url.trim() || !key.trim() || wire.isPending}
            onClick={add}
          >
            {t('settings.wiring.add')}
          </Button>
        </div>
      </Card>

      <HarnessRoster notify={notify} />
    </section>
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
  const { t } = useTranslation();
  const roster = useHarnesses();
  const refresh = useRefreshHarnesses();
  const act = useHarnessAction();
  useErrorNotify(roster.error, notify);

  // Which action is running, so its console can be shown under the harness that is doing it. One at
  // a time by construction: two installers racing over one PATH is not a thing to make easy.
  const [running, setRunning] = useState<string | null>(null);

  const run = (harness: string, action: 'install' | 'update' | 'login', profile?: string) => {
    setRunning(`${harness}:${action}`);
    act.mutate({ harness, action, profile }, {
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
  if (!answered || !harnesses) return null;

  return (
    <Card className="mt-3.5">
      <SectionTitle>{t('harness.title')}</SectionTitle>
      <p className="mt-1.5 text-[0.85rem] text-ink-soft">{t('harness.body')}</p>
      <p className="mt-2 break-all font-mono text-[0.78rem] text-ink-faint">{answered.settingsPath}</p>

      {harnesses.map((harness) => (
        <div key={harness.harness} className="mt-4 border-t border-line pt-3.5 first:border-t-0">
          <header className="flex flex-wrap items-baseline gap-2">
            <span className="text-[0.9rem] font-semibold">{harness.harness}</span>
            {harness.present
              ? <span className="font-mono text-[0.78rem] text-ink-faint">{harness.version}</span>
              : <Pill tone="neutral">{t('harness.absent')}</Pill>}
            {answered.adapter === harness.harness && <Chip accent>{t('harness.spawns')}</Chip>}

            <span className="ml-auto flex gap-2">
              {!harness.present && (
                <Button disabled={act.isPending} onClick={() => run(harness.harness, 'install')}>
                  {t('harness.install')}
                </Button>
              )}
              {harness.present && (
                <Button disabled={act.isPending} onClick={() => run(harness.harness, 'update')}>
                  {t('harness.update')}
                </Button>
              )}
            </span>
          </header>

          {/* The absence names what it is, rather than leaving a person to guess at a blank row. */}
          {harness.problem && (
            <p className="mt-1.5 text-[0.8rem] text-ink-soft">{harness.problem}</p>
          )}

          {(harness.profiles ?? []).length === 0 ? (
            <p className="mt-2 text-[0.85rem] text-ink-soft">{t('harness.noProfiles')}</p>
          ) : (
            <ul className="m-0 mt-2 list-none p-0">
              {harness.profiles.map((profile) => (
                <li
                  key={profile.name}
                  className="flex flex-wrap items-baseline gap-3 border-t border-line py-2 first:border-t-0"
                >
                  <Chip>{profile.name}</Chip>
                  <Pill tone={profile.login === 'in' ? 'done' : profile.login === 'out' ? 'open' : 'neutral'}>
                    {t(`harness.login.${profile.login}`)}
                  </Pill>
                  {harness.machineDefault === profile.name && (
                    <span className="text-[0.78rem] text-ink-faint">{t('harness.machineDefault')}</span>
                  )}
                  <Tip content={t('harness.homeTip')}>
                    <span className="break-all font-mono text-[0.72rem] text-ink-faint">{profile.home}</span>
                  </Tip>
                  <Button
                    variant="ghost"
                    className="ml-auto"
                    disabled={act.isPending || !harness.present}
                    onClick={() => run(harness.harness, 'login', profile.name)}
                  >
                    {t('harness.login.action')}
                  </Button>
                </li>
              ))}
            </ul>
          )}

          {/* A harness action is a process like any other, so it streams through the same console
              (D49 §2). An install that printed nothing until it finished is indistinguishable from
              one that hung. */}
          {running?.startsWith(`${harness.harness}:`) && <SessionConsole id={running} />}
        </div>
      ))}

      <p className="mt-4 text-[0.8rem] text-ink-soft">{t('harness.secrets')}</p>
      <Button className="mt-3" disabled={refresh.isPending} onClick={() => refresh.mutate()}>
        {t('harness.refresh')}
      </Button>
    </Card>
  );
}
