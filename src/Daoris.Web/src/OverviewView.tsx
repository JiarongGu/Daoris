import { useEffect, useState } from 'react';
import { api, type Quest, type Registration, type Repository } from './api';
import { ago, compact, sittingDays } from './format';
import { EmptyState, Icon, PageHeader, SkeletonRows } from './ui';

/**
 * The management landing (D40). A person overseeing several projects' agents opens this window to
 * answer one question first — is anything sitting, and for how long — so that is what leads: family
 * health as stat tiles, the outstanding quests oldest-first, and the repositories by what the index
 * holds. Every row is a door.
 *
 * The repository bars are ONE series in one hue: entries per repository is magnitude, not identity,
 * and a value-ramp or per-bar colors would decorate what the length already says. Values sit beside
 * the marks in ink, never in the mark's color.
 */
export function OverviewView({ repositories, onNavigate, onError }: {
  repositories: Repository[];
  onNavigate: (tab: 'quests' | 'projects') => void;
  onError: (message: string) => void;
}) {
  const [registry, setRegistry] = useState<Registration[]>([]);
  const [quests, setQuests] = useState<Quest[] | null>(null);

  useEffect(() => {
    const abort = new AbortController();
    api.registry(abort.signal)
      .then(setRegistry)
      .catch((e: Error) => { if (e.name !== 'AbortError') onError(e.message); });
    api.quests(null, false, abort.signal)
      .then(setQuests)
      .catch((e: Error) => { if (e.name !== 'AbortError') onError(e.message); });
    return () => abort.abort();
  }, [onError]);

  const adopted = registry.filter((r) => r.adopted);
  const open = (quests ?? []).filter((q) => q.status === 'Open');
  const taken = (quests ?? []).filter((q) => q.status === 'Taken');
  const entries = repositories.reduce((sum, r) => sum + r.total, 0);
  const oldest = open.length ? Math.max(...open.map((q) => sittingDays(q.filed))) : 0;
  // The service already orders open before taken, oldest first — exactly the reading order here.
  const outstanding = quests ?? [];
  const most = Math.max(1, ...repositories.map((r) => r.total));
  const ranked = [...repositories].sort((a, b) => b.total - a.total);

  return (
    <section className="overview">
      <PageHeader
        title="Overview"
        description="Is anything sitting, and is the family healthy — the state of the thing being managed."
      />

      <div className="tiles">
        <div className="tile">
          <span className="tile-label">Adopted projects</span>
          <span className="tile-value">{adopted.length}</span>
          <span className="tile-note">of {registry.length} in the family</span>
        </div>
        {/* A quest sitting for a week is the signal this whole view exists to surface. */}
        <div className={`tile${oldest >= 7 ? ' warn' : ''}`}>
          <span className="tile-label">Open quests</span>
          <span className="tile-value">{open.length}</span>
          <span className="tile-note">
            {open.length === 0 ? 'nothing is waiting' : `oldest has sat ${oldest === 0 ? 'under a day' : `${oldest}d`}`}
          </span>
        </div>
        <div className="tile">
          <span className="tile-label">In progress</span>
          <span className="tile-value">{taken.length}</span>
          <span className="tile-note">taken, not yet answered</span>
        </div>
        <div className="tile">
          <span className="tile-label">Knowledge entries</span>
          <span className="tile-value">{compact(entries)}</span>
          <span className="tile-note">across {repositories.length} repositories</span>
        </div>
      </div>

      <div className="overview-grid">
        <article className="group">
          <header>
            <span className="method">Outstanding — oldest first</span>
            <button onClick={() => onNavigate('quests')}>
              {outstanding.length > 0 ? 'answer them' : 'ask for something'}
            </button>
          </header>
          {quests === null && <SkeletonRows />}
          {quests?.length === 0 && (
            <EmptyState
              icon="check"
              headline="Nothing is sitting"
              body="The family owes itself nothing right now. When a project needs something from a sibling, it is asked for here."
            />
          )}
          <ul className="sitting-list">
            {outstanding.slice(0, 6).map((quest) => (
              <li key={quest.id}>
                <button className="row" onClick={() => onNavigate('quests')}>
                  <span className={`pill ${quest.status.toLowerCase()}`}>{quest.status}</span>
                  <span className="title">{quest.title}</span>
                  <span className="where">
                    {quest.from} → {quest.to} · {quest.status === 'Open' ? `filed ${ago(quest.filed)}` : `taken ${ago(quest.updated)}`}
                  </span>
                </button>
              </li>
            ))}
          </ul>
          {outstanding.length > 6 && (
            <p className="method-hint">…and {outstanding.length - 6} more in Quests.</p>
          )}
        </article>

        <article className="group">
          <header>
            <span className="method">Repositories, by what the index holds</span>
            <button onClick={() => onNavigate('projects')}>
              <Icon name="projects" />projects
            </button>
          </header>
          {repositories.length === 0 && <SkeletonRows />}
          <ul className="bars">
            {ranked.map((repository) => {
              const declared = registry.find((r) => r.repository === repository.name);
              return (
                <li key={repository.name}>
                  <span className="bar-name">
                    {declared?.adopted && <span className="adopted-dot" title="adopted — addressable for quests" />}
                    {repository.name}
                  </span>
                  <span className="bar-track">
                    <span className="bar" style={{ width: `${(repository.total / most) * 100}%` }} />
                  </span>
                  <span className="bar-value">
                    {repository.total.toLocaleString()}
                    <span className="bar-split"> · {repository.local.toLocaleString()} local</span>
                  </span>
                </li>
              );
            })}
          </ul>
          <p className="method-hint">
            ● adopted — a member, addressable for quests. The others are readable but not joined: the
            index scans the family's folder, and being seen is not being a member. “Local” is the
            repository's own material — the part no sibling can reach without this index.
          </p>
        </article>
      </div>
    </section>
  );
}
