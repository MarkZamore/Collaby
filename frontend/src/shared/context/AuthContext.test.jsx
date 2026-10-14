import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import AdminRoute from '../components/AdminRoute';
import { AuthProvider } from './AuthContext';

const authApiMocks = vi.hoisted(() => ({
  completeGoogleRegistration: vi.fn(),
  getCurrentUser: vi.fn(),
  getGoogleAuthConfig: vi.fn(),
  loginUser: vi.fn(),
  loginWithAccessIdentity: vi.fn(),
  logoutUser: vi.fn(),
  refreshAccessToken: vi.fn(),
  registerUser: vi.fn(),
  startGoogleAuth: vi.fn(),
}));

const authSessionState = vi.hoisted(() => ({
  accessToken: null,
  explicitLogout: false,
  listener: null,
  reset() {
    this.accessToken = null;
    this.explicitLogout = false;
    this.listener = null;
  },
}));

vi.mock('../utils/authApi', () => authApiMocks);

vi.mock('../utils/authSession', () => ({
  clearStoredAccessToken: () => {
    authSessionState.accessToken = null;
  },
  clearExplicitLogoutMarker: () => {
    authSessionState.explicitLogout = false;
  },
  getStoredAccessToken: () => authSessionState.accessToken,
  hasExplicitLogoutMarker: () => authSessionState.explicitLogout,
  markExplicitLogout: () => {
    authSessionState.explicitLogout = true;
  },
  setStoredAccessToken: (token) => {
    authSessionState.accessToken = token || null;
  },
  subscribeToAccessToken: (listener) => {
    authSessionState.listener = listener;
    return () => {
      if (authSessionState.listener === listener) {
        authSessionState.listener = null;
      }
    };
  },
}));

vi.mock('../utils/panelHosts', () => ({
  isDedicatedAdminHost: () => true,
}));

vi.mock('../utils/publicRouteRuntime', () => ({
  teardownPublicRouteRuntime: vi.fn(),
}));

describe('AuthProvider', () => {
  beforeEach(() => {
    authSessionState.reset();
    authApiMocks.getCurrentUser.mockReset();
    authApiMocks.getGoogleAuthConfig.mockReset();
    authApiMocks.loginUser.mockReset();
    authApiMocks.loginWithAccessIdentity.mockReset();
    authApiMocks.logoutUser.mockReset();
    authApiMocks.refreshAccessToken.mockReset();
    authApiMocks.registerUser.mockReset();
    authApiMocks.startGoogleAuth.mockReset();
    authApiMocks.completeGoogleRegistration.mockReset();

    authApiMocks.refreshAccessToken.mockRejectedValue(new Error('No refresh token'));
    authApiMocks.loginWithAccessIdentity.mockResolvedValue({
      accessToken: 'trusted-admin-token',
      user: {
        email: 'admin@example.com',
        isSystemAdmin: true,
      },
    });
  });

  it('keeps a cold dedicated-admin session on the protected route after access login', { timeout: 15000 }, async () => {
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/admin/users']}>
          <Routes>
            <Route
              path="/admin/users"
              element={(
                <AdminRoute>
                  <div>Admin content</div>
                </AdminRoute>
              )}
            />
            <Route path="/auth" element={<div>Auth route</div>} />
            <Route path="/dashboard" element={<div>Dashboard</div>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    );

    await waitFor(() => expect(screen.getByText('Admin content')).toBeInTheDocument(), { timeout: 5000 });
    expect(screen.queryByText('Auth route')).not.toBeInTheDocument();
    expect(authApiMocks.loginWithAccessIdentity).toHaveBeenCalledTimes(1);
  });

  it('stops the reload loop and shows a recovery screen when trusted access bootstrap fails', async () => {
    authApiMocks.loginWithAccessIdentity.mockRejectedValueOnce(new Error('Trusted admin identity is required'));

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/admin/users']}>
          <Routes>
            <Route
              path="/admin/users"
              element={(
                <AdminRoute>
                  <div>Admin content</div>
                </AdminRoute>
              )}
            />
            <Route path="/auth" element={<div>Auth route</div>} />
            <Route path="/dashboard" element={<div>Dashboard</div>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    );

    await waitFor(() => expect(screen.getByText('Admin panel sign-in needs attention')).toBeInTheDocument(), { timeout: 5000 });
    expect(screen.getByText('Trusted admin identity is required')).toBeInTheDocument();
    expect(screen.queryByText('Admin content')).not.toBeInTheDocument();
  });
});
