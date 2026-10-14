import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import AppLoadingScreen from './AppLoadingScreen';
import CloudflareAccessRedirect from './CloudflareAccessRedirect';
import AdminAccessRecoveryScreen from './AdminAccessRecoveryScreen';
import { isDedicatedAdminHost } from '../utils/panelHosts';

const AdminRoute = ({ children }) => {
  const location = useLocation();
  const {
    isAuthenticated,
    isInitialized,
    loading,
    currentUser,
    adminBootstrapError,
  } = useAuth();

  if (!isInitialized || loading) {
    return <AppLoadingScreen label="Loading admin panel..." />;
  }

  if (!isAuthenticated) {
    if (isDedicatedAdminHost()) {
      if (adminBootstrapError) {
        return <AdminAccessRecoveryScreen message={adminBootstrapError} />;
      }

      return <CloudflareAccessRedirect label="Redirecting to Cloudflare Access..." />;
    }

    const returnTo = `${location.pathname}${location.search}${location.hash}`;
    const encodedReturnTo = encodeURIComponent(returnTo);
    return <Navigate to={`/auth?returnTo=${encodedReturnTo}`} replace state={{ returnTo }} />;
  }

  if (!currentUser?.isSystemAdmin) {
    return <Navigate to="/dashboard" replace />;
  }

  return children;
};

export default AdminRoute;
