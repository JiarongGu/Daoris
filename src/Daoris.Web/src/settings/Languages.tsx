import { useTranslation } from 'react-i18next';
import { Card, Chip, Prose, SectionTitle, SelectField, SettingRow } from '../ui';

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

/**
 * The language each workspace's sessions write to the person in (LANG1c, D142 point 7), and each repository's, beside the
 * lines: their questions, closing notes, decline reasons and last words.
 *
 * @remarks
 * **The work's, never the window's** (D142 point 8): the window's language is Appearance's, its viewer's, and neither sets the
 * other. **What each row shows is the driver's own resolution**, read rather than recomputed, so the page cannot name a
 * language a session would not be handed. Each field is the screen's half of `daoris driver language` (D50); a repository's
 * page in Repositories is the other screen, and a refusal comes back as the driver's sentence.
 */
export function LanguageList({ languages, workspaceLanguages, table, onSet }: {
  languages: RepositoryLanguage[];
  /** What each workspace sets, by name. A workspace with no repository here still shows its own. */
  workspaceLanguages: { workspace: string; language: string }[];
  table: LanguageOption[];
  onSet: (change: LanguageChange) => void;
}) {
  const { t } = useTranslation();
  const circles = [...new Set([...languages.map((one) => one.workspace), ...workspaceLanguages.map((w) => w.workspace)])]
    .sort((a, b) => a.localeCompare(b));
  const nameOf = (code: string | undefined) => table.find((row) => row.code === code)?.name;

  return (
    <Card id="settings-session-language" className="mt-3.5 scroll-mt-3">
      <SectionTitle>{t('settings.sessionLanguage.title')}</SectionTitle>
      <Prose className="mt-1 text-small text-ink-soft">{t('settings.sessionLanguage.body')}</Prose>

      {circles.length === 0 && <Prose className="mt-3">{t('settings.sessionLanguage.none')}</Prose>}

      {circles.map((workspace) => {
        const shared = workspaceLanguages.find((w) => w.workspace === workspace)?.language;
        return (
          <section key={workspace} aria-label={workspace} className="mt-3 border-t border-line pt-3">
            <SettingRow
              label={<Chip accent>{workspace}</Chip>}
              hint={t('settings.sessionLanguage.workspaceHint')}
              control={(
                <LanguageChoice
                  label={t('settings.sessionLanguage.field', { name: workspace })}
                  set={shared}
                  table={table}
                  onChoose={(language) => onSet({ workspace, ...(language ? { language } : {}) })}
                />
              )}
            />
            {languages.filter((one) => one.workspace === workspace).map((one) => (
              <SettingRow
                key={one.repository}
                label={one.repository}
                hint={one.language && one.source
                  ? t(`settings.sessionLanguage.from.${one.source}`, { name: one.name ?? nameOf(one.language) ?? one.language })
                  : t('settings.sessionLanguage.from.none')}
                control={(
                  <LanguageChoice
                    label={t('settings.sessionLanguage.field', { name: one.repository })}
                    set={one.source === 'repository' ? one.language : undefined}
                    inherited={nameOf(shared)}
                    table={table}
                    onChoose={(language) => onSet({ repository: one.repository, ...(language ? { language } : {}) })}
                  />
                )}
              />
            ))}
          </section>
        );
      })}
    </Card>
  );
}
