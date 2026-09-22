import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { useQuests, useRegistry, useSessions } from '../queries';
import { partition } from './rail';
import { useDriver } from '../shell';
import { EmptyState, type Notify, SESSION_ACTIVE, SkeletonRows, useErrorNotify } from '../ui';
import { RepositoryGroup } from './RepositoryGroup';
import { SessionRow } from './SessionRow';
import { ownTree } from './identity';

/**
 * The rail: everything running, grouped by the repository it runs in (design §3).
 *
 * @remarks
 * **An organism — it holds the hooks so the molecules below it hold none** (components plan §2).
 * That is the whole arrangement: a row and a group header are reviewable in every state without a
 * service or a driver, and this file is the one place that knows where their props come from.
 *
 * **Selection is not held here.** The rail is told which session is attended and reports a choice,
 * because one selection binds every region of the Work frame (IDE study §3) and a frame cannot
 * bind a selection its rail keeps to itself.
 *
 * **What it lists is what is still running** — plus the attended session, whatever state it reached.
 * A session that finished while its person was reading it must not vanish out from under them, and
 * reviewing finished work is a surface of its own (SURF6), not a growing list here.
 */
export function SessionRail({ selected = null, onSelect, notify }: {
  /** The attended session's id, held by the frame. */
  selected?: string | null;
  onSelect?: (id: string) => void;
  notify: Notify;
}) {
  const { t } = useTranslation();
  // Closed records included, then filtered here: the rail needs the attended one whatever state it
  // reached, and this is the query key the quest view already warms.
  const sessions = useSessions(null, true);
  const quests = useQuests(null, true);
  const registry = useRegistry();
  const driver = useDriver();

  useErrorNotify(sessions.error, notify);

  const questFor = new Map((quests.data ?? []).map((quest): [string, Quest] => [quest.id, quest]));
  const registered = new Map((registry.data ?? []).map((row) => [row.repository, row]));

  // Live sessions grouped by repository, and beneath them the ones that ended — reachable after a
  // restart, which §7 of the working-surface design promises and which this rail did not keep until
  // the deployed application showed four empty-state sentences over four real records (`rail.ts`).
  const { active: shown, ended, hiddenEnded } = partition(sessions.data ?? [], selected);

  const groups = new Map<string, Session[]>();
  for (const session of shown) {
    const rows = groups.get(session.repository) ?? [];
    rows.push(session);
    groups.set(session.repository, rows);
  }

  // Repository order is the name, so the rail does not reshuffle itself as states change. Inside a
  // group, the session that needs a person comes first — the one ordering that earns its keep.
  const ordered = [...groups.entries()].sort(([a], [b]) => a.localeCompare(b));
  for (const [, rows] of ordered) {
    rows.sort((a, b) => Number(a.state !== 'awaiting-person') - Number(b.state !== 'awaiting-person'));
  }

  if (sessions.isPending) return <div className="px-2.5 py-2"><SkeletonRows rows={5} /></div>;

  // The full empty state only when there is truly nothing — a machine with no live session but
  // four ended ones is not empty, and saying so at the top of a list of records reads as "these
  // records are nothing", which is the misreading this whole change removes.
  if (!shown.length && !ended.length) {
    return (
      <EmptyState
        icon="inbox"
        headline={t('work.rail.empty.headline')}
        body={t('work.rail.empty.body')}
      />
    );
  }

  return (
    <nav aria-label={t('work.rail.label')}>
      {!shown.length && (
        <p className="px-3 py-2 text-small text-ink-faint">{t('work.rail.empty.headline')}</p>
      )}
      {ordered.map(([repository, rows]) => {
        const holding = rows.find((session) => SESSION_ACTIVE.has(session.state));
        const registration = registered.get(repository);
        return (
          <RepositoryGroup
            key={repository}
            repository={repository}
            count={rows.length}
            // The driver answers only where a shell is attached (D46 §6); with no answer these stay
            // undefined, and the header asserts nothing rather than reading silence as "no".
            drivable={driver.data && driver.data.drivable.includes(repository)}
            held={driver.data && driver.data.holds.includes(repository)}
            busy={holding ? (ownTree(holding, registration?.root) ?? true) : null}
            adopted={registration?.adopted}
            // A root is answered only to a caller on the machine that holds it (D48 §7), so the
            // question is only ASKED where a driver answered — otherwise every registration would
            // look like a teammate's, which is a claim rather than an absence.
            hasCheckout={driver.data && registration ? Boolean(registration.root) : undefined}
          >
            {rows.map((session) => (
              <SessionRow
                key={session.id}
                session={session}
                quest={session.quest ? questFor.get(session.quest) : null}
                root={registration?.root}
                selected={session.id === selected}
                onSelect={onSelect}
              />
            ))}
          </RepositoryGroup>
        );
      })}

      {/* 🔴 The ended sessions — the driver's record of what it actually did. No repository group
          headers here: those carry live facts (drivable, held, which tree is busy) and an ended
          session has none to state; the row's own derived title already names its repository.
          Newest first, capped, and the remainder COUNTED rather than silently cut. */}
      {ended.length > 0 && (
        <section aria-label={t('work.rail.ended')} className="mt-2 border-t border-line pt-2">
          <h3 className="px-3 py-1 text-meta font-semibold uppercase tracking-wide text-ink-faint">
            {t('work.rail.ended')}
          </h3>
          {ended.map((session) => (
            <SessionRow
              key={session.id}
              session={session}
              quest={session.quest ? questFor.get(session.quest) : null}
              root={registered.get(session.repository)?.root}
              selected={session.id === selected}
              onSelect={onSelect}
            />
          ))}
          {hiddenEnded > 0 && (
            <p className="px-3 py-1.5 text-small text-ink-faint">
              {t('work.rail.endedMore', { count: hiddenEnded })}
            </p>
          )}
        </section>
      )}
    </nav>
  );
}
