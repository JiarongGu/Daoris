import { cn } from '../../lib/cn';

/**
 * A path on one line, cut in the middle where it is wider than its room: the folders it starts in give way to an
 * ellipsis, and its last name stays whole, since that is the part that tells two paths apart. The whole path is its title,
 * shown on hover, and what a copy takes (`data-copy`), so nothing is lost by the cut.
 *
 * Where a path must be read whole on the page, `PathText` wraps it at its separators instead.
 */
export function MiddlePath({ path, className }: { path: string; className?: string }) {
  const cut = Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\'));
  const head = cut > 0 ? path.slice(0, cut + 1) : '';
  const tail = cut > 0 ? path.slice(cut + 1) : path;
  return (
    <span data-copy={path} title={path} className={cn('flex min-w-0 font-mono text-small text-ink-soft', className)}>
      {head && <span className="min-w-0 shrink truncate">{head}</span>}
      <span className="max-w-full shrink-0 truncate">{tail}</span>
    </span>
  );
}
