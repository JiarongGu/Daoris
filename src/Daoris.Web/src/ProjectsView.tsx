import { useEffect, useState } from 'react';
import { api, type Registration, type Repository } from './api';
import { PageHeader, SkeletonRows } from './ui';

/**
 * The setup half of the platform (D38): who is in the family, what each repository owns and accepts —
 * as chips a person can scan rather than prose they must parse — and, just as deliberately, who
 * cannot be asked yet. Search answers "has anyone solved this"; this answers "whose problem is this"
 * (D34), and a silent omission would read as the repository not existing.
 *
 * For a repository that has not adopted, the view proposes the join steps as text, never as a button
 * (D31's shape): the change belongs in that repository, made by whoever works there.
 */
export function ProjectsView({ repositories, onError }: {
  repositories: Repository[];
  onError: (message: string) => void;
}) {
  const [registry, setRegistry] = useState<Registration[] | null>(null);

  useEffect(() => {
    const abort = new AbortController();
    api.registry(abort.signal)
      .then(setRegistry)
      .catch((e: Error) => { if (e.name !== 'AbortError') onError(e.message); });
    return () => abort.abort();
  }, [onError]);

  const adopted = (registry ?? []).filter((r) => r.adopted);
  const outside = (registry ?? []).filter((r) => !r.adopted);
  const indexed = (name: string) => repositories.find((r) => r.name === name);

  return (
    <section className="projects">
      <PageHeader
        title="Projects"
        description="Who is in the family, what each owns and accepts — and who cannot be asked yet."
      />

      {registry === null && <SkeletonRows rows={4} />}

      <div className="cards-2">
      {adopted.map((project) => {
        const counts = indexed(project.repository);
        return (
          <article key={project.repository} className="group project">
            <header>
              <span className="method"><span className="adopted-dot" title="adopted" />{project.repository}</span>
              <span className="score">
                {counts
                  ? `${counts.total.toLocaleString()} entries · ${counts.local.toLocaleString()} local · ${counts.canonical.toLocaleString()} canonical`
                  : 'nothing indexed yet'}
              </span>
            </header>
            {project.summary
              ? <p className="excerpt">{project.summary}</p>
              : (
                /* Addressable regardless — adoption gates addressing, declaration does not (D34) — but
                   an asker deserves to know they would be guessing. */
                <p className="note">
                  Adopted, but has not declared a domain — a quest here may not be its problem. In that
                  repository: fill in `domain` in daoris.json, then `daoris connect`.
                </p>
              )}
            {project.owns.length > 0 && (
              <p className="chip-row">
                <span className="chip-kind">owns</span>
                {project.owns.map((item) => <span key={item} className="chip">{item}</span>)}
              </p>
            )}
            {project.accepts.length > 0 && (
              <p className="chip-row">
                <span className="chip-kind">accepts</span>
                {project.accepts.map((item) => <span key={item} className="chip accent">{item}</span>)}
              </p>
            )}
            {project.packs.length > 0 && (
              <p className="chip-row">
                <span className="chip-kind">packs</span>
                {project.packs.map((item) => <span key={item} className="chip">{item}</span>)}
              </p>
            )}
          </article>
        );
      })}
      </div>

      {outside.length > 0 && (
        <article className="group outside">
          <header>
            <span className="method">Not adopted, so not addressable yet</span>
            <span className="score">{outside.length}</span>
          </header>
          <p className="excerpt">
            Membership is a repository's own act — Daoris never writes into a sibling, so nothing joins
            by being seen. These appear because the index can still <em>read</em> their tracked
            knowledge from this machine, which is why they carry entry counts: readable is not joined.
            Until one joins, nothing can be asked of it — a quest addressed there would sit in a queue
            nobody reads. Listed rather than hidden, because "who cannot be asked yet" is the same
            question as "who can".
          </p>
          <ul className="outside-list">
            {outside.map((project) => {
              const counts = indexed(project.repository);
              return (
                <li key={project.repository}>
                  <span>{project.repository}</span>
                  <span className="where">
                    {counts && counts.total > 0 ? `${counts.total.toLocaleString()} entries indexed read-only` : '—'}
                  </span>
                </li>
              );
            })}
          </ul>
          <p className="join">
            to join — run in that repository, by its own agent: daoris init → fill in `domain` (what
            it is, what it owns, what it accepts) → daoris sync → daoris check → daoris connect.
            the worked example is examples/ in the Daoris repository.
          </p>
        </article>
      )}
    </section>
  );
}
