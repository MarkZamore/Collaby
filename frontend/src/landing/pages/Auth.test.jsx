import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import Auth from './Auth';

const authApiMocks = vi.hoisted(() => ({
  checkNickname: vi.fn(),
  getGoogleAuthConfig: vi.fn(),
}));

const authContextState = vi.hoisted(() => ({
  value: {
    login: vi.fn(),
    register: vi.fn(),
    authenticateWithGoogle: vi.fn(),
    finishGoogleRegistration: vi.fn(),
    currentUser: null,
    isAuthenticated: false,
    isInitialized: true,
    loading: false,
  },
}));

const panelHostsState = vi.hoisted(() => ({
  dedicatedAdminHost: false,
}));

const toastMocks = vi.hoisted(() => ({
  success: vi.fn(),
  error: vi.fn(),
}));

vi.mock('../../shared/utils/authApi', () => authApiMocks);

vi.mock('../../shared/context/AuthContext', () => ({
  useAuth: () => authContextState.value,
}));

vi.mock('../../platform/context/PreferencesContext', () => ({
  usePreferences: () => ({
    resolvedTheme: 'light',
  }),
}));

vi.mock('../../shared/utils/panelHosts', () => ({
  isDedicatedAdminHost: () => panelHostsState.dedicatedAdminHost,
}));

vi.mock('../../shared/utils/googleIdentity', () => ({
  loadGoogleIdentityScript: vi.fn(() => Promise.resolve({})),
}));

vi.mock('../components/auth/GoogleIdentityButton', () => ({
  default: ({ onCredential }) => (
    <button type="button" data-testid="google-identity-button" onClick={() => onCredential('google-credential')}>
      Google
    </button>
  ),
}));

vi.mock('react-hot-toast', () => ({
  default: {
    success: (...args) => toastMocks.success(...args),
    error: (...args) => toastMocks.error(...args),
  },
  Toaster: () => null,
}));

describe('Auth page Google flow', () => {
  beforeEach(() => {
    panelHostsState.dedicatedAdminHost = false;
    authApiMocks.checkNickname.mockReset();
    authApiMocks.getGoogleAuthConfig.mockReset();
    authContextState.value = {
      login: vi.fn(),
      register: vi.fn(),
      authenticateWithGoogle: vi.fn(),
      finishGoogleRegistration: vi.fn(),
      currentUser: null,
      isAuthenticated: false,
      isInitialized: true,
      loading: false,
    };
    toastMocks.success.mockReset();
    toastMocks.error.mockReset();

    authApiMocks.getGoogleAuthConfig.mockResolvedValue({
      enabled: true,
      clientId: 'test-google-client-id',
    });
    authApiMocks.checkNickname.mockResolvedValue({ available: true });
  });

  const renderAuth = (initialEntry = '/auth?mode=login') => {
    return render(
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path="/auth" element={<Auth />} />
          <Route path="/dashboard" element={<div>Dashboard page</div>} />
        </Routes>
      </MemoryRouter>,
    );
  };

  it('renders the Google Identity button when config is enabled', async () => {
    renderAuth();

    await waitFor(() => expect(screen.getByTestId('google-identity-button')).toBeInTheDocument());
  });

  it('does not expose the removed test account shortcut', async () => {
    renderAuth();

    await waitFor(() => expect(authApiMocks.getGoogleAuthConfig).toHaveBeenCalledTimes(1));
    expect(screen.queryByRole('button', { name: /test account|тестовый аккаунт/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/test account|тестовый аккаунт/i)).not.toBeInTheDocument();
  });

  it('hides the Google UI when config is disabled and keeps email auth available', async () => {
    authApiMocks.getGoogleAuthConfig.mockResolvedValueOnce({
      enabled: false,
      clientId: null,
    });

    renderAuth();

    await waitFor(() => expect(authApiMocks.getGoogleAuthConfig).toHaveBeenCalledTimes(1));
    expect(screen.queryByTestId('google-identity-button')).not.toBeInTheDocument();
    expect(screen.getByTestId('login-email')).toBeInTheDocument();
    expect(screen.getByTestId('login-submit')).toBeInTheDocument();
  });

  it('navigates to dashboard after successful Google login', async () => {
    authContextState.value.authenticateWithGoogle.mockResolvedValue({
      success: true,
      accessToken: 'google-access-token',
      user: {
        id: '11111111-1111-1111-1111-111111111111',
        email: 'member@collaby.test',
        username: 'member',
      },
    });

    renderAuth();

    fireEvent.click(await screen.findByTestId('google-identity-button'));

    await waitFor(() => expect(screen.getByText('Dashboard page')).toBeInTheDocument());
    expect(authContextState.value.authenticateWithGoogle).toHaveBeenCalledWith('google-credential');
  });

  it('switches into one-step nickname completion for new Google users and finishes sign-in', async () => {
    authContextState.value.authenticateWithGoogle.mockResolvedValue({
      success: true,
      requiresNickname: true,
      registrationToken: 'pending-google-registration',
      suggestedNickname: 'new_google_user',
      profile: {
        email: 'new-google@collaby.test',
      },
    });
    authContextState.value.finishGoogleRegistration.mockResolvedValue({
      success: true,
      accessToken: 'google-access-token',
      user: {
        id: '22222222-2222-2222-2222-222222222222',
        email: 'new-google@collaby.test',
        username: 'new_google_user',
      },
    });

    renderAuth();

    fireEvent.click(await screen.findByTestId('google-identity-button'));

    await waitFor(() => expect(screen.getByDisplayValue('new_google_user')).toBeInTheDocument());
    expect(screen.queryByText('Step 1 of 1')).not.toBeInTheDocument();
    expect(screen.getByText('Choose a username')).toBeInTheDocument();
    expect(screen.queryByTestId('google-identity-button')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /create account/i }));

    await waitFor(() => expect(screen.getByText('Dashboard page')).toBeInTheDocument());
    expect(authContextState.value.finishGoogleRegistration).toHaveBeenCalledWith(
      'pending-google-registration',
      'new_google_user',
    );
  });

  it('keeps the dedicated admin host on Cloudflare Access bootstrap and does not render Google auth', () => {
    panelHostsState.dedicatedAdminHost = true;

    renderAuth();

    expect(screen.getByText('Redirecting to Cloudflare Access...')).toBeInTheDocument();
    expect(screen.queryByTestId('google-identity-button')).not.toBeInTheDocument();
  });
});
