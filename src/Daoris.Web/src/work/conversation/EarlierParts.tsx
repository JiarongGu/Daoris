import { type RefObject, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { SessionConversation } from './SessionConversation';
import { drawnOf } from './line';

/** One earlier part of a line, as its record says it (SWITCHCTX1c-i): what its conversation is read by, and how it began. */
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
 * The press above the oldest part drawn (SWITCHCTX1c-i): how many earlier parts it draws, by the count and not by the language's
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
 * **The parts a session's page draws above the attended one** (SWITCHCTX1c-i; the switch-context design §2.7): the parts it
 * continues by `from`, oldest first, each its own conversation, read-only and read with its own record's liveness. At most the three
 * nearest are drawn; above them one press draws up to three more, and says how many.
 *
 * @remarks
 * Every part is a `SessionConversation` in its earlier mode, which owns neither the region's scroll nor any fact of the frame, so the
 * attended part below alone does. The host keys this by the attended part: attending another starts the count again, and an earlier
 * part attended draws its own ancestry, so the parts after it are not drawn.
 */
export function EarlierParts({ line, scroller }: { line: readonly EarlierPart[]; scroller: RefObject<HTMLElement | null> }) {
  const [pressed, setPressed] = useState(0);
  const { drawn, more } = drawnOf(line.map((part) => part.id), pressed);
  const shown = line.filter((part) => drawn.includes(part.id));
  return (
    <>
      {more > 0 && <ShowEarlier count={more} onPress={() => setPressed((was) => was + 1)} />}
      {shown.map((part) => (
        <div key={part.id} data-part={part.id} className="mb-2 min-w-0">
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
