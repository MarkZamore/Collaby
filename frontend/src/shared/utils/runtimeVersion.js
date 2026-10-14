export const RUNTIME_VERSION_URL = '/runtime-version.json';
export const CURRENT_FRONTEND_BUILD_ID = import.meta.env.VITE_BUILD_ID || 'dev-local';
export const BUILD_RECOVERY_STORAGE_KEY = 'collaby:build-recovery';
export const BUILD_RECOVERY_WINDOW_MS = 60_000;
export const BUILD_RECOVERY_QUERY_KEY = '__build';

const parsePayload = async (response) => {
  const payload = await response.json().catch(() => null);
  if (!payload || typeof payload !== 'object') {
    throw new Error('Invalid runtime version payload');
  }

  return payload;
};

const resolveBaseOrigin = () => {
  if (typeof window !== 'undefined' && window.location?.origin) {
    return window.location.origin;
  }

  return 'https://collaby.org';
};

export const fetchPublishedRuntimeVersion = async (fetchImpl = fetch) => {
  const response = await fetchImpl(`${RUNTIME_VERSION_URL}?ts=${Date.now()}`, {
    cache: 'no-store',
    credentials: 'same-origin',
  });

  if (!response.ok) {
    throw new Error(`Runtime version request failed with ${response.status}`);
  }

  return parsePayload(response);
};

export const shouldReloadForPublishedBuild = (
  publishedVersion,
  currentBuildId = CURRENT_FRONTEND_BUILD_ID,
) => {
  const publishedBuildId = String(publishedVersion?.buildId || '').trim();
  const current = String(currentBuildId || '').trim();

  if (!publishedBuildId || !current) {
    return false;
  }

  return publishedBuildId !== current;
};

export const shouldTriggerBuildRecovery = (
  storage,
  targetBuildId,
  now = Date.now(),
  recoveryWindowMs = BUILD_RECOVERY_WINDOW_MS,
) => {
  if (!storage || !targetBuildId) {
    return false;
  }

  try {
    const raw = storage.getItem(BUILD_RECOVERY_STORAGE_KEY);
    if (!raw) {
      return true;
    }

    const parsed = JSON.parse(raw);
    if (parsed?.targetBuildId !== targetBuildId) {
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

export const rememberBuildRecoveryAttempt = (storage, targetBuildId, now = Date.now()) => {
  if (!storage || !targetBuildId) {
    return;
  }

  storage.setItem(
    BUILD_RECOVERY_STORAGE_KEY,
    JSON.stringify({
      targetBuildId,
      timestamp: now,
    }),
  );
};

export const buildRuntimeRefreshUrl = (locationLike, targetBuildId) => {
  if (!locationLike || !targetBuildId) {
    return null;
  }

  const href =
    typeof locationLike === 'string'
      ? locationLike
      : `${locationLike.origin}${locationLike.pathname}${locationLike.search}${locationLike.hash}`;

  const url = new URL(href, resolveBaseOrigin());
  url.searchParams.set(BUILD_RECOVERY_QUERY_KEY, targetBuildId);
  return url.toString();
};

export const cleanupRuntimeRefreshUrl = (
  locationLike,
  historyLike,
  currentBuildId = CURRENT_FRONTEND_BUILD_ID,
) => {
  const href =
    typeof locationLike === 'string'
      ? locationLike
      : locationLike?.href;

  if (!href || !historyLike?.replaceState) {
    return;
  }

  const url = new URL(href, resolveBaseOrigin());
  const queryBuildId = String(url.searchParams.get(BUILD_RECOVERY_QUERY_KEY) || '').trim();
  const current = String(currentBuildId || '').trim();

  if (!queryBuildId || !current || queryBuildId !== current) {
    return;
  }

  url.searchParams.delete(BUILD_RECOVERY_QUERY_KEY);
  historyLike.replaceState(historyLike.state, '', `${url.pathname}${url.search}${url.hash}`);
};

export const navigateToPublishedBuild = (
  publishedVersion,
  windowLike = typeof window !== 'undefined' ? window : null,
) => {
  const targetBuildId = String(publishedVersion?.buildId || '').trim();
  if (!windowLike?.location || !targetBuildId) {
    windowLike?.location?.reload?.();
    return;
  }

  const nextUrl = buildRuntimeRefreshUrl(windowLike.location, targetBuildId);
  if (!nextUrl) {
    windowLike.location.reload();
    return;
  }

  windowLike.location.replace(nextUrl);
};

export const startRuntimeVersionMonitor = ({
  fetchImpl = fetch,
  storage = typeof window !== 'undefined' ? window.sessionStorage : null,
  intervalMs = 30_000,
  onMismatch = (publishedVersion) => navigateToPublishedBuild(publishedVersion),
  currentBuildId = CURRENT_FRONTEND_BUILD_ID,
  logger = console,
} = {}) => {
  if (typeof window === 'undefined' || typeof document === 'undefined') {
    return () => {};
  }

  let disposed = false;
  let inFlight = false;

  const check = async () => {
    if (disposed || inFlight) {
      return;
    }

    inFlight = true;
    try {
      const publishedVersion = await fetchPublishedRuntimeVersion(fetchImpl);
      const nextBuildId = String(publishedVersion?.buildId || '').trim();

      if (!shouldReloadForPublishedBuild(publishedVersion, currentBuildId)) {
        return;
      }

      if (!shouldTriggerBuildRecovery(storage, nextBuildId)) {
        return;
      }

      rememberBuildRecoveryAttempt(storage, nextBuildId);
      onMismatch(publishedVersion);
    } catch (error) {
      logger?.warn?.('Не удалось проверить актуальность frontend runtime:', error);
    } finally {
      inFlight = false;
    }
  };

  const onVisibilityChange = () => {
    if (document.visibilityState === 'visible') {
      void check();
    }
  };

  const timerId = window.setInterval(() => {
    void check();
  }, intervalMs);

  window.addEventListener('focus', check);
  document.addEventListener('visibilitychange', onVisibilityChange);
  window.setTimeout(() => {
    void check();
  }, 5_000);

  return () => {
    disposed = true;
    window.clearInterval(timerId);
    window.removeEventListener('focus', check);
    document.removeEventListener('visibilitychange', onVisibilityChange);
  };
};
