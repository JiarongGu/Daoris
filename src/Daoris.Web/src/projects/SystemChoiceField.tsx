import { useTranslation } from 'react-i18next';

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
      <label className="mt-4 block text-small text-ink-faint" htmlFor="add-system">{t('projects.manage.system')}</label>
      <select
        id="add-system"
        value={system}
        onChange={(event) => onSystem(event.target.value)}
        className="mt-1 w-full rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body"
      >
        <option value="">{t('projects.manage.systemPick')}</option>
        {offered.map((each) => (
          <option key={each.system} value={each.system}>{`${each.name} · ${each.plugin}`}</option>
        ))}
      </select>
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
