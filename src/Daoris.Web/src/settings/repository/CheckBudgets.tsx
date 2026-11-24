import type { TFunction } from 'i18next';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, SettingRow } from '../../design/ui';
import { shellWord } from '../../lib/shellWord';
import type { Answered } from '../../work/InlineConfirm';

/** One gate's budget as the driver read it (`CHECK_BUDGETS`): where its timeout comes from, and what this machine keeps for it. */
export type GateBudgetView = {
  budget: { minutes: number; source: 'pin' | 'learned' | 'default'; why?: string | null; rise?: { from: number; to: number } | null };
  greens: number;
  pin?: { minutes: number; why: string } | null;
  memoryGb?: number | null;
  alone?: boolean;
};

/** A repository's check budgets on this machine: each gate's, and the values the repository's declaration no longer holds. */
export type CheckBudgetsView = {
  repository: string;
  gates: Record<string, GateBudgetView>;
  setTimeoutMs: Record<string, number>;
  treeBaseline?: string | null;
  devkit?: string | null;
  localInputs?: string[] | null;
  patternsFile?: string | null;
};

/** One change as `SET_CHECK_BUDGETS` takes it: a member, the gate or set it is on, its value or a clear, and the why a pin needs. */
export type BudgetChange = { member: string; name?: string; value?: unknown; clear?: boolean; why?: string };

/** A gate's timeout in a sentence: its minutes and where they come from, a rise said. */
export function budgetSaid(t: TFunction, gate: GateBudgetView): string {
  const { minutes, source, why, rise } = gate.budget;
  if (source === 'pin') return t('settings.budgets.pinned', { minutes, why: why ?? '' });
  if (source === 'default') return t('settings.budgets.default', { minutes });
  const learned = t('settings.budgets.learned', { minutes, count: gate.greens });
  return rise ? `${learned}${t('settings.budgets.rose', { from: rise.from })}` : learned;
}

/** The row's folded line: how many gates are pinned and how many run slower, or that the times are learned. */
export function budgetsSummary(t: TFunction, view: CheckBudgetsView): string {
  const gates = Object.values(view.gates);
  const pinned = gates.filter((gate) => gate.budget.source === 'pin').length;
  const slower = gates.filter((gate) => gate.budget.rise).length;
  const parts = [
    ...(pinned > 0 ? [t('settings.budgets.summary.pinned', { n: pinned })] : []),
    ...(slower > 0 ? [t('settings.budgets.summary.slower', { n: slower })] : []),
  ];
  return parts.length > 0 ? parts.join(t('projects.setup.summary.listJoin')) : t('settings.budgets.summary.learned');
}

const FIELD = 'w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint';

/** The values of a repository this machine keeps beside its gates' budgets, each with the word that sets it. */
const VALUES = [
  { member: 'treeBaseline', key: 'treeBaseline', lines: false },
  { member: 'devkit', key: 'devkit', lines: false },
  { member: 'localInputs', key: 'localInputs', lines: true },
  { member: 'patternsFile', key: 'patternsFile', lines: false },
] as const;

const textOf = (value: string | string[] | null | undefined): string => (Array.isArray(value) ? value.join('\n') : value ?? '');

/**
 * **A repository's check budgets on this machine**: each gate's timeout with where it comes from, a pin set over it or taken away, and the
 * values of the repository this machine keeps (its tree baseline, devkit version, local inputs and patterns file), the same record
 * `daoris settings checks` edits. A timeout is learned from a gate's green runs; a pin stands over it with its why; a gate that runs
 * slower is said once by the merge. The page judges nothing: a press goes out and the driver's refusal is said under it in its words.
 *
 * A molecule: every state is reached by its props, and every press goes out.
 */
export function CheckBudgetsRow({ repository, view, busy = false, onChange }: {
  repository: string;
  view: CheckBudgetsView;
  busy?: boolean;
  onChange: (change: BudgetChange, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [pinning, setPinning] = useState<string | null>(null);
  const [minutes, setMinutes] = useState('');
  const [why, setWhy] = useState('');
  const [values, setValues] = useState<Record<string, string>>({});
  const [refused, setRefused] = useState<string | null>(null);
  const names = Object.keys(view.gates).sort((a, b) => a.localeCompare(b));

  const press = (change: BudgetChange, landed: () => void = () => undefined) => {
    setRefused(null);
    onChange(change, { done: landed, refused: setRefused });
  };
  const closePin = () => { setPinning(null); setMinutes(''); setWhy(''); };
  const shown = (member: (typeof VALUES)[number]) => values[member.member] ?? textOf(view[member.key]);

  return (
    <SettingRow
      label={t('settings.budgets.label')}
      hint={t('settings.budgets.twin', { repository: shellWord(repository, '<repository>') })}
      why={t('settings.budgets.why')}
    >
      <div className="grid gap-2">
        {names.length === 0 && <p className="m-0 text-small text-ink-soft">{t('settings.budgets.none')}</p>}
        <ul aria-label={t('settings.budgets.list', { repository })} className="m-0 grid list-none gap-1 p-0">
          {names.map((name) => {
            const gate = view.gates[name]!;
            return (
              <li key={name} className="grid gap-1">
                <span className="inline-flex min-w-0 flex-wrap items-baseline gap-x-2">
                  <span className="font-medium text-body text-ink">{name}</span>
                  <span className="text-small text-ink-soft">{budgetSaid(t, gate)}</span>
                  {gate.memoryGb != null && <span className="text-small text-ink-faint">{t('settings.budgets.memory', { gb: gate.memoryGb })}</span>}
                  {gate.alone === true && <span className="text-small text-ink-faint">{t('settings.budgets.alone')}</span>}
                  <Button variant="ghost" disabled={busy} aria-label={t('settings.budgets.pin', { name })} onClick={() => { closePin(); setPinning(name); }}>
                    {t('settings.budgets.pinAct')}
                  </Button>
                  {gate.pin && (
                    <Button variant="ghost" disabled={busy} aria-label={t('settings.budgets.unpin', { name })} onClick={() => press({ member: 'pin', name, clear: true })}>
                      {t('settings.budgets.unpinAct')}
                    </Button>
                  )}
                </span>
                {pinning === name && (
                  <form
                    className="flex flex-wrap items-center gap-2"
                    onSubmit={(event) => {
                      event.preventDefault();
                      if (!busy && minutes.trim() && why.trim()) press({ member: 'pin', name, value: Number(minutes), why: why.trim() }, closePin);
                    }}
                  >
                    <input aria-label={t('settings.budgets.minutesOf', { name })} inputMode="numeric" value={minutes} className={`${FIELD} max-w-24`} onChange={(event) => setMinutes(event.target.value)} />
                    <input aria-label={t('settings.budgets.whyOf', { name })} placeholder={t('settings.budgets.whyHint')} value={why} className={`${FIELD} max-w-96`} onChange={(event) => setWhy(event.target.value)} />
                    <Button type="submit" variant="ghost" disabled={busy || !minutes.trim() || !why.trim()} aria-label={t('settings.budgets.savePin', { name })}>
                      {t('settings.budgets.save')}
                    </Button>
                    <Button variant="ghost" disabled={busy} onClick={closePin}>{t('settings.budgets.cancel')}</Button>
                  </form>
                )}
              </li>
            );
          })}
        </ul>
        {VALUES.map((member) => (
          <div key={member.member} className="flex flex-wrap items-center gap-2">
            <label className="grid min-w-0 flex-1 gap-0.5">
              <span className="text-caption text-ink-faint">{t(`settings.budgets.value.${member.key}`)}</span>
              {member.lines
                ? <textarea aria-label={t(`settings.budgets.value.${member.key}`)} rows={2} spellCheck={false} value={shown(member)} className={FIELD}
                    onChange={(event) => setValues((now) => ({ ...now, [member.member]: event.target.value }))} />
                : <input aria-label={t(`settings.budgets.value.${member.key}`)} spellCheck={false} value={shown(member)} className={FIELD}
                    onChange={(event) => setValues((now) => ({ ...now, [member.member]: event.target.value }))} />}
            </label>
            <Button variant="ghost" disabled={busy || shown(member).trim() === textOf(view[member.key]).trim() || shown(member).trim() === ''} aria-label={t('settings.budgets.saveValue', { what: t(`settings.budgets.value.${member.key}`).toLowerCase() })}
              onClick={() => press({ member: member.member, value: member.lines ? shown(member).split('\n').map((line) => line.trim()).filter(Boolean) : shown(member).trim() }, () => setValues(({ [member.member]: _gone, ...rest }) => rest))}>
              {t('settings.budgets.save')}
            </Button>
            <Button variant="ghost" disabled={busy || textOf(view[member.key]) === ''} aria-label={t('settings.budgets.clearValue', { what: t(`settings.budgets.value.${member.key}`).toLowerCase() })}
              onClick={() => press({ member: member.member, clear: true }, () => setValues(({ [member.member]: _gone, ...rest }) => rest))}>
              {t('settings.budgets.clear')}
            </Button>
          </div>
        ))}
        {refused && <p role="alert" className="m-0 text-small text-danger">{refused}</p>}
      </div>
    </SettingRow>
  );
}
