import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from './format';
import {
  useDriver, useHarnessAction, useHarnesses, useRefreshHarnesses, useRemotes, useSetNotify,
  useUnwireRemote, useWireRemote,
} from './shell';
import { SessionConsole } from './SessionConsole';
import {
  Button, Card, CheckField, Chip, type Notify, PageHeader, Pill, Prose, SectionTitle, Tip,
  useErrorNotify,
} from './ui';

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

  // Whether this machine interrupts the person (SURF5b) — the same `driver.json` field
  // `daoris driver notify on|off` edits, which is what makes this a door rather than the door.
  const driver = useDriver();
  const setNotify = useSetNotify();

  const [workspace, setWorkspace] = useState('');
  const [url, setUrl] = useState('');
  const [key, setKey] = useState('');

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
      },
      onError,
    });

  return (
    <section>
      <PageHeader title={t('settings.title')} description={t('settings.description')} />

      {/* Off in one click, which is what design §4 asks for — and the reason it sits above the
          wiring is that it is the setting a person is most likely to have come here to change. */}
      <Card>
        <SectionTitle>{t('settings.notify.title')}</SectionTitle>
        <Prose className="mt-1.5">{t('settings.notify.body')}</Prose>

        <div className="mt-3">
          <CheckField
            checked={driver.data?.notify ?? true}
            onChange={(on) => setNotify.mutate({ notify: on }, {
              onSuccess: () => notify(t(on ? 'settings.notify.on' : 'settings.notify.off')),
              onError,
            })}
            label={t('settings.notify.label')}
          />
        </div>

        {/* The other door, named where the switch is. A person who finds this on a machine they
            reach over ssh should learn it is the same file, not go looking for a second one. */}
        <p className="mt-2.5 text-small text-ink-faint">{t('settings.notify.terminal')}</p>
      </Card>

      <Card>
        <SectionTitle>{t('settings.wiring.title')}</SectionTitle>
        <Prose className="mt-1.5">{t('settings.wiring.body')}</Prose>

        {wiring.data && (
          <p className="mt-2 break-all font-mono text-small text-ink-faint">{wiring.data.path}</p>
        )}

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

        <div className="mt-4 border-t border-line pt-3.5">
          <SectionTitle>{t('settings.wiring.addTitle')}</SectionTitle>
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
  // The version being typed per harness (TOOL2). Local to the form: a pin only exists once the
  // install behind it succeeded, so there is nothing to remember until then.
  const [pinning, setPinning] = useState<Record<string, string>>({});

  const run = (
    harness: string,
    action: 'install' | 'update' | 'login' | 'pin' | 'unpin',
    profile?: string,
    version?: string,
  ) => {
    setRunning(`${harness}:${action}`);
    act.mutate({ harness, action, profile, version }, {
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
      <Prose className="mt-1.5">{t('harness.body')}</Prose>
      <p className="mt-2 break-all font-mono text-small text-ink-faint">{answered.settingsPath}</p>

      {harnesses.map((harness) => (
        <div key={harness.harness} className="mt-4 border-t border-line pt-3.5 first:border-t-0">
          <header className="flex flex-wrap items-baseline gap-2">
            <span className="text-body font-semibold">{harness.harness}</span>
            {harness.present
              ? <span className="font-mono text-small text-ink-faint">{harness.version}</span>
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
            <p className="mt-1.5 text-small text-ink-soft">{harness.problem}</p>
          )}

          {/* The managed toolchain (TOOL2/D57). Absent means PATH, which is the usual case and is
              stated rather than left blank — "Daoris manages this" and "the machine happens to have
              one" are different facts about the same working session.

              Absent entirely where the harness declares no package: a control whose only outcome is
              a refusal is worse than none, and the WHY lives once in the card's body rather than
              beside every row (measured — repeated per harness it was two long lines each). */}
          {harness.pinnable && (
          <div className="mt-2 flex flex-wrap items-baseline gap-2">
            {harness.pinned ? (
              <>
                <Pill tone={harness.managed ? 'done' : 'declined'}>
                  {t(harness.managed ? 'harness.pin.pinned' : 'harness.pin.missing',
                    { version: harness.pinned })}
                </Pill>
                {harness.managed && (
                  <Tip content={t('harness.pin.managedTip')}>
                    <span className="break-all font-mono text-meta text-ink-faint">{harness.managed}</span>
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
                <span className="text-small text-ink-faint">{t('harness.pin.fromPath')}</span>
                <span className="ml-auto flex items-baseline gap-2">
                  <input
                    aria-label={t('harness.pin.version', { harness: harness.harness })}
                    value={pinning[harness.harness] ?? ''}
                    onChange={(event) => setPinning(
                      (held) => ({ ...held, [harness.harness]: event.target.value }))}
                    placeholder={t('harness.pin.placeholder')}
                    className="w-32 rounded-control border border-line bg-raised px-2 py-1 font-mono text-small"
                  />
                  <Button
                    disabled={act.isPending || !(pinning[harness.harness] ?? '').trim()}
                    onClick={() => run(
                      harness.harness, 'pin', undefined, (pinning[harness.harness] ?? '').trim())}
                  >
                    {t('harness.pin.action')}
                  </Button>
                </span>
              </>
            )}
          </div>
          )}

          {(harness.profiles ?? []).length === 0 ? (
            <Prose className="mt-2">{t('harness.noProfiles')}</Prose>
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
                    <span className="text-small text-ink-faint">{t('harness.machineDefault')}</span>
                  )}
                  <Tip content={t('harness.homeTip')}>
                    <span className="break-all font-mono text-meta text-ink-faint">{profile.home}</span>
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

      <p className="mt-4 text-small text-ink-soft">{t('harness.secrets')}</p>
      <Button className="mt-3" disabled={refresh.isPending} onClick={() => refresh.mutate()}>
        {t('harness.refresh')}
      </Button>
    </Card>
  );
}
