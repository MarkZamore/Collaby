import React from 'react';
import { useTranslation } from 'react-i18next';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import AppLoadingScreen from './AppLoadingScreen';

const ProtectedRoute = ({ children }) => {
  const { isAuthenticated, isInitialized, loading } = useAuth();
  const { t } = useTranslation('common');

  if (!isInitialized || loading) {
    return <AppLoadingScreen label={t('status.loading')} />;
  }

  if (!isAuthenticated) {
    return <Navigate to="/auth" replace />;
  }

  return children;
};

export default ProtectedRoute;
