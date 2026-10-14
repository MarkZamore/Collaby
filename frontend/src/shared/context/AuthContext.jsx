import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import i18n, { normalizeLanguage } from '../i18n/config';
import {
  completeGoogleRegistration,
  loginUser,
  loginWithAccessIdentity,
  registerUser,
  getCurrentUser,
  startGoogleAuth,
  refreshAccessToken,
  logoutUser,
} from '../utils/authApi';
import {
  clearStoredAccessToken,
  clearExplicitLogoutMarker,
  getStoredAccessToken,
  hasExplicitLogoutMarker,
  markExplicitLogout,
  setStoredAccessToken,
  subscribeToAccessToken,
} from '../utils/authSession';
import { redirectToCloudflareAccessLogout } from '../utils/cloudflareAccess';
import { isDedicatedAdminHost } from '../utils/panelHosts';
import { teardownPublicRouteRuntime } from '../utils/publicRouteRuntime';
import { updateMyProfile } from '../utils/usersApi';

const AuthContext = createContext(null);

const normalizeAuthUser = (user) => {
  if (!user) {
    return null;
  }

  return {
    ...user,
    isSystemAdmin: Boolean(user.isSystemAdmin),
  };
};

const preloadProtectedShell = async () => {
  try {
    await Promise.all([
      import('../../platform/pages/Dashboard'),
      i18n.loadNamespaces(['platform']),
    ]);
  } catch {
    // Если префетч не удался, обычная навигация всё равно догрузит маршрут.
  }
};

export const AuthProvider = ({ children }) => {
  const [accessToken, setAccessToken] = useState(() => getStoredAccessToken());
  const [currentUser, setCurrentUser] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [adminBootstrapError, setAdminBootstrapError] = useState(null);
  const [isInitialized, setIsInitialized] = useState(false);

  useEffect(() => subscribeToAccessToken(setAccessToken), []);

  const syncAccessToken = useCallback((token) => {
    const nextToken = token || null;
    setAccessToken(nextToken);
    setStoredAccessToken(nextToken);
    return nextToken;
  }, []);

  useEffect(() => {
    const initializeAuth = async () => {
      setLoading(true);
      setError(null);
      setAdminBootstrapError(null);

      try {
        const storedToken = getStoredAccessToken();

        if (storedToken) {
          const user = await getCurrentUser(storedToken);
          setCurrentUser(normalizeAuthUser(user));
          return;
        }

        const dedicatedAdminHost = isDedicatedAdminHost();
        if (hasExplicitLogoutMarker()) {
          if (dedicatedAdminHost) {
            clearExplicitLogoutMarker();
          } else {
            return;
          }
        }

        const refreshedToken = await refreshAccessToken().catch(() => null);
        if (refreshedToken) {
          const user = await getCurrentUser(refreshedToken);
          setCurrentUser(normalizeAuthUser(user));
          return;
        }

        if (!dedicatedAdminHost) {
          return;
        }

        let accessLoginError = null;
        const accessLoginResponse = await loginWithAccessIdentity().catch((err) => {
          accessLoginError = err;
          return null;
        });
        const trustedToken = accessLoginResponse?.accessToken || null;
        const trustedUser = normalizeAuthUser(accessLoginResponse?.user || null);

        if (!trustedToken || !trustedUser) {
          setAdminBootstrapError(accessLoginError?.message || 'Unable to establish the admin session from Cloudflare Access.');
          return;
        }

        syncAccessToken(trustedToken);
        clearExplicitLogoutMarker();
        teardownPublicRouteRuntime();
        setCurrentUser(trustedUser);
        void preloadProtectedShell();
      } catch (err) {
        syncAccessToken(null);
        setCurrentUser(null);
        if (isDedicatedAdminHost()) {
          setAdminBootstrapError(err?.message || 'Unable to initialize the admin session.');
        }
      } finally {
        setLoading(false);
        setIsInitialized(true);
      }
    };

    initializeAuth();
  }, [syncAccessToken]);

  const establishAuthenticatedSession = useCallback(async (authPayload) => {
    const nextToken = authPayload?.accessToken || null;
    const user = normalizeAuthUser(authPayload?.user || authPayload);

    syncAccessToken(nextToken);
    teardownPublicRouteRuntime();
    setCurrentUser(user);
    void preloadProtectedShell();

    return user;
  }, [syncAccessToken]);

  const syncRegistrationLanguage = useCallback(async (authPayload, requestedLanguage = null) => {
    const accessToken = authPayload?.accessToken || null;
    if (!accessToken) {
      return authPayload;
    }

    const nextLanguage = normalizeLanguage(requestedLanguage || i18n.resolvedLanguage || i18n.language);

    try {
      await updateMyProfile(accessToken, { language: nextLanguage });
    } catch (error) {
      console.warn('Не удалось синхронизировать язык нового аккаунта после регистрации:', error);
    }

    return authPayload;
  }, []);

  const login = useCallback(async (email, password) => {
    setLoading(true);
    setError(null);

    try {
      const response = await loginUser(email, password);
      const user = await establishAuthenticatedSession(response);
      return user;
    } catch (err) {
      const errorMsg = err.message || i18n.t('errors:auth.loginFailed');
      setError(errorMsg);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [establishAuthenticatedSession]);

  const register = useCallback(async (userData) => {
    setLoading(true);
    setError(null);

    try {
      const response = await registerUser(userData);
      return await syncRegistrationLanguage(response, userData?.language);
    } catch (err) {
      const errorMsg = err.message || i18n.t('errors:auth.registerFailed');
      setError(errorMsg);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [syncRegistrationLanguage]);

  const authenticateWithGoogle = useCallback(async (idToken) => {
    setLoading(true);
    setError(null);

    try {
      const response = await startGoogleAuth(idToken);
      if (response?.requiresNickname) {
        return response;
      }

      const user = await establishAuthenticatedSession(response);
      return {
        ...response,
        user,
      };
    } catch (err) {
      const errorMsg = err.message || i18n.t('errors:auth.loginFailed');
      setError(errorMsg);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [establishAuthenticatedSession]);

  const finishGoogleRegistration = useCallback(async (registrationToken, nickname) => {
    setLoading(true);
    setError(null);

    try {
      const response = await completeGoogleRegistration(registrationToken, nickname);
      await syncRegistrationLanguage(response);
      const user = await establishAuthenticatedSession(response);
      return {
        ...response,
        user,
      };
    } catch (err) {
      const errorMsg = err.message || i18n.t('errors:auth.registerFailed');
      setError(errorMsg);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [establishAuthenticatedSession, syncRegistrationLanguage]);

  const logout = useCallback(async () => {
    setLoading(true);
    setError(null);
    setAdminBootstrapError(null);
    const dedicatedAdminHost = isDedicatedAdminHost();

    try {
      if (dedicatedAdminHost) {
        clearExplicitLogoutMarker();
      } else {
        markExplicitLogout();
      }
      await logoutUser();
    } catch (err) {
      console.error('Ошибка выхода:', err);
    } finally {
      clearStoredAccessToken();
      setAccessToken(null);
      setCurrentUser(null);
      setLoading(false);
      if (dedicatedAdminHost) {
        redirectToCloudflareAccessLogout();
        return;
      }

      window.location.href = '/';
    }
  }, []);

  const refresh = useCallback(async () => {
    try {
      const newToken = await refreshAccessToken();
      syncAccessToken(newToken);
      return newToken;
    } catch (err) {
      syncAccessToken(null);
      setCurrentUser(null);
      throw err;
    }
  }, [syncAccessToken]);

  const getMe = useCallback(async () => {
    const token = getStoredAccessToken();
    if (!token) {
      throw new Error(i18n.t('errors:generic.noAccessToken'));
    }

    try {
      const user = await getCurrentUser(token);
      setCurrentUser(normalizeAuthUser(user));
      return user;
    } catch (err) {
      if (err.status === 401) {
        const refreshedToken = await refresh();
        const user = await getCurrentUser(refreshedToken);
        setCurrentUser(normalizeAuthUser(user));
        return user;
      }

      throw err;
    }
  }, [refresh]);

  const effectiveAccessToken = accessToken || getStoredAccessToken();

  const value = {
    accessToken: effectiveAccessToken,
    currentUser,
    loading,
    error,
    adminBootstrapError,
    isInitialized,
    isAuthenticated: !!effectiveAccessToken && !!currentUser,
    login,
    register,
    authenticateWithGoogle,
    finishGoogleRegistration,
    logout,
    refresh,
    getMe,
    setError,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return context;
};

export default AuthContext;
