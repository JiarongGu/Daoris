import { useTranslation } from 'react-i18next';
import { CheckField, SettingRow } from '../../design/ui';
import { useUxTrail } from '../../app/uxTrail';

/**
 * The UX trail's switch (UXTRAIL1): off by default; on, this window keeps the pages the person opens and the presses they make, never
 * typing or a field's value, on this desktop only, and how long a step that waited on them took to be pressed.
 */
export function UxTrailSetting() {
  const { t } = useTranslation();
  const { on, set } = useUxTrail();
  return (
    <SettingRow
      label={t('settings.uxTrail.label')}
      why={t('settings.uxTrail.body')}
      control={<CheckField hideLabel checked={on} disabled={set.isPending} onChange={(next) => set.mutate(next)} label={t('settings.uxTrail.label')} />}
    >
      <p className="m-0 text-small text-ink-soft">{t(on ? 'settings.uxTrail.state.on' : 'settings.uxTrail.state.off')}</p>
    </SettingRow>
  );
}
