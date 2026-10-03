import { useTranslation } from 'react-i18next';
import { LanguageChoice, type LanguageOption, type RepositoryLanguage } from '../settings/Languages';

/**
 * **A repository's session language on this machine** (LANG1c, D142 point 7): the language its sessions write to the person
 * in — their questions, closing notes, decline reasons and last words — as the driver resolved it, with where it was set, and
 * the field that sets its own. The same `languages` `daoris driver language` edits (D50); Settings → Workspace sets its
 * workspace's. The work's, never the window's: the window's language stays its viewer's, in Settings → Appearance.
 *
 * Props only, no hook from the query layer or the shell (components §2).
 */
export function SessionLanguage({ repository, language, inherited, table, onSet }: {
  repository: string;
  /** What its sessions are asked to write in, as the driver resolved it; null where nothing is set here or for its workspace. */
  language: RepositoryLanguage | null;
  /**
   * Its workspace's language by the table's name, which its unset choice names; by default the resolution's, where it came
   * from the workspace.
   */
  inherited?: string;
  table: LanguageOption[];
  /** Set its own, or with null clear it to take its workspace's again. */
  onSet: (language: string | null) => void;
}) {
  const { t } = useTranslation();
  const own = language?.source === 'repository' ? language.language : undefined;
  const fromWorkspace = inherited ?? (language?.source === 'workspace' ? language.name : undefined);
  const name = language?.name ?? table.find((row) => row.code === language?.language)?.name ?? language?.language;

  return (
    <div className="mt-3 grid gap-1.5">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
        <span className="min-w-12 text-meta text-ink-faint">{t('projects.language.label')}</span>
        <span className="ml-auto">
          <LanguageChoice
            label={t('projects.language.field', { repository })}
            set={own}
            inherited={fromWorkspace}
            table={table}
            onChoose={(code) => onSet(code ?? null)}
          />
        </span>
      </div>
      <p className="m-0 text-small text-ink-soft">
        {language?.source && name
          ? t(`projects.language.from.${language.source}`, { name, workspace: language.workspace })
          : t('projects.language.from.none')}
      </p>
      <p className="m-0 text-small text-ink-soft">{t('projects.language.hint')}</p>
    </div>
  );
}
