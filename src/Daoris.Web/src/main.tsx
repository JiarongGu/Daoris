import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { App } from './App';
import './tokens.css';
import './i18n';

const client = new QueryClient({
  defaultOptions: {
    queries: {
      // A console glanced at all day: fresh enough to trust, quiet enough not to hammer the host.
      staleTime: 15_000,
      retry: 1,
    },
  },
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={client}>
      <Tooltip.Provider delayDuration={300}>
        <App />
      </Tooltip.Provider>
    </QueryClientProvider>
  </StrictMode>,
);
