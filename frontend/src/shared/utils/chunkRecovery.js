export const CHUNK_RECOVERY_STORAGE_KEY = 'collaby:chunk-recovery';
export const CHUNK_RECOVERY_WINDOW_MS = 60_000;

const DYNAMIC_IMPORT_ERROR_PATTERNS = [
  'Failed to fetch dynamically imported module',
  'Failed to load module script',
  'Importing a module script failed',
  'ChunkLoadError',
  'Loading chunk',
  'Unable to preload CSS',
];

const normalizeErrorMessage = (candidate) => {
  if (!candidate) {
    return '';
  }

  if (typeof candidate === 'string') {
    return candidate;
  }

  if (typeof candidate?.message === 'string') {
    return candidate.message;
  }

  return String(candidate);
};

export const isDynamicImportError = (candidate) => {
  const message = normalizeErrorMessage(candidate).toLowerCase();
  return DYNAMIC_IMPORT_ERROR_PATTERNS.some((pattern) => message.includes(pattern.toLowerCase()));
};

export const shouldTriggerChunkRecovery = (
  storage,
  locationKey,
  now = Date.now(),
  recoveryWindowMs = CHUNK_RECOVERY_WINDOW_MS,
) => {
  if (!storage || !locationKey) {
    return false;
  }

  try {
    const raw = storage.getItem(CHUNK_RECOVERY_STORAGE_KEY);
    if (!raw) {
      return true;
    }

    const parsed = JSON.parse(raw);
    if (parsed?.locationKey !== locationKey) {
      return true;
    }

    if (typeof parsed?.timestamp !== 'number') {
      return true;
    }

    return now - parsed.timestamp > recoveryWindowMs;
  } catch {
    return true;
  }
};

export const rememberChunkRecoveryAttempt = (storage, locationKey, now = Date.now()) => {
  if (!storage || !locationKey) {
    return;
  }

  storage.setItem(
    CHUNK_RECOVERY_STORAGE_KEY,
    JSON.stringify({
      locationKey,
      timestamp: now,
    }),
  );
};
