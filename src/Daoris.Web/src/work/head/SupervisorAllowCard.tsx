import { useTranslation } from 'react-i18next';
import { type AllowRead, allowSays } from './lineSupervision';

/** How long after it is kept the supervisor's answer to a held tool call is still said on the line's card. */
export const ALLOW_CARD_MINUTES = 10;

/**
 * The supervisor's answer to a tool call the session held for the person: *the supervisor allowed this once* (or refused it once), and
 * when. It takes effect where it is kept, so the card has no *Overrule*; it only says what happens to a request that no longer waits.
 *
 * @remarks
 * Drawn for the newest such answer on the line while it is recent, then gone: it is an account of an answer, not a notice.
 * A molecule: handed the decision.
 */
export function SupervisorAllowCard({ allow, now = Date.now() }: { allow: AllowRead | null; now?: number }) {
  const { t } = useTranslation();
  if (!allow || allow.shape !== 'request' || now - Date.parse(allow.keptAt) > ALLOW_CARD_MINUTES * 60_000) return null;
  const title = t('work.supervision.allow.title');
  return (
    <section aria-label={title} className="rounded-card border border-line bg-raised px-[1.15rem] py-3.5">
      <h3 className="m-0 text-small font-semibold">{title}</h3>
      <p className="m-0 mt-1.5 text-small text-ink-soft">{allowSays(t, allow)}</p>
    </section>
  );
}
