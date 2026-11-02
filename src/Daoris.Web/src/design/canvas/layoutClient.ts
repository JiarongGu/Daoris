import { type Layout, layoutScene, type Scene } from './layoutScene';

// Who lays a scene out (CANVAS1d; the canvas design §4): inline for a small one, in a worker for a large one so typing is never blocked.
// Every request carries the scene's revision; an answer for an older revision is dropped, and a request can be cancelled.

/** The part of a Worker the client uses, so a test hands in a fake. */
export type WorkerLike = {
  postMessage: (message: { revision: number; scene: Scene }) => void;
  terminate: () => void;
  onmessage: ((event: { data: Layout }) => void) | null;
  onerror: ((event: unknown) => void) | null;
};

/** Nodes plus edges from which a layout is sent to a worker: measured, the layered layout passes 50 ms near here. */
export const WORKER_FROM = 8000;

export const weight = (scene: Scene): number => scene.nodes.length + scene.edges.length;

export type LayoutClient = {
  /** Resolves with the layout, or null when a newer request or a cancel came first. */
  run: (scene: Scene) => Promise<Layout | null>;
  cancel: () => void;
  dispose: () => void;
  /** The revision of the latest request. */
  revision: () => number;
};

export function createLayoutClient(spawn: (() => WorkerLike) | null, workerFrom = WORKER_FROM): LayoutClient {
  let latest = 0;
  let worker: WorkerLike | null = null;
  let waiting: { revision: number; done: (layout: Layout | null) => void } | null = null;

  const settle = (layout: Layout | null) => {
    const held = waiting;
    waiting = null;
    held?.done(layout);
  };
  const stop = () => {
    worker?.terminate();
    worker = null;
  };
  const cancel = () => {
    latest += 1;
    // A worker still laying out a scene nobody wants is ended, not waited for.
    if (waiting) { settle(null); stop(); }
  };

  return {
    revision: () => latest,
    cancel,
    dispose: () => { cancel(); stop(); },
    run: (scene) => {
      latest += 1;
      const revision = latest;
      if (waiting) { settle(null); stop(); }
      if (spawn === null || weight(scene) < workerFrom) return Promise.resolve(layoutScene(scene, revision));
      return new Promise<Layout | null>((resolve) => {
        waiting = { revision, done: resolve };
        const made = (worker ??= spawn());
        made.onmessage = (event) => {
          if (event.data.revision === latest && waiting?.revision === event.data.revision) settle(event.data);
        };
        made.onerror = () => {
          // A worker that cannot run lays the scene out here, as a scene is never left undrawn.
          if (waiting?.revision === revision) { stop(); settle(layoutScene(scene, revision)); }
        };
        made.postMessage({ revision, scene });
      });
    },
  };
}

/** The shipped worker, or null where none can be made (a test's DOM). */
export function spawnLayoutWorker(): (() => WorkerLike) | null {
  if (typeof Worker === 'undefined') return null;
  return () => new Worker(new URL('./layout.worker.ts', import.meta.url), { type: 'module' }) as unknown as WorkerLike;
}
