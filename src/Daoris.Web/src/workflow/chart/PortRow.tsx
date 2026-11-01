import { cn } from '../../lib/cn';
import type { NodeContext, NodeFoot } from '../../design/FlowCanvas';
import type { ChartPort } from './stepKinds';

// A decision's outcomes as ports on the node's lower edge (FLOWCHART2a; the flowchart design §2): one cell for each, in the declared
// order, ● where a route follows it and ○ where it ends at a ◎. A port is a foot line made visible: pressing it moves the focus to its
// target as the foot's line does, and the foot then omits that line, so nothing is said twice. The route leaves the port's own box.

/** The ports with the foot line each carries, and the rest of the foot, which stays the node's. */
export function splitFeet(ports: ChartPort[], feet: NodeFoot[]): { ported: { port: ChartPort; foot: NodeFoot | undefined }[]; rest: NodeFoot[] } {
  const taken = new Set<NodeFoot>();
  const ported = ports.map((port) => {
    const foot = feet.find((each) => each.port === port.outcome && !taken.has(each));
    if (foot) taken.add(foot);
    return { port, foot };
  });
  return { ported, rest: feet.filter((foot) => !taken.has(foot)) };
}

/** A foot line's words as a name: the outcome's code is plain here. */
const plain = (text: string) => text.replace(/`/g, '');

/** The row of ports, flush with the node's lower edge so a route leaves from the node's foot. */
export function PortRow({ node, ported, onFollow }: {
  node: string;
  ported: { port: ChartPort; foot: NodeFoot | undefined }[];
  onFollow: NodeContext['onFollow'];
}) {
  if (ported.length === 0) return null;
  return (
    <div className="-mx-3 -mb-2 mt-2 grid overflow-hidden rounded-b-card border-t border-line" style={{ gridTemplateColumns: `repeat(${ported.length}, minmax(0, 1fr))` }}>
      {ported.map(({ port, foot }, index) => (
        <button
          key={port.outcome}
          type="button"
          tabIndex={-1}
          disabled={!foot}
          data-flow-port={`${node}:${port.outcome}`}
          data-routed={String(port.routed)}
          data-taken={port.taken ? 'true' : undefined}
          aria-label={foot ? plain(foot.text) : port.outcome}
          onClick={() => foot && onFollow(node, foot.to)}
          className={cn(
            'flex min-w-0 cursor-pointer items-baseline gap-1 border-0 bg-transparent px-2 py-1 text-left text-meta text-ink-soft hover:text-ink',
            index > 0 && 'border-l border-line',
            port.taken ? 'font-semibold text-ink' : '',
          )}
        >
          <span aria-hidden>{port.routed ? '●' : '○'}</span>
          <span className="min-w-0 font-mono [overflow-wrap:anywhere]">{port.outcome}</span>
        </button>
      ))}
    </div>
  );
}
