import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import type { Session } from '../../data/api';
import { Button, Icon, Menu, Tip } from '../../design/ui';
import { useWidth } from '../../design/useWidth';
import { ACT_LOOK, actMenu, headerActs, type SessionActId } from '../acts';

/**
 * **The head's acts**, the session panel's: the primary act where the state has one (*Try again*, *Review*), then *Stop…* while it
 * is live, then ⋯ with the rest in the one rule's order (`headerActs`, the split the page's right-click offers whole). Which acts
 * are offered is the one rule's (`offeredActs`), handed in; a press is reported, and the one owner (`sessionActs.ts`) carries it
 * out. Narrow (640 px of its own width or less), *Stop…* is its square, its words its accessible name and its tip.
 */
export function HeadActs({ session, acts, primary = null, busy = false, onAct }: {
  session: Session;
  acts: readonly SessionActId[];
  primary?: SessionActId | null;
  busy?: boolean;
  onAct: (act: SessionActId) => void;
}) {
  const { t } = useTranslation();
  const own = useRef<HTMLDivElement>(null);
  const width = useWidth(own);
  const narrow = width !== undefined && width <= 640;
  const { lead, stop, rest } = headerActs(acts, primary);
  return (
    // One group names the buttons together, the ⋯ with them: its name is how the page's tests and readers find them.
    <div ref={own} role="group" aria-label={t('work.head.acts')} className="flex items-center gap-2">
      {lead && <Button variant="primary" disabled={busy} onClick={() => onAct(lead)}>{t(ACT_LOOK[lead].label)}</Button>}
      {stop && (narrow
        ? (
          <Tip content={t(ACT_LOOK.stop.label)}>
            <Button
              variant="danger" disabled={busy} aria-label={t(ACT_LOOK.stop.label)} onClick={() => onAct('stop')}
              className="h-[1.9rem] w-[1.9rem] justify-center px-0"
            >
              <Icon name={ACT_LOOK.stop.icon} size={15} />
            </Button>
          </Tip>
        )
        : <Button variant="danger" disabled={busy} onClick={() => onAct('stop')}>{t(ACT_LOOK.stop.label)}</Button>)}
      {rest.length > 0 && (
        <Menu.Root>
          <Menu.Trigger asChild>
            <Button variant="ghost" aria-label={t('work.head.more')} className="h-[1.9rem] w-[1.9rem] justify-center px-0">
              <Icon name="more" size={15} />
            </Button>
          </Menu.Trigger>
          <Menu.Content side="bottom" align="end" className="min-w-48">
            <Menu.Acts acts={actMenu(rest, t, (act) => onAct(act), false, session)} />
          </Menu.Content>
        </Menu.Root>
      )}
    </div>
  );
}
