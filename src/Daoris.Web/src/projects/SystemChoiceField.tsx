import { useTranslation } from 'react-i18next';
import { SelectField } from '../design/ui';

/** One system an installed plugin adapts, as a department's registration names it: `<plugin>/<system>`. */
export type SystemOffered = { system: string; name: string; plugin: string };

/**
 * Adding a system reached only through its console: which installed adapter's system it is, and the department's name, the
 * system's id unless the person names it. A session there plans and changes nothing; the plan is applied after its review.
 */
export function SystemChoiceField({ offered, system, name, onSystem, onName }: {
  offered: readonly SystemOffered[];
  system: string;
  name: string;
  onSystem: (system: string) => void;
  onName: (name: string) => void;
}) {
  const { t } = useTranslation();
  if (offered.length === 0) return <p className="mt-3 text-body text-ink-soft">{t('projects.manage.systemNone')}</p>;
  return (
    <>
      <p className="mt-4 text-small text-ink-faint">{t('projects.manage.system')}</p>
      <div className="mt-1">
        <SelectField
          value={system}
          onChange={onSystem}
          ariaLabel={t('projects.manage.system')}
          placeholder={t('projects.manage.systemPick')}
          options={offered.map((each) => ({
            value: each.system,
            label: `${each.name} · ${each.plugin}`,
          }))}
        />
      </div>
      <label className="mt-4 block text-small text-ink-faint" htmlFor="add-system-name">{t('projects.manage.systemName')}</label>
      <input
        id="add-system-name"
        value={name}
        onChange={(event) => onName(event.target.value)}
        placeholder={system.split('/')[1] ?? ''}
        className="mt-1 w-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
      />
      <p className="mt-3 border-l-[3px] border-line-strong bg-raised px-3.5 py-2 text-body text-ink-soft">{t('projects.manage.systemNote')}</p>
    </>
  );
}
