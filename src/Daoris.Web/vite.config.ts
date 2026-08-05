import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The build lands in the HTTP host's wwwroot, so the API and the UI are ONE origin in a real
// deployment — which is what makes CORS unnecessary there, and what lets the desktop shell host
// exactly the same bytes rather than a second copy built differently.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../Daoris.Service/Daoris.Service.Http/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5178,
    // In development the two are on different ports, so the dev server proxies rather than the host
    // relaxing CORS. Same-origin in dev as well as in production means no code path differs between
    // them — the class of bug where a feature works only in one.
    proxy: { '/api': 'http://localhost:5177' },
  },
});
