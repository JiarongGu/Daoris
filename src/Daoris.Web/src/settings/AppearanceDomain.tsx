import { useTranslation } from 'react-i18next';
import { useThemeChoice } from '../design/theme';
import { Card, Segmented, SettingList, SettingRow } from '../design/ui';
import { languages } from '../locales/index';
import { languageName, pageLanguage } from '../locales/languages';

/**
 * How this window looks and speaks — per viewer, remembered by this window's profile like the scope,
 * never machine wiring and never a file. Both settings apply at once; nothing to save.
 */
export function AppearanceDomain() {
  const { t, i18n } = useTranslation();
  const [theme, setTheme] = useThemeChoice();
  const language = pageLanguage(i18n.language);

  return (
    // No title of its own: the main area's header names the domain, and a card alone in it would
    // say it twice.
    <Card>
      <SettingList>
      <SettingRow
        label={t('settings.theme.label')}
        hint={t('settings.theme.hint')}
        control={(
          <Segmented
            label={t('settings.theme.label')}
            value={theme}
            onChange={setTheme}
            options={[
              { value: 'system', label: t('settings.theme.system') },
              { value: 'light', label: t('settings.theme.light') },
              { value: 'dark', label: t('settings.theme.dark') },
            ]}
          />
        )}
      />
      <SettingRow
        label={t('settings.language.label')}
        hint={t('settings.language.hint')}
        control={(
          <Segmented
            label={t('settings.language.label')}
            value={language}
            onChange={(next) => void i18n.changeLanguage(next)}
            // Each language named in itself — a person who cannot read the current one can still
            // find their own.
            options={languages.map((each) => ({ value: each, label: languageName(each) }))}
          />
        )}
      />
      </SettingList>
    </Card>
  );
}
