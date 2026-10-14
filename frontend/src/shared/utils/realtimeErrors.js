const BENIGN_REALTIME_TOKENS = [
  'AbortError',
  'Failed to fetch',
  'WebSocket closed with status code: 1001',
  'The connection was stopped during negotiation',
  'The connection was stopped before the hub handshake could complete',
  'Invocation canceled due to the underlying connection being closed.',
  "Cannot send data if the connection is not in the 'Connected' State.",
  'Cannot send data if the connection is not in the Connected State',
];

const toRealtimeErrorText = (error) => {
  if (!error) {
    return '';
  }

  if (typeof error === 'string') {
    return error;
  }

  return [error.name, error.message, error.stack].filter(Boolean).join(' ');
};

export const isBenignRealtimeError = (error) => {
  const text = toRealtimeErrorText(error);
  if (!text) {
    return false;
  }

  return BENIGN_REALTIME_TOKENS.some((token) => text.includes(token));
};

export const shouldIgnoreRealtimeError = (error, { isShuttingDown = false } = {}) => {
  if (isShuttingDown) {
    return true;
  }

  return isBenignRealtimeError(error);
};

export const logRealtimeIssue = (
  scope,
  errorCode,
  error,
  { isShuttingDown = false, silent = false } = {},
) => {
  if (silent || shouldIgnoreRealtimeError(error, { isShuttingDown })) {
    return false;
  }

  console.warn(`[${scope}]`, errorCode, error);
  return true;
};
