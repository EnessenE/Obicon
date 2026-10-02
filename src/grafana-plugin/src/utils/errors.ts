/**
 * Extracts a human-readable message from an unknown thrown error. Backend
 * fetch errors carry the API message in `data.message`; other errors may be
 * plain Error objects or anything else.
 */
export function errText(e: unknown): string {
  const withData = e as { data?: { message?: unknown } };
  if (typeof withData?.data?.message === 'string' && withData.data.message) {
    return withData.data.message;
  }
  const withMessage = e as { message?: unknown };
  if (typeof withMessage?.message === 'string' && withMessage.message) {
    return withMessage.message;
  }
  return String(e);
}
