import { useTranslation } from 'react-i18next';
import { useRegistry, useRepositories } from './queries';
import { Card, Chip, PageHeader, SkeletonRows, Tip, useErrorNotify } from './ui';

/**
 * The setup half of the platform (D38): who is in the family, what each repository owns and accepts —
 * as chips a person can scan — and, just as deliberately, who cannot be asked yet. Membership is a
 * repository's own act (D32): Daoris never writes into a sibling, so nothing joins by being seen; the
 * join steps are proposed as text, never a button (D31's shape).
 */
export function ProjectsView({ notify }: { notify: (text: string, kind?: 'ok' | 'error') => void }) {
  const { t } = useTranslation();
  const registry = useRegistry();
  const repositories = useRepositories();
  useErrorNotify(registry.error ?? repositories.error, notify);

  const adopted = (registry.data ?? []).filter((r) => r.adopted);
  const outside = (registry.data ?? []).filter((r) => !r.adopted);
  const indexed = (name: string) => (repositories.data ?? []).find((r) => r.name === name);

  return (
    <section>
      <PageHeader title={t('projects.title')} description={t('projects.description')} />

      {registry.isPending && <SkeletonRows rows={4} />}

      <div className="grid items-start gap-3.5 lg:grid-cols-2">
        {adopted.map((project) => {
          const counts = indexed(project.repository);
          return (
            <Card key={project.repository}>
              <header className="flex items-baseline justify-between gap-4">
                <span className="inline-flex items-center gap-2 text-[0.95rem] font-semibold">
                  <Tip content={t('projects.adoptedDot')}>
                    <span className="inline-block size-2 shrink-0 rounded-full bg-accent" />
                  </Tip>
                  {project.repository}
                </span>
                <span className="whitespace-nowrap font-mono text-[0.78rem] tabular-nums text-ink-faint">
                  {counts
                    ? t('projects.entries', {
                        total: counts.total.toLocaleString(),
                        local: counts.local.toLocaleString(),
                        canonical: counts.canonical.toLocaleString(),
                      })
                    : t('projects.nothingIndexed')}
                </span>
              </header>
              {project.summary
                ? <p className="mt-1.5 text-[0.85rem] text-ink-soft">{project.summary}</p>
                : (
                  /* Addressable regardless — adoption gates addressing, declaration does not (D34) —
                     but an asker deserves to know they would be guessing. */
                  <p className="mt-2 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-[0.875rem] text-ink-soft">
                    {t('projects.undeclared')}
                  </p>
                )}
              {project.owns.length > 0 && (
                <p className="mt-2 flex flex-wrap items-baseline gap-1.5">
                  <span className="min-w-12 text-[0.72rem] text-ink-faint">{t('projects.owns')}</span>
                  {project.owns.map((item) => <Chip key={item}>{item}</Chip>)}
                </p>
              )}
              {project.accepts.length > 0 && (
                <p className="mt-2 flex flex-wrap items-baseline gap-1.5">
                  <span className="min-w-12 text-[0.72rem] text-ink-faint">{t('projects.accepts')}</span>
                  {project.accepts.map((item) => <Chip key={item} accent>{item}</Chip>)}
                </p>
              )}
              {project.packs.length > 0 && (
                <p className="mt-2 flex flex-wrap items-baseline gap-1.5">
                  <span className="min-w-12 text-[0.72rem] text-ink-faint">{t('projects.packs')}</span>
                  {project.packs.map((item) => <Chip key={item}>{item}</Chip>)}
                </p>
              )}
            </Card>
          );
        })}
      </div>

      {outside.length > 0 && (
        <Card className="mt-3.5">
          <header className="flex items-baseline justify-between gap-4">
            <span className="text-[0.95rem] font-semibold">{t('projects.outside.title')}</span>
            <span className="font-mono text-[0.78rem] tabular-nums text-ink-faint">{outside.length}</span>
          </header>
          <p className="mt-1.5 text-[0.85rem] text-ink-soft">{t('projects.outside.body')}</p>
          <ul className="m-0 mt-2 list-none p-0">
            {outside.map((project) => {
              const counts = indexed(project.repository);
              return (
                <li
                  key={project.repository}
                  className="flex items-baseline justify-between gap-4 border-t border-line py-1.5 text-[0.9rem] first:border-t-0"
                >
                  <span>{project.repository}</span>
                  <span className="font-mono text-[0.72rem] text-ink-faint">
                    {counts && counts.total > 0
                      ? t('projects.outside.readable', { count: counts.total.toLocaleString() })
                      : '—'}
                  </span>
                </li>
              );
            })}
          </ul>
          <p className="mt-3 rounded-control bg-accent-soft px-3 py-2.5 font-mono text-[0.8rem]">
            {t('projects.outside.join')}
          </p>
        </Card>
      )}
    </section>
  );
}
