import { useTranslation } from 'react-i18next';
import { ago } from '../locales/format';
import { Pill } from '../design/ui';
import { contextOffer } from '../menus/press';
import type { ElsewhereAsk } from '../work/channel';
import { ListRowDoor } from '../work/frame/ListPane';

/**
 * **A row of the asks held elsewhere** (SESSONE2h): a teammate's ask this machine has only the channel messages of. It has no record
 * here, so it has no title and no state, and is named by its id, with its circle and when its latest message came. Its unread count
 * is the channel's, against the person's local mark. A molecule: the press goes out.
 */
export function TeammateAskRow({ ask, chosen = false, onOpen }: { ask: ElsewhereAsk; chosen?: boolean; onOpen: (ask: ElsewhereAsk) => void }) {
  const { t } = useTranslation();
  return (
    // A row offers what it does on a right-click (CTX1, D138 §4): opening it, and its id.
    <li
      data-list-row=""
      {...contextOffer({
        label: `#${ask.id}`,
        acts: [
          { id: 'open', label: t('contextMenu.act.open'), onSelect: () => onOpen(ask) },
          { id: 'copy', label: t('contextMenu.act.copyAsk'), icon: 'copy', copy: ask.id },
        ],
      })}
    >
      <ListRowDoor chosen={chosen} onPress={() => onOpen(ask)}>
        <span className="flex items-baseline justify-between gap-2">
          <Pill tone="neutral">{t('asks.elsewhere.pill')}</Pill>
          {/* News waits on nobody: a neutral pill, never open's hue, the person's waits' alone (BADGEUX1). */}
          {ask.unread > 0 && <Pill tone="neutral">{t('asks.channel.unread', { count: ask.unread })}</Pill>}
        </span>
        <span className="mt-0.5 block truncate font-mono text-body text-ink">#{ask.id}</span>
        <span className="block truncate font-mono text-meta text-ink-faint">
          {ask.workspace && <><span className="text-accent">{t('asks.card.workspace', { workspace: ask.workspace })}</span> · </>}
          {t('asks.elsewhere.latest', { ago: ago(ask.latest) })}
        </span>
      </ListRowDoor>
    </li>
  );
}
