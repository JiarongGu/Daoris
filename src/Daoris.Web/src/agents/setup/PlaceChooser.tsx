import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField, Icon } from '../../design/ui';

/** A repository of the workspace the plan may cover, with whether it is driven now. */
export type ChooserRow = { repository: string; driven: boolean };

/** The rows drawn at once; a search narrows the rest. */
const SHOWN = 100;

/**
 * **A workspace's repository chooser** (SETUPUX1j, the setup-UX design §2.2): the workspace's whole list with each repository's driving, the
 * choice by *All*, *Driven*, *Not driven* or *None* among those the search matches, a count, and a tick for each.
 *
 * A molecule: the choice arrives as a prop, and every change goes out.
 */
export function PlaceChooser({ rows, chosen, onChange }: { rows: readonly ChooserRow[]; chosen: readonly string[]; onChange: (repositories: string[]) => void }) {
  const { t } = useTranslation();
  const id = useId();
  const [search, setSearch] = useState('');
  const text = search.trim().toLowerCase();
  const matching = text ? rows.filter((each) => each.repository.toLowerCase().includes(text)) : rows;
  const shown = matching.slice(0, SHOWN);
  const choose = (pick: (row: ChooserRow) => boolean) => onChange(matching.filter(pick).map((each) => each.repository));
  const toggle = (repository: string, on: boolean) =>
    onChange(rows.map((each) => each.repository).filter((each) => (each === repository ? on : chosen.includes(each))));

  return (
    <div role="group" aria-labelledby={`${id}-title`} className="grid min-w-0 gap-1.5">
      <span id={`${id}-title`} className="text-body font-semibold text-ink">{t('agents.setup.set.repositories')}</span>
      <p className="m-0 text-small text-ink-soft">{t('agents.setup.set.repositoriesBody')}</p>
      <div role="group" aria-label={t('agents.setup.set.chooser.pick')} className="flex min-w-0 flex-wrap items-center gap-1">
        <Button variant="ghost" className="min-h-0 border border-line px-2.5 py-0.5 text-small" onClick={() => choose(() => true)}>{t('agents.setup.set.chooser.all')}</Button>
        <Button variant="ghost" className="min-h-0 border border-line px-2.5 py-0.5 text-small" onClick={() => choose((row) => row.driven)}>{t('agents.setup.set.chooser.driven')}</Button>
        <Button variant="ghost" className="min-h-0 border border-line px-2.5 py-0.5 text-small" onClick={() => choose((row) => !row.driven)}>{t('agents.setup.set.chooser.notDriven')}</Button>
        <Button variant="ghost" className="min-h-0 border border-line px-2.5 py-0.5 text-small" onClick={() => onChange([])}>{t('agents.setup.set.chooser.none')}</Button>
        <span aria-live="polite" className="ml-auto text-small text-ink-soft">
          {t('agents.setup.set.chooser.count', { chosen: chosen.length, total: rows.length })}
        </span>
      </div>
      {rows.length > 8 && (
        <div className="flex items-center gap-1.5 rounded-control border border-line-strong bg-raised py-0.5 pl-2 pr-0.5 text-ink-faint focus-within:border-accent">
          <Icon name="search" size={12} className="shrink-0" />
          <input
            type="search"
            value={search}
            aria-label={t('agents.setup.set.chooser.search')}
            placeholder={t('agents.setup.set.chooser.search')}
            onChange={(event) => setSearch(event.target.value)}
            className="min-h-6 min-w-0 flex-1 bg-transparent text-small text-ink outline-none placeholder:text-ink-faint"
          />
        </div>
      )}
      <div className="grid max-h-48 min-w-0 gap-1 overflow-auto pr-1">
        {shown.map((each) => (
          <CheckField
            key={each.repository}
            checked={chosen.includes(each.repository)}
            onChange={(on) => toggle(each.repository, on)}
            label={each.driven ? each.repository : `${each.repository} · ${t('agents.setup.page.pill.notDriven')}`}
            className="whitespace-normal text-ink [overflow-wrap:anywhere]"
          />
        ))}
        {matching.length === 0 && <p className="m-0 text-small text-ink-soft">{t('agents.setup.nothing')}</p>}
        {matching.length > shown.length && (
          <p className="m-0 text-small text-ink-soft">{t('agents.setup.set.chooser.more', { count: matching.length - shown.length })}</p>
        )}
      </div>
    </div>
  );
}
