import { useTranslation } from 'react-i18next';
import { SelectField } from '../ui';

/**
 * One row of the driver's closed table of session languages (LANG1c): its code, and the name a session's line gives it. The
 * page names each option by it, so adding a language is a row in the driver's table and needs no catalogue here.
 */
export type LanguageOption = { code: string; name: string };

/** Where a repository's session language was set (LANG1c): for it, or for its workspace. */
export type LanguageSource = 'repository' | 'workspace';

/** One repository's session language as the driver resolves it; `language`, `name` and `source` absent where none is set. */
export type RepositoryLanguage = {
  repository: string;
  workspace: string;
  language?: string;
  name?: string;
  source?: LanguageSource;
};

/** A change to a session language: a repository's or a workspace's, cleared when it names no language. */
export type LanguageChange = { repository?: string; workspace?: string; language?: string };

/** The select's value for nothing set here: Radix takes no empty value. */
const UNSET = 'unset';

/**
 * **One session language's field** (LANG1c): what is set here, or the unset choice, which names what stands without it — a
 * repository's workspace's language, or none. Choosing what is already chosen sends nothing. Props only (components §2).
 */
export function LanguageChoice({ label, set, inherited, table, onChoose }: {
  /** The field's accessible name, naming whose it is. */
  label: string;
  /** The code set here, or undefined where none is. */
  set?: string;
  /** What stands without it, by the table's name: a repository's workspace's language; undefined for none. */
  inherited?: string;
  table: LanguageOption[];
  /** Set this code, or with undefined clear it. */
  onChoose: (language: string | undefined) => void;
}) {
  const { t } = useTranslation();
  const chosen = set ?? UNSET;
  return (
    <SelectField
      ariaLabel={label}
      value={chosen}
      onChange={(next) => { if (next !== chosen) onChoose(next === UNSET ? undefined : next); }}
      options={[
        {
          value: UNSET,
          label: inherited ? t('settings.sessionLanguage.inherit', { name: inherited }) : t('settings.sessionLanguage.notSet'),
        },
        ...table.map((row) => ({ value: row.code, label: row.name })),
      ]}
    />
  );
}
