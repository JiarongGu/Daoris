import { useTranslation } from 'react-i18next';
import type { Registration } from '../api';
import { figure } from '../format';
import { Inline, Pill } from '../ui';
import { cn } from '../lib/cn';
import { contextOffer } from '../menus/press';
import { ListGroup, ListRowDoor } from '../work/ListPane';

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
};

/**
 * **Repositories' list** (FRAME1e, D118 §2): the adopted repositories, then *Registered, not adopted*, each group with
 * its count and absent with none. A row is for reading and choosing; its page opens in the main area, where the acting
 * is.
 *
 * @remarks
 * **A molecule**: the rows arrive with what each says, and every press goes out. Each row is a row of its list
 * (`data-list-row`), named by its repository, so ↑, ↓, Home and End move along it, and Enter opens it.
 *
 * - **The name leads, its standing at the right** (platform language §4): *held*, *drives here*, *not on this machine*.
 * - **Its one line is content**: an adopter's summary as it wrote it, cut to one line with the whole in its tip; one
 *   that declared nothing says so, since an asker would be guessing (D34). One not adopted says what the index reads
 *   of it, its only fact the list has room for.
 */
export function ProjectList({ adopted, outside, chosen, unanswered = null, onChoose }: {
  adopted: RepositoryRowFacts[];
  outside: RepositoryRowFacts[];
  /** The list's chosen item: a repository's name. */
  chosen: string | null;
  /** The sentence for a list that has never had an answer, said in place rather than left blank (D118 §3h). */
  unanswered?: string | null;
  onChoose: (repository: string) => void;
}) {
  const { t } = useTranslation();

  if (unanswered) {
    return <p className="m-0 px-3 py-3 text-small text-ink-soft"><Inline text={unanswered} /></p>;
  }

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
        </ListRowDoor>
      </li>
    );
  };

  return (
    <div>
      {adopted.length > 0 && (
        <ListGroup title={t('projects.group.adopted', { count: adopted.length })}>
          {adopted.map((facts) => row(facts, facts.registration.summary || t('projects.row.undeclared'), false))}
        </ListGroup>
      )}
      {outside.length > 0 && (
        <ListGroup title={t('projects.group.outside', { count: outside.length })}>
          {outside.map((facts) => row(
            facts,
            facts.entries
              ? t('projects.outside.readable', { count: facts.entries, total: figure(facts.entries) })
              : t('projects.nothingIndexed'),
            true,
          ))}
        </ListGroup>
      )}
    </div>
  );
}
