import { useTranslation } from 'react-i18next';
import { LifeFlow } from '../../design/canvas/LifeFlow';
import { partsFlow } from './partsLife';
import { ranLine } from '../actors';
import { PartsHeading, PartsList, type PartsProps } from './PartsStrip';

/**
 * **A session's parts drawn as a flowchart**: in the parts strip's place on the
 * session's page, its heading and then the parts as nodes, each hung by what it followed and labelled by how it began, the part
 * the page is on lit. Where the drawing does
 * not fit (the side bar's floor), or the person chose the list, the strip is the flow's rail.
 *
 * @remarks
 * - **Lineage, not concurrency**: a part keeps no start, end or overlap, so the flow hangs parts by their `after` and
 *   says nothing of which ran when or at once; the oldest-first order is the ledger's, never a cause.
 * - **A press shows a part**: Enter on its node, and a control inside it, as the strip's button did; every act on the page stays
 *   the part shown's.
 * - Nothing is drawn for a line of work of one part, whose page needs neither strip nor flow.
 */
export function PartsFlow(props: PartsProps) {
  const { t } = useTranslation();
  const { entry, attended, rows, onShow } = props;
  if (entry.parts.length < 2) return null;
  const life = partsFlow(t, entry, { attended, rows });
  const nodes = life.nodes.map((node) => {
    const at = entry.parts.findIndex((part) => part.session === node.id);
    if (at < 0) return node;
    const own = [...(life.lines[node.id] ?? []), ranLine(t, rows[node.id]?.session)];
    return {
      ...node,
      open: () => onShow(node.id),
      lines: own.length > 0 || node.id !== attended
        ? (
          <div className="grid min-w-0 justify-items-start gap-0.5">
            {own.map((line) => <p key={line} className="m-0 min-w-0 [overflow-wrap:anywhere]">{line}</p>)}
            {node.id !== attended && (
              <button
                type="button"
                tabIndex={-1}
                onClick={() => onShow(node.id)}
                className="cursor-pointer border-0 bg-transparent p-0 text-left text-meta text-ink-soft underline-offset-2 hover:text-ink hover:underline"
              >
                {t('work.parts.show', { n: at + 1 })}
              </button>
            )}
          </div>
        )
        : undefined,
    };
  });
  return (
    <nav aria-label={t('work.parts.title')}>
      <PartsHeading entry={entry} attended={attended} />
      <LifeFlow label={life.label} nodes={nodes} edges={life.edges} entries={life.entries} rail={<PartsList {...props} />} />
    </nav>
  );
}
