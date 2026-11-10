import type { TFunction } from 'i18next';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, SelectField } from '../../design/ui';

/** An integration policy as the file keeps it, each key null or absent where unset (the driver reads its defaults). */
export type LandingIntegrate = {
  request?: string | null; commit?: string | null; batch?: number | string | null; order?: string | null; gates?: string | null;
  unconfirmed?: { unreached?: string | null; reached?: string | null } | null;
  conflicts?: string | null; line?: string | null;
};

/** The chooser's value for "left to the default": never a value any choice or plugin takes. */
export const NO_CHOICE = '-';

/** The values each integrate choice offers, in the order `LandingKey` lists them; the first option of each is "the default". */
export const INTEGRATE_CHOICES = {
  request: ['owner', 'done', 'steward'],
  commit: ['merge', 'rebase', 'squash'],
  order: ['arrival', 'apart'],
  gates: ['declared', 'none'],
  unreached: ['split', 'hold', 'land'],
  reached: ['split', 'hold', 'land'],
  conflicts: ['session', 'hold'],
} as const;

export type IntegrateChoice = keyof typeof INTEGRATE_CHOICES;

const POLICY_CHOICES: readonly IntegrateChoice[] = ['commit', 'order', 'gates', 'unreached', 'reached'];

/** The integrate choices as the draft holds them: text, empty where unset. */
export type IntegrateDraft = Record<IntegrateChoice | 'batch' | 'line', string>;

export const integrateDraft = (integrate?: LandingIntegrate | null): IntegrateDraft => ({
  request: integrate?.request ?? '', commit: integrate?.commit ?? '', batch: integrate?.batch == null ? '' : String(integrate.batch),
  order: integrate?.order ?? '', gates: integrate?.gates ?? '',
  unreached: integrate?.unconfirmed?.unreached ?? '', reached: integrate?.unconfirmed?.reached ?? '',
  conflicts: integrate?.conflicts ?? '', line: integrate?.line ?? '',
});

/** The draft as a press sends it: only the keys set, the batch a number where it reads as one, anything else left for the driver to refuse. */
export const integrateOf = (draft: IntegrateDraft): LandingIntegrate => {
  const batch = draft.batch.trim();
  return {
    ...(draft.request ? { request: draft.request } : {}),
    ...(draft.commit ? { commit: draft.commit } : {}),
    ...(batch ? { batch: Number.isNaN(Number(batch)) ? batch : Number(batch) } : {}),
    ...(draft.order ? { order: draft.order } : {}),
    ...(draft.gates ? { gates: draft.gates } : {}),
    ...(draft.unreached || draft.reached
      ? { unconfirmed: { ...(draft.unreached ? { unreached: draft.unreached } : {}), ...(draft.reached ? { reached: draft.reached } : {}) } }
      : {}),
    ...(draft.conflicts ? { conflicts: draft.conflicts } : {}),
    ...(draft.line.trim() ? { line: draft.line.trim() } : {}),
  };
};

/** The integrate keys in the order the file writes them, set ones only, so two spellings of one policy compare equal. */
export const integrateKey = (integrate?: LandingIntegrate | null) => {
  if (!integrate) return '';
  const { unreached, reached } = integrate.unconfirmed ?? {};
  const keys = [integrate.request, integrate.commit, integrate.batch, integrate.order, integrate.gates, unreached, reached,
    integrate.conflicts, integrate.line];
  return JSON.stringify(keys.map((value) => (value === null || value === undefined || value === '' ? null : String(value))));
};

/** What a policy says in a line: its batch, order and gates as the design's §3 shows, "batch 4, oldest first, gates declared". */
export function policySays(t: TFunction, integrate: LandingIntegrate): string {
  const { batch, order, gates } = integrate;
  const parts = [
    ...(batch == null || batch === '' ? [] : [t('settings.landing.integrate.says.batch', { count: Number(batch) })]),
    ...(order ? [t(`settings.landing.integrate.says.order.${order}`, { defaultValue: order })] : []),
    ...(gates ? [t(`settings.landing.integrate.says.gates.${gates}`, { defaultValue: gates })] : []),
  ];
  return parts.length > 0 ? t('settings.landing.integrate.says', { parts: parts.join(t('settings.landing.integrate.join')) }) : t('settings.landing.integrate.saysPlain');
}

const inputClass = 'min-w-0 max-w-full rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

/** The integrate choices, the batch and the line a policy names, each at its default until chosen; `only` limits the choices offered. */
export function IntegrateFields({ name, value, onChange, busy, only }: {
  name: string;
  value: IntegrateDraft;
  onChange: (next: IntegrateDraft) => void;
  busy?: boolean;
  only?: readonly IntegrateChoice[];
}) {
  const { t } = useTranslation();
  const offered = (Object.keys(INTEGRATE_CHOICES) as IntegrateChoice[]).filter((choice) => only === undefined || only.includes(choice));
  return (
    <div role="group" aria-label={t('settings.landing.integrate.group', { name })} className="flex min-w-0 basis-full flex-wrap items-center gap-2">
      {offered.map((choice) => (
        <SelectField
          key={choice}
          ariaLabel={t(`settings.landing.integrate.${choice}`, { name })}
          value={value[choice] || NO_CHOICE}
          onChange={(next) => onChange({ ...value, [choice]: next === NO_CHOICE ? '' : next })}
          disabled={busy}
          options={[
            { value: NO_CHOICE, label: t('settings.landing.integrate.choice.default') },
            ...INTEGRATE_CHOICES[choice].map((each) => ({ value: each, label: t(`settings.landing.integrate.choice.${choice}.${each}`) })),
          ]}
        />
      ))}
      <input
        aria-label={t('settings.landing.integrate.batch', { name })}
        value={value.batch}
        onChange={(event) => onChange({ ...value, batch: event.target.value })}
        placeholder={t('settings.landing.integrate.batchHint')}
        inputMode="numeric"
        spellCheck={false}
        className={`w-24 ${inputClass}`}
      />
      <input
        aria-label={t('settings.landing.integrate.line', { name })}
        value={value.line}
        onChange={(event) => onChange({ ...value, line: event.target.value })}
        placeholder={t('settings.landing.integrate.lineHint')}
        spellCheck={false}
        className={`w-40 ${inputClass}`}
      />
    </div>
  );
}

/**
 * **A line's policy**: how work is integrated into the line, edited by `SET_LINE`'s `policy` and
 * withdrawn with `null`. The values are `LandingKey`'s to judge, never the page's.
 */
export function LinePolicyField({ name, set, busy, onSave }: {
  name: string;
  /** The policy that stands for the line, as the driver answers it; absent where none is set. */
  set?: LandingIntegrate | null;
  busy?: boolean;
  onSave: (policy: LandingIntegrate | null) => void;
}) {
  const { t } = useTranslation();
  const [draft, setDraft] = useState(() => integrateDraft(set));
  const standing = integrateKey(set);
  // The answer moves when either door edits the file, and the field follows it.
  useEffect(() => setDraft(integrateDraft(set)), [standing]); // eslint-disable-line react-hooks/exhaustive-deps
  const next = integrateOf(draft);
  const changed = integrateKey(next) !== standing;
  return (
    <form
      className="flex min-w-0 flex-wrap items-center gap-2"
      onSubmit={(event) => {
        event.preventDefault();
        if (changed) onSave(Object.keys(next).length > 0 ? next : null);
      }}
    >
      <IntegrateFields name={name} value={draft} onChange={setDraft} busy={busy} only={POLICY_CHOICES} />
      <Button type="submit" disabled={busy || !changed}>{t('settings.lines.set')}</Button>
      {set && <Button variant="ghost" disabled={busy} onClick={() => onSave(null)}>{t('settings.lines.clear')}</Button>}
    </form>
  );
}
