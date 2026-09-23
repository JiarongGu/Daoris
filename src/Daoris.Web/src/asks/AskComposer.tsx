import { useTranslation } from 'react-i18next';
import { type Carry, CarryFields, useCarry } from '../compose/carry';
import { Button, Drawer, SelectField } from '../ui';

/** An ask being written: its circle, its words, a receiver if the person names one, and what it carries. */
export type AskDraft = Carry & {
  /** The circle chosen here — read only when the page is scoped to none of several. */
  circle: string;
  sentence: string;
  /** The receiver the person named, or empty: the declarations propose, and nothing is published. */
  to: string;
};

/** Radix Select cannot carry an empty value, so "let the declarations propose" travels as a sentinel. */
const NOBODY = '*';

/**
 * The ask composer (INT4c) — the screen twin of `daoris-driver ask --workspace … [--to …] [--file …]
 * [--url …] "…"` (D50, D65 §1a).
 *
 * @remarks
 * **An ask is made in ONE circle**, because its circle is whom it can reach (D48 §4). That is the
 * circle the page is scoped to, or the only one the machine holds — `fixed` — and then no choice is
 * offered. Scoped to none of several, the person says which: "every circle" has no single circle, and
 * assuming `default` would ask somewhere nobody chose.
 *
 * **Naming a receiver is optional.** Left unnamed, the declarations tier proposes and publishes
 * nothing (INT4a); named, the ask is published to it at once, as `--to` does.
 *
 * It carries what the quest composer carries, through the same fields and the same drop, paste and
 * chooser (`compose/carry`), so the two cannot drift. Props only (components §2).
 */
export function AskComposer({ draft, onChange, fixed, circles, receivers, busy = false, onSubmit, onCancel }: {
  draft: AskDraft;
  onChange: (draft: AskDraft) => void;
  /** The circle the ask is made in when the page decides it; null asks the person. */
  fixed: string | null;
  /** Every circle this machine holds — what the person chooses among when nothing is fixed. */
  circles: string[];
  /** Who can be named: the adopted repositories in the ask's circle. */
  receivers: string[];
  busy?: boolean;
  onSubmit: () => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const carry = useCarry(draft, (next) => onChange({ ...draft, ...next }), true);
  const circle = fixed ?? draft.circle;
  const ready = !busy && Boolean(circle) && Boolean(draft.sentence.trim());

  return (
    <Drawer
      title={t('asks.compose.title')}
      onClose={onCancel}
      meta={<span className="font-mono text-meta text-ink-faint">{t('asks.compose.meta')}</span>}
      footer={
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="primary" disabled={!ready} onClick={onSubmit}>{t('asks.compose.submit')}</Button>
          <Button variant="ghost" disabled={busy} onClick={onCancel}>{t('common.cancel')}</Button>
        </div>
      }
    >
      <form
        className="grid gap-3"
        onSubmit={(e) => { e.preventDefault(); if (ready) onSubmit(); }}
        {...carry.handlers}
      >
        <p className="m-0 text-body text-ink-soft">{t('asks.compose.hint')}</p>
        {fixed ? (
          <p className="m-0 text-small text-ink-soft">{t('asks.compose.askedIn', { circle: fixed })}</p>
        ) : (
          <label className="grid gap-1 text-small text-ink-soft">
            {t('asks.compose.circle')}
            <SelectField
              value={draft.circle} required
              onChange={(chosen) => onChange({ ...draft, circle: chosen, to: '' })}
              placeholder={t('asks.compose.circlePlaceholder')}
              ariaLabel={t('asks.compose.circle')}
              options={circles.map((name) => ({ value: name, label: name }))}
            />
          </label>
        )}
        <label className="grid gap-1 text-small text-ink-soft">
          {t('asks.compose.sentence')}
          <textarea
            required value={draft.sentence}
            onChange={(e) => onChange({ ...draft, sentence: e.target.value })}
            className="min-h-28 resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
          />
        </label>
        <label className="grid gap-1 text-small text-ink-soft">
          {t('asks.compose.to')}
          <SelectField
            value={draft.to || NOBODY}
            onChange={(to) => onChange({ ...draft, to: to === NOBODY ? '' : to })}
            ariaLabel={t('asks.compose.to')}
            options={[
              { value: NOBODY, label: t('asks.compose.toNobody') },
              ...receivers.map((name) => ({ value: name, label: name })),
            ]}
          />
        </label>
        <CarryFields
          carry={draft}
          filesLabel={t('asks.compose.filesLabel')}
          leftOff={carry.leftOff}
          dragging={carry.dragging}
          busy={busy}
          onLinks={(links) => onChange({ ...draft, links })}
          onAttach={carry.attach}
          onRemove={carry.remove}
        />
      </form>
    </Drawer>
  );
}
