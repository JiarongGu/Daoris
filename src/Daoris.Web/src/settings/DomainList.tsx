import { cn } from '../lib/cn';
import { LIST_ROW } from '../work/listKeys';

/** A domain as the list names it: the id it is opened at, and its name in the window's language. */
export type DomainRow = { id: string; label: string };

/**
 * **Settings' list** (D118 §2, §5; FRAME1g): its domains, one row each, the chosen one marked as the page
 * shown, as Settings' list pane holds them.
 *
 * @remarks
 * **A molecule**: the domains a window is offered, their names and the one chosen arrive as props, and a
 * choice goes out by the domain's id. Which domains a window is offered is the page's answer, since only a
 * shell has a machine's (D47 §4), so nothing here asks whether one is attached.
 *
 * Its rows are the frame's list rows (`data-list-row`), so ↑, ↓, Home and End move between them and Enter
 * opens one (D118 §3a), and the chosen row is where F6 lands. They are drawn as the session rail's are,
 * flush to the pane with the accent on their edge, where they were rounded rows inside the page.
 */
export function DomainList({ label, domains, chosen, onChoose, note }: {
  /** The list's accessible name: *Settings domains*. */
  label: string;
  domains: readonly DomainRow[];
  /** The domain shown in the main area. */
  chosen: string;
  onChoose: (id: string) => void;
  /** What the window is not offered, said beneath the domains: a browser's (D47 §4). */
  note?: string;
}) {
  return (
    <nav aria-label={label} className="py-1">
      <ul className="m-0 list-none p-0">
        {domains.map(({ id, label: name }) => (
          <li key={id} {...{ [LIST_ROW]: '' }}>
            <button
              type="button"
              aria-current={id === chosen ? 'page' : undefined}
              onClick={() => onChoose(id)}
              className={cn(
                'block w-full truncate border-l-[3px] px-3 py-1.5 text-left text-body transition-colors duration-(--speed)',
                id === chosen
                  ? 'border-l-accent bg-accent-soft font-medium text-ink'
                  : 'border-l-transparent text-ink-soft hover:bg-accent-soft/50',
              )}
            >
              {name}
            </button>
          </li>
        ))}
      </ul>
      {note && <p className="m-0 mt-3 px-3 text-small text-ink-faint">{note}</p>}
    </nav>
  );
}
