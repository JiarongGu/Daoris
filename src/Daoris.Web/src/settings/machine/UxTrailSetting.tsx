import { useTranslation } from 'react-i18next';
import { CheckField, SettingRow } from '../../design/ui';

/** The trail's state as the shell reads it, and the press that sets it. */
export type UxTrailShown = { on: boolean; pending: boolean; onChange: (on: boolean) => void };

/**
 * The UX trail's switch: off by default; on, this window keeps the pages the person opens and the presses they make, never
 * typing or a field's value, on this desktop only, and how long a step that waited on them took to be pressed.
 */
export function UxTrailSetting({ on, pending, onChange }: UxTrailShown) {
  const { t } = useTranslation();
  return (
    <SettingRow
      label={t('settings.uxTrail.label')}
      why={t('settings.uxTrail.body')}
      control={<CheckField hideLabel checked={on} disabled={pending} onChange={onChange} label={t('settings.uxTrail.label')} />}
    >
      <p className="m-0 text-small text-ink-soft">{t(on ? 'settings.uxTrail.state.on' : 'settings.uxTrail.state.off')}</p>
    </SettingRow>
  );
}
