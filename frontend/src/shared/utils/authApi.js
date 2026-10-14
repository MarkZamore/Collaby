// API-обёртка для аутентификации.
import i18n from '../i18n/config';
import { apiRequest, buildApiUrl, publicRequest } from './apiClient';
import { clearStoredAccessToken, setStoredAccessToken } from './authSession';

const AUTH_PATH = '/auth';

const withFallbackMessage = (error, fallbackKey) => {
  if (!error.message) {
    error.message = i18n.t(fallbackKey);
  }

  return error;
};

export const registerUser = async (userData) => {
  try {
    return await publicRequest(buildApiUrl(AUTH_PATH, '/register'), {
      method: 'POST',
      body: JSON.stringify(userData),
    });
  } catch (error) {
    throw withFallbackMessage(error, 'errors:auth.registerFailed');
  }
};

export const loginUser = async (email, password) => {
  try {
    const payload = await publicRequest(buildApiUrl(AUTH_PATH, '/login'), {
      method: 'POST',
      body: JSON.stringify({ emailOrUsername: email, password }),
    });

    if (payload?.accessToken) {
      setStoredAccessToken(payload.accessToken);
    }

    return payload;
  } catch (error) {
    throw withFallbackMessage(error, 'errors:auth.loginFailed');
  }
};

export const loginWithAccessIdentity = async () => {
  try {
    const payload = await publicRequest(buildApiUrl(AUTH_PATH, '/access-login'), {
      method: 'POST',
      body: JSON.stringify({}),
    });

    if (payload?.accessToken) {
      setStoredAccessToken(payload.accessToken);
    }

    return payload;
  } catch (error) {
    throw withFallbackMessage(error, 'errors:auth.loginFailed');
  }
};

export const getGoogleAuthConfig = async () => {
  try {
    return await publicRequest(buildApiUrl(AUTH_PATH, '/google/config'));
  } catch (error) {
    throw withFallbackMessage(error, 'errors:generic.tryAgain');
  }
};

export const startGoogleAuth = async (idToken) => {
  try {
    const payload = await publicRequest(buildApiUrl(AUTH_PATH, '/google'), {
      method: 'POST',
      body: JSON.stringify({ idToken }),
    });

    if (payload?.accessToken) {
      setStoredAccessToken(payload.accessToken);
    }

    return payload;
  } catch (error) {
    throw withFallbackMessage(error, 'errors:auth.loginFailed');
  }
};

export const completeGoogleRegistration = async (registrationToken, nickname) => {
  try {
    const payload = await publicRequest(buildApiUrl(AUTH_PATH, '/google/register'), {
      method: 'POST',
      body: JSON.stringify({ registrationToken, nickname }),
    });

    if (payload?.accessToken) {
      setStoredAccessToken(payload.accessToken);
    }

    return payload;
  } catch (error) {
    throw withFallbackMessage(error, 'errors:auth.registerFailed');
  }
};

export const getCurrentUser = async (accessToken) => {
  if (!accessToken) {
    throw new Error(i18n.t('errors:generic.noAccessToken'));
  }

  try {
    const payload = await apiRequest(buildApiUrl(AUTH_PATH, '/me'), {
      accessToken,
    });

    return payload?.user || payload;
  } catch (error) {
    throw withFallbackMessage(error, 'errors:auth.loadCurrentUser');
  }
};

export const refreshAccessToken = async () => {
  try {
    const payload = await publicRequest(buildApiUrl(AUTH_PATH, '/refresh'), {
      method: 'POST',
      body: JSON.stringify({}),
    });

    const nextToken = payload?.accessToken || null;
    if (!nextToken) {
      throw new Error(i18n.t('errors:auth.loginFailed'));
    }

    setStoredAccessToken(nextToken);
    return nextToken;
  } catch (error) {
    clearStoredAccessToken();
    throw withFallbackMessage(error, 'errors:auth.loginFailed');
  }
};

export const logoutUser = async () => {
  try {
    const payload = await publicRequest(buildApiUrl(AUTH_PATH, '/logout'), {
      method: 'POST',
      body: JSON.stringify({}),
    });
    clearStoredAccessToken();
    return payload;
  } catch (error) {
    clearStoredAccessToken();
    throw withFallbackMessage(error, 'errors:auth.logoutFailed');
  }
};

export const checkNickname = async (nickname) => {
  try {
    return await publicRequest(
      `${buildApiUrl(AUTH_PATH, '/check-nickname')}?nickname=${encodeURIComponent(nickname)}`,
    );
  } catch (error) {
    throw withFallbackMessage(error, 'errors:auth.checkNickname');
  }
};

export const verifyEmail = async (token) => {
  try {
    return await publicRequest(buildApiUrl(AUTH_PATH, '/verify-email'), {
      method: 'POST',
      body: JSON.stringify({ token }),
    });
  } catch (error) {
    throw withFallbackMessage(error, 'errors:auth.verificationFailed');
  }
};

export const resendVerificationEmail = async (email) => {
  try {
    return await publicRequest(buildApiUrl(AUTH_PATH, '/resend-verification-email'), {
      method: 'POST',
      body: JSON.stringify({ email }),
    });
  } catch (error) {
    throw withFallbackMessage(error, 'errors:auth.resendVerificationFailed');
  }
};

export const forgotPassword = async (email) => {
  try {
    return await publicRequest(buildApiUrl(AUTH_PATH, '/forgot-password'), {
      method: 'POST',
      body: JSON.stringify({ email }),
    });
  } catch (error) {
    throw withFallbackMessage(error, 'errors:auth.resendVerificationFailed');
  }
};

export const resetPassword = async (token, email, newPassword, confirmPassword) => {
  try {
    return await publicRequest(buildApiUrl(AUTH_PATH, '/reset-password'), {
      method: 'POST',
      body: JSON.stringify({ token, email, newPassword, confirmPassword }),
    });
  } catch (error) {
    throw withFallbackMessage(error, 'errors:auth.verificationFailed');
  }
};

export const validateResetPasswordToken = async (token, email) => {
  try {
    return await publicRequest(buildApiUrl(AUTH_PATH, '/validate-reset-password-token'), {
      method: 'POST',
      body: JSON.stringify({ token, email }),
    });
  } catch (error) {
    throw withFallbackMessage(error, 'errors:auth.verificationFailed');
  }
};

export const getUserProfile = async (accessToken) => {
  if (!accessToken) {
    throw new Error(i18n.t('errors:generic.noAccessToken'));
  }

  try {
    return await apiRequest(buildApiUrl('/users', '/me/profile'), {
      accessToken,
    });
  } catch (error) {
    throw withFallbackMessage(error, 'errors:auth.loadUserProfile');
  }
};

export const createUserProfile = async (accessToken, data) => {
  if (!accessToken) {
    throw new Error(i18n.t('errors:generic.noAccessToken'));
  }

  try {
    return await apiRequest(buildApiUrl('/users', '/me/profile'), {
      accessToken,
      method: 'PUT',
      body: JSON.stringify(data),
    });
  } catch (error) {
    throw withFallbackMessage(error, 'errors:auth.saveUserProfile');
  }
};
