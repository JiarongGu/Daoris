import { layoutScene, type Scene } from './layoutScene';

// The layout worker (CANVAS1d): a scene in, its layout out under the same revision.
const scope = self as unknown as {
  onmessage: ((event: { data: { revision: number; scene: Scene } }) => void) | null;
  postMessage: (message: unknown, transfer?: Transferable[]) => void;
};

scope.onmessage = (event) => {
  const layout = layoutScene(event.data.scene, event.data.revision);
  scope.postMessage(layout, [layout.at.buffer]);
};
