import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Tip } from '../ui';
import type { Usage } from './conversation';

/** From here on, the ring says the window is filling: the harness compacts or refuses near the top. */
export const CONTEXT_WARN = 0.8;

const RADIUS = 6;
const AROUND = 2 * Math.PI * RADIUS;

/**
 * How full the session's context is (CONV5): used against the window, as the agent last reported it,
 * under the composer where the reference keeps it.
 *
 * @remarks
 * **Absent is never zero** (TOOL3). A session that has reported nothing draws a dashed ring and a dash,
 * and its tip says which absence it is: a structured door that has not reported yet, or a door that
 * carries only text and never will. A ring at 0% would claim a measurement nobody made.
 *
 * **The numbers are the harness's**, carried as given (D57 §4): no model is named and no price claimed.
 * The high-water mark is in the tip, because the latest reading falls when the harness compacts, and
 * the most the session held is what says it came close.
 */
export function ContextRing({ usage, door }: {
  usage?: Usage;
  /** Whether this session's door reports context at all — undefined where the roster has not said. */
  door?: 'structured' | 'text';
}) {
  const { t, i18n } = useTranslation();
  const number = (value: number) => value.toLocaleString(i18n.language);

  if (!usage || usage.size <= 0) {
    const why = door === 'text' ? 'text' : door === 'structured' ? 'pending' : 'unknown';
    return (
      <Tip content={t(`work.context.${why}`)} side="top">
        <span
          tabIndex={0}
          role="img"
          aria-label={t('work.context.none')}
          className="inline-flex items-center gap-1.5 text-meta text-ink-faint"
        >
          <Ring fraction={0} dashed />
          <span aria-hidden>—</span>
        </span>
      </Tip>
    );
  }

  const fraction = Math.min(1, usage.used / usage.size);
  const warn = fraction >= CONTEXT_WARN;
  const percent = usage.used > 0 && fraction < 0.01 ? '<1%' : `${Math.round(fraction * 100)}%`;
  const said = t('work.context.measured', {
    used: number(usage.used), size: number(usage.size), percent, most: number(usage.most),
  });

  return (
    <Tip content={said} side="top">
      <span
        tabIndex={0}
        role="meter"
        aria-label={t('work.context.label')}
        aria-valuemin={0}
        aria-valuemax={usage.size}
        aria-valuenow={usage.used}
        aria-valuetext={said}
        data-tone={warn ? 'warn' : 'normal'}
        className={cn('inline-flex items-center gap-1.5 font-mono text-meta', warn ? 'text-warn' : 'text-ink-faint')}
      >
        <Ring fraction={fraction} warn={warn} />
        {percent}
      </span>
    </Tip>
  );
}

function Ring({ fraction, warn = false, dashed = false }: { fraction: number; warn?: boolean; dashed?: boolean }) {
  return (
    <svg width={16} height={16} viewBox="0 0 16 16" aria-hidden className="shrink-0 -rotate-90">
      <circle
        cx={8} cy={8} r={RADIUS} fill="none" strokeWidth={2}
        className="stroke-line-strong"
        strokeDasharray={dashed ? '2 2' : undefined}
      />
      {fraction > 0 && (
        <circle
          cx={8} cy={8} r={RADIUS} fill="none" strokeWidth={2} strokeLinecap="round"
          className={warn ? 'stroke-warn' : 'stroke-accent'}
          strokeDasharray={`${Math.max(0.5, fraction * AROUND)} ${AROUND}`}
        />
      )}
    </svg>
  );
}
