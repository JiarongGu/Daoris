import { useTranslation } from 'react-i18next';
import { Inline } from '../design/ui';
import { type SessionHelperDoor } from './sessionHelperDoor';

/**
 * What a helper or pass proposal asks, line by line (COWORKUX1): the question, the agent, the ask it asks again after and whose answer
 * it is. The question and names are the person's content, shown as written; the labels are chrome.
 */
export function SessionHelperFacts({ door }: { door: SessionHelperDoor }) {
  const { t } = useTranslation();
  const lines: [string, string][] = door.door === 'pass'
    ? [[t('help.proposal.helper.passes'), `#${door.ask}`]]
    : [
      [t('help.proposal.helper.question'), door.question],
      [t('help.proposal.helper.agent'), door.agent ?? t('help.proposal.helper.anyAgent')],
      ...(door.after ? [[t('help.proposal.helper.after'), `#${door.after}`] as [string, string]] : []),
      [t('help.proposal.helper.answer'), t(door.toMe ? 'help.proposal.helper.toMe' : 'help.proposal.helper.toSession')],
    ];
  return (
    <ul aria-label={t('help.proposal.helper.list')} className="m-0 mt-1 flex list-none flex-col gap-0.5 p-0">
      {lines.map(([label, value]) => (
        <li key={label} className="m-0 text-meta text-ink-soft">{label}: <span className="text-ink"><Inline text={value} /></span></li>
      ))}
    </ul>
  );
}
