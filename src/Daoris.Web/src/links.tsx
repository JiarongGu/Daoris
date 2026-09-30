import { createContext, type MouseEvent, type ReactNode, useContext } from 'react';

/**
 * The one place a link on the page opens (BRW7): a ticket in a quest or an ask, a URL in the
 * conversation, a link in the Markdown an agent wrote, a file the host serves.
 *
 * @remarks
 * **Where it opens is the person's choice**, `links` in the browser's settings file — the same one
 * `daoris browser links` edits (D50). The application reads it and, where it says Daoris's browser and a
 * shell is here to open one, provides an opener; this component honours whatever it is given. With no
 * opener — a browser, which has no bridge, or the system's choice — a link opens as a link always has,
 * outside the window: in the desktop a plain link would navigate the whole window away, which is the
 * drop-a-file failure in another form (platform language §4).
 *
 * **Only a web page goes to Daoris's browser** (`webAddress`): whatever opens there is a page agents can
 * drive, so a script, a file or a credential in the address is left to the link itself. The shell
 * checks the address again by its own rule before it opens anything.
 *
 * **Context, not a prop**, so a molecule deep in a conversation holds no hook of the bridge's and needs
 * nothing threaded through every level above it: rendered with no provider it is simply a link.
 */

/** What opens a web page in Daoris's browser, or null where links open as links do. */
export const LinkOpener = createContext<((address: string) => void) | null>(null);

/**
 * A link's address as a web page Daoris's browser may open — absolute `http` or `https`, with a host and
 * no credentials — or null. A relative link, an anchor, `mailto:` and the like are left to the link.
 */
export function webAddress(href: string | null | undefined): string | null {
  if (!href) return null;
  let url: URL;
  try {
    // No base: a relative link has none worth opening in a browser of its own.
    url = new URL(href);
  } catch {
    return null;
  }
  if (url.protocol !== 'http:' && url.protocol !== 'https:') return null;
  if (url.username || url.password || !url.hostname) return null;
  return url.href;
}

/**
 * A link that opens outside the window: in Daoris's browser where the application provides an opener
 * and the address is a web page, and otherwise in the system's, as `target="_blank"` always has.
 */
export function ExternalLink({ href, system = false, className, title, children }: {
  href: string | undefined;
  /**
   * Always the system's browser, whatever the person chose: a sign-in to an agent's account is the
   * person's own, and never belongs in a browser any process on this machine can drive (D78 §3.4).
   */
  system?: boolean;
  className?: string;
  title?: string;
  children: ReactNode;
}) {
  const open = useContext(LinkOpener);

  const onClick = (event: MouseEvent<HTMLAnchorElement>) => {
    const address = webAddress(href);
    if (system || !open || !address) return;
    event.preventDefault();
    open(address);
  };

  return (
    <a href={href} target="_blank" rel="noreferrer" className={className} title={title} onClick={onClick}>
      {children}
    </a>
  );
}
