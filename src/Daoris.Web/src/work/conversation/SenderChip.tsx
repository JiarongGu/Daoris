import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { clockOf } from '../../locales/format';

/**
 * The head of a word's turn (COMMS1h, the comms design §7): who it is from, as the one sender rule reads it, and when it was taken;
 * beside it, the door to the word's home where that is elsewhere and the fold of a long word. The doors wrap under the sender at a
 * narrow width, and come after it in the keyboard's order.
 */
export function SenderChip({ label, at, children }: { label: string; at: string; children?: ReactNode }) {
  const { i18n } = useTranslation();
  return (
    <div data-sender className="flex flex-wrap items-baseline justify-between gap-x-2">
      <span className="min-w-0 text-meta text-ink-faint wrap-anywhere">
        <span>{label}</span>
        {' · '}
        <time dateTime={at}>{clockOf(at, new Date(), i18n.language)}</time>
      </span>
      {children && <span className="flex items-baseline gap-2">{children}</span>}
    </div>
  );
}

/** *In the channel*: opens the channel where a message the turn took is drawn whole (COMMS1h), rather than drawing it again here. */
export function ChannelDoor({ message, onOpen }: { message: string; onOpen: (message: string) => void }) {
  const { t } = useTranslation();
  return (
    <button
      type="button"
      data-channel-door={message}
      onClick={() => onOpen(message)}
      className="border-0 bg-transparent p-0 text-meta text-ink-faint hover:text-ink"
    >
      {t('work.conversation.inChannel')}
    </button>
  );
}
