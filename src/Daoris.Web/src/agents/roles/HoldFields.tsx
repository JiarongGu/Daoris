import { useTranslation } from 'react-i18next';
import { FormField, SelectField } from '../../design/ui';
import { ACTS, HOLDERS, type HoldDraft, holdReads, WINDOW_BOUNDS } from './appointedHold';

const FIELD = 'w-24 min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink';

/**
 * **An appointed role's fields in the role editor**: the acts it holds, who holds it, and the supervisor's window, in place of the
 * choices a working role has. The kind is the record's own and is not changed here.
 */
export function HoldFields({ hold, disabled, onChange }: { hold: HoldDraft; disabled: boolean; onChange: (hold: HoldDraft) => void }) {
  const { t } = useTranslation();
  const toggle = (act: string) => onChange({ ...hold, acts: hold.acts.includes(act) ? hold.acts.filter((each) => each !== act) : [...hold.acts, act] });
  return (
    <div className="grid gap-2">
      <fieldset className="m-0 grid gap-1 border-0 p-0">
        <legend className="mb-1 p-0 text-meta text-ink-faint">{t('agents.setup.roles.hold.field.acts')}</legend>
        {ACTS[hold.kind].map((act) => (
          <label key={act} className="flex items-center gap-2 text-small text-ink">
            <input type="checkbox" checked={hold.acts.includes(act)} disabled={disabled} onChange={() => toggle(act)} />
            {t(`agents.setup.roles.hold.act.${act}`)}
          </label>
        ))}
      </fieldset>
      <FormField label={t('agents.setup.roles.hold.field.holder')}>
        <SelectField
          value={hold.holder} ariaLabel={t('agents.setup.roles.hold.field.holder')} onChange={(value) => onChange({ ...hold, holder: value as HoldDraft['holder'] })}
          options={HOLDERS[hold.kind].map((value) => ({ value, label: t(`agents.setup.roles.hold.holder.${value}`) }))}
        />
      </FormField>
      {hold.kind === 'supervisor' && (
        <FormField label={t('agents.setup.roles.hold.field.window')}>
          <input
            type="number" inputMode="numeric" min={WINDOW_BOUNDS.least} max={WINDOW_BOUNDS.most} value={hold.window} disabled={disabled}
            aria-label={t('agents.setup.roles.hold.field.window')} aria-invalid={!holdReads(hold)} className={FIELD}
            onChange={(event) => onChange({ ...hold, window: event.target.value })}
          />
        </FormField>
      )}
    </div>
  );
}
