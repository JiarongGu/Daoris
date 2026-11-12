import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Segmented } from '../../../design/ui';
import { Markdown } from '../../conversation/Markdown';
import { languageOf } from '../codeLines';
import { DiffLines } from '../PatchView';
import { lineDiff } from './lineDiff';

const MARKDOWN = /\.(md|markdown)$/i;

/**
 * **A file of the record, opened**: its two versions at the record compared line by line; a Markdown file that still stands is read
 * rendered as it was handed back, with its changes one press away.
 *
 * A molecule: the two texts arrive as props, null where a side holds no file.
 */
export function RecordFileView({ path, before, after }: { path: string; before: string | null; after: string | null }) {
  const { t } = useTranslation();
  const markdown = MARKDOWN.test(path) && after !== null;
  const [view, setView] = useState<'rendered' | 'changes'>('rendered');
  const switcher = markdown && (
    <div className="pb-1">
      <Segmented
        label={t('work.runReview.fileView')}
        value={view}
        options={[{ value: 'rendered', label: t('work.runReview.fileRendered') }, { value: 'changes', label: t('work.runReview.fileChanges') }]}
        onChange={setView}
      />
    </div>
  );
  if (markdown && view === 'rendered') {
    return <div className="px-3 pb-2">{switcher}<Markdown text={after} /></div>;
  }
  const { lines, whole } = lineDiff(before, after);
  return (
    <div className="px-3 pb-2">
      {switcher}
      {!whole && <p className="m-0 pb-1 text-meta text-ink-faint">{t('work.runReview.fileTooLarge')}</p>}
      <DiffLines lines={lines} language={languageOf(path)} />
    </div>
  );
}
