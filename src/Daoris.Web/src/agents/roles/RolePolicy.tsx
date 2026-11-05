import type { TFunction } from 'i18next';
import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { CheckField, SelectField } from '../../design/ui';
import { digits, type PolicyFields, type Preset, stepped, worksOf } from './staffPolicyDraft';

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

/** A number typed or composed: the digits kept as they come, full-width ones as ASCII at the composition's end. */
export function NumberField({ value, label, onChange, step, width = 'w-16' }: {
  value: string; label: string; onChange: (value: string) => void; step?: (by: 1 | -1) => void; width?: string;
}) {
  const composing = useRef(false);
  return (
    <input
      aria-label={label} inputMode="numeric" autoComplete="off" value={value} className={`${FIELD} ${width}`}
      onCompositionStart={() => { composing.current = true; }}
      onChange={(event) => onChange(composing.current ? event.target.value : digits(event.target.value))}
      onCompositionEnd={(event) => { composing.current = false; onChange(digits(event.currentTarget.value)); }}
      onKeyDown={step ? (event) => {
        if (event.key !== 'ArrowUp' && event.key !== 'ArrowDown') return;
        event.preventDefault();
        step(event.key === 'ArrowUp' ? 1 : -1);
      } : undefined}
    />
  );
}

/** *How it chooses*: the three presets, which fill the form and save nothing. */
export function HowItChooses({ preset, onPreset, disabled }: { preset: Preset; onPreset: (preset: Preset) => void; disabled: boolean }) {
  const { t } = useTranslation();
  return (
    <div className="flex flex-wrap items-center gap-2">
      <span className="text-small font-medium text-ink-soft">{t('settings.ai.policy.how')}</span>
      <SelectField
        value={preset} disabled={disabled} onChange={(value) => onPreset(value as Preset)} ariaLabel={t('settings.ai.policy.how')}
        options={(['first', 'even', 'weighted'] as const).map((each) => ({ value: each, label: t(`settings.ai.policy.preset.${each}`) }))}
      />
    </div>
  );
}

/** A choice's weight, stepped by the arrow keys within 0 to 10. */
export function WeightField({ n, value, onChange }: { n: number; value: string; onChange: (value: string) => void }) {
  const { t } = useTranslation();
  return <NumberField value={value} label={t('settings.ai.policy.weightOf', { n })} onChange={onChange} step={(by) => onChange(stepped(value, by))} width="w-14" />;
}

/** The limits of a choice in a line: none, or its works, daily budget and share. */
export function limitsText(t: TFunction, fields: PolicyFields, labelOf: (work: string) => string): string {
  const parts = [
    fields.works ? t('settings.ai.policy.limits.worksOnly', { works: fields.works.map(labelOf).join(t('settings.ai.role.and')) }) : null,
    fields.budget?.trim() ? t(fields.budgetKind === 'tokens' ? 'settings.ai.policy.limits.tokens' : 'settings.ai.policy.limits.starts', { count: Number(fields.budget) }) : null,
    fields.share?.trim() ? t('settings.ai.policy.limits.share', { count: Number(fields.share) }) : null,
  ].filter((part): part is string => part !== null);
  return parts.length > 0 ? parts.join(' · ') : t('settings.ai.policy.limits.none');
}

/** *Limits ▸* of one choice: its share, its daily budget (starts or reported tokens) and the works it serves. */
export function LimitsFold({ n, fields, takes, labelOf, disabled, onChange }: {
  n: number; fields: PolicyFields; takes: readonly string[]; labelOf: (work: string) => string; disabled: boolean; onChange: (patch: Partial<PolicyFields>) => void;
}) {
  const { t } = useTranslation();
  const works = worksOf(takes);
  const served = fields.works ?? works;
  return (
    <details className="min-w-0">
      <summary className="cursor-pointer text-small text-ink-soft wrap-anywhere" aria-label={t('settings.ai.policy.limits.of', { n })}>
        {t('settings.ai.policy.limits.summary', { what: limitsText(t, fields, labelOf) })}
      </summary>
      <div className="mt-1 grid gap-2 rounded-control border border-line p-2">
        <label className="flex flex-wrap items-center gap-2 text-small text-ink-soft">
          {t('settings.ai.policy.limits.shareLabel')}
          <NumberField value={fields.share ?? ''} label={t('settings.ai.policy.limits.shareOf', { n })} onChange={(share) => onChange({ share })} width="w-14" />
        </label>
        <div className="flex flex-wrap items-center gap-2 text-small text-ink-soft">
          {t('settings.ai.policy.limits.budgetLabel')}
          <SelectField
            value={fields.budgetKind ?? 'none'} disabled={disabled} ariaLabel={t('settings.ai.policy.limits.budgetOf', { n })}
            onChange={(kind) => onChange(kind === 'none' ? { budget: '', budgetKind: undefined } : { budgetKind: kind as 'starts' | 'tokens', budget: fields.budget || '1' })}
            options={[
              { value: 'none', label: t('settings.ai.policy.limits.budgetNone') },
              { value: 'starts', label: t('settings.ai.policy.limits.budgetStarts') },
              { value: 'tokens', label: t('settings.ai.policy.limits.budgetTokens') },
            ]}
          />
          {fields.budgetKind !== undefined && (
            <NumberField value={fields.budget ?? ''} label={t('settings.ai.policy.limits.budgetCountOf', { n })} onChange={(budget) => onChange({ budget })} width="w-24" />
          )}
        </div>
        {fields.budgetKind === 'tokens' && <p className="m-0 text-small text-ink-faint">{t('settings.ai.policy.limits.tokensNote')}</p>}
        {works.length > 1 && (
          <fieldset className="m-0 flex flex-wrap gap-x-3 gap-y-1 border-0 p-0">
            <legend className="mb-1 p-0 text-small text-ink-soft">{t('settings.ai.policy.limits.worksLegend')}</legend>
            {works.map((work) => (
              <CheckField
                key={work} checked={served.includes(work)} disabled={disabled} label={labelOf(work)} className="text-small text-ink"
                onChange={(checked) => {
                  const next = checked ? works.filter((each) => each === work || served.includes(each)) : served.filter((each) => each !== work);
                  onChange({ works: next.length === works.length || next.length === 0 ? null : next });
                }}
              />
            ))}
          </fieldset>
        )}
      </div>
    </details>
  );
}

