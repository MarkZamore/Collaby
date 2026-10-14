const ACCESS_TOKEN_KEY = 'accessToken';
const EXPLICIT_LOGOUT_KEY = 'collaby-auth-explicit-logout';

let accessToken = null;
const listeners = new Set();

const notify = () => {
  listeners.forEach((listener) => {
    try {
      listener(accessToken);
    } catch (error) {
      console.error('Не удалось уведомить слушателя auth session:', error);
    }
  });
};

export const getStoredAccessToken = () => {
  if (accessToken) {
    return accessToken;
  }

  if (typeof window === 'undefined') {
    return null;
  }

  accessToken = sessionStorage.getItem(ACCESS_TOKEN_KEY);
  return accessToken;
};

export const setStoredAccessToken = (token) => {
  accessToken = token || null;

  if (typeof window !== 'undefined') {
    if (accessToken) {
      sessionStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
      sessionStorage.removeItem(EXPLICIT_LOGOUT_KEY);
    } else {
      sessionStorage.removeItem(ACCESS_TOKEN_KEY);
    }
  }

  notify();
  return accessToken;
};

export const clearStoredAccessToken = () => {
  setStoredAccessToken(null);
};

export const markExplicitLogout = () => {
  if (typeof window === 'undefined') {
    return;
  }

  sessionStorage.setItem(EXPLICIT_LOGOUT_KEY, '1');
};

export const hasExplicitLogoutMarker = () => {
  if (typeof window === 'undefined') {
    return false;
  }

  return sessionStorage.getItem(EXPLICIT_LOGOUT_KEY) === '1';
};

export const clearExplicitLogoutMarker = () => {
  if (typeof window === 'undefined') {
    return;
  }

  sessionStorage.removeItem(EXPLICIT_LOGOUT_KEY);
};

export const subscribeToAccessToken = (listener) => {
  listeners.add(listener);
  return () => listeners.delete(listener);
};
