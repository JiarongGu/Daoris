import { type KeyboardEvent, type ReactNode, type RefObject, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { isComposing } from '../lib/composing';
import { Button, Icon, Inline, Prose, Tip } from '../ui';
import { Splitter } from '../work/frame';
import type { Starter, StarterDoor } from './starters';

/** The conversation, as the organism hands it over: the record, the composer, and the way to start again. */
export type AskConversationSlot = {
  /** The newest conversation's record, or null when there is none to show. */
  body: ReactNode | null;
  /** What it proposes that waits for the person (HELP1c), or null. */
  proposals?: ReactNode;
  /** The box, or null where nothing may be written yet: a conversation being checked, or one that cannot go on (ASKHIST1c). */
  composer: ReactNode;
  /** Whether the conversation shown has ended, so a message starts the next. */
  ended: boolean;
  /**
   * What the next words do, said right above the box where they are written (ASKHIST1, as ASKHIST1c placed it): that an
   * ended one goes on, cannot and offers a new one from its words, or is being checked, each with its press.
   */
  note?: ReactNode;
  /** The way back to the conversation's tail, where the reader left it (ASKHIST1c): a strip of its own above the box. */
  tail?: ReactNode;
  /** What the open conversation is called (ASKHIST1c); null for a new one with nothing said yet. */
  title?: string | null;
  /** Changed to a new number to put the focus on the conversation's heading, as one is opened from the history. */
  focusHead?: number;
  /** Whether Escape goes back to the history: where the conversation shown was opened from it. */
  escapeToList?: boolean;
  /** Start again: finish the one running and clear the panel. Absent while there is nothing to clear. */
  onNew?: () => void;
  /** The history (ASKHIST1), drawn in the conversation's place while it is open, its own head with it. */
  history?: ReactNode;
  /** Whether the history is open. */
  historyOpen?: boolean;
  /** Open the history. Absent where there is none to show. */
  onHistory?: () => void;
};

/** Escape is a field's, a form's, a menu's or a dialog's before it is the conversation's. */
const KEEPS_ESCAPE = 'input, textarea, select, [contenteditable="true"], form, [role="menu"], [role="dialog"]';

/**
 * Ask Daoris (HELP1, D89): a panel beside whatever the person is looking at, which stays open across
 * views. With no agent named it holds the no-agent tier (HELP1d): what the machine lacks, each with the
 * screen that fixes it and the terminal command that does the same (D50). With one, the conversation
 * (HELP1a) takes the panel, and the starters show until there is a conversation to read.
 *
 * @remarks
 * A chat agent to help configure a workspace. A molecule: the starters and the conversation arrive made, a door press goes
 * out, and the region the conversation scrolls in is handed back to the organism that follows its tail.
 *
 * **Two views, each with its own head** (ASKHIST1c), the same in the side bar, in its own region and at 680 px, where the
 * dock is the whole frame: the history's (what it is, a new conversation, the search), drawn by `AskHistory`; and the open
 * conversation's, left-led: *← History*, its title whole, and a new conversation at its end. The head says which view is
 * open, where a *History* button at the far right looked the same open or closed. Under the conversation's words, outside
 * their scroll and in this order: the way back to the tail, what the next words do, and the box. Nothing floats over the
 * words.
 */
export function AskPanel({
  starters, helper, setup, conversation, scroller, framed = true, width, range, onResize, onResetWidth, onGo, onClose,
}: {
  starters: Starter[];
  /** The agent Ask Daoris runs on, or null. */
  helper: string | null;
  /**
   * How far the setup guide's required steps are done (SETUP1a, D97), which the starters lead to while
   * any is left — or absent where that is not known yet.
   */
  setup?: { done: number; of: number };
  /** The conversation, where an agent is named. */
  conversation?: AskConversationSlot;
  /** The region the conversation scrolls in. */
  scroller?: RefObject<HTMLDivElement | null>;
  /**
   * Whether it is its own region, with a frame, a title and a close — or a tab of the right dock on
   * Sessions, whose frame, title and close are the dock's (one right region, as VS Code's chat is a view
   * of its secondary side bar).
   */
  framed?: boolean;
  /** Its width where it stands alone, and how far its left edge may be dragged. */
  width?: number;
  range?: { min: number; max: number };
  onResize?: (width: number) => void;
  onResetWidth?: () => void;
  onGo: (door: StarterDoor) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const browsing = Boolean(conversation?.historyOpen && conversation.history);
  const talking = Boolean(conversation?.body);

  // Escape goes back to the history the conversation was opened from; a field, a menu or an input method takes it first.
  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (isComposing(event) || event.key !== 'Escape' || event.defaultPrevented) return;
    if (browsing || !conversation?.escapeToList || !conversation.onHistory) return;
    const target = event.target as HTMLElement;
    if (!event.currentTarget.contains(target) || target.closest(KEEPS_ESCAPE)) return;
    event.preventDefault();
    conversation.onHistory();
  };

  const starting = (
    <>
      <Prose className="m-0 text-small text-ink-soft">
        {t(helper ? 'help.introAgent' : 'help.intro', { agent: helper ?? '' })}
      </Prose>

      {starters.length === 0
        ? <Prose className="mt-3 text-small text-ink-faint">{t('help.none')}</Prose>
        : (
          <ul className="m-0 mt-3 grid list-none gap-2.5 p-0">
            {starters.map((starter) => (
              <li key={starter.id} className="rounded-control border border-line bg-raised px-3 py-2.5">
                <p className="m-0 text-small text-ink">{t(`help.starter.${starter.id}`, starter.values)}</p>
                {starter.command && (
                  <p className="m-0 mt-1 text-meta text-ink-faint">
                    <Inline text={t('help.command', { command: starter.command })} />
                  </p>
                )}
                <Button className="mt-2 text-small" onClick={() => onGo(starter.door)}>
                  {t(`help.door.${starter.door.section ?? starter.door.view}`)}
                </Button>
              </li>
            ))}
          </ul>
        )}

      {/* What is missing now, above; what is missing in the order a setup goes, one press away. */}
      {setup && setup.done < setup.of && (
        <Button
          variant="ghost"
          className="mt-2 px-0 text-small text-accent"
          onClick={() => onGo({ view: 'settings', section: 'start' })}
        >
          <Icon name="plan" size={13} />
          {t('help.setup', setup)}
        </Button>
      )}
    </>
  );

  // What the next words do: the organism's line, or the panel's own for one that ended.
  const note = conversation && (conversation.note
    ?? (talking && conversation.ended ? <Prose className="m-0 text-small text-ink-faint">{t('help.ended')}</Prose> : null));

  const content = browsing ? (
    // The history holds its own head, its search and its rows' scroll.
    <div className="flex min-h-0 flex-1 flex-col">{conversation!.history}</div>
  ) : (
    <div className="flex min-h-0 flex-1 flex-col" onKeyDown={onKeyDown}>
      {conversation && (
        <ConversationHead
          title={conversation.title ?? null}
          focus={conversation.focusHead ?? 0}
          onHistory={conversation.onHistory}
          onNew={conversation.onNew}
        />
      )}
      <div ref={scroller} className="min-h-0 flex-1 overflow-y-auto px-4 py-3">
        {talking ? (
          <>
            {conversation!.body}
            {conversation!.proposals}
          </>
        ) : starting}
      </div>
      {/* Outside the scroll, so neither ever covers the words above it. */}
      {conversation?.tail && <div className="flex shrink-0 justify-end px-4 pb-1 pt-1.5">{conversation.tail}</div>}
      {note && (
        <div className="grid shrink-0 grid-cols-[minmax(0,1fr)] justify-items-start gap-1.5 border-t border-line px-4 py-2">{note}</div>
      )}
      {conversation?.composer}
    </div>
  );

  if (!framed) return content;

  return (
    <aside
      aria-label={t('help.title')}
      className={cn('relative flex shrink-0 flex-col border-l border-line bg-page max-md:w-full', width === undefined && 'w-[24rem]')}
      style={width === undefined ? undefined : { width }}
    >
      {range && onResize && width !== undefined && (
        <Splitter
          label={t('help.resize')}
          value={width}
          min={range.min}
          max={range.max}
          edge="left"
          onChange={onResize}
          onReset={onResetWidth}
        />
      )}
      <header className="flex items-center gap-2 border-b border-line px-4 py-2.5">
        <Icon name="help" size={15} className="text-ink-soft" />
        <h2 className="m-0 text-body font-semibold text-ink">{t('help.title')}</h2>
        <Button variant="ghost" aria-label={t('help.close')} onClick={onClose} className="ml-auto px-1.5">
          <Icon name="x" size={14} />
        </Button>
      </header>
      {content}
    </aside>
  );
}

/**
 * The open conversation's head (ASKHIST1c), left-led: the way back to the history, the conversation's title whole and
 * wrapping where it must, so two pasted URLs read apart, and a new conversation at its end. Its heading takes the focus as a
 * conversation is opened from the history, so a keyboard lands where the person now is.
 */
function ConversationHead({ title, focus, onHistory, onNew }: {
  title: string | null;
  focus: number;
  onHistory?: () => void;
  onNew?: () => void;
}) {
  const { t } = useTranslation();
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    if (focus > 0) heading.current?.focus();
  }, [focus]);

  return (
    <header className="flex shrink-0 items-start gap-1 border-b border-line px-2 py-1">
      {onHistory && (
        <Button
          variant="ghost"
          aria-label={t('help.history.back')}
          onClick={onHistory}
          className="h-7 shrink-0 gap-1 px-1.5 text-small"
        >
          <Icon name="back" size={14} />
          {t('help.history.open')}
        </Button>
      )}
      <h3
        ref={heading}
        tabIndex={-1}
        className={cn(
          'm-0 min-w-0 flex-1 self-center px-1 py-1 text-body font-semibold wrap-anywhere',
          title === null ? 'text-ink-soft' : 'text-ink',
        )}
      >
        {title ?? t('help.new')}
      </h3>
      {onNew && (
        <Tip content={t('help.new')}>
          <Button variant="ghost" aria-label={t('help.new')} onClick={onNew} className="h-7 w-7 shrink-0 justify-center px-0">
            <Icon name="plus" size={15} />
          </Button>
        </Tip>
      )}
    </header>
  );
}
