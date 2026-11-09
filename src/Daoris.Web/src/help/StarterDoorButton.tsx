import type { ReactNode } from 'react';
import { Button } from '../design/ui';

/**
 * **A door of Get started**, drawn one way wherever a setup step or a lack leads out: Settings' guide, Ask Daoris's
 * starters and its *Set up Daoris step by step*. A step's state is the pill's to say, so a done step's door is the same
 * button as a to-do one's, and no door is a link.
 *
 * A molecule's part: a label and a press, no spacing of its own (its parent places it).
 */
export function StarterDoorButton({ onClick, children }: { onClick: () => void; children: ReactNode }) {
  return (
    <Button className="text-small whitespace-nowrap" onClick={onClick}>
      {children}
    </Button>
  );
}
