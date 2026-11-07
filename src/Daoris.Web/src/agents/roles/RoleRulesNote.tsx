import { useTranslation } from 'react-i18next';

/**
 * A role's moves to another choice and their bound are the supervisor's rules: this says the rules naming the role,
 * each with where it is set, and where they are changed. The role's own form writes none.
 */
export function RoleRulesNote({ rules }: { rules: readonly string[] }) {
  const { t } = useTranslation();
  return (
    <div className="grid gap-1" data-role-rules="">
      <p className="m-0 text-small text-ink-soft">{rules.length > 0 ? t('settings.supervisor.role.some') : t('settings.supervisor.role.none')}</p>
      {rules.length > 0 && (
        <ul className="m-0 grid list-none gap-0.5 p-0 text-small text-ink-faint">
          {rules.map((rule) => <li key={rule}>{rule}</li>)}
        </ul>
      )}
    </div>
  );
}
