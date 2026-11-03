import type { TFunction } from 'i18next';
import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import type { StaffCondition } from '../../data/wiring';
import { CheckField, SelectField } from '../../design/ui';
import {
  digits, FIXED_CONDITIONS, MOST_BOUND, type OfferedMove, OFFERED_MOVES, type PolicyDraft, type PolicyFields, type PolicyMove, type Preset, stepped, worksOf,
} from './staffPolicyDraft';

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

const TODAY = 'today';
const USUAL = 'usual';

/** A number typed or composed: the digits kept as they come, full-width ones as ASCII at the composition's end. */
function NumberField({ value, label, onChange, step, width = 'w-16' }: {
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

function MoveRow({ offered, move, rule, conversation, disabled, onMove }: {
  offered: OfferedMove; move: PolicyMove | undefined; rule: 'order' | 'weight'; conversation: boolean; disabled: boolean; onMove: (move: PolicyMove | null) => void;
}) {
  const { t } = useTranslation();
  const { condition } = offered;
  const label = t(`settings.ai.policy.when.${condition}`);
  const allowed = offered.actions.filter((action) => !(conversation && condition === 'limit' && (action === 'wait' || action === 'ask')) && (rule === 'weight' || action !== 'weighted'));
  const actions = move && !allowed.includes(move.do) ? [...allowed, move.do] : allowed;
  const tries = offered.tries;
  return (
    <div className="flex flex-wrap items-center gap-2" data-move={condition}>
      <span className="min-w-48 text-small text-ink">{label}</span>
      <SelectField
        value={move?.do ?? TODAY} disabled={disabled} ariaLabel={label}
        onChange={(value) => onMove(value === TODAY ? null : { do: value, after: move?.after ?? '', upTo: move?.upTo ?? '' })}
        options={[{ value: TODAY, label: t('settings.ai.policy.action.today') }, ...actions.map((action) => ({ value: action, label: t(`settings.ai.policy.action.${action}`) }))]}
      />
      {move && tries !== undefined && move.do !== 'retry' && (
        <span className="inline-flex items-center gap-1 text-small text-ink-soft">
          {t('settings.ai.policy.after')}
          <SelectField
            value={move.after || USUAL} disabled={disabled} ariaLabel={t('settings.ai.policy.afterOf', { condition: label })}
            onChange={(value) => onMove({ ...move, after: value === USUAL ? '' : value })}
            options={[{ value: USUAL, label: t('settings.ai.policy.usual') }, ...Array.from({ length: tries }, (_, at) => ({ value: String(at + 1), label: String(at + 1) }))]}
          />
          {t(condition === 'failed' ? 'settings.ai.policy.failures' : 'settings.ai.policy.tries')}
        </span>
      )}
      {move?.do === 'wait' && (
        <span className="inline-flex items-center gap-1 text-small text-ink-soft">
          {t('settings.ai.policy.upTo')}
          <NumberField value={move.upTo} label={t('settings.ai.policy.upToOf', { condition: label })} onChange={(upTo) => onMove({ ...move, upTo })} width="w-16" />
          {t('settings.ai.policy.minutes')}
        </span>
      )}
      <span className="text-small text-ink-faint">{t(`settings.ai.policy.today.${condition}`)}</span>
    </div>
  );
}

/** *When a choice cannot go on*: a move per condition the door takes, each *as today* unless set; the rest said as fixed. */
export function MovesFold({ policy, rule, fields, conversation, disabled, onMove, onMost }: {
  policy: PolicyDraft; rule: 'order' | 'weight'; fields: readonly string[]; conversation: boolean; disabled: boolean;
  onMove: (condition: StaffCondition, move: PolicyMove | null) => void; onMost: (most: string) => void;
}) {
  const { t } = useTranslation();
  const any = Object.keys(policy.moves).length + Object.keys(policy.carried).length > 0;
  return (
    <details className="grid gap-2" open={any}>
      <summary className="cursor-pointer text-small font-medium text-ink-soft">{t('settings.ai.policy.moves.title')}</summary>
      <div className="mt-1 grid gap-2">
        {OFFERED_MOVES.filter((offered) => fields.includes(`moves.${offered.condition}`)).map((offered) => (
          <MoveRow
            key={offered.condition} offered={offered} move={policy.moves[offered.condition]} rule={rule} conversation={conversation} disabled={disabled}
            onMove={(move) => onMove(offered.condition, move)}
          />
        ))}
        {FIXED_CONDITIONS.map((condition) => (
          <p key={condition} className="m-0 text-small text-ink-faint">
            {t(`settings.ai.policy.when.${condition}`)}: {t(`settings.ai.policy.today.${condition}`)}
          </p>
        ))}
        {any && !conversation && (
          <div className="flex flex-wrap items-center gap-2 text-small text-ink-soft">
            {t('settings.ai.policy.mostBefore')}
            <SelectField
              value={policy.most || '3'} disabled={disabled} ariaLabel={t('settings.ai.policy.mostOf')} onChange={onMost}
              options={Array.from({ length: MOST_BOUND }, (_, at) => ({ value: String(at + 1), label: String(at + 1) }))}
            />
            {t('settings.ai.policy.mostAfter')}
          </div>
        )}
        <p className="m-0 text-small text-ink-faint">{t('settings.ai.policy.always')}</p>
      </div>
    </details>
  );
}
