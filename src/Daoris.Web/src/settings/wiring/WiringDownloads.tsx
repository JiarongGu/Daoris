import { useTranslation } from 'react-i18next';
import type { DownloadCleared, DownloadedVersion, DownloadsListing } from '../../data/wiring';
import { Button, Pill, Prose } from '../../design/ui';
import { size } from '../../locales/format';

/** A kept row's words: why it stays, with what keeps it where that is a workspace or a file. */
function keptWords(t: (key: string, options?: Record<string, unknown>) => string, keeps: string, by: readonly string[] = []) {
  const workspaces = by.filter((each) => each.startsWith('workspace:')).map((each) => each.slice('workspace:'.length));
  const file = by.find((each) => !each.startsWith('workspace:') && each !== 'machine') ?? '';
  if (keeps === 'pinned') {
    const machine = by.includes('machine');
    if (workspaces.length === 0) return t('settings.daoris.downloads.keeps.pinned');
    return t(machine ? 'settings.daoris.downloads.keeps.pinnedBoth' : 'settings.daoris.downloads.keeps.pinnedWorkspace', {
      workspaces: workspaces.join(', '),
    });
  }
  return t(`settings.daoris.downloads.keeps.${keeps}`, { file, defaultValue: t('settings.daoris.downloads.keeps.other', { why: keeps }) });
}

/** What can go: a listed row nothing keeps, or one an earlier clearing left part of. */
export const removable = (row: DownloadedVersion) => row.unfinished || !row.keeps;

/**
 * The Downloads row's clearing (CACHECLEAR1, D88's shape): every version the agents and tools downloaded under `cache/`, listed
 * first with its size and what keeps it, then one press that removes those nothing uses. The driver judges each again right before
 * it goes, so the press's answer may keep a row the list offered, and says why; a version kept is never offered to be forced.
 *
 * @remarks
 * A molecule: drawn from props, so every state is reachable without a shell. `listing` absent is the list on its way.
 */
export function WiringDownloads({ listing, busy = false, cleared = null, onClear, onClose }: {
  listing?: DownloadsListing;
  busy?: boolean;
  /** The last press's answer, row by row; null before one. */
  cleared?: DownloadCleared[] | null;
  onClear: (rows: DownloadedVersion[]) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const rows = listing?.versions ?? [];
  const going = rows.filter(removable);
  const bytes = going.reduce((sum, row) => sum + row.bytes, 0);
  const removed = (cleared ?? []).filter((row) => row.outcome === 'removed' || row.outcome === 'unfinished');
  const keptAtPress = (cleared ?? []).filter((row) => row.outcome === 'kept');

  return (
    <section aria-label={t('settings.daoris.downloads.title')} className="grid gap-2 border-t border-line pt-2">
      <Prose className="text-small text-ink-soft">{t('settings.daoris.downloads.what')}</Prose>
      {!listing && <p className="m-0 text-small text-ink-faint">{t('settings.daoris.downloads.listing')}</p>}
      {listing?.why && <p className="m-0 text-small text-ink-soft">{t(`settings.daoris.downloads.listWhy.${listing.why}`, { defaultValue: listing.why })}</p>}
      {listing && !listing.why && rows.length === 0 && <p className="m-0 text-small text-ink-soft">{t('settings.daoris.downloads.none')}</p>}
      {rows.length > 0 && (
        <ul className="m-0 grid list-none gap-1.5 p-0">
          {rows.map((row) => (
            <li key={`${row.kind}:${row.name}:${row.version}:${row.unfinished}`} className="flex flex-wrap items-baseline gap-x-3 gap-y-1 text-small">
              <span className="text-ink-faint">{t(`settings.daoris.downloads.kind.${row.kind}`)}</span>
              <span className="font-mono text-ink-soft">{row.name} {row.version}</span>
              <span className="text-ink-faint">{row.partial ? t('settings.daoris.downloads.atLeast', { size: size(row.bytes) }) : size(row.bytes)}</span>
              {removable(row)
                ? <Pill tone="open">{t(row.unfinished ? 'settings.daoris.downloads.unfinished' : 'settings.daoris.downloads.goes')}</Pill>
                : <span className="text-ink-faint">{keptWords(t, row.keeps!, row.by)}</span>}
            </li>
          ))}
        </ul>
      )}
      {listing && going.length === 0 && rows.length > 0 && <p className="m-0 text-small text-ink-soft">{t('settings.daoris.downloads.allKept')}</p>}
      {cleared && (
        <div className="grid gap-1 text-small text-ink-soft" role="status">
          <p className="m-0">{t('settings.daoris.downloads.removed', { count: removed.length, size: size(removed.reduce((sum, row) => sum + (row.bytes ?? 0), 0)) })}</p>
          {keptAtPress.map((row) => (
            <p key={`${row.kind}:${row.name}:${row.version}`} className="m-0 text-ink-faint">
              <span className="font-mono">{row.name} {row.version}</span>{' · '}{keptWords(t, row.keeps ?? 'other', row.by ?? [])}
            </p>
          ))}
        </div>
      )}
      <div className="flex flex-wrap gap-2">
        <Button variant="primary" disabled={busy || going.length === 0} onClick={() => onClear(going)}>
          {t('settings.daoris.downloads.clear', { count: going.length, size: size(bytes) })}
        </Button>
        <Button variant="ghost" disabled={busy} onClick={onClose}>{t('settings.daoris.downloads.close')}</Button>
      </div>
    </section>
  );
}
