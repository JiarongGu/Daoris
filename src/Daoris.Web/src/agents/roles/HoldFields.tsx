import { useTranslation } from 'react-i18next';
import { CheckField, FormField, SelectField } from '../../design/ui';
import { ACTS, AT_ONCE, DECISION_KINDS, decidedKinds, HOLDERS, type HoldDraft, holdReads, toggleKind, WINDOW_BOUNDS } from './appointedHold';

const FIELD = 'w-24 min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink';

/**
 * **An appointed role's fields in the role editor**: a supervisor's kinds of decision, the acts it holds (a supervisor's grouped by the
 * kinds it decides), who holds it, and the supervisor's window, in place of the choices a working role has. The role's own kind is the
 * record's and is not changed here; the machine's generic supervisor keeps every kind, said, since it holds the person's rules.
 */
export function HoldFields({ hold, disabled, machine = false, onChange }: { hold: HoldDraft; disabled: boolean; machine?: boolean; onChange: (hold: HoldDraft) => void }) {
  const { t } = useTranslation();
  const toggle = (act: string) => onChange({ ...hold, acts: hold.acts.includes(act) ? hold.acts.filter((each) => each !== act) : [...hold.acts, act] });
  const check = (act: string) => (
    <CheckField
      key={act} checked={hold.acts.includes(act)} disabled={disabled} onChange={() => toggle(act)}
      label={t(`agents.setup.roles.hold.act.${act}`)} className="text-small text-ink"
    />
  );
  const fixed = machine && hold.decides.length === 0;
  return (
    <div className="grid gap-2">
      {hold.kind === 'supervisor' && (
        <fieldset className="m-0 grid gap-1 border-0 p-0">
          <legend className="mb-1 p-0 text-meta text-ink-faint">{t('agents.setup.roles.hold.field.decides')}</legend>
          {fixed ? (
            <p className="m-0 text-small text-ink-soft">{t('agents.setup.roles.hold.decides.machine')}</p>
          ) : (
            <>
              <CheckField
                checked={hold.decides.length === 0} disabled={disabled} onChange={() => onChange({ ...hold, decides: [] })}
                label={t('agents.setup.roles.hold.decides.every')} className="text-small text-ink"
              />
              {Object.keys(DECISION_KINDS).map((kind) => (
                <CheckField
                  key={kind} checked={hold.decides.includes(kind)} disabled={disabled} onChange={() => onChange(toggleKind(hold, kind))}
                  label={t(`agents.setup.roles.hold.decides.${kind}`)} className="text-small text-ink"
                />
              ))}
            </>
          )}
        </fieldset>
      )}
      <fieldset className="m-0 grid gap-1 border-0 p-0">
        <legend className="mb-1 p-0 text-meta text-ink-faint">{t('agents.setup.roles.hold.field.acts')}</legend>
        {hold.kind === 'supervisor'
          ? decidedKinds(hold.decides).map((kind) => (
            <div key={kind} role="group" aria-label={t(`agents.setup.roles.hold.decides.${kind}`)} className="grid gap-1">
              <span className="text-meta text-ink-faint">
                {t(`agents.setup.roles.hold.decides.${kind}`)}
                {AT_ONCE.includes(kind) ? ` · ${t('agents.setup.roles.hold.atOnce')}` : ''}
              </span>
              {(DECISION_KINDS[kind] ?? []).map(check)}
            </div>
          ))
          : ACTS[hold.kind].map(check)}
        {hold.kind === 'supervisor' && <p className="m-0 text-small text-ink-soft">{t('agents.setup.roles.hold.leaves')}</p>}
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
