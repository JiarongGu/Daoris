import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { ExternalLink } from '../../design/links';

/**
 * The words *Signed in* sends as the park's answer: the agent's, never translated, so its next prompt reads the same in every
 * language; `question_ask` tells it they come back so.
 */
export const SIGNED_IN = 'Signed in';

const hostOf = (site: string): string => {
  try {
    return new URL(site).hostname;
  } catch {
    return site;
  }
};

/**
 * **A park waiting on the person's sign-in** (`question_ask`'s `site`): the site, *Open the sign-in…*, which opens it in Daoris's
 * browser where the window has one (else the system's), and *Signed in*, which answers the park. A sign-in Daoris's browser sees
 * there answers it as well, by the driver's look. A molecule: the press's door and pending are the card's.
 */
export function SignInAsk({ site, busy = false, onSigned }: {
  site: string;
  busy?: boolean;
  onSigned?: () => void;
}) {
  const { t } = useTranslation();
  const lead = t('work.wait.signIn.lead', { site: hostOf(site) });
  return (
    <div role="group" aria-label={lead} data-wait="sign-in" className="mt-2 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small">
      <span className="text-ink-soft">{lead}</span>
      <ExternalLink
        href={site}
        className="rounded-control border border-line-strong px-2 py-0.5 text-small text-ink no-underline hover:bg-raised"
      >
        {t('work.wait.signIn.open')}
      </ExternalLink>
      {onSigned && (
        <Button variant="primary" className="px-2 py-0.5 text-small" disabled={busy} onClick={onSigned}>
          {t('work.wait.signIn.done')}
        </Button>
      )}
    </div>
  );
}
