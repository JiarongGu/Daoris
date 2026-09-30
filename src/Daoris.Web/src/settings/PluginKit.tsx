import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, CheckField, Inline, MonoWell, Pill, Prose, SectionTitle } from '../ui';

/** A point a new plugin may speak on (PLUG8): the driver's, with what kind of question it is. */
export type KitPoint = { name: string; kind: string };

/** A new plugin as the form sends it: `daoris-driver plugins new <id> --point <p>… --in <folder>`. */
export type NewPlugin = { id: string; points: string[]; folder: string };

/** One thing a trial checked, in the driver's own sentence. */
export type TrialStep = { name: string; ok: boolean; sentence: string };

/** What `plugins try` found (D101). */
export type PluginTrialResult = {
  plugin: string;
  folder: string;
  command: string[];
  passed: boolean;
  summary: string;
  steps: TrialStep[];
  /** What the plugin said on stderr meanwhile, as it said it. */
  said: string[];
};

const FIELD =
  'min-w-0 flex-1 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

/**
 * The kit a plugin is made with (PLUG8, D101), on the screen: New writes a plugin's folder where the
 * person names, and Try starts a folder's plugin as the driver would. The screen's half of
 * `daoris-driver plugins new|try` (D50).
 *
 * @remarks
 * **Making a plugin is work.** A plugin is code that runs on this machine as the person, so New writes a
 * folder for a repository — its manifest, a wire script, a wire test that needs nothing of Daoris's, a
 * README — and installs nothing; `daoris plugin add` does that once someone has read it. What Try says is
 * the driver's own sentences, verbatim: content, not chrome.
 */
export function PluginKitCard({ points, busy, made, trial, onPick, onMake, onTry }: {
  points: KitPoint[];
  busy?: boolean;
  /** The folder New last made, offered to Try. */
  made?: string | null;
  /** The last folder trial's report. */
  trial?: PluginTrialResult | null;
  /** Ask the shell for a folder; null is the person cancelling. */
  onPick: () => Promise<string | null>;
  onMake: (plugin: NewPlugin) => void;
  onTry: (folder: string) => void;
}) {
  const { t } = useTranslation();
  const [id, setId] = useState('');
  const [chosen, setChosen] = useState<string[]>([]);
  const [folder, setFolder] = useState('');
  const [tryFolder, setTryFolder] = useState(made ?? '');
  // The folder just made is the one to try next.
  useEffect(() => {
    if (made) setTryFolder(made);
  }, [made]);

  const pick = async (set: (folder: string) => void) => {
    const picked = await onPick();
    if (picked) set(picked);
  };
  const ready = id.trim().length > 0 && chosen.length > 0 && folder.trim().length > 0;
  // The points in the driver's order, whatever order they were ticked in.
  const ordered = points.map((point) => point.name).filter((name) => chosen.includes(name));

  return (
    <Card id="settings-plugin-kit" className="mt-3.5 scroll-mt-3">
      <SectionTitle>{t('plugin.kit.title')}</SectionTitle>
      <Prose className="mt-1 text-small text-ink-soft"><Inline text={t('plugin.kit.body')} /></Prose>

      <fieldset aria-label={t('plugin.kit.new')} className="m-0 mt-3 border-0 border-t border-line p-0 pt-3">
        <form
          className="flex flex-col gap-2.5"
          onSubmit={(event) => {
            event.preventDefault();
            if (ready) onMake({ id: id.trim(), points: ordered, folder: folder.trim() });
          }}
        >
          <label className="flex flex-wrap items-center gap-2 text-small text-ink-soft">
            <span className="w-28 shrink-0">{t('plugin.kit.id')}</span>
            <input
              aria-label={t('plugin.kit.id')}
              value={id}
              onChange={(event) => setId(event.target.value)}
              placeholder="acme.quiet-hours"
              spellCheck={false}
              className={FIELD}
            />
          </label>
          <div className="flex flex-wrap items-start gap-2 text-small text-ink-soft">
            <span className="w-28 shrink-0 pt-0.5">{t('plugin.kit.points')}</span>
            <div className="flex min-w-0 flex-col gap-1.5">
              {points.map((point) => (
                <CheckField
                  key={point.name}
                  label={`${point.name} — ${t(`plugin.kit.kind.${point.kind}`, { defaultValue: point.kind })}`}
                  checked={chosen.includes(point.name)}
                  onChange={(on) => setChosen((was) => (on ? [...was, point.name] : was.filter((name) => name !== point.name)))}
                  disabled={busy}
                  className="whitespace-normal text-small"
                />
              ))}
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-2 text-small text-ink-soft">
            <span className="w-28 shrink-0">{t('plugin.kit.folder')}</span>
            <input
              aria-label={t('plugin.kit.folder')}
              value={folder}
              onChange={(event) => setFolder(event.target.value)}
              placeholder={t('plugin.kit.folderPlaceholder')}
              spellCheck={false}
              className={FIELD}
            />
            <Button variant="ghost" disabled={busy} onClick={() => void pick(setFolder)}>{t('plugin.kit.choose')}</Button>
            <Button type="submit" disabled={busy || !ready}>{t('plugin.kit.new')}</Button>
          </div>
          <p className="m-0 text-meta text-ink-faint">{t('plugin.kit.folderHint')}</p>
        </form>
      </fieldset>

      <fieldset aria-label={t('plugin.kit.tryFolder')} className="m-0 mt-3 border-0 border-t border-line p-0 pt-3">
        <form
          className="flex flex-wrap items-center gap-2 text-small text-ink-soft"
          onSubmit={(event) => {
            event.preventDefault();
            if (tryFolder.trim()) onTry(tryFolder.trim());
          }}
        >
          <span className="w-28 shrink-0">{t('plugin.kit.tryFolder')}</span>
          <input
            aria-label={t('plugin.kit.tryFolderField')}
            value={tryFolder}
            onChange={(event) => setTryFolder(event.target.value)}
            placeholder={t('plugin.kit.folderPlaceholder')}
            spellCheck={false}
            className={FIELD}
          />
          <Button variant="ghost" disabled={busy} onClick={() => void pick(setTryFolder)}>{t('plugin.kit.choose')}</Button>
          <Button type="submit" disabled={busy || !tryFolder.trim()} aria-busy={busy || undefined}>{t('plugin.kit.try')}</Button>
        </form>
        <p className="m-0 mt-1.5 text-meta text-ink-faint">
          <Inline text={t(busy ? 'plugin.kit.working' : 'plugin.kit.tryHint')} />
        </p>
      </fieldset>

      {trial && <div className="mt-3"><TrialReport trial={trial} /></div>}
      <p className="m-0 mt-3 text-meta text-ink-faint"><Inline text={t('plugin.kit.terminal')} /></p>
    </Card>
  );
}

/**
 * What a trial found: each check with its verdict and the driver's own sentence, the summary, and what
 * the plugin said on stderr. Under an installed plugin's row, and under the kit's folder trial.
 */
export function TrialReport({ trial }: { trial: PluginTrialResult }) {
  const { t } = useTranslation();

  return (
    <section aria-label={t('plugin.kit.report')} className="rounded-control border border-line bg-page/60 px-3.5 py-2.5">
      <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
        {trial.steps.map((step, index) => (
          <li key={`${step.name}-${index}`} className="grid grid-cols-[auto_8.5rem_minmax(0,1fr)] items-baseline gap-2 text-small">
            <Pill tone={step.ok ? 'done' : 'declined'}>{t(step.ok ? 'plugin.kit.ok' : 'plugin.kit.failed')}</Pill>
            <code className="truncate font-mono text-meta text-ink-soft">{step.name}</code>
            <span className="min-w-0 break-words text-ink-soft"><Inline text={step.sentence} /></span>
          </li>
        ))}
      </ul>
      <p className={`m-0 mt-2 text-small font-medium ${trial.passed ? 'text-st-done' : 'text-warn'}`}>
        <Inline text={trial.summary} />
      </p>
      {trial.said.length > 0 && (
        <div className="mt-2">
          <MonoWell label={t('plugin.kit.said')} text={trial.said.join('\n')} />
        </div>
      )}
    </section>
  );
}
