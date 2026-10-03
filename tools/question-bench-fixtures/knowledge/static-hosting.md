---
name: static-hosting
applies_when: linking to a page of the app, or a link that fails to load
enforces: link through the router form; the bucket serves no index for a bare path
---

# Static hosting

The app is served from a storage bucket. The bucket does not serve `index.html` for a path without a trailing
slash, so a direct link such as `/app/reports/humidity` returns 404 from the origin.

Link through the router form instead: `/app/#/reports/humidity` always loads.
