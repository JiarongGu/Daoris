import { useTranslation } from 'react-i18next';
import type { Entry } from './api';
import { Drawer, Pill } from './ui';

/**
 * One knowledge entry, in the drawer every detail uses (D41). The body stays monospaced and
 * unrendered: doctrine is read as it is written, and a markdown renderer would be a second opinion
 * about what the file says.
 */
export function Reader({ entry, onClose }: { entry: Entry; onClose: () => void }) {
  const { t } = useTranslation();
  return (
    <Drawer
      title={entry.title}
      onClose={onClose}
      meta={
        <>
          <Pill>{t(`kind.${entry.kind}`)}</Pill>
          <Pill>{t(`provenance.${entry.provenance}`)}</Pill>
          <span className="font-mono text-[0.72rem] text-ink-faint">{entry.repository} · {entry.path}</span>
        </>
      }
    >
      <pre className="m-0 whitespace-pre-wrap break-words font-mono text-[0.82rem] leading-[1.65]">
        {entry.body}
      </pre>
    </Drawer>
  );
}
