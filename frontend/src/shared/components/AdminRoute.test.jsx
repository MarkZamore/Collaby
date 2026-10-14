import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import AdminRoute from './AdminRoute';

const authState = vi.hoisted(() => ({
  value: null,
}));

const panelHostState = vi.hoisted(() => ({
  dedicatedAdminHost: false,
}));

const cloudflareAccessMocks = vi.hoisted(() => ({
  redirectToCloudflareAccessLogin: vi.fn(),
}));

vi.mock('../context/AuthContext', () => ({
  useAuth: () => authState.value,
}));

vi.mock('../utils/panelHosts', () => ({
  isDedicatedAdminHost: () => panelHostState.dedicatedAdminHost,
}));

vi.mock('../utils/cloudflareAccess', () => cloudflareAccessMocks);

const AuthRouteProbe = () => {
  const location = useLocation();
  return <div>{`Auth route ${location.pathname}${location.search}`}</div>;
};

describe('AdminRoute', () => {
  beforeEach(() => {
    panelHostState.dedicatedAdminHost = false;
    cloudflareAccessMocks.redirectToCloudflareAccessLogin.mockReset();
  });

  it('renders admin content for system admins', () => {
    authState.value = {
      isAuthenticated: true,
      isInitialized: true,
      loading: false,
      currentUser: { isSystemAdmin: true },
      adminBootstrapError: null,
    };

    render(
      <MemoryRouter initialEntries={['/admin']}>
        <Routes>
          <Route path="/admin" element={<AdminRoute><div>Admin content</div></AdminRoute>} />
          <Route path="/dashboard" element={<div>Dashboard</div>} />
          <Route path="/auth" element={<div>Auth</div>} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByText('Admin content')).toBeInTheDocument();
  });

  it('redirects non-admin users back to the dashboard', () => {
    authState.value = {
      isAuthenticated: true,
      isInitialized: true,
      loading: false,
      currentUser: { isSystemAdmin: false },
      adminBootstrapError: null,
    };

    render(
      <MemoryRouter initialEntries={['/admin']}>
        <Routes>
          <Route path="/admin" element={<AdminRoute><div>Admin content</div></AdminRoute>} />
          <Route path="/dashboard" element={<div>Dashboard</div>} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByText('Dashboard')).toBeInTheDocument();
    expect(screen.queryByText('Admin content')).not.toBeInTheDocument();
  });

  it('redirects anonymous users to auth and preserves the admin return path', () => {
    authState.value = {
      isAuthenticated: false,
      isInitialized: true,
      loading: false,
      currentUser: null,
      adminBootstrapError: null,
    };

    render(
      <MemoryRouter initialEntries={['/admin/ops?tab=services']}>
        <Routes>
          <Route path="/admin/ops" element={<AdminRoute><div>Admin content</div></AdminRoute>} />
          <Route path="/auth" element={<AuthRouteProbe />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByText('Auth route /auth?returnTo=%2Fadmin%2Fops%3Ftab%3Dservices')).toBeInTheDocument();
    expect(screen.queryByText('Admin content')).not.toBeInTheDocument();
  });

  it('forces dedicated admin hosts back through Cloudflare Access instead of local auth', async () => {
    panelHostState.dedicatedAdminHost = true;
    authState.value = {
      isAuthenticated: false,
      isInitialized: true,
      loading: false,
      currentUser: null,
      adminBootstrapError: null,
    };

    render(
      <MemoryRouter initialEntries={['/admin/ops']}>
        <Routes>
          <Route path="/admin/ops" element={<AdminRoute><div>Admin content</div></AdminRoute>} />
          <Route path="/auth" element={<AuthRouteProbe />} />
        </Routes>
      </MemoryRouter>,
    );

    await waitFor(() => expect(cloudflareAccessMocks.redirectToCloudflareAccessLogin).toHaveBeenCalledTimes(1));
    expect(screen.getByText('Redirecting to Cloudflare Access...')).toBeInTheDocument();
    expect(screen.queryByText(/Auth route/)).not.toBeInTheDocument();
  });

  it('shows a recovery screen instead of redirecting forever when admin bootstrap fails', () => {
    panelHostState.dedicatedAdminHost = true;
    authState.value = {
      isAuthenticated: false,
      isInitialized: true,
      loading: false,
      currentUser: null,
      adminBootstrapError: 'Trusted admin identity is required',
    };

    render(
      <MemoryRouter initialEntries={['/admin/ops']}>
        <Routes>
          <Route path="/admin/ops" element={<AdminRoute><div>Admin content</div></AdminRoute>} />
          <Route path="/auth" element={<AuthRouteProbe />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByText('Admin panel sign-in needs attention')).toBeInTheDocument();
    expect(screen.getByText('Trusted admin identity is required')).toBeInTheDocument();
    expect(cloudflareAccessMocks.redirectToCloudflareAccessLogin).not.toHaveBeenCalled();
  });
});
