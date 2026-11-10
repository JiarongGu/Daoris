import { Fragment } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { holds, type Place } from '../focus';

/** The workspace `default`, whose room is the whole machine's: a window on it marks nothing. */
const DEFAULT = 'default';

const same = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();

/**
 * The open conversation's focus under its title (`docs/2026-11-11-ask-scope-ux-design.md` §2): a breadcrumb from this machine down
 * to the focus, its own segment in ink and its ancestors faint, a workspace or repository a door to its page and this machine none.
 * It only names the focus: the chip is the one way to change it. Its marks say what the breadcrumb cannot: a focus outside the
 * window's workspace, a conversation started in another, a change waiting for the next message, a focus no longer here.
 *
 * @param window The window's workspace; null for a window on every workspace, which marks neither the window nor the home.
 * @param home The workspace the conversation started in.
 */
export function FocusCrumb({ focus, next, gone = false, window, home, onOpen }: {
  focus: Place;
  next: Place | null;
  gone?: boolean;
  window: string | null;
  home: string;
  onOpen: (place: Place) => void;
}) {
  const { t } = useTranslation();
  const segments: Place[] = [
    { kind: 'machine' },
    ...(focus.kind === 'machine' ? [] : [{ kind: 'workspace' as const, workspace: focus.workspace }]),
    ...(focus.kind === 'repository' ? [focus] : []),
  ];
  const nameOf = (place: Place) => (place.kind === 'machine' ? t('help.focus.machine')
    : place.kind === 'workspace' ? place.workspace : place.repository);

  const shows = window !== null && !same(window, DEFAULT);
  // A change waiting trails the breadcrumb as the chip's words trail its place: after a dot, even first.
  const marks: { glyph?: string; words: string; trails?: true }[] = [
    ...(shows && !holds({ kind: 'workspace', workspace: window }, focus) ? [{ glyph: '◇', words: t('help.focus.crumb.window', { workspace: window }) }] : []),
    ...(shows && !same(home, window) ? [{ words: t('help.focus.crumb.home', { workspace: home }) }] : []),
    ...(next ? [{ words: t('help.focus.crumb.next', { next: nameOf(next) }), trails: true as const }] : []),
    ...(gone ? [{ glyph: '⚠', words: t('help.focus.crumb.gone') }] : []),
  ];

  return (
    <nav aria-label={t('help.focus.list')} className="flex flex-wrap items-baseline gap-x-1.5 px-3 pb-1 text-meta">
      <span className="min-w-0 wrap-anywhere">
        {segments.map((place, at) => {
          const own = at === segments.length - 1;
          return (
            <Fragment key={place.kind}>
              {at > 0 && <span aria-hidden className="text-ink-faint">{' › '}</span>}
              {place.kind === 'machine' ? (
                <span className={own ? 'text-ink' : 'text-ink-faint'} aria-current={own ? 'location' : undefined}>{nameOf(place)}</span>
              ) : (
                <button
                  type="button"
                  aria-current={own ? 'location' : undefined}
                  onClick={() => onOpen(place)}
                  className={cn(
                    'cursor-pointer rounded-sm hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent',
                    own ? 'text-ink' : 'text-ink-faint',
                  )}
                >
                  {nameOf(place)}
                </button>
              )}
            </Fragment>
          );
        })}
      </span>
      {marks.map((mark, at) => (
        <span key={mark.words} className={cn('text-ink-faint', mark.glyph === '⚠' && 'text-warn')}>
          {' '}
          {(at > 0 || mark.trails) && <span aria-hidden>{'· '}</span>}
          {mark.glyph && <span aria-hidden>{`${mark.glyph} `}</span>}
          {mark.words}
        </span>
      ))}
    </nav>
  );
}
