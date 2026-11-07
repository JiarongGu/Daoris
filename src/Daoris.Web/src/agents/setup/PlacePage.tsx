import { type ReactNode, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { Button, Icon, Menu, Pill } from '../../design/ui';
import { useWidth } from '../../design/useWidth';
import { SetupSection } from '../../projects/setup/SetupParts';
import { type Answered, InlineConfirm } from '../../work/InlineConfirm';
import { PageHead } from '../../work/frame/ViewMain';
import { PlaceAside } from './placeAside';
import type { HeadFacts, PlaceSection } from './placeShell';

/** The room, in px, where a place's page sets the open Workflow's chart in a column of its own: 64 rem. */
const TWO_COLUMNS = 64 * 16;

// A place page's shell (SETUPUX1h, the setup-UX design §2.1): the head, and three sections folded to the line that names their values.
// Molecules: the page's state and every press arrive as props.

/** A repository's driving as the head presses it: the hold, and the ⋯'s driving and trees. */
export type PlaceDriving = {
  drivable: boolean;
  held: boolean;
  ownTree: boolean;
  /** A change on its way: every press waits for it. */
  busy: boolean;
  /** What driving here does that the choice alone does not say. */
  note?: string;
  /** When the steward last set driving here, in words. */
  stewardSet?: string | null;
  /** The terminal's line for the press the menu would make. */
  terminal: string;
  /** Drive or stop driving, told back to the ask that confirmed it. */
  onDrive: (drivable: boolean, answered: Answered) => void;
  onHold: (held: boolean) => void;
  onTrees: (ownTree: boolean) => void;
  onOpenAppointed?: () => void;
  onOpenRepositories?: () => void;
};

export type PlaceHeadProps = {
  title: string;
  pill: HeadFacts['pill'];
  facts: readonly string[];
  /** The parent place, a door on the facts line (`PageHead`'s `line` is a string). */
  parent?: { name: string; onOpen: () => void };
  /** *Set up…*, where the place has one: primary on a place not driven. */
  setUp?: { onOpen: () => void };
};

export type PlaceSectionSpec = {
  id: PlaceSection;
  title: string;
  summary: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  body: ReactNode;
};

/** Brings the row a door asked for into view once its section has drawn it, and says so. */
function useBringRow(root: React.RefObject<HTMLElement | null>, row: string | null | undefined, onBrought: (() => void) | undefined) {
  const brought = useRef(onBrought);
  brought.current = onBrought;
  useEffect(() => {
    const node = root.current;
    if (!row || !node) return undefined;
    const find = (): boolean => {
      const target = Array.from(node.querySelectorAll<HTMLElement>('[data-place-row]')).find((each) => each.dataset.placeRow === row);
      if (!target) return false;
      target.scrollIntoView?.({ block: 'nearest' });
      target.focus({ preventScroll: true });
      brought.current?.();
      return true;
    };
    if (find()) return undefined;
    const observer = new MutationObserver(() => { if (find()) observer.disconnect(); });
    observer.observe(node, { childList: true, subtree: true });
    return () => observer.disconnect();
  }, [root, row]);
}

/** Takes the focus to the head when a choice asks and nothing else has taken it (a list laid over the page has gone). */
function useHeadFocus(head: React.RefObject<HTMLElement | null>, asked: number) {
  const handled = useRef(asked);
  useEffect(() => {
    if (asked === handled.current) return undefined;
    handled.current = asked;
    let inner = 0;
    const outer = requestAnimationFrame(() => {
      inner = requestAnimationFrame(() => {
        const active = document.activeElement;
        if (!active || active === document.body) head.current?.focus();
      });
    });
    return () => { cancelAnimationFrame(outer); cancelAnimationFrame(inner); };
  }, [asked, head]);
}

/** The head's ⋯: driving, a tree per session, the supervisor and Repositories, and the terminal's line. */
function HeadMenu({ driving, onAsk }: { driving: PlaceDriving; onAsk: (act: 'drive' | 'stop') => void }) {
  const { t } = useTranslation();
  return (
    <Menu.Root>
      <Menu.Trigger asChild>
        <Button variant="ghost" aria-label={t('work.head.more')} className="h-[1.9rem] w-[1.9rem] justify-center px-0">
          <Icon name="more" size={15} />
        </Button>
      </Menu.Trigger>
      <Menu.Content side="bottom" align="end" className="min-w-48">
        <Menu.Item disabled={driving.busy} onSelect={() => onAsk(driving.drivable ? 'stop' : 'drive')}>
          {t(driving.drivable ? 'agents.setup.page.more.stop' : 'agents.setup.page.more.drive')}
        </Menu.Item>
        <Menu.CheckboxItem checked={driving.ownTree} disabled={driving.busy} onCheckedChange={driving.onTrees}>
          {t('agents.setup.page.more.trees')}
        </Menu.CheckboxItem>
        {(driving.onOpenAppointed || driving.onOpenRepositories) && <Menu.Separator />}
        {driving.onOpenAppointed && (
          <Menu.Item onSelect={driving.onOpenAppointed}>{t('agents.setup.page.more.supervisor')}</Menu.Item>
        )}
        {driving.onOpenRepositories && (
          <Menu.Item onSelect={driving.onOpenRepositories}>{t('agents.setup.page.more.repositories')}</Menu.Item>
        )}
        <Menu.Separator />
        <Menu.Item onSelect={() => { void navigator.clipboard?.writeText(driving.terminal).catch(() => {}); }}>
          {t('agents.setup.page.more.copy')}
        </Menu.Item>
      </Menu.Content>
    </Menu.Root>
  );
}

/**
 * **A place's page** (SETUPUX1h, the setup-UX design §2.1): the head with its pill, facts, parent door and acts, and three sections each
 * folded to the line that names its values. A section's state is its holder's, so a door out and back finds it as left; a folded section
 * mounts nothing, so it reads nothing. A door's row is brought into view and takes the focus once drawn; *Back* returns to the page a
 * door came from; and a choice in the list can ask the head to take the focus.
 *
 * A molecule: every state is reached by props, and every press goes out.
 */
export function PlacePageView({ head, sections, notes, driving, back, row, onRowBrought, focusAsked }: {
  head: PlaceHeadProps;
  sections: readonly PlaceSectionSpec[];
  /** What waits on the person, a line or two between the head and the sections (This machine's staffing). */
  notes?: ReactNode;
  /** A repository's driving, in the head's acts and ⋯; none where the place has none. */
  driving?: PlaceDriving | null;
  /** The page a door came from, once: *← Back to engine · Roles*. */
  back?: { place: string; section: string; onBack: () => void } | null;
  /** The row a door asked for, until it is brought into view. */
  row?: string | null;
  onRowBrought?: () => void;
  focusAsked: number;
}) {
  const { t } = useTranslation();
  const root = useRef<HTMLDivElement>(null);
  const headRef = useRef<HTMLDivElement>(null);
  const [asking, setAsking] = useState<'drive' | 'stop' | null>(null);
  // Where the page holds 64 rem its sections stand beside a column for the open Workflow's chart.
  const width = useWidth(root);
  const twoColumns = width !== undefined && width >= TWO_COLUMNS;
  const [aside, setAside] = useState<HTMLElement | null>(null);
  useBringRow(root, row, onRowBrought);
  useHeadFocus(headRef, focusAsked);
  const driven = driving?.drivable === true;

  return (
    <div ref={root} data-place-page="">
      {back && (
        <Button variant="ghost" className="mb-2 px-0" onClick={back.onBack}>
          {t('agents.setup.page.back', { place: back.place, section: back.section })}
        </Button>
      )}
      <div ref={headRef} data-place-head="" tabIndex={-1} className="outline-none focus-visible:outline-2 focus-visible:outline-accent">
        <PageHead
          title={head.title}
          pills={head.pill && <Pill tone={head.pill.tone}>{head.pill.label}</Pill>}
          facts={head.facts}
          fact={head.parent && (
            <Button variant="ghost" className="px-0 py-0 text-small" onClick={head.parent.onOpen}>{head.parent.name} ›</Button>
          )}
          acts={(head.setUp || driven) && (
            <>
              {head.setUp && <Button variant={driven ? 'default' : 'primary'} onClick={head.setUp.onOpen}>{t('agents.setup.set.open')}</Button>}
              {driven && driving && (
                <Button disabled={driving.busy} onClick={() => driving.onHold(!driving.held)}>
                  {t(driving.held ? 'agents.setup.page.resume' : 'agents.setup.page.hold')}
                </Button>
              )}
            </>
          )}
          more={driving && <HeadMenu driving={driving} onAsk={setAsking} />}
        />
        {driving && (driving.note || driving.stewardSet) && (
          <div className="-mt-2 mb-3 grid gap-0.5 text-small text-ink-soft">
            {driving.note && <p className="m-0">{driving.note}</p>}
            {driving.stewardSet && <p data-steward-set="" className="m-0">{driving.stewardSet}</p>}
          </div>
        )}
        {driving && asking && (
          <InlineConfirm
            className="mb-3"
            tone={asking === 'stop' ? 'danger' : 'primary'}
            label={t(asking === 'stop' ? 'agents.setup.page.confirm.stopLabel' : 'agents.setup.page.confirm.driveLabel', { name: head.title })}
            says={t(asking === 'stop' ? 'agents.setup.page.confirm.stopSays' : 'agents.setup.page.confirm.driveSays', { name: head.title })}
            meanIt={t(asking === 'stop' ? 'agents.setup.page.confirm.stop' : 'agents.setup.page.confirm.drive')}
            busy={driving.busy}
            onConfirm={(answered) => driving.onDrive(asking === 'drive', answered)}
            onClose={() => setAsking(null)}
          />
        )}
      </div>
      {notes}
      {/* A tighter header here than a Setup section's own, so the head and the three lines fit the first screen (§2.1). */}
      <PlaceAside.Provider value={twoColumns ? aside : null}>
        <div
          data-place-columns={twoColumns ? 'two' : 'one'}
          className={cn('-mt-2 grid min-w-0 items-start gap-x-6', twoColumns && 'grid-cols-2 has-[[data-place-aside]:empty]:grid-cols-1')}
        >
          <div className="min-w-0 [&>section>h2>button]:py-2">
            {sections.map((section) => (
              <SetupSection
                key={section.id}
                id={section.id === 'workflow' ? 'place-fold-workflow' : `place-${section.id}`}
                title={section.title}
                summary={section.summary}
                startsOpen={false}
                open={section.open}
                onOpenChange={section.onOpenChange}
              >
                {section.body}
              </SetupSection>
            ))}
          </div>
          {twoColumns && <div ref={setAside} data-place-aside="" className="mt-2 min-w-0 empty:hidden" />}
        </div>
      </PlaceAside.Provider>
    </div>
  );
}
