import type * as React from 'react';
import { useEffect, useRef } from 'react';
import { registerAskFill } from '../../help/askFill';
import { clipped, type ContextDoors } from '../../menus/press';
import { type OpenPart } from '../opener';
import { failure } from '../../design/ui';
import { type View } from '../../menus/commands';
import { type Notify } from '../../design/atoms/Toasts';
import { type TFunction } from 'i18next';
import { type UseMutationResult } from '@tanstack/react-query';

export function useContextDoors({ notify, t, openings, setSearchAsked, open, attached, askQuickly, openBrowser, copyText }: {
  notify: Notify;
  t: TFunction<"translation", undefined>;
  openings: React.RefObject<number>;
  setSearchAsked: React.Dispatch<React.SetStateAction<{ text: string; id: number; } | null>>;
  open: (target: View, item?: string | null | undefined, part?: OpenPart | undefined) => void;
  attached: boolean;
  askQuickly: (question?: string | undefined, draft?: boolean) => void;
  openBrowser: UseMutationResult<{ opened: boolean; windows: string[]; }, Error, string | undefined, unknown>;
  copyText: React.RefObject<(text: string) => void>;
}) {
  // The right-click menu's doors: a copy, said once copied, since nothing on the screen shows it; Search
  // with the words in its box; and, a shell's, Quick Ask with them quoted and Daoris's browser at the address.
  const contextDoors: ContextDoors = {
    copy: (text) => {
      void navigator.clipboard?.writeText(text).then(() => notify(t('contextMenu.copied', { what: clipped(text) })), () => {});
    },
    search: (text) => {
      openings.current += 1;
      setSearchAsked({ text, id: openings.current });
      open('knowledge', null, { knowledge: 'search' });
    },
    ...(attached ? {
      ask: (text: string) => askQuickly(text, true),
      openInBrowser: (address: string) => openBrowser.mutate(address, { onError: failure(notify) }),
    } : {}),
  };
  // A page that composes words for Ask Daoris (the workflow editor's *Ask why* and *Fix it*) fills Quick Ask's box through this door.
  const fillAsk = useRef(askQuickly);
  fillAsk.current = askQuickly;
  useEffect(() => (attached ? registerAskFill((text) => fillAsk.current(text, true)) : undefined), [attached]);
  // Edit › Copy ID copies what a page's own *Copy … ID* would, and says so the same way.
  copyText.current = contextDoors.copy;
  return { contextDoors };
}
