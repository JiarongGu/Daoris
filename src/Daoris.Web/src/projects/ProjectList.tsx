import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Registration } from '../api';
import { figure } from '../format';
import { Button, Icon, Inline, Pill } from '../ui';
import { cn } from '../lib/cn';
import { contextOffer } from '../menus/press';
import { ListRowDoor } from '../work/ListPane';

/**
 * A repository's standing on this machine, as the session list's group header says it (`RepositoryGroup`): *held*
 * outranks *drives here*, which it suspends; *not adopted* and *not on this machine* are facts, and an absent fact
 * asserts nothing, since only a shell's driver answers (D46 §6) and only a local host names a checkout (D47 §4).
 */
export type RepositoryStanding = {
  /** The machine's standing choice: the driver may start work here. */
  drivable?: boolean;
  /** The person has stopped it starting anything new. */
  held?: boolean;
  /** Whether doctrine is installed there; said where nothing else says it (a page's head, not a row of its group). */
  adopted?: boolean;
  /** Whether it has a checkout on this machine. */
  here?: boolean;
};

/** The marks of a repository's standing, in the order the session list gives them. */
export function RepositoryMarks({ drivable, held, adopted, here }: RepositoryStanding) {
  const { t } = useTranslation();
  const marks = [
    held ? <Pill key="held" tone="declined">{t('work.group.held')}</Pill> : null,
    drivable && !held ? <span key="drivable" className="text-meta text-ink-faint">{t('work.group.drivable')}</span> : null,
    adopted === false ? <Pill key="adopted">{t('work.group.notAdopted')}</Pill> : null,
    here === false ? <Pill key="here">{t('work.group.noCheckout')}</Pill> : null,
  ].filter(Boolean);
  return marks.length > 0 ? <>{marks}</> : null;
}

/** What a repository's row says beside the repository itself. */
export type RepositoryRowFacts = RepositoryStanding & {
  registration: Registration;
  /** What the index holds of one not adopted, which its row says, where nothing else would: null for nothing. */
  entries?: number | null;
  /** How many of Daoris's branches it holds on this machine (§4.1): a shell's, absent in a browser. */
  branches?: number;
};

/** One workspace's group: its adopted repositories, then those registered and not adopted (D118 §2). */
export type WorkspaceGroup = { workspace: string; adopted: RepositoryRowFacts[]; outside: RepositoryRowFacts[] };

/** How many repositories a list holds before it offers a filter (§4.1), as the map offers a search past twelve (MAP4a). */
export const FILTER_PAST = 12;

/**
 * **Repositories' list** (FRAME1e, D118 §2; UX6g, D150 §4.1): a group per workspace, always, since a workspace is always
 * named (D75 §4). A workspace's row heads its group and opens the workspace's page; under it, its adopted repositories,
 * then *Registered, not adopted*. A row is for reading and choosing; its page opens in the main area, where the acting is.
 *
 * @remarks
 * **A molecule**: the rows arrive with what each says, and every press goes out. Each row is a row of its list
 * (`data-list-row`), a workspace's head among them, so ↑, ↓, Home and End move along it, and Enter opens it.
 *
 * - **The name leads, its standing at the right** (platform language §4): *held*, *drives here*, *not on this machine*.
 * - **Its one line is content**: an adopter's summary as it wrote it, cut to one line with the whole in its tip; one
 *   that declared nothing says so, since an asker would be guessing (D34). One not adopted says what the index reads
 *   of it, its only fact the list has room for. One that holds Daoris's branches says how many.
 * - **A group folds** by its chevron, and its head stays a door. Past twelve repositories the list offers a filter, by
 *   name, and *Registered, not adopted* starts folded unless it holds the chosen repository: on a machine of 29, twenty
 *   rows nobody drives stood between the person and the next workspace.
 */
export function ProjectList({ groups, chosen, chosenWorkspace = null, unanswered = null, onChoose, onChooseWorkspace }: {
  groups: WorkspaceGroup[];
  /** The list's chosen item, where it is a repository: its name. */
  chosen: string | null;
  /** The list's chosen item, where it is a workspace: its name. */
  chosenWorkspace?: string | null;
  /** The sentence for a list that has never had an answer, said in place rather than left blank (D118 §3h). */
  unanswered?: string | null;
  onChoose: (repository: string) => void;
  /** Open a workspace's page; absent, its head is a heading and no door. */
  onChooseWorkspace?: (workspace: string) => void;
}) {
  const { t } = useTranslation();
  const [query, setQuery] = useState('');
  const [folded, setFolded] = useState<ReadonlySet<string>>(new Set());
  // Each group's *Registered, not adopted*, opened or folded by its press; until then it stands as `outsideStarts` says.
  const [outsideOpen, setOutsideOpen] = useState<Readonly<Record<string, boolean>>>({});

  if (unanswered) {
    return <p className="m-0 px-3 py-3 text-small text-ink-soft"><Inline text={unanswered} /></p>;
  }

  const total = groups.reduce((sum, group) => sum + group.adopted.length + group.outside.length, 0);
  const filterable = total > FILTER_PAST;
  const wanted = filterable ? query.trim().toLowerCase() : '';
  const matches = (facts: RepositoryRowFacts) => facts.registration.repository.toLowerCase().includes(wanted);
  const shown = groups.map((group) => {
    // A workspace whose name matches is shown whole; otherwise its repositories that match.
    const whole = !wanted || group.workspace.toLowerCase().includes(wanted);
    return {
      ...group,
      adopted: whole ? group.adopted : group.adopted.filter(matches),
      outside: whole ? group.outside : group.outside.filter(matches),
      count: group.adopted.length + group.outside.length,
      whole,
    };
  }).filter((group) => group.whole || group.adopted.length + group.outside.length > 0);
  const outsideStarts = (group: WorkspaceGroup) => Boolean(wanted) || !filterable || group.adopted.length === 0
    || group.outside.some((facts) => facts.registration.repository === chosen);

  const row = (facts: RepositoryRowFacts, line: string | null, faint: boolean) => {
    const { repository } = facts.registration;
    // A row offers what it does on a right-click (CTX1, D138 §4): opening it, and its name.
    const menu = {
      label: repository,
      acts: [
        { id: 'open', label: t('contextMenu.act.open'), onSelect: () => onChoose(repository) },
        { id: 'copy', label: t('contextMenu.act.copyRepository'), icon: 'copy' as const, copy: repository },
      ],
    };
    return (
      <li key={repository} aria-label={repository} data-list-row="" {...contextOffer(menu)}>
        <ListRowDoor chosen={chosen === repository} onPress={() => onChoose(repository)}>
          <span className="flex min-w-0 items-baseline gap-2">
            <span className={cn('min-w-0 truncate text-body', faint ? 'text-ink-soft' : 'text-ink')}>{repository}</span>
            <span className="ml-auto flex shrink-0 items-baseline gap-1.5">
              <RepositoryMarks drivable={facts.drivable} held={facts.held} here={facts.here} />
            </span>
          </span>
          {line && <span title={line} className="mt-0.5 block truncate text-small text-ink-soft">{line}</span>}
          {facts.branches !== undefined && facts.branches > 0 && (
            <span className="mt-0.5 block text-meta text-ink-faint">{t('projects.row.branches', { count: facts.branches })}</span>
          )}
        </ListRowDoor>
      </li>
    );
  };

  const toggle = (workspace: string) => setFolded((was) => {
    const next = new Set(was);
    if (next.has(workspace)) next.delete(workspace);
    else next.add(workspace);
    return next;
  });

  return (
    <div>
      {filterable && (
        // Above the rows, and in reach as they scroll under it; ↓ goes from it into the rows (D118 §3e).
        <div className="sticky top-0 z-10 border-b border-line bg-page px-2 pb-1.5 pt-1.5">
          <label className="flex items-center gap-1.5 rounded-control border border-line-strong bg-raised px-2 py-1 text-ink-faint focus-within:border-accent">
            <Icon name="search" size={12} className="shrink-0" />
            <input
              type="search"
              value={query}
              aria-label={t('projects.list.filter')}
              placeholder={t('projects.list.filter')}
              onChange={(event) => setQuery(event.target.value)}
              // Escape clears a filter, and one with nothing in it is left to the list laid over (D118 §3a).
              onKeyDown={(event) => { if (event.key === 'Escape' && query) { event.preventDefault(); setQuery(''); } }}
              className="min-w-0 flex-1 bg-transparent text-small text-ink outline-none placeholder:text-ink-faint"
            />
          </label>
        </div>
      )}
      {wanted && shown.length === 0 && (
        <p className="m-0 px-3 py-3 text-small text-ink-soft">{t('projects.list.filterNone', { query: query.trim() })}</p>
      )}
      {shown.map((group) => {
        const open = Boolean(wanted) || !folded.has(group.workspace);
        const outsideShown = outsideOpen[group.workspace] ?? outsideStarts(group);
        return (
          <section key={group.workspace} aria-label={group.workspace} className="border-t border-line first:border-t-0">
            <h3 className="m-0 flex items-center pr-1 font-normal" data-list-row="">
              {onChooseWorkspace
                ? (
                  <ListRowDoor chosen={chosenWorkspace === group.workspace} onPress={() => onChooseWorkspace(group.workspace)}>
                    <WorkspaceHead name={group.workspace} count={group.count} />
                  </ListRowDoor>
                )
                : <span className="block min-w-0 flex-1 border-l-[3px] border-l-transparent px-2.5 py-1.5"><WorkspaceHead name={group.workspace} count={group.count} /></span>}
              <Button
                variant="ghost"
                aria-expanded={open}
                aria-label={t(open ? 'projects.list.fold' : 'projects.list.unfold', { workspace: group.workspace })}
                disabled={Boolean(wanted)}
                onClick={() => toggle(group.workspace)}
                className="h-6 w-6 shrink-0 justify-center px-0 text-ink-faint"
              >
                <Icon name={open ? 'chevronDown' : 'chevronRight'} size={14} />
              </Button>
            </h3>
            {open && group.adopted.length > 0 && (
              <ul aria-label={t('projects.group.adoptedIn', { workspace: group.workspace })} className="m-0 list-none p-0">
                {group.adopted.map((facts) => row(facts, facts.registration.summary || t('projects.row.undeclared'), false))}
              </ul>
            )}
            {open && group.outside.length > 0 && (
              <div className="pb-1">
                <h4 className="m-0 font-normal">
                  <button
                    type="button"
                    aria-expanded={outsideShown}
                    onClick={() => setOutsideOpen((was) => ({ ...was, [group.workspace]: !outsideShown }))}
                    className="flex w-full items-center gap-1 px-2.5 pb-1 pt-2 text-left text-small font-semibold text-ink-soft hover:text-ink"
                  >
                    <span className="min-w-0 flex-1 truncate">{t('projects.group.outside', { count: group.outside.length })}</span>
                    <Icon name={outsideShown ? 'chevronDown' : 'chevronRight'} size={12} className="shrink-0 text-ink-faint" />
                  </button>
                </h4>
                {outsideShown && (
                  <ul aria-label={t('projects.group.outside', { count: group.outside.length })} className="m-0 list-none p-0">
                    {group.outside.map((facts) => row(
                      facts,
                      facts.entries
                        ? t('projects.outside.readable', { count: facts.entries, total: figure(facts.entries) })
                        : t('projects.nothingIndexed'),
                      true,
                    ))}
                  </ul>
                )}
              </div>
            )}
          </section>
        );
      })}
    </div>
  );
}

/** A workspace's head: its name, and how many repositories it holds. */
function WorkspaceHead({ name, count }: { name: string; count: number }) {
  const { t } = useTranslation();
  return (
    <span className="flex min-w-0 items-baseline gap-2">
      <span className="min-w-0 truncate text-body font-semibold text-ink">{name}</span>
      {' '}
      <span className="ml-auto shrink-0 text-meta tabular-nums text-ink-faint">{t('projects.workspace.repositories', { count })}</span>
    </span>
  );
}
