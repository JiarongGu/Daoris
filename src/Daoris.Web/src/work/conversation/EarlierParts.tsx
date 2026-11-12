import { type RefObject, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { SessionConversation } from './SessionConversation';
import { drawnOf, EARLIER_AT_MOST } from './line';

/** One earlier part of a line, as its record says it: what its conversation is read by, and how it began. */
export type EarlierPart = {
  id: string;
  adapter?: string;
  chat: boolean;
  tree?: string | null;
  /** Its own record's liveness, never `false` for being earlier: `settle` reads a part that is not live as ended. */
  live: boolean;
  /** How it began, in the *Parts* strip's words, drawn as its boundary where it began by no move. */
  began?: string;
};

/**
 * The press above the oldest part drawn: how many earlier parts it draws, by the count and not by the language's
 * plural rule, since zh has one form and says the single part as its own sentence.
 */
export function ShowEarlier({ count, onPress }: { count: number; onPress: () => void }) {
  const { t } = useTranslation();
  return (
    <Button variant="ghost" onClick={onPress} className="justify-self-center text-small">
      {count === 1 ? t('work.parts.earlier_one') : t('work.parts.earlier_other', { count })}
    </Button>
  );
}

/**
 * **The parts a line's session draws above its newest one**: the line's earlier parts by its membership, oldest first, each its own
 * conversation under its boundary, read-only and read with its own record's liveness. At most the three nearest are drawn; above
 * them one press draws up to three more, and says how many.
 *
 * @remarks
 * Every part is a `SessionConversation` in its earlier mode, which owns neither the region's scroll nor any fact of the frame, so the
 * newest part below alone does. Opening an earlier part opens the line's session at it (`at`): drawn whatever the count, and
 * brought into view. The host keys this by the line and the part opened, so another starts the count again.
 */
export function EarlierParts({ line, at = null, scroller }: {
  line: readonly EarlierPart[];
  /** The earlier part the person opened, which the session opens at; null opens at its newest part. */
  at?: string | null;
  scroller: RefObject<HTMLElement | null>;
}) {
  const from = at ? line.findIndex((part) => part.id === at) : -1;
  const [pressed, setPressed] = useState(from < 0 ? 0 : Math.max(0, Math.ceil((line.length - from) / EARLIER_AT_MOST) - 1));
  const opened = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (from >= 0) opened.current?.scrollIntoView?.({ block: 'start' });
  }, [at, from]);
  const { drawn, more } = drawnOf(line.map((part) => part.id), pressed);
  const shown = line.filter((part) => drawn.includes(part.id));
  return (
    <>
      {more > 0 && <ShowEarlier count={more} onPress={() => setPressed((was) => was + 1)} />}
      {shown.map((part) => (
        <div
          key={part.id}
          ref={part.id === at ? opened : undefined}
          data-part={part.id}
          data-opened={part.id === at ? '' : undefined}
          className="mb-2 min-w-0 scroll-mt-12"
        >
          <SessionConversation
            mode="earlier"
            session={part.id}
            adapter={part.adapter}
            chat={part.chat}
            tree={part.tree}
            live={part.live}
            began={part.began}
            scroller={scroller}
          />
        </div>
      ))}
    </>
  );
}
