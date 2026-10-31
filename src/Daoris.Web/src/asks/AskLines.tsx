import { useTranslation } from 'react-i18next';
import { Pill, SESSION_TONE, type ShownState } from '../design/ui';
import type { AskLine } from '../work/entries';

/** A door into Sessions, in the accent, as the ask's other doors are. */
const DOOR = 'cursor-pointer border-0 bg-transparent p-0 text-left text-accent underline-offset-2 hover:underline';

/**
 * **An ask's sessions by entry** (SESSONE1c, D167 point 5, the co-working design §1.6): one line per repository's line of work
 * that began with the ask, however many records it holds, with its parts counted and its leading part's word. Each is a door
 * into Sessions where Sessions is here (an entry opens at its leading part, whose page lists the rest), and named where it is not.
 *
 * @remarks
 * A molecule: the lines arrive worked out (`askLines`). The ask's intake keeps its own section, and the quests keep theirs
 * (`AskWork`): this says whose work each repository's session is, which is what the person counts.
 */
export function AskLines({ lines, onAttend }: { lines: readonly AskLine[]; onAttend?: (session: string) => void }) {
  const { t } = useTranslation();
  return (
    <ul className="m-0 grid list-none gap-1.5 p-0">
      {lines.map((line) => (
        <li key={line.entry} className="flex min-w-0 flex-wrap items-center gap-x-2.5 gap-y-1">
          {onAttend
            ? <button type="button" onClick={() => onAttend(line.opens)} className={`${DOOR} min-w-0 wrap-anywhere text-body`}>{line.repository}</button>
            : <span className="min-w-0 wrap-anywhere text-body text-ink">{line.repository}</span>}
          {line.parts > 1 && <span className="text-meta text-ink-faint">{t('work.rail.parts', { count: line.parts })}</span>}
          {/* A word this page has none for is said as the driver wrote it, quiet. */}
          <Pill tone={SESSION_TONE[line.shown as ShownState] ?? 'neutral'}>{t(`sessionState.${line.shown}`, { defaultValue: line.shown })}</Pill>
        </li>
      ))}
    </ul>
  );
}
