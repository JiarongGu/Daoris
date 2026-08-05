import { useEffect } from 'react';
import type { Entry } from './api';

/**
 * Reading an entry in full. No editing, deliberately (D31) — an improvement goes through `upstream`
 * in the repository that found it, where it meets that repository's review.
 */
export function Reader({ entry, onClose }: { entry: Entry; onClose: () => void }) {
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  return (
    <div className="reader-backdrop" onClick={onClose} role="presentation">
      <article className="reader" onClick={(e) => e.stopPropagation()}>
        <header>
          <div>
            <h2>{entry.title}</h2>
            <p className="where">
              {entry.repository} · {entry.kind} · {entry.provenance} · {entry.path}
            </p>
          </div>
          <button onClick={onClose} aria-label="Close">✕</button>
        </header>
        {/* Rendered as text, not as HTML. The bodies are markdown from repositories this UI does not
            own, and a renderer is a parser someone else's file gets to drive. */}
        <pre>{entry.body}</pre>
      </article>
    </div>
  );
}
