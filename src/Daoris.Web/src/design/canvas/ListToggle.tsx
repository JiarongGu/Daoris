import { useTranslation } from 'react-i18next';
import { Button } from '../ui';

/**
 * *Show as list* and *Show as graph*: the one toolbar control that turns a world into its `CanvasList` and
 * back, offered at every width. A molecule: whether the list stands is given.
 */
export function ListToggle({ listed, onToggle }: { listed: boolean; onToggle: () => void }) {
  const { t } = useTranslation();
  return (
    <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" data-canvas-list-toggle="" onClick={onToggle}>
      {t(listed ? 'canvas.asGraph' : 'canvas.asList')}
    </Button>
  );
}
