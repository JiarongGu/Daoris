import { useTranslation } from 'react-i18next';
import { type Carry, CarryFields, useCarry } from '../compose/carry';
import { Button, Drawer, EmptyState, SelectField } from '../ui';

/** An ask being written: its circle, its words, a receiver if the person names one, and what it carries. */
export type AskDraft = Carry & {
  /** The circle chosen here — read only when the page is scoped to none of several. */
  circle: string;
  sentence: string;
  /** The receiver the person named, or empty: the declarations propose, and nothing is published. */
  to: string;
  /**
   * The person's review choice for the ask's work (REVIEWENV1g, design §1.5): `off`, `on` or an environment's name; empty
   * leaves each repository's rule to decide, which is what an ask carried before.
   */
  review?: string;
  /** Their words with the choice, kept with it. */
  reviewWords?: string;
};

/** Radix Select cannot carry an empty value, so "let the declarations propose" travels as a sentinel. */
const NOBODY = '*';
/** And "each repository's rule decides" the same way. */
const BY_RULE = '*';

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
 * **Naming a receiver is optional.** Named, the ask is published to it at once, as `--to` does.
 * Left unnamed, the declarations tier proposes and publishes nothing (INT4a), unless the machine sets
 * an intake agent, which reads the ask and publishes on its own (INT4b). 🔴 So what the composer
 * promises follows `intake` (UX5 U34): it said *nothing is published until you name a receiver*,
 * which an intake breaks, and a door that cannot know, a browser, promises neither.
 *
 * It carries what the quest composer carries, through the same fields and the same drop, paste and
 * chooser (`compose/carry`), so the two cannot drift. Props only (components §2).
 *
 * **The person may choose the review of its work** (REVIEWENV1g, D154 point 3; the review environment design §1.5): each
 * repository's rule by default, which sends nothing; a review in the default environment, in an environment the circle's
 * rules declare, or none, with their words, which the host judges and refuses in its sentence for a repository that declares
 * none. Only where a shell says which environments are declared is a named one offered; `on` and `off` need no rule read.
 */
export function AskComposer({
  draft, onChange, fixed, circles, receivers, busy = false, onSubmit, onCancel, intake, environments = [],
}: {
  /** The review environments the circle's rules declare, by name (REVIEWENV1a); none where no shell says. */
  environments?: string[];
  draft: AskDraft;
  onChange: (draft: AskDraft) => void;
  /** The circle the ask is made in when the page decides it; null asks the person. */
  fixed: string | null;
  /** Every circle this machine holds — what the person chooses among when nothing is fixed. */
  circles: string[];
  /** Who can be named: the repositories the host says can be asked, in the ask's circle (D70). */
  receivers: string[];
  /** Whether this machine sets an intake agent: true, false, or absent where the door cannot know. */
  intake?: boolean | null;
  busy?: boolean;
  onSubmit: () => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const carry = useCarry(draft, (next) => onChange({ ...draft, ...next }), true);
  const circle = fixed ?? draft.circle;
  const ready = !busy && Boolean(circle) && Boolean(draft.sentence.trim());
  const meta = <span className="font-mono text-meta text-ink-faint">{t('asks.compose.meta')}</span>;

  // 🔴 A machine with no repository holds no circle. The form offered an empty choice of one, and a
  // sentence written in full could never be sent: seen on the installed window. So it says so instead.
  if (!fixed && circles.length === 0) {
    return (
      <Drawer
        title={t('asks.compose.title')}
        onClose={onCancel}
        meta={meta}
        footer={<Button variant="ghost" onClick={onCancel}>{t('common.close')}</Button>}
      >
        <EmptyState icon="projects" headline={t('asks.compose.nowhere.headline')} body={t('asks.compose.nowhere.body')} />
      </Drawer>
    );
  }

  return (
    <Drawer
      title={t('asks.compose.title')}
      onClose={onCancel}
      meta={meta}
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
        <p className="m-0 text-body text-ink-soft">
          {t(intake === true ? 'asks.compose.hintIntake' : intake === false ? 'asks.compose.hint' : 'asks.compose.hintUnknown')}
        </p>
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
        <label className="grid gap-1 text-small text-ink-soft">
          {t('asks.compose.review')}
          <SelectField
            value={draft.review || BY_RULE}
            onChange={(review) => onChange({ ...draft, review: review === BY_RULE ? '' : review })}
            ariaLabel={t('asks.compose.review')}
            options={[
              { value: BY_RULE, label: t('asks.compose.reviewRule') },
              { value: 'on', label: t('asks.compose.reviewOn') },
              ...environments.map((name) => ({ value: name, label: t('asks.compose.reviewIn', { environment: name }) })),
              { value: 'off', label: t('asks.compose.reviewOff') },
            ]}
          />
        </label>
        {draft.review && (
          <input
            aria-label={t('asks.compose.reviewWords')}
            placeholder={t('asks.compose.reviewWords')}
            value={draft.reviewWords ?? ''}
            onChange={(e) => onChange({ ...draft, reviewWords: e.target.value })}
            className="min-h-[1.9rem] rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
          />
        )}
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
