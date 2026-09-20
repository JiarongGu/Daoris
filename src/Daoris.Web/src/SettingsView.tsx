import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useRemotes, useUnwireRemote, useWireRemote } from './shell';
import { Button, Card, Chip, type Notify, PageHeader, SectionTitle, Tip, useErrorNotify } from './ui';

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
  const onError = (error: unknown) => notify((error as Error).message, 'error');

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
    </section>
  );
}
