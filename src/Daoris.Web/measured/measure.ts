/**
 * **The measured-box check, page-side** (FLOWCHART1a1; the flowchart design §7, trap 3): every drawn flow on the page,
 * measured as the browser laid it out. Each node's box and each shown label's box are read, and each edge's path is
 * sampled along its length by the browser's own geometry (`getPointAtLength`), never by the code that drew it.
 *
 * - **A collision** is a sample of a path inside a node that is not one of its own two ends, or inside a shown label that
 *   is not its own.
 * - **An overlap** is a shown label over a node or over another shown label.
 * - **A crossing** is a point where two paths' sampled lines cross, counted once however many samples meet there.
 *
 * Self-contained, with no import and nothing from outside its body, so Playwright can send it to the page as source.
 */
export type FlowMeasure = {
  /** The width of the story's frame the flow stands in. */
  frame: number;
  placement: string;
  nodes: number;
  paths: number;
  labels: number;
  collisions: string[];
  overlaps: string[];
  crossings: number;
  /** How many times the flow drew its edges (trap 14). */
  drawn: number;
};

export function measureFlows(): FlowMeasure[] {
  type Box = { x: number; y: number; w: number; h: number };
  type Pt = { x: number; y: number };
  const boxOf = (element: Element): Box => {
    const rect = element.getBoundingClientRect();
    return { x: rect.left, y: rect.top, w: rect.width, h: rect.height };
  };
  const inside = (point: Pt, box: Box, inset: number) =>
    point.x > box.x + inset && point.x < box.x + box.w - inset && point.y > box.y + inset && point.y < box.y + box.h - inset;
  const overlap = (a: Box, b: Box) =>
    Math.min(a.x + a.w, b.x + b.w) - Math.max(a.x, b.x) > 0.5 && Math.min(a.y + a.h, b.y + b.h) - Math.max(a.y, b.y) > 0.5;
  /** Where two segments cross, or null: touching ends and parallel runs are no crossing. */
  const cross = (a: Pt, b: Pt, c: Pt, d: Pt): Pt | null => {
    const r = { x: b.x - a.x, y: b.y - a.y };
    const s = { x: d.x - c.x, y: d.y - c.y };
    const denominator = r.x * s.y - r.y * s.x;
    if (Math.abs(denominator) < 1e-9) return null;
    const t = ((c.x - a.x) * s.y - (c.y - a.y) * s.x) / denominator;
    const u = ((c.x - a.x) * r.y - (c.y - a.y) * r.x) / denominator;
    return t > 0 && t < 1 && u > 0 && u < 1 ? { x: a.x + t * r.x, y: a.y + t * r.y } : null;
  };

  const results: FlowMeasure[] = [];
  for (const pane of document.querySelectorAll<HTMLElement>('[data-flow-pane]')) {
    const frame = Number(pane.closest('[data-frame]')?.getAttribute('data-frame') ?? 0);
    const placement = pane.dataset.placement ?? '';
    const nodes = [...pane.querySelectorAll<HTMLElement>('[data-flow-node]')].map((element) => ({ id: element.dataset.flowNode!, box: boxOf(element) }));
    const labels = [...pane.querySelectorAll<HTMLElement>('[data-flow-label][data-shown]')].map((element) => ({ id: element.dataset.flowLabel!, box: boxOf(element) }));
    const paths = [...pane.querySelectorAll<SVGPathElement>('path[data-flow-edge]')].map((element) => {
      const total = element.getTotalLength();
      const matrix = element.getScreenCTM()!;
      const points: Pt[] = [];
      for (let at = 0; at < total; at += 2) {
        const point = element.getPointAtLength(at);
        const there = new DOMPoint(point.x, point.y).matrixTransform(matrix);
        points.push({ x: there.x, y: there.y });
      }
      const last = element.getPointAtLength(total);
      const end = new DOMPoint(last.x, last.y).matrixTransform(matrix);
      points.push({ x: end.x, y: end.y });
      return { id: element.dataset.flowEdge!, from: element.dataset.from!, to: element.dataset.to!, points };
    });

    const collisions: string[] = [];
    for (const path of paths) {
      for (const node of nodes) {
        if (node.id === path.from || node.id === path.to) continue;
        if (path.points.some((point) => inside(point, node.box, 1))) collisions.push(`${path.id} runs through the node ${node.id}`);
      }
      for (const label of labels) {
        if (label.id === path.id) continue;
        if (path.points.some((point) => inside(point, label.box, 0.5))) collisions.push(`${path.id} runs under the label of ${label.id}`);
      }
    }

    const overlaps: string[] = [];
    labels.forEach((label, index) => {
      for (const node of nodes) if (overlap(label.box, node.box)) overlaps.push(`the label of ${label.id} lies over the node ${node.id}`);
      for (const other of labels.slice(index + 1)) if (overlap(label.box, other.box)) overlaps.push(`the labels of ${label.id} and ${other.id} overlap`);
    });

    // Crossings, by chunks of each path's samples whose boxes meet first, so a dense graph is counted in time.
    const chunks = paths.map((path) => {
      const out: { from: number; to: number; box: Box }[] = [];
      for (let start = 0; start < path.points.length - 1; start += 16) {
        const slice = path.points.slice(start, Math.min(path.points.length, start + 17));
        const xs = slice.map((point) => point.x);
        const ys = slice.map((point) => point.y);
        out.push({ from: start, to: start + slice.length - 1, box: { x: Math.min(...xs), y: Math.min(...ys), w: Math.max(...xs) - Math.min(...xs), h: Math.max(...ys) - Math.min(...ys) } });
      }
      return out;
    });
    const touches = (a: Box, b: Box) => a.x <= b.x + b.w && b.x <= a.x + a.w && a.y <= b.y + b.h && b.y <= a.y + a.h;
    let crossings = 0;
    for (let i = 0; i < paths.length; i += 1) {
      for (let j = i + 1; j < paths.length; j += 1) {
        const [a, b] = [paths[i]!, paths[j]!];
        const found: Pt[] = [];
        for (const ca of chunks[i]!) {
          for (const cb of chunks[j]!) {
            if (!touches(ca.box, cb.box)) continue;
            for (let p = ca.from; p < ca.to; p += 1) {
              for (let q = cb.from; q < cb.to; q += 1) {
                const at = cross(a.points[p]!, a.points[p + 1]!, b.points[q]!, b.points[q + 1]!);
                if (at && found.every((seen) => Math.hypot(seen.x - at.x, seen.y - at.y) > 4)) found.push(at);
              }
            }
          }
        }
        crossings += found.length;
      }
    }

    results.push({
      frame, placement, nodes: nodes.length, paths: paths.length, labels: labels.length, collisions, overlaps, crossings,
      drawn: Number(pane.querySelector('[data-drawn]')?.getAttribute('data-drawn') ?? 0),
    });
  }
  return results;
}
