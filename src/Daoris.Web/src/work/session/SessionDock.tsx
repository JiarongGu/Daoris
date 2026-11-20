import { useContext, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import type { Quest } from '../../data/api';
import type { WorkAsk } from '../review/Lands';
import type { RuleAsk } from '../waits/ProposedRules';
import { leftFor, type Seek, useSought } from '../waits/WaitAnchor';
import type { RunWaitDoors, WaitProps } from '../waits/waitProps';
import { type Asker, dockCardsOf } from './dockCards';
import { PanelNarrow } from './panelLayout';
import { Dock } from './SessionPanel';

/**
 * **The dock**, the session panel's: every wait the person settles as its card (`dockCardsOf`), oldest first under its count, and
 * what its run waits on below them. The only place a wait is pressed on the session's page. A raising block's *To the wait* or the
 * head's state line leads here (`seek`): the card takes the focus, outlined, until the person moves on. Nothing waits, nothing is drawn.
 */
export function SessionDock({ answerBelow = false, workAsk, ruleAsk, seek, runWaits = null, quest = null, asker, narrow: given, now = Date.now(), onRefused, ...props }: WaitProps & {
  /** The foot's box takes the park's answer: a driven park's answer box, or a parked chat's composer. */
  answerBelow?: boolean;
  workAsk?: WorkAsk;
  ruleAsk?: RuleAsk;
  seek?: Seek | null;
  runWaits?: RunWaitDoors | null;
  quest?: Quest | null;
  /** Who asks, as the head names the session's agent; its role alone where the page knows no more. */
  asker?: Asker;
  /** Whether the panel is narrow; absent, as the panel it stands in says (`PanelNarrow`). */
  narrow?: boolean;
  now?: number;
  /** Where a press's refusal is said. */
  onRefused?: (sentence: string) => void;
}) {
  const { t } = useTranslation();
  const box = useRef<HTMLDivElement>(null);
  const inNarrow = useContext(PanelNarrow);
  const narrow = given ?? inNarrow;
  const { cards, onAnswer, foot, count } = dockCardsOf({
    t, props, answerBelow, workAsk, ruleAsk, runWaits, quest, refused: onRefused ?? (() => {}),
    asker: asker ?? { role: props.session.repository, agent: props.session.door, model: null },
  });
  // The sought card is looked for again only as the cards change, never on every render.
  const { letGo } = useSought(box, seek, undefined, cards.map((card) => card.id).join('|'));
  if (cards.length === 0 && !foot) return null;
  return (
    <div
      ref={box}
      className="contents"
      onBlur={(event) => {
        if (leftFor(event)) letGo();
      }}
    >
      <Dock waits={cards} now={now} narrow={narrow} onAnswer={onAnswer} count={count}>{foot}</Dock>
    </div>
  );
}
