import { useTranslation } from 'react-i18next';
import { Button, Inline, Pill, Prose, SkeletonRows } from '../../design/ui';
import { size } from '../../locales/format';
import { PageSection } from '../../work/frame/ViewMain';
import type { FilesAct, FilesFile, FilesLook, FilesPress } from './filesLook';
import { FilesPermissions } from './FilesPermissions';
import { FilesRules } from './FilesRules';

/** What a page hands its files section: the reading, or that it is on its way or refused, and the person's presses. */
export type FilesTabProps = {
  look?: FilesLook | null;
  reading?: boolean;
  /** Why there is no reading, in the page's words. */
  refusal?: string | null;
  /** The page's words for a press the driver refused. */
  said?: string | null;
  onPress?: (press: FilesPress) => void;
};

const KIND_TONE = {
  dependency: 'done', intermediate: 'neutral', output: 'neutral', cache: 'neutral', local: 'open', scratch: 'neutral',
  repository: 'declined', unknown: 'idle',
} as const;

/**
 * **A repository's files git does not manage**: each ignored top's effective rule and the layer that decided, what the layers below it
 * said, its evidence and size; the acts those rules would make with the person's answer to each, an `ask` allowed in one press and the
 * allow taken back; and the person's own rules and permissions, added or removed. Read from the rule file's layers, never copied;
 * a terminal's twin is `daoris files`. A file of the person's that does not read is said, and nothing is written over it.
 */
export function FilesTab({ repository, look, reading = false, refusal = null, said = null, onPress }: FilesTabProps & { repository: string }) {
  const { t } = useTranslation();
  if (!look) {
    return (
      <div aria-busy={reading || undefined} className="grid gap-2">
        <Prose className="text-small"><Inline text={refusal ?? t('files.view.reading')} /></Prose>
        {!refusal && <SkeletonRows rows={3} />}
      </div>
    );
  }

  return (
    <div className="grid min-w-0 gap-5">
      <section className="grid min-w-0 gap-1.5">
        <h2 className="m-0 text-title font-semibold text-ink">{t('files.view.title')}</h2>
        <Prose className="text-small"><Inline text={t('files.view.body', { repository })} /></Prose>
        <p className="m-0 text-meta text-ink-faint"><Inline text={t('files.view.twin', { repository })} /></p>
        <p className="m-0 text-meta text-ink-faint">
          {look.foundCommit ? t('files.view.read', { commit: look.commit, found: look.foundCommit }) : t('files.view.neverRead', { commit: look.commit })}
        </p>
        {onPress && (
          <div>
            <Button variant="ghost" className="border border-line px-2.5 py-0.5 text-small" onClick={() => onPress({ act: 'read' })}>{t('files.view.readAgain')}</Button>
          </div>
        )}
      </section>

      {look.unread && (
        <p role="alert" className="m-0 rounded-control bg-accent-soft px-3 py-2 text-small">
          <Inline text={t('files.view.unread', { why: look.unread })} />
        </p>
      )}
      {said && <p role="status" className="m-0 text-small text-warn">{said}</p>}
      {look.problems.length > 0 && (
        <ul aria-label={t('files.view.problems')} className="m-0 grid list-none gap-0.5 p-0 text-meta text-warn">
          {look.problems.map((problem) => <li key={problem}>{t('files.view.refused', { problem })}</li>)}
        </ul>
      )}

      <PageSection title={t('files.view.tops')}>
        {look.files.length === 0
          ? <Prose className="text-small">{t('files.view.none')}</Prose>
          : (
            <ul aria-label={t('files.view.tops')} className="m-0 grid list-none gap-2 p-0 text-small">
              {look.files.map((file) => <Top key={file.path} file={file} acts={look.acts.filter((act) => act.path === file.path)} />)}
            </ul>
          )}
      </PageSection>

      <FilesPermissions look={look} onPress={onPress} />
      <FilesRules look={look} onPress={onPress} />
    </div>
  );
}

function Top({ file, acts }: { file: FilesFile; acts: FilesAct[] }) {
  const { t } = useTranslation();
  const copy = acts.find((act) => act.act.startsWith('copy('));
  return (
    <li className="grid min-w-0 gap-0.5">
      <span className="flex min-w-0 flex-wrap items-baseline gap-x-2">
        <span className="font-mono text-ink [overflow-wrap:anywhere]">{file.path}</span>
        <Pill tone={file.kind ? KIND_TONE[file.kind] : 'neutral'}>{file.kind ? t(`files.view.kind.${file.kind}`) : t('files.view.kind.git')}</Pill>
        <span className="text-meta text-ink-soft">{file.layer ? t(`files.view.layer.${file.layer}`) : t('files.view.layer.none')}</span>
        {file.files != null && file.bytes != null && (
          <span className="text-meta text-ink-faint tabular-nums">
            {t(file.more ? 'files.view.sizeMore' : 'files.view.size', { count: file.files, size: size(file.bytes) })}
          </span>
        )}
      </span>
      {file.floor && <Prose className="text-meta text-ink-soft">{t(`files.view.floor.${file.floor}`)}</Prose>}
      {file.idle && <Prose className="text-meta text-ink-soft">{t(`files.view.idle.${file.idle}`)}</Prose>}
      {file.evidence && file.evidence.length > 0 && <Prose className="text-meta text-ink-faint">{file.evidence.join('; ')}</Prose>}
      {file.copy && (
        <Prose className="text-meta text-ink-soft">
          {t('files.view.copy')} {copy && t(`files.view.copyVerdict.${copy.verdict}`)}
        </Prose>
      )}
      {file.below && file.below.length > 0 && (
        <span className="text-meta text-ink-faint">
          {t('files.view.below', {
            said: file.below.map((each) => t('files.view.belowOne', { layer: t(`files.view.layerName.${each.layer}`), kind: t(`files.view.kind.${each.kind}`) })).join(t('files.view.belowJoin')),
          })}
        </span>
      )}
    </li>
  );
}
