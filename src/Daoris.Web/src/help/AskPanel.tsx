import { useTranslation } from 'react-i18next';
import { Button, Icon, Inline, Prose } from '../ui';
import type { Starter, StarterDoor } from './starters';

/**
 * Ask Daoris (HELP1, D89): a panel beside whatever the person is looking at, which stays open across
 * views. This build holds its no-agent tier (HELP1d): what the machine lacks, each with the screen that
 * fixes it and the terminal command that does the same (D50).
 *
 * @remarks
 * Written from the owner's *"we will need some chat agent to support configure for workspace"*. The
 * conversation joins this panel when the room and its session are built (HELP1a); a starter is its
 * first message then. A molecule: the starters arrive computed, a door press goes out.
 */
export function AskPanel({ starters, helper, onGo, onClose }: {
  starters: Starter[];
  /** The agent Ask Daoris runs on, or null. */
  helper: string | null;
  onGo: (door: StarterDoor) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();

  return (
    <aside aria-label={t('help.title')} className="flex w-[24rem] shrink-0 flex-col border-l border-line bg-page max-md:w-full">
      <header className="flex items-center gap-2 border-b border-line px-4 py-2.5">
        <Icon name="help" size={15} className="text-ink-soft" />
        <h2 className="m-0 text-body font-semibold text-ink">{t('help.title')}</h2>
        <Button variant="ghost" aria-label={t('help.close')} onClick={onClose} className="ml-auto px-1.5">
          <Icon name="x" size={14} />
        </Button>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto px-4 py-3">
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
      </div>
    </aside>
  );
}
