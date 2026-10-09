import { type ReactNode, useLayoutEffect } from 'react';
import { cn } from '../lib/cn';

// What the Plugins view's stories share (PLUGUI2, D140): declared icons as the driver hands them, and a theme to look at
// each state in. Stories only: nothing in the product imports this.

/** A declared SVG icon as its bytes, the shape `PluginIcon.Read` hands the page: a plug on a teal disc. */
export const SVG_ICON = `data:image/svg+xml;base64,${btoa(
  '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">'
  + '<circle cx="16" cy="16" r="15" fill="#1c7a7a"/>'
  + '<path d="M12 7v6M20 7v6M9 13h14v4a7 7 0 0 1-14 0zM16 24v4" stroke="#f4efe2" stroke-width="2.4" fill="none" stroke-linecap="round"/>'
  + '</svg>',
)}`;

/** A declared PNG icon as its bytes: a real 32 px picture, a rounded tile with a band. */
export const PNG_ICON = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAArklEQVR42sXYwQ2AIAyF4U7ERI7AKIzlQG6AiYkREbAtfeXwn/slcGnp2DdqFWJyqR6cy0JMqJqA3AqNGA73QLAAQAQfAELQ9Q5cAABB92/MixAPYBHiDViA+AKcEW2AI6IPcEKMAQ6IfwAYwQMAEXwACCEDABBygDFCBzBE6AFGiDmAAWIeMImwAUwg7ABKhC1AgbAHCBEYgACBAzARVC6K3giqAZ4I6gFg9c4AJ98nM2TuJe/oAAAAAElFTkSuQmCC';

/**
 * A story drawn in a chosen theme, the way a person chooses one (D66): `data-theme` on the document, put back when the
 * story goes. Without it a story follows the OS, so both themes are reviewed by a pair of stories.
 */
export function InTheme({ theme, children, className }: { theme: 'light' | 'dark'; children: ReactNode; className?: string }) {
  useLayoutEffect(() => {
    const root = document.documentElement;
    const was = root.dataset.theme;
    root.dataset.theme = theme;
    return () => {
      if (was === undefined) delete root.dataset.theme;
      else root.dataset.theme = was;
    };
  }, [theme]);
  return <div className={cn('bg-page p-2 text-ink', className)}>{children}</div>;
}
