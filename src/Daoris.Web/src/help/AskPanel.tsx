import type { ReactNode, RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Button, Icon, Inline, Prose } from '../ui';
import { Splitter } from '../work/frame';
import type { Starter, StarterDoor } from './starters';

/** The conversation, as the organism hands it over: the record, the composer, and the way to start again. */
export type AskConversationSlot = {
  /** The newest conversation's record, or null when there is none to show. */
  body: ReactNode | null;
  /** What it proposes that waits for the person (HELP1c), or null. */
  proposals?: ReactNode;
  composer: ReactNode;
  /** Whether the conversation shown has ended, so a message starts the next. */
  ended: boolean;
  /** Start again: finish the one running and clear the panel. Absent while there is nothing to clear. */
  onNew?: () => void;
};

/**
 * Ask Daoris (HELP1, D89): a panel beside whatever the person is looking at, which stays open across
 * views. With no agent named it holds the no-agent tier (HELP1d): what the machine lacks, each with the
 * screen that fixes it and the terminal command that does the same (D50). With one, the conversation
 * (HELP1a) takes the panel, and the starters show until there is a conversation to read.
 *
 * @remarks
 * A chat agent to help configure a workspace. A molecule: the starters and the conversation arrive made, a door press goes out, and the region the
 * conversation scrolls in is handed back to the organism that follows its tail.
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
  const talking = Boolean(conversation?.body);

  const again = conversation?.onNew && (
    <Button variant="ghost" className="px-2 text-small" onClick={conversation.onNew}>
      {t('help.new')}
    </Button>
  );

  const content = (
    <>
      <div ref={scroller} className="min-h-0 flex-1 overflow-y-auto px-4 py-3">
        {talking ? (
          <>
            {conversation!.body}
            {conversation!.proposals}
            {conversation!.ended && <Prose className="mt-3 text-small text-ink-faint">{t('help.ended')}</Prose>}
          </>
        ) : (
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
        )}
      </div>

      {conversation?.composer}
    </>
  );

  if (!framed) {
    return (
      <div className="flex min-h-0 flex-1 flex-col">
        {again && <div className="flex justify-end border-b border-line px-2 py-1">{again}</div>}
        {content}
      </div>
    );
  }

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
        <span className="ml-auto flex items-center gap-1">
          {again}
          <Button variant="ghost" aria-label={t('help.close')} onClick={onClose} className="px-1.5">
            <Icon name="x" size={14} />
          </Button>
        </span>
      </header>
      {content}
    </aside>
  );
}
