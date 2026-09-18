import type { Entry } from './api';
import { Drawer } from './ui';

/**
 * One knowledge entry, in the drawer every detail uses (D41). The body stays monospaced and
 * unrendered: doctrine is read as it is written, and a markdown renderer would be a second opinion
 * about what the file says.
 */
export function Reader({ entry, onClose }: { entry: Entry; onClose: () => void }) {
  return (
    <Drawer
      title={entry.title}
      onClose={onClose}
      meta={
        <>
          <span className="pill">{entry.kind}</span>
          <span className="pill">{entry.provenance}</span>
          <span className="where-inline">{entry.repository} · {entry.path}</span>
        </>
      }
    >
      <pre>{entry.body}</pre>
    </Drawer>
  );
}
