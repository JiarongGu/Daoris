import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient } from '@tanstack/react-query';
import { PageRoot } from './app/PageRoot';
import { secondaryWindow } from './work/window';
import { applyTheme } from './design/theme';
import { applyZoom } from './design/zoom';
import { installPageErrors } from './bridge/shell';
import './tokens.css';
import './locales/i18n';

// The viewer's theme (D66), on the document BEFORE the first paint — applied after React mounted,
// every launch with a chosen theme would flash the OS's one first. A secondary window runs this
// too: same origin, same remembered choice.
applyTheme();
// The window's zoom (ZOOM1), likewise before the first paint and in every window: the machine's one level.
applyZoom();

// The page's own failures into the machine log (LOG1b), from before anything can fail. A browser has
// no bridge and reports nothing. A render that throws is the error screen's to report (PAGELOOP1).
installPageErrors();

const client = new QueryClient({
  defaultOptions: {
    queries: {
      // A console glanced at all day: fresh enough to trust, quiet enough not to hammer the host.
      staleTime: 15_000,
      retry: 1,
    },
  },
});

// Which window this page is (SURF8): the application's main window, or a secondary one. `PageRoot` holds both roots under
// the page's error screen.
const window_ = secondaryWindow(globalThis.location?.search ?? '');

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <PageRoot client={client} window={window_} />
  </StrictMode>,
);
