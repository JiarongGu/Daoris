import { useEffect, useState } from 'react';
import { api, type Registration } from './api';

/**
 * The setup half of the platform (D38): who is in the family, what each repository owns and accepts,
 * and — just as deliberately — who cannot be asked yet. Search answers "has anyone solved this"; this
 * answers "whose problem is this" (D34), and a silent omission would read as the repository not
 * existing.
 *
 * For a repository that has not adopted, the view proposes the join steps as text, never as a button
 * (D31's shape): the change belongs in that repository, made by whoever works there.
 */
export function ProjectsView({ onError }: { onError: (message: string) => void }) {
  const [registry, setRegistry] = useState<Registration[] | null>(null);

  useEffect(() => {
    const abort = new AbortController();
    api.registry(abort.signal)
      .then(setRegistry)
      .catch((e: Error) => { if (e.name !== 'AbortError') onError(e.message); });
    return () => abort.abort();
  }, [onError]);

  if (registry === null) return <p className="loading">reading…</p>;

  const adopted = registry.filter((r) => r.adopted);
  const outside = registry.filter((r) => !r.adopted);

  return (
    <section className="projects">
      {adopted.map((project) => (
        <article key={project.repository} className="group project">
          <header>
            <span className="method">{project.repository}</span>
            <span className="score">{project.entries} indexed entries</span>
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
          <div className="lists">
            {project.owns.length > 0 && <span><strong>owns</strong> — {project.owns.join('; ')}</span>}
            {project.accepts.length > 0 && <span><strong>accepts</strong> — {project.accepts.join('; ')}</span>}
            {project.packs.length > 0 && <span><strong>packs</strong> — {project.packs.join(', ')}</span>}
          </div>
        </article>
      ))}

      {outside.length > 0 && (
        <article className="group outside">
          <header>
            <span className="method">Not adopted, so not addressable yet</span>
            <span className="score">{outside.length}</span>
          </header>
          <p className="excerpt">
            These are in the family's folder and cannot be asked for anything: without the client a
            quest addressed to them would sit in a queue nobody reads. Listed rather than hidden —
            "who cannot be asked yet" is the same question as "who can".
          </p>
          <ul>
            {outside.map((project) => (
              <li key={project.repository}>
                <span className="method">{project.repository}</span>
                {project.entries > 0 && <span className="where">{project.entries} entries indexed read-only</span>}
              </li>
            ))}
          </ul>
          <p className="join">
            to join, in that repository: daoris init → fill in `domain` (what it is, what it owns,
            what it accepts) → daoris sync → daoris check → daoris connect
          </p>
        </article>
      )}
    </section>
  );
}
