import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { FlowCanvas, type FlowProps } from '../FlowCanvas';
import { FlowNodeBox } from '../FlowNodeBox';
import { type CanvasOptions } from './Canvas';

/**
 * **A life-flow on the canvas**: the landing's, a quest's, the parts' and a session's lives, and a workflow's
 * routes, each drawn as the generic box in the canvas's viewport from its 36rem floor, with its fit, its zoom and its focus, and below
 * it as the rail. The layout, ink, keys and folds are `FlowCanvas`'s; the one box is `FlowNodeBox`.
 *
 * A molecule: the flow arrives worded and its doors go out.
 */
export function LifeFlow(props: FlowProps) {
  const { t } = useTranslation();
  const viewport = useMemo<CanvasOptions>(
    () => ({ mode: 'display', zoom: { min: 0.5, max: 2 }, fit: t('canvas.fit'), moves: t('canvas.moves') }),
    [t],
  );
  return <FlowCanvas {...props} renderNode={(context) => <FlowNodeBox {...context} />} viewport={viewport} selection={null} />;
}
