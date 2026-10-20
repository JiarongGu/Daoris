import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { EntryAcceptPlan } from '../review/EntryAccept';
import { beganLine } from './partsLife';
import { PartsFlow } from './PartsFlow';
import type { PartsProps } from './PartsStrip';

const PRESS = 'cursor-pointer border-0 bg-transparent p-0 text-small text-accent hover:underline';

/**
 * **A session's parts, folded to one line** (HEADFIT1; the UI/UX design §3.7 and R4, the UX7 design §6.2): which part the page is
 * on out of how many, and how the newest began, with a press that opens the parts' flowchart (`PartsFlow`) in its place.
 *
 * @remarks
 * Measured in a real browser, the flowchart alone cost 339 to 484 px between the record's head and its conversation, more than
 * the whole 200 px budget, and the relations beside it 126: both fold, as the milestone does, so the head says what is there and
 * the person opens what they want. **The line gives way to the flow rather than sitting above it**, so the parts are named once
 * (the flow's own heading says *Part 2 of 3* again) and *Hide the parts* is the press back, as the chain's *Show how it ran* is.
 *
 * - **Where the fold's state lives**: in this molecule, as the milestone's does, and the page mounts it under the line of work's
 *   id, not the session's: pressing a part in the open flow shows another session of the same line of work, and the flow the
 *   person is reading must not fold under their press. Another line of work starts folded.
 * - **Accept's plan is read for the open flow only**: folded, no landing node is drawn, and the person's press on Accept is not
 *   what opens it (a block opening itself under a press elsewhere would move the control the person just pressed).
 * - **The newest is the last of the list**, which is oldest first (the ledger's order, never a time): how it began is the strip's
 *   own sentence (`beganLine`).
 * - Nothing is drawn for a line of work of one part, as the strip and the flow draw none.
 */
export function PartsFold({ plan, defaultOpen = false, ...props }: PartsProps & {
  plan?: EntryAcceptPlan | null;
  /** Open on first draw: a story's or a test's, since the page's own default is folded. */
  defaultOpen?: boolean;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(defaultOpen);
  const { entry, attended } = props;
  const { parts } = entry;
  if (parts.length < 2) return null;

  if (open) {
    return (
      <div className="grid gap-1">
        <PartsFlow {...props} plan={plan} />
        <button type="button" onClick={() => setOpen(false)} className={`${PRESS} justify-self-start`}>
          {t('work.parts.hideAll')}
        </button>
      </div>
    );
  }

  const shown = Math.max(0, parts.findIndex((part) => part.session === attended));
  const newest = parts[parts.length - 1]!;
  return (
    <nav aria-label={t('work.parts.title')} className="flex min-w-0 flex-wrap items-baseline gap-x-2 text-small text-ink-faint">
      <span className="font-semibold">{t('work.parts.title')}</span>
      <span className="min-w-0 text-ink-soft [overflow-wrap:anywhere]">
        {t('work.parts.of', { n: shown + 1, total: parts.length })}
        {' · '}
        {t('work.parts.newest', { began: beganLine(t, newest, parts) })}
      </span>
      <button type="button" onClick={() => setOpen(true)} className={PRESS}>
        {t('work.parts.showAll')}
      </button>
    </nav>
  );
}
