import { useTranslation } from 'react-i18next';
import type { SetUpRef } from '../api';
import { cn } from '../lib/cn';
import { Icon, Menu, Tip } from '../ui';

/**
 * A set-up waiting for the person's review on this machine, as the driver's state says it (`inReview`, REVIEWENV1d): the step,
 * what it showed and where, and whether Daoris still serves its tab.
 */
export type ReviewShown = {
  quest: string;
  title: string;
  environment: string;
  look?: string | null;
  shows?: string | null;
  served: boolean;
  /**
   * The newest set-up of the step as its record says it, named whole (REVIEWENV1g): what *Reviewed* answers. The page adds it
   * from the quests it holds; absent where it holds no whole one, and then no verdict is offered here.
   */
  setUp?: SetUpRef | null;
};

/** A chip's frame, the driving chip's beside it: a bordered token a size down from the strip's text. */
const CHIP = cn(
  'flex h-6 min-w-0 max-w-64 items-center gap-1.5 rounded-control border border-line px-2 text-meta text-ink-soft',
  'cursor-pointer bg-transparent transition-colors duration-(--speed)',
  'hover:bg-accent-soft hover:text-ink focus-visible:bg-accent-soft focus-visible:outline-none',
  'focus-visible:ring-1 focus-visible:ring-accent',
);

/**
 * The review's chip on the app strip, beside Daoris's browser's door (REVIEWENV1d; D154 point 8, the review environment design
 * §3.3): while a set-up waits for the person, it says the review is open, and its menu says what the step showed, whether its
 * tab is still served, and offers *Show it again*.
 *
 * @remarks
 * **Beside the browser, never inside it.** Every page in Daoris's browser is one an agent can drive, so nothing the person
 * presses about a review is drawn there; the strip of the window beside it is where they answer (REVIEWENV1g): *Reviewed*,
 * naming the newest set-up its step's record holds, and *Not yet…*, which opens the step's page, where the words a not yet
 * needs are asked, since a menu holds no box.
 *
 * **Whether it is still served is said**, since a tab Daoris no longer serves loads the person's own server at the next reload,
 * with nothing on the page to say so.
 *
 * **A molecule**: the presses go out, and the shell serves the build again and brings its tab forward.
 */
export function ReviewChip({ reviews = [], onShowAgain, onReviewed, onNotYet }: {
  /** The set-ups waiting here; none says nothing. */
  reviews?: readonly ReviewShown[];
  /** *Show it again* for one set-up step. */
  onShowAgain: (quest: string) => void;
  /** *Reviewed* for one set-up step, naming the set-up its record holds (REVIEWENV1g); absent, none is offered. */
  onReviewed?: (quest: string, setUp: SetUpRef) => void;
  /** *Not yet…*: the step's page, where the person's words are asked (REVIEWENV1g); absent, none is offered. */
  onNotYet?: (quest: string) => void;
}) {
  const { t } = useTranslation();
  if (reviews.length === 0) return null;

  const [first] = reviews as [ReviewShown];
  const one = reviews.length === 1;
  const text = one ? t('browser.review.one', { quest: first.quest }) : t('browser.review.several', { count: reviews.length });
  const label = one
    ? t('browser.review.oneLabel', { quest: first.quest, title: first.title })
    : t('browser.review.severalLabel', { count: reviews.length });

  return (
    // Never modal (`Menu.Root`): a menu on the strip has no business making the page inert.
    <Menu.Root>
      <Tip content={`${text} — ${t('browser.review.tip')}`}>
        <Menu.Trigger asChild>
          <button type="button" aria-label={label} className={CHIP}>
            <Icon name="inbox" size={11} className="shrink-0" />
            <span className="min-w-0 truncate">{text}</span>
          </button>
        </Menu.Trigger>
      </Tip>
      <Menu.Content align="end" className="w-80 max-w-[calc(100vw-2rem)]">
        <Menu.Label>{t('browser.review.menu')}</Menu.Label>
        {reviews.map((review, index) => (
          <div key={review.quest}>
            {index > 0 && <Menu.Separator />}
            <div className="flex flex-col gap-0.5 px-2 py-1.5 text-meta">
              <span className="truncate text-ink">{review.title}</span>
              {review.shows && <span className="line-clamp-3 text-ink-soft">{review.shows}</span>}
              <span className={review.served ? 'text-ink-soft' : 'text-warn'}>
                {review.served
                  ? t('browser.review.served', { environment: review.environment })
                  : t('browser.review.unserved')}
              </span>
            </div>
            {onReviewed && review.setUp && (
              <Menu.Item onSelect={() => onReviewed(review.quest, review.setUp!)}>
                <Icon name="check" size={12} className="shrink-0" />
                <span className="min-w-0 truncate">{t('browser.review.reviewed', { quest: review.quest })}</span>
              </Menu.Item>
            )}
            {onNotYet && (
              <Menu.Item onSelect={() => onNotYet(review.quest)}>
                <Icon name="quests" size={12} className="shrink-0" />
                <span className="min-w-0 truncate">{t('browser.review.notYet', { quest: review.quest })}</span>
              </Menu.Item>
            )}
            <Menu.Item onSelect={() => onShowAgain(review.quest)}>
              <Icon name="refresh" size={12} className="shrink-0" />
              <span className="min-w-0 truncate">{t('browser.review.again', { quest: review.quest })}</span>
            </Menu.Item>
          </div>
        ))}
      </Menu.Content>
    </Menu.Root>
  );
}
