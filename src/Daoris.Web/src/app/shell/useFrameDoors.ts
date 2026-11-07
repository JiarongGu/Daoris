import { useCallback, useRef, useState } from 'react';
import { type AskOpening } from '../../help/ask/askTypes';
import { useFrameClosings } from '../../work/frame/closings';
import { type ListMode } from '../../work/frame/layout';
import { usePlacements } from '../../work/frame/placements';

export function useFrameDoors() {
  // Ask Daoris is a view of the frame's regions on every view since DOCK1a: bumped each time the
  // person asks for it, and the frame opens whichever region holds it, on it.
  const [helpFocus, setHelpFocus] = useState(0);
  // What the person closed in the frame: held here, so the strip's toggles, the View menu and
  // the keys reach them from every view.
  const closings = useFrameClosings();
  // What the view's list is now, as the frame measured it: open, a strip, or laid over the
  // main area. A strip the window drew is not shown, and its toggle lays the list over.
  const [listMode, setListMode] = useState<ListMode | null>(null);
  const listShown = listMode === 'open' || listMode === 'over';
  // Where each view stands: held here, so the View menu's reset reaches it.
  const placements = usePlacements();
  // Its doors open it, never close it — `F1`, `Ctrl+Alt+I`, the palette, Quick Ask's *Open in the side
  // bar*; the region's own close and toggle are what close it, on every view alike.
  const openHelp = useCallback(() => setHelpFocus((was) => was + 1), []);
  const onSessions = useRef(false);
  // A question handed to Ask Daoris gets an id no earlier one had, whichever door handed it: each is let
  // go once sent, so an id counted from the last one held would repeat.
  const openings = useRef(0);
  // Quick Ask: open or not, and the question the palette handed it, one id per question.
  const [quick, setQuick] = useState(false);
  const [quickOpening, setQuickOpening] = useState<AskOpening | null>(null);
  // Words a right-click asked Search for, one id per ask, let go once in its box.
  const [searchAsked, setSearchAsked] = useState<{ text: string; id: number } | null>(null);
  // The side bar's question: the setup guide's *Set up with Ask Daoris*, held until it is sent.
  const [helpOpening, setHelpOpening] = useState<{ text: string; id: number } | null>(null);
  const askSetup = (message: string) => {
    openings.current += 1;
    setHelpOpening({ text: message, id: openings.current });
    openHelp();
  };
  /** Quick Ask, with a question it sends, or with a draft in its box for the person to finish (CTX1's *Ask Daoris about it*). */
  const askQuickly = (question?: string, draft = false) => {
    if (question) {
      openings.current += 1;
      setQuickOpening({ text: question, id: openings.current, ...(draft ? { draft } : {}) });
    }
    // 🔴 A beat after the palette closes, never with it: closing, the palette hands focus back to where
    // it was, which pulled it out of a box opened in the same step, and the box's trap caught it on its
    // frame instead of its message box (found looking at DOCK1d).
    window.setTimeout(() => setQuick(true), 0);
  };
  return { onSessions, closings, listMode, askSetup, listShown, placements, openHelp, askQuickly, searchAsked, setSearchAsked, openings, helpOpening, setHelpOpening, helpFocus, setListMode, quick, setQuick, quickOpening, setQuickOpening };
}
