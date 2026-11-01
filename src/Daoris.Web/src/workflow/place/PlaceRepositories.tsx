import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import type { WorkflowRepository } from '../current';

/**
 * **A workspace's repositories against its Workflow** (SETUPUX1o): each follows it, or sets a rule of its own and so follows its own, which a
 * rule it sets replaces whole (D145 §1, D154 §1.3, D155 §2.3); one that sets its own is a door to its own Workflow. Null where the registry
 * was not read.
 */
export function PlaceRepositories({ repositories, onOpen }: { repositories: WorkflowRepository[] | null; onOpen?: (repository: string) => void }) {
  const { t } = useTranslation();
  const headId = useId();
  const title = t('workflow.repositories.title');
  return (
    <section aria-labelledby={headId} className="grid min-w-0 gap-2">
      <h3 id={headId} className="m-0 text-body font-semibold text-ink">{title}</h3>
      {repositories === null
        ? <p className="m-0 text-small text-ink-soft">{t('workflow.repositories.unread')}</p>
        : repositories.length === 0
          ? <p className="m-0 text-small text-ink-soft">{t('workflow.repositories.none')}</p>
          : (
            <>
              <p className="m-0 text-small text-ink-soft">{t('workflow.repositories.body')}</p>
              <ul aria-label={title} className="m-0 grid list-none grid-cols-[minmax(0,max-content)_minmax(0,1fr)] gap-x-3 gap-y-1 p-0">
                {repositories.map(({ repository, own }) => (
                  <li key={repository} className="col-span-2 grid grid-cols-subgrid items-center">
                    {own && onOpen
                      ? (
                        <Button
                          variant="ghost"
                          className="min-w-0 justify-self-start border border-line px-2.5 py-0.5 text-small text-ink [overflow-wrap:anywhere]"
                          aria-label={t('workflow.door.repositoryNamed', { repository })}
                          onClick={() => onOpen(repository)}
                        >
                          {repository}
                        </Button>
                      )
                      : (
                        <span className="inline-flex min-h-[1.9rem] min-w-0 items-center border border-transparent px-2.5 py-0.5 text-small text-ink [overflow-wrap:anywhere]">
                          {repository}
                        </span>
                      )}
                    <span className="justify-self-start rounded-full border border-line-strong px-2 py-px text-meta text-ink-soft">
                      {t(own ? 'workflow.repositories.own' : 'workflow.repositories.follows')}
                    </span>
                  </li>
                ))}
              </ul>
            </>
          )}
    </section>
  );
}
