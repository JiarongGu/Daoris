import { useTranslation } from 'react-i18next';
import { useQuests, useRegistry, useRepositories } from './queries';
import { ago, compact, sittingDays } from './format';
import { ranked, widest } from './overview';
import {
  Card, CardHeader, Button, EmptyState, Icon, type Notify, PageHeader, Pill, QUEST_TONE,
  SkeletonRows, Tile, Tip, useErrorNotify,
} from './ui';
import { AttentionBand } from './work/AttentionBand';
import type { Attention } from './work/AttentionRow';

/**
 * The management landing (D40). A person overseeing several projects' agents opens this window to
 * answer one question first — is anything sitting, and for how long — so that is what leads: family
 * health as stat tiles, the outstanding quests oldest-first, and the repositories by what the index
 * holds. Every row is a door.
 *
 * The repository bars are ONE series in one hue: entries per repository is magnitude, not identity.
 * Values sit beside the marks in ink, never in the mark's color.
 */
export function OverviewView({ onNavigate, onAttend, notify }: {
  onNavigate: (tab: 'quests' | 'projects') => void;
  /**
   * The *what needs you* band's door (design §4). Absent where there is nowhere to go — a browser
   * has no Work frame — and then the band still SHOWS what is waiting, because knowing is the half
   * that travels.
   */
  onAttend?: (item: Attention) => void;
  notify: Notify;
}) {
  const { t } = useTranslation();
  const repositories = useRepositories();
  const registry = useRegistry();
  const quests = useQuests(null, false);
  useErrorNotify(repositories.error ?? registry.error ?? quests.error, notify);

  const repos = repositories.data ?? [];
  const adopted = (registry.data ?? []).filter((r) => r.adopted);
  const open = (quests.data ?? []).filter((q) => q.status === 'Open');
  const taken = (quests.data ?? []).filter((q) => q.status === 'Taken');
  const entries = repos.reduce((sum, r) => sum + r.total, 0);
  const oldest = open.length ? Math.max(...open.map((q) => sittingDays(q.filed))) : 0;
  // The service already orders open before taken, oldest first — exactly the reading order here.
  const outstanding = quests.data ?? [];
  // Ranked and capped — a real machine has fourteen of these and the card ran past the fold, taking
  // its own legend with it (D62: the fixture holds two, so it looked finished).
  const { shown, hidden } = ranked(repos);
  const most = widest(shown);

  return (
    <section>
      <PageHeader title={t('overview.title')} description={t('overview.description')} />

      {/* Above the tiles, because "what needs me" outranks "how is the family" — and absent
          entirely when nothing is waiting, since a band that always says all-clear stops being
          read (design §4). */}
      <AttentionBand onOpen={onAttend} />

      <div className="mb-5 grid grid-cols-[repeat(auto-fit,minmax(10.5rem,1fr))] gap-3">
        <Tile
          label={t('overview.tiles.adopted.label')}
          value={adopted.length}
          note={t('overview.tiles.adopted.note', { total: (registry.data ?? []).length })}
        />
        {/* A quest sitting for a week is the signal this whole view exists to surface. */}
        <Tile
          warn={oldest >= 7}
          label={t('overview.tiles.open.label')}
          value={open.length}
          note={
            open.length === 0
              ? t('overview.tiles.open.noteNone')
              : oldest === 0
                ? t('overview.tiles.open.noteUnderDay')
                : t('overview.tiles.open.noteOldest', { days: oldest })
          }
        />
        <Tile
          label={t('overview.tiles.progress.label')}
          value={taken.length}
          note={t('overview.tiles.progress.note')}
        />
        <Tile
          label={t('overview.tiles.knowledge.label')}
          value={compact(entries)}
          note={t('overview.tiles.knowledge.note', { count: repos.length })}
        />
      </div>

      <div className="grid items-start gap-3.5 lg:grid-cols-2">
        <Card>
          <CardHeader
            title={t('overview.outstanding.title')}
            aside={
              <Button onClick={() => onNavigate('quests')}>
                {outstanding.length > 0 ? t('overview.outstanding.answer') : t('overview.outstanding.ask')}
              </Button>
            }
          />
          {quests.isPending && <SkeletonRows />}
          {quests.data?.length === 0 && (
            <EmptyState
              icon="check"
              headline={t('overview.outstanding.emptyHeadline')}
              body={t('overview.outstanding.emptyBody')}
            />
          )}
          <ul className="m-0 list-none p-0">
            {outstanding.slice(0, 6).map((quest) => (
              <li key={quest.id} className="border-t border-line first:border-t-0">
                <button
                  onClick={() => onNavigate('quests')}
                  className="flex w-full flex-wrap items-baseline gap-2.5 rounded-none px-1 py-2 text-left text-body hover:bg-accent-soft"
                >
                  <Pill tone={QUEST_TONE[quest.status]}>
                    {t(`status.${quest.status}`)}
                  </Pill>
                  <span className="font-medium">{quest.title}</span>
                  <span className="ml-auto font-mono text-meta text-ink-faint">
                    {quest.from} → {quest.to} · {quest.status === 'Open'
                      ? t('overview.outstanding.filed', { ago: ago(quest.filed) })
                      : t('overview.outstanding.taken', { ago: ago(quest.updated) })}
                  </span>
                </button>
              </li>
            ))}
          </ul>
          {outstanding.length > 6 && (
            <p className="mt-2 text-small italic text-ink-faint">
              {t('overview.outstanding.more', { count: outstanding.length - 6 })}
            </p>
          )}
        </Card>

        <Card>
          <CardHeader
            title={t('overview.repositories.title')}
            aside={
              <Button onClick={() => onNavigate('projects')}>
                <Icon name="projects" size={14} />{t('overview.repositories.button')}
              </Button>
            }
          />
          {repositories.isPending && <SkeletonRows />}
          <ul className="m-0 list-none p-0">
            {shown.map((repository) => {
              const declared = (registry.data ?? []).find((r) => r.repository === repository.name);
              return (
                <li
                  key={repository.name}
                  className="grid grid-cols-[minmax(8.5rem,12rem)_1fr_minmax(7.5rem,auto)] items-center gap-3 py-1.5 max-md:grid-cols-[1fr_auto] max-md:[&>span:nth-child(2)]:col-span-2 max-md:[&>span:nth-child(2)]:row-start-2"
                >
                  <span className="flex min-w-0 items-center gap-2 text-body">
                    {declared?.adopted && (
                      <Tip content={t('overview.repositories.adoptedDot')}>
                        <span className="inline-block size-2 shrink-0 rounded-full bg-accent" />
                      </Tip>
                    )}
                    <span className="truncate">{repository.name}</span>
                  </span>
                  <span className="h-3">
                    <span
                      className="block h-full min-w-[2px] rounded-r-[4px] bg-accent"
                      style={{ width: `${(repository.total / most) * 100}%` }}
                    />
                  </span>
                  <span className="whitespace-nowrap text-right text-small tabular-nums">
                    {repository.total.toLocaleString()}
                    <span className="text-ink-faint"> · {t('overview.repositories.local', { count: repository.local })}</span>
                  </span>
                </li>
              );
            })}
          </ul>
          {/* Counted, never silently dropped: a list that stops without saying so is a list a
              person reads as complete. The way to the rest is the card's own header button. */}
          {hidden > 0 && (
            <p className="mt-1.5 text-small text-ink-faint">
              {t('overview.repositories.more', { count: hidden })}
            </p>
          )}
          <p className="mt-2 text-small italic text-ink-faint">{t('overview.repositories.hint')}</p>
        </Card>
      </div>
    </section>
  );
}
