import { useEffect, useRef, useState } from 'react';
import type { TFunction } from 'i18next';
import type { Compose } from '../../app/opener';
import { useStartChat } from '../../bridge/shell';
import { type Carry, NO_CARRY } from '../../compose/carry';
import type { Session } from '../../data/api';
import type { Notify } from '../../design/ui';
import { sentence } from '../../locales/format';
import { keepDraft } from '../conversation/drafts';
import type { Refusal } from '../conversation/useWords';
import { sessionOrigin } from '../identity';
import type { NewStart } from './NewConversation';
import { keepChosen, keepRecent, readChosen, readRecent } from './startRecent';

/**
 * *New conversation* as the Work frame holds it: its doors' presses, the composer's words and files kept
 * across a visit to a session, the drawers its picker opens, this machine's last starts, and one request id per press.
 */
export function useNewStart({ t, notify, attended, selected, onSelect, compose, attend, setRefusal }: {
  t: TFunction;
  notify: Notify;
  attended: Session | null;
  selected: string | null;
  onSelect: (id: string | null) => void;
  compose: (Compose & { id: number }) | null;
  attend: (id: string) => void;
  setRefusal: (refusal: Refusal | null) => void;
}) {
  // *New conversation*: bumped by each of its doors (the list's ＋, the menu, Ctrl+N, the palette),
  // which puts the attended session down and the focus in the composer's prompt.
  const [composing, setComposing] = useState(0);
  // Whether that press's focus is still owed: met once the prompt took it, so a composer that comes back by another path
  // (the attended session deleted) takes none.
  const [focusOwed, setFocusOwed] = useState(false);
  // The repository of the session attended at the press: the press puts the selection down, so the composer is told
  // where the person is. Each press sets it afresh, to null where nothing is attended.
  const [pressedFrom, setPressedFrom] = useState<string | null>(null);
  const newConversationPressed = () => {
    setPressedFrom(attended && sessionOrigin(attended) === null ? attended.repository : null);
    setComposing((count) => count + 1);
    setFocusOwed(true);
  };
  // The composer's words and files, held here so a visit to a session and back keeps them; a start that opened a
  // conversation lets them go. Its last refusal is said under Start until the next press.
  // The drawers the composer's picker opens over Sessions: Projects' own, on this frame's state, so the draft stays where
  // it is and App's views are not asked to navigate. `pickNew` and `reveal` are the answers it hands the composer, each a counted
  // ask; `refocus` is bumped when a drawer goes down, so the prompt takes the focus Radix would return to a press that is gone.
  const [adding, setAdding] = useState(false);
  const [importing, setImporting] = useState(false);
  const [pickNew, setPickNew] = useState<{ repository: string; n: number } | null>(null);
  const [reveal, setReveal] = useState<{ workspace: string; n: number } | null>(null);
  const [refocus, setRefocus] = useState(0);
  // An import that opened the picker keeps the focus in its field: the drawer's closing must not take it back to the prompt.
  const listOpened = useRef(false);
  const [newDraft, setNewDraft] = useState('');
  const [newCarry, setNewCarry] = useState<Carry>(NO_CARRY);
  const [startRefusal, setStartRefusal] = useState<string | null>(null);
  // This machine's last starts and the repository last chosen, read once and kept as they change.
  const [recentStarts, setRecentStarts] = useState(readRecent);
  const [chosenStart, setChosenStart] = useState(readChosen);
  // Kept in step with the storage, which `chosenStart` alone would leave stale within a mount.
  const chooseStart = (repository: string) => {
    setChosenStart(repository);
    keepChosen(repository);
  };
  // One press, one request id: a press again with the same words and choices after a failure carries the same id,
  // so a start that did open is answered rather than repeated; an answer lets it go.
  const pressed = useRef<{ key: string; id: string } | null>(null);
  const startChat = useStartChat();

  // *New conversation* puts the attended session down, so the composer stands in the main area.
  useEffect(() => {
    if (composing > 0 && selected !== null) onSelect(null);
    // Only a new press: the selection is read, not watched.
  }, [composing]);

  const onStart = (start: NewStart) => {
    setStartRefusal(null);
    const key = JSON.stringify([
      start.repository, start.text, start.door ?? '', start.profile ?? '', start.ownTree ?? null,
      start.files.map((file) => [file.name, file.size, file.lastModified]),
    ]);
    if (pressed.current?.key !== key) pressed.current = { key, id: crypto.randomUUID() };
    const requestId = pressed.current.id;
    startChat.mutate({ ...start, requestId }, {
      onSuccess: (result) => {
        pressed.current = null;
        // The ledger's own sentence: the tree is busy, the repository has no checkout here, the
        // harness is missing, the chosen account is logged out. Each names the action that fixes
        // it, which is why it is shown rather than summarised — whole, under Start where it is
        // pressed, with the words kept.
        if (!result.sessionId) {
          setStartRefusal(result.message);
          return;
        }
        const session = result.sessionId;
        setRecentStarts(keepRecent(start.repository));
        setNewDraft('');
        setNewCarry(NO_CARRY);
        // Opened without the words (`sent: false`): they wait in its box, put there before it is attended so its
        // draft is read with them, and the driver's sentence stands beside them. The files are named, as a send handed
        // back names them: the page no longer holds their bytes.
        if (!result.sent) keepDraft(session, start.text);
        attend(session);
        if (!result.sent) {
          setRefusal({ session, text: result.message });
          if (start.files.length > 0) {
            notify(t('work.composer.filesNotSent', { count: 1, names: start.files.map((file) => file.name).join(', ') }), 'error');
          }
        }
      },
      // No answer comes: the bridge's sentence where Start is pressed, and the press keeps its id for the next.
      onError: (error: unknown) => setStartRefusal(sentence(error)),
    });
  };

  // A go to New conversation: consumed by its id, as `intent` is by identity. The repository is the picker's pick, which waits
  // until the registry lists it; the words go above what is typed, as `keepDraft` puts them. Files are not touched.
  const [composed, setComposed] = useState<number | null>(null);
  if (compose && compose.id !== composed) {
    setComposed(compose.id);
    newConversationPressed();
    if (compose.repository) setPickNew((last) => ({ repository: compose.repository!, n: (last?.n ?? 0) + 1 }));
    if (compose.sentence) {
      const sentence = compose.sentence;
      setNewDraft((was) => [sentence, was.trim()].filter(Boolean).join('\n\n'));
    }
  }

  const drawerDown = (close: () => void) => () => {
    close();
    if (listOpened.current) listOpened.current = false;
    else setRefocus((count) => count + 1);
  };

  return {
    composing, focusOwed, setFocusOwed, pressedFrom, newConversationPressed, adding, setAdding, importing, setImporting, pickNew,
    setPickNew, reveal, setReveal, refocus, listOpened, newDraft, setNewDraft, newCarry, setNewCarry, startRefusal, recentStarts,
    chosenStart, chooseStart, startChat, onStart, drawerDown,
  };
}
