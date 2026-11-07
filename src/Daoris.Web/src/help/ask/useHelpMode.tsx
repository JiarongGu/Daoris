import type * as React from 'react';
import { useEffect, useRef, useState } from 'react';
import { sentence } from '../../locales/format';
import { helpModeOf, useDriver, useEndChat, useHelpWired, usePlugins, useSetHelpMode } from '../../bridge/shell';
import { Button } from '../../design/ui';
import { FullAccessAsk, HelpModeChip, useModeChoice } from '../access/HelpModePicker';
import { Refused } from '../../work/InlineConfirm';
import { fullAdds } from '../access/modes';
import { WhatItHas } from '../access/WhatItHas';
import { type HelpConversationRow } from '../history';
import { type StarterDoor } from '../starters';
import { type Session } from '../../data/api';
import { type TFunction } from 'i18next';

export function useHelpMode({ shown, known, live, picked, setChosen, readied, readiedLive, setRound, t, onGo }: {
  shown: Session | null;
  known: HelpConversationRow | null;
  live: boolean;
  picked: Session | null;
  setChosen: React.Dispatch<React.SetStateAction<string | null>>;
  readied: string | null;
  readiedLive: boolean;
  setRound: React.Dispatch<React.SetStateAction<number>>;
  t: TFunction<"translation", undefined>;
  onGo: ((door: StarterDoor) => void) | undefined;
}) {
  // Ask Daoris's access (HELPMODE1b, D183 points 2-4; the modes design §1, §5, §6). The chip shows the mode the conversation in view
  // runs under, its record's, or the mode chosen before any start; a shell that answers no mode offers no choice, and no *What it
  // has*, which came after it.
  const driver = useDriver();
  const offered = driver.data !== undefined && 'helpMode' in driver.data;
  const selectedMode = helpModeOf(driver.data?.helpMode);
  const runningMode = shown && known?.mode !== undefined ? helpModeOf(known.mode) : null;
  // The driver ends a conversation whose access changed at its turn's end, and the person's next words
  // resume it under the access chosen. Seen running under another access, it is kept in view as chosen once it ends, so those
  // words go on in it rather than start another beside it. The ending and the driver's word that it no longer runs may come in either
  // order, so having been seen pending is enough.
  const swapping = Array.isArray(driver.data?.helpSwapping) ? driver.data.helpSwapping : [];
  const swappingKey = swapping.join(' ');
  const swapped = useRef<string | null>(null);
  useEffect(() => {
    if (!shown) return;
    if (live) {
      if (swapping.includes(shown.id)) swapped.current = shown.id;
      return;
    }
    if (swapped.current !== shown.id) return;
    swapped.current = null;
    if (picked === null) setChosen(shown.id);
    // Judged as the conversation shown, its running and the driver's list move; the rest is read from this render.
  }, [shown?.id, live, swappingKey]);
  const setMode = useSetHelpMode();
  // Its own mutation, so ending the one opened ahead never reads as the person's New conversation pending.
  const retire = useEndChat();
  const [chipOpen, setChipOpen] = useState(0);
  // A refusal is said where its press was (ACCESSASK1, the UI/UX design R3): full access's inside its ask, a switch back under the
  // chip, never in the composer's line, which is about the words.
  const choice = useModeChoice(selectedMode, (mode, answered) => setMode.mutate({ mode }, {
    onSuccess: () => {
      answered.done();
      // The conversation opened ahead was started under the mode before: nobody has spoken in it, so it is ended and the
      // next opens under the mode chosen, rather than the person's first words reaching one the chip no longer names.
      if (readied === null || !readiedLive) return;
      retire.mutate(readied, { onSuccess: () => setRound((was) => was + 1) });
    },
    onError: (error) => answered.refused(sentence(error)),
  }));
  const [wiredOpen, setWiredOpen] = useState(false);
  useEffect(() => {
    setWiredOpen(false);
  }, [shown?.id]);
  const wired = useHelpWired(shown?.id ?? null, offered && (wiredOpen || choice.confirming));
  // What full access would add is read only while it is asked: the plugins' servers waiting for an approval, and what the
  // newest start of the conversation in view lacked.
  const plugins = usePlugins({ enabled: choice.confirming });
  const addsKnown = (plugins.isSuccess || plugins.isError) && (shown === null || wired.isSuccess || wired.isError);
  const adds = addsKnown ? fullAdds(wired.data?.receipts[0], plugins.data?.plugins) : null;
  const access = offered ? (
    <div className="grid min-w-0 gap-1.5">
      {/* Above the chip, as the design's sketch draws it, so the composer's box stays where the person types (§3.4). */}
      {choice.confirming && <FullAccessAsk adds={adds} onEnable={choice.enable} onClose={choice.cancel} />}
      <div className="flex min-w-0 items-center gap-2">
        <HelpModeChip selected={selectedMode} running={runningMode} busy={setMode.isPending} open={chipOpen} onPick={choice.pick} />
        {shown && (
          <Button
            variant="ghost"
            aria-pressed={wiredOpen}
            className="ml-auto h-7 shrink-0 px-1.5 text-small"
            onClick={() => setWiredOpen((was) => !was)}
          >
            {t('help.wired.open')}
          </Button>
        )}
      </div>
      {choice.refusal && <Refused sentence={choice.refusal} />}
    </div>
  ) : undefined;
  const whatItHas = offered && wiredOpen && shown ? (
    <WhatItHas
      wired={wired.data}
      loading={wired.isPending}
      error={wired.isError ? sentence(wired.error) : null}
      onRetry={() => void wired.refetch()}
      onDoor={(door) => onGo?.(door)}
      onMode={() => setChipOpen((was) => was + 1)}
      onClose={() => setWiredOpen(false)}
    />
  ) : undefined;
  return { access, whatItHas, offered, selectedMode };
}
