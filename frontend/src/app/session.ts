/** Liest das beim Serverstart injizierte Sitzungstoken aus dem Meta-Tag. */
export function getSessionToken(): string {
  const meta = document.querySelector('meta[name="x-session-token"]')
  return meta?.getAttribute('content') ?? ''
}

export function authHeaders(extra?: Record<string, string>): Record<string, string> {
  return { 'X-Session-Token': getSessionToken(), ...extra }
}
