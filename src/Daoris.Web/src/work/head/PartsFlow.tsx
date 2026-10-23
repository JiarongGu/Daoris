import { useTranslation } from 'react-i18next';
import { Flow } from '../../design/Flow';
import type { EntryAcceptPlan } from '../review/EntryAccept';
import { partsFlow } from './partsLife';
import { ranLine } from '../actors';
import { PartsHeading, PartsList, type PartsProps } from './PartsStrip';

/**
 * **A session's parts drawn as a flowchart** (FLOWCHART1b; D186, the flowchart design §2.3): in the parts strip's place on the
 * session's page, its heading and then the parts as nodes, each hung by what it followed and labelled by how it began, the part
 * the page is on lit, and, once Accept's plan is read, the place the parts *would* land as a dashed node. Where the drawing does
 * not fit (the side bar's floor), or the person chose the list, the strip is the flow's rail, as it was.
 *
 * @remarks
 * - **Lineage, not concurrency** (trap 19): a part keeps no start, end or overlap, so the flow hangs parts by their `after` and
 *   says nothing of which ran when or at once; the oldest-first order is the ledger's, never a cause.
 * - **A press shows a part**: Enter on its node, and a control inside it, as the strip's button did; every act on the page stays
 *   the part shown's (D167 point 1).
 * - **The landing is a plan**, read only while Accept's ask is open, drawn from `plan`; what did land is the landing flow's.
 * - Nothing is drawn for a line of work of one part, whose page needs neither strip nor flow.
 */
export function PartsFlow({ plan, ...props }: PartsProps & { plan?: EntryAcceptPlan | null }) {
  const { t } = useTranslation();
  const { entry, attended, rows, onShow } = props;
  if (entry.parts.length < 2) return null;
  const life = partsFlow(t, entry, { attended, rows, plan });
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
      <Flow label={life.label} nodes={nodes} edges={life.edges} entries={life.entries} rail={<PartsList {...props} />} />
    </nav>
  );
}
