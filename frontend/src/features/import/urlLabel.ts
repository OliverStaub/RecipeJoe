/** Host + path, without a leading `www.`; falls back to the raw string for an unparseable URL. */
export function urlLabel(url: string): string {
  try {
    const { hostname, pathname } = new URL(url);
    const host = hostname.replace(/^www\./, '');
    return pathname === '/' ? host : `${host}${pathname}`;
  } catch {
    return url;
  }
}
