import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { App } from './App';
import { SecondaryWindowRoot } from './SecondaryWindowRoot';
import { WorkspaceScopeProvider } from './scope';
import { secondaryWindow } from './work/window';
import { applyTheme } from './theme';
import './tokens.css';
import './i18n';

// The viewer's theme (D66), on the document BEFORE the first paint — applied after React mounted,
// every launch with a chosen theme would flash the OS's one first. A secondary window runs this
// too: same origin, same remembered choice.
applyTheme();

const client = new QueryClient({
  defaultOptions: {
    queries: {
      // A console glanced at all day: fresh enough to trust, quiet enough not to hammer the host.
      staleTime: 15_000,
      retry: 1,
    },
  },
});

// Which window this page is (SURF8). A secondary window is a ROUTE into the same bundle — the same
// bytes, the same components, a different root — which is what makes a monitor on a second screen
// cost the components it already has instead of a second frontend to keep in step.
const window_ = secondaryWindow(globalThis.location?.search ?? '');

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={client}>
      <Tooltip.Provider delayDuration={300}>
        {/* The workspace scope wraps the whole shell (WSP5): one scope per query, everywhere —
            including a secondary window, which reads the same remembered choice because it is the
            same origin. A monitor scoped to a different circle than the window that opened it
            would be the worst kind of wrong. */}
        <WorkspaceScopeProvider>
          {window_ ? <SecondaryWindowRoot window={window_} /> : <App />}
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>
  </StrictMode>,
);
