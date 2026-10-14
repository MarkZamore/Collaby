import { clearStoredAccessToken, getStoredAccessToken, setStoredAccessToken } from './authSession';

const API_BASE = import.meta.env.VITE_API_BASE_URL || '/api';

let refreshPromise = null;

const parsePayload = async (response) => {
  if (response.status === 204) {
    return null;
  }

  const contentType = response.headers.get('content-type') || '';
  if (contentType.includes('application/json')) {
    return response.json().catch(() => null);
  }

  return response.text().catch(() => null);
};

const createApiError = (response, payload) => {
  const message =
    payload?.error ||
    payload?.message ||
    payload?.title ||
    `Request failed with status ${response.status}`;

  const error = new Error(message);
  error.status = response.status;
  error.code = payload?.code || null;
  error.payload = payload;
  return error;
};

const refreshAccessTokenInternal = async () => {
  if (!refreshPromise) {
    refreshPromise = fetch(`${API_BASE}/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({}),
    })
      .then(async (response) => {
        const payload = await parsePayload(response);
        if (!response.ok) {
          throw createApiError(response, payload);
        }

        const nextToken = payload?.accessToken || null;
        if (!nextToken) {
          throw new Error('Refresh endpoint did not return access token');
        }

        setStoredAccessToken(nextToken);
        return nextToken;
      })
      .catch((error) => {
        clearStoredAccessToken();
        throw error;
      })
      .finally(() => {
        refreshPromise = null;
      });
  }

  return refreshPromise;
};

export const apiRequest = async (
  url,
  {
    accessToken,
    method = 'GET',
    headers = {},
    body,
    retryOnUnauthorized = true,
    credentials = 'include',
    rawResponse = false,
  } = {},
) => {
  const token = accessToken ?? getStoredAccessToken();
  const response = await fetch(url, {
    method,
    credentials,
    headers: {
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...headers,
    },
    ...(body !== undefined ? { body } : {}),
  });

  if (response.status === 401 && retryOnUnauthorized) {
    const refreshedToken = await refreshAccessTokenInternal();
    return apiRequest(url, {
      accessToken: refreshedToken,
      method,
      headers,
      body,
      retryOnUnauthorized: false,
      credentials,
      rawResponse,
    });
  }

  const payload = await parsePayload(response);

  if (!response.ok) {
    throw createApiError(response, payload);
  }

  if (rawResponse) {
    return { response, payload };
  }

  return payload;
};

export const buildApiUrl = (servicePath, path = '') => `${API_BASE}${servicePath}${path}`;

export const publicRequest = async (
  url,
  {
    method = 'GET',
    headers = {},
    body,
    credentials = 'include',
  } = {},
) => {
  const response = await fetch(url, {
    method,
    credentials,
    headers: {
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...headers,
    },
    ...(body !== undefined ? { body } : {}),
  });

  const payload = await parsePayload(response);
  if (!response.ok) {
    throw createApiError(response, payload);
  }

  return payload;
};
