import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { shellWord } from '../shellWord';
import { Button, CodeText, SelectField } from '../ui';
import type { AccountSettings, AccountSettingsChange, SettingsChoices } from '../tools';

/** A choice that is not a value: the tool's own default, a model the aliases do not name, a key not set. */
const TOOL = '__tool';
const OTHER = '__other';
const NOT_SET = '__unset';

/** A list of the tool's own words as options, with a value the file holds outside them kept as itself. */
const offered = (values: readonly string[], held: string | null) =>
  [...values, ...(held && !values.includes(held) ? [held] : [])].map((value) => ({ value, label: value }));

/**
 * What an account's own settings say (AGT6, D98), as a line on its row: the model and the effort in the
 * tool's own words, the tool's default where nothing is set, and how many models have an effort of their
 * own. A file the driver could not read is its sentence, verbatim.
 */
export function AccountSettingsSummary({ settings }: { settings: AccountSettings }) {
  const { t } = useTranslation();
  if (settings.problem) return <span className="text-meta text-ink-danger">{settings.problem}</span>;

  // NAME1b: the choice's name is sentence case; inside this line the value is a fragment of its own.
  const unset = t('harness.settings.summary.default');
  const said = settings.model === null && settings.effort === null && settings.perModel.length === 0
    ? t('harness.settings.summary.own')
    : [
      t('harness.settings.summary.model', { model: settings.model ?? unset }),
      t('harness.settings.summary.effort', { effort: settings.effort ?? unset }),
      ...(settings.perModel.length > 0 ? [t('harness.settings.summary.perModel', { count: settings.perModel.length })] : []),
    ].join(' · ');
  return <span className="text-meta text-ink-faint">{said}</span>;
}

/**
 * An account's own model and effort, to change (AGT6, D98): the tool's aliases with a field for a full
 * id, the efforts its settings keep, and each model's own effort where the file sets one.
 *
 * @remarks
 * **It sends only what the person changed**, as a key set or cleared — the tool's default is a clearing,
 * never a value Daoris invents — so a save never rewrites a key the person did not touch. The choices are
 * the tool's own, handed in: Daoris names no model of its own (D24).
 *
 * **The terminal door is named on the form** (D50): the same file, `daoris agent settings`, its account spelled for
 * whichever shell it is pasted into (`shellWord`, ACCTQUOTE1c).
 */
export function AccountSettingsForm({
  harness, account, accountLabel, settings, choices, busy = false, onSave, onCancel,
}: {
  /** The agent a terminal names — the account-owning door's id. */
  harness: string;
  /** The account's directory name, which the terminal names. */
  account: string;
  /** What a person calls the account: who signed in, or the name. */
  accountLabel: string;
  settings: AccountSettings;
  choices: SettingsChoices;
  busy?: boolean;
  onSave: (change: AccountSettingsChange) => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const named = settings.model !== null && choices.models.includes(settings.model);
  const [model, setModel] = useState(settings.model === null ? TOOL : named ? settings.model : OTHER);
  const [modelId, setModelId] = useState(settings.model !== null && !named ? settings.model : '');
  const [effort, setEffort] = useState(settings.effort ?? TOOL);
  const [perModel, setPerModel] = useState<Record<string, string>>(
    () => Object.fromEntries(settings.perModel.map((entry) => [entry.model, entry.effort])));

  const chosenModel = model === TOOL ? null : model === OTHER ? modelId.trim() : model;
  const change: AccountSettingsChange = {};
  if (chosenModel !== settings.model) change.model = chosenModel;
  const chosenEffort = effort === TOOL ? null : effort;
  if (chosenEffort !== settings.effort) change.effort = chosenEffort;
  const perModelChange: Record<string, string | null> = {};
  for (const entry of settings.perModel) {
    const chosen = perModel[entry.model] === NOT_SET ? null : perModel[entry.model] ?? entry.effort;
    if (chosen !== entry.effort) perModelChange[entry.model] = chosen;
  }
  if (Object.keys(perModelChange).length > 0) change.perModel = perModelChange;
  const changed = Object.keys(change).length > 0;
  // A full id is asked for once "Another model…" is chosen; with none typed there is nothing to send.
  const incomplete = model === OTHER && modelId.trim() === '';

  return (
    <div
      role="group"
      aria-label={t('harness.settings.title', { account: accountLabel })}
      className="grid basis-full gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
    >
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5">
        <span className="text-small font-medium text-ink-soft">{t('harness.settings.model')}</span>
        <SelectField
          value={model}
          onChange={setModel}
          ariaLabel={t('harness.settings.modelLabel')}
          options={[
            { value: TOOL, label: t('harness.settings.toolDefault') },
            ...choices.models.map((name) => ({ value: name, label: name })),
            { value: OTHER, label: t('harness.settings.otherModel') },
          ]}
        />
        {model === OTHER && (
          <input
            value={modelId}
            onChange={(event) => setModelId(event.target.value)}
            spellCheck={false}
            aria-label={t('harness.settings.modelId')}
            placeholder={t('harness.settings.modelIdPlaceholder')}
            className="min-w-56 flex-1 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
          />
        )}
        <span className="text-small font-medium text-ink-soft">{t('harness.settings.effort')}</span>
        <SelectField
          value={effort}
          onChange={setEffort}
          ariaLabel={t('harness.settings.effortLabel')}
          options={[{ value: TOOL, label: t('harness.settings.toolDefault') }, ...offered(choices.efforts, settings.effort)]}
        />
      </div>

      {settings.perModel.length > 0 && (
        <div role="group" aria-label={t('harness.settings.perModelLabel')} className="grid gap-1">
          <span className="text-meta text-ink-faint">{t('harness.settings.perModel')}</span>
          {settings.perModel.map((entry) => (
            <div key={entry.model} className="flex flex-wrap items-center gap-2">
              <span className="min-w-40 font-mono text-small text-ink">{entry.model}</span>
              <SelectField
                value={perModel[entry.model] ?? entry.effort}
                onChange={(value) => setPerModel((held) => ({ ...held, [entry.model]: value }))}
                ariaLabel={t('harness.settings.effortFor', { model: entry.model })}
                options={[{ value: NOT_SET, label: t('harness.settings.notSet') }, ...offered(choices.efforts, entry.effort)]}
              />
            </div>
          ))}
        </div>
      )}

      <p className="m-0 text-meta text-ink-faint">{t('harness.settings.hint')}</p>
      <p className="m-0 flex flex-wrap items-baseline gap-1.5 text-meta text-ink-faint">
        <span>{t('harness.settings.terminal')}</span>
        <CodeText text={`daoris agent settings ${harness} --account ${shellWord(account, '<account>')} model <model> effort <effort>`} />
      </p>

      <div className="flex flex-wrap items-center gap-2">
        <Button variant="primary" disabled={busy || !changed || incomplete} onClick={() => onSave(change)}>
          {t('harness.settings.save')}
        </Button>
        <Button variant="ghost" onClick={onCancel}>{t('common.cancel')}</Button>
      </div>
    </div>
  );
}
