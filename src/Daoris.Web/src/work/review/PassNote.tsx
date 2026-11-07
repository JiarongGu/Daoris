import { useTranslation } from 'react-i18next';

/** How long an Accept waits for the pass that lands it: the driver's `GraphPasses.Bound`, in seconds. */
export const PASS_BOUND_SECONDS = 20;

/**
 * Beside *Accept* while it waits for the pass that lands it: why it waits, which is the pass and never a tree's hold,
 * and for how long before it says the work is accepted and lands when the pass does.
 *
 * @remarks A molecule: the bound arrives as a prop, the driver's by default.
 */
export function PassNote({ seconds = PASS_BOUND_SECONDS }: { seconds?: number }) {
  const { t } = useTranslation();
  return (
    <p className="m-0 border-l-[3px] border-st-open pl-2 text-small text-ink-soft">
      {t('work.review.acceptingPassNote', { seconds })}
    </p>
  );
}
