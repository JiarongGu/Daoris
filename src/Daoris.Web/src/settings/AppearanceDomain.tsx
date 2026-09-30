import { useTranslation } from 'react-i18next';
import { useThemeChoice } from '../theme';
import { Card, Segmented, SettingRow } from '../ui';

/**
 * How this window looks and speaks — per viewer, remembered by this window's profile like the scope,
 * never machine wiring and never a file (D66). Both settings apply at once; nothing to save.
 */
export function AppearanceDomain() {
  const { t, i18n } = useTranslation();
  const [theme, setTheme] = useThemeChoice();
  const language = i18n.language.startsWith('zh') ? 'zh' : 'en';

  return (
    // No title of its own: the domain list names it, and a card alone in its domain would say it twice.
    <Card>
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
            options={[
              { value: 'en', label: t('language.en') },
              { value: 'zh', label: t('language.zh') },
            ]}
          />
        )}
      />
    </Card>
  );
}
