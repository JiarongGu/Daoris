import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Pill } from '../ui';

/**
 * The rail's group header — one repository, and the sessions running in it.
 *
 * @remarks
 * **Grouping is by repository because that is the axis a person switches on** (design §3), and
 * two-level grouping is what survives contact with many running things — the one thing JetBrains's
 * Services window has that the agent consoles mostly do not (IDE study §2).
 *
 * **The header is where the REPOSITORY's facts live**, so a row never repeats them: whether the
 * driver works here, whether the person is holding it, and which tree is busy — which is a
 * question a repository can have more than one answer to since D51.
 *
 * **Unknown is not false.** Every fact is optional and an absent one asserts nothing: the driver
 * answers only where a shell is attached (D46 §6), and a header that read silence as "not adopted"
 * would be inventing news out of a query that had not returned.
 */
export function RepositoryGroup({
  repository, label, count, drivable, held, busy, adopted, hasCheckout, children,
}: {
  repository: string;
  /** What the header says, where the group is not a repository's (Ask Daoris's). Absent, the repository. */
  label?: string;
  /** How many session rows sit under this header. */
  count: number;
  /** The machine's standing choice: the driver may start work here (D46 §6). */
  drivable?: boolean;
  /** The person has stopped it starting anything new. Outranks `drivable`, which it suspends. */
  held?: boolean;
  /** The tree an active session holds: its NAME for a session tree, `true` for the registered root. */
  busy?: string | true | null;
  /** Whether doctrine is installed here. `false` is a fact; absent is an unanswered question. */
  adopted?: boolean;
  /** Whether there is a checkout on this machine at all (D48 §3) — same rule as `adopted`. */
  hasCheckout?: boolean;
  children: ReactNode;
}) {
  const { t } = useTranslation();

  const marks = [
    held ? <Pill key="held" tone="declined">{t('work.group.held')}</Pill> : null,
    // A fact about the TREE — it is held — so it is words, as *drives here* is. It wore the live
    // mark a working session wears, and a group whose one chat was idle or parked read live (UX5 U60).
    busy
      ? (
        <span key="busy" className="text-meta text-ink-soft">
          {busy === true ? t('work.group.busy') : t('work.group.busyTree', { tree: busy })}
        </span>
      )
      : null,
    drivable && !held
      ? <span key="drivable" className="text-meta text-ink-faint">{t('work.group.drivable')}</span>
      : null,
    adopted === false ? <Pill key="adopted">{t('work.group.notAdopted')}</Pill> : null,
    hasCheckout === false ? <Pill key="checkout">{t('work.group.noCheckout')}</Pill> : null,
  ].filter(Boolean);

  return (
    <section className="border-t border-line first:border-t-0">
      <header className="flex items-baseline justify-between gap-2 px-2.5 pb-1 pt-2.5">
        <h3 className="m-0 truncate text-small font-semibold text-ink">{label ?? repository}</h3>
        <span
          title={t('work.group.sessions', { count })}
          className="shrink-0 font-mono text-meta text-ink-faint"
        >
          {count}
        </span>
      </header>
      {marks.length > 0 && (
        <p className="m-0 flex flex-wrap items-center gap-x-2 gap-y-1 px-2.5 pb-1.5">{marks}</p>
      )}
      <ul className="m-0 list-none p-0">{children}</ul>
    </section>
  );
}
