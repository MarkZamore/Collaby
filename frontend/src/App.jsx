import React, { Suspense, lazy, useEffect, useLayoutEffect } from 'react';
import { BrowserRouter as Router, Navigate, Routes, Route, useLocation } from 'react-router-dom';

// Общие provider-ы и защитный маршрут.
import { AuthProvider } from './shared/context/AuthContext';
import { TaskProvider } from './shared/context/TaskContext';
import { PreferencesProvider, usePreferences } from './platform/context/PreferencesContext';
import { FirstWorkspaceModalProvider } from './platform/context/FirstWorkspaceModalContext';
import ProtectedRoute from './shared/components/ProtectedRoute';
import AdminRoute from './shared/components/AdminRoute';
import AppLoadingScreen from './shared/components/AppLoadingScreen';
import ChunkRecoveryBoundary from './shared/components/ChunkRecoveryBoundary';
import LandingContentHydrator from './landing/components/LandingContentHydrator';
import { syncPublicRouteRuntime } from './shared/utils/publicRouteRuntime';


const Landing = lazy(() => import('./landing/pages/Landing'));
const Auth = lazy(() => import('./landing/pages/Auth'));
const About = lazy(() => import('./landing/pages/About'));
const Contact = lazy(() => import('./landing/pages/Contact'));
const PrivacyPolicy = lazy(() => import('./landing/pages/PrivacyPolicy'));
const TermsOfUse = lazy(() => import('./landing/pages/TermsOfUse'));
const CookiePolicy = lazy(() => import('./landing/pages/CookiePolicy'));
const VerifyEmailSent = lazy(() => import('./landing/pages/VerifyEmailSent'));
const VerifyEmail = lazy(() => import('./landing/pages/VerifyEmail'));
const ForgotPassword = lazy(() => import('./landing/pages/ForgotPassword'));
const ResetPassword = lazy(() => import('./landing/pages/ResetPassword'));
const AcceptInvitation = lazy(() => import('./landing/pages/AcceptInvitation'));
const Dashboard = lazy(() => import('./platform/pages/Dashboard'));
const MentorAssistantPage = lazy(() => import('./platform/pages/MentorAssistantPage'));
const WorkspaceAssistantPage = lazy(() => import('./platform/pages/WorkspaceAssistantPage'));
const AdminLayout = lazy(() => import('@admin/layout/AdminLayout.jsx'));
const AdminOverview = lazy(() => import('@admin/pages/AdminOverview.jsx'));
const AdminUsers = lazy(() => import('@admin/pages/AdminUsers.jsx'));
const AdminWorkspaces = lazy(() => import('@admin/pages/AdminWorkspaces.jsx'));
const AdminOps = lazy(() => import('@admin/pages/AdminOps.jsx'));
const AdminLanding = lazy(() => import('@admin/pages/AdminLanding.jsx'));
const AdminPanels = lazy(() => import('@admin/pages/AdminPanels.jsx'));
const AdminOperationDetail = lazy(() => import('@admin/pages/AdminOperationDetail.jsx'));

const AppRouteFallback = () => <AppLoadingScreen />;

const PUBLIC_ROUTE_PATHS = new Set([
  '/',
  '/auth',
  '/auth/',
  '/verify-email-sent',
  '/verify-email',
  '/forgot-password',
  '/reset-password',
  '/accept-invitation',
  '/about',
  '/contact',
  '/privacy',
  '/terms',
  '/cookies',
]);

const LANDING_CONTENT_ROUTE_PATHS = new Set([
  '/',
  '/about',
  '/contact',
]);

const RouteRuntimeEffects = () => {
  const location = useLocation();
  const { language } = usePreferences();

  useLayoutEffect(() => {
    const isPublicRoute = PUBLIC_ROUTE_PATHS.has(location.pathname);
    syncPublicRouteRuntime(isPublicRoute);
  }, [location.pathname]);

  return (
    <LandingContentHydrator
      active={LANDING_CONTENT_ROUTE_PATHS.has(location.pathname) && Boolean(language)}
    />
  );
};

export default function App() {
  useEffect(() => {
    // На старте убираем устаревшие клиентские ключи, которые больше не участвуют в runtime.
    localStorage.removeItem('collaby-data'); // Старые mock-данные.
    localStorage.removeItem('collaby-theme');
    localStorage.removeItem('collaby-theme-prefs');
  }, []);

  const renderLazyRoute = (element) => (
    <ChunkRecoveryBoundary fallback={<AppRouteFallback />}>
      <Suspense fallback={<AppRouteFallback />}>
        {element}
      </Suspense>
    </ChunkRecoveryBoundary>
  );

  const renderProtectedRoute = (element) => (
    <ProtectedRoute>
      {renderLazyRoute(element)}
    </ProtectedRoute>
  );

  const renderAdminRoute = (element) => (
    <AdminRoute>
      {renderLazyRoute(element)}
    </AdminRoute>
  );

  return (
    <AuthProvider>
      <PreferencesProvider>
        <TaskProvider>
          <FirstWorkspaceModalProvider>
            <Router>
              <RouteRuntimeEffects />
              <Routes>
                <Route path="/" element={renderLazyRoute(<Landing />)} />
                <Route path="/auth" element={renderLazyRoute(<Auth />)} />
                <Route path="/auth/" element={renderLazyRoute(<Auth />)} />
                <Route path="/verify-email-sent" element={renderLazyRoute(<VerifyEmailSent />)} />
                <Route path="/verify-email" element={renderLazyRoute(<VerifyEmail />)} />
                <Route path="/forgot-password" element={renderLazyRoute(<ForgotPassword />)} />
                <Route path="/reset-password" element={renderLazyRoute(<ResetPassword />)} />
                <Route path="/accept-invitation" element={renderLazyRoute(<AcceptInvitation />)} />

                <Route
                  path="/app"
                  element={renderProtectedRoute(<Navigate to="/dashboard" replace />)}
                />

                {/* Защищённые маршруты требуют авторизации. */}
                <Route
                  path="/dashboard"
                  element={renderProtectedRoute(<Dashboard />)}
                />
                <Route
                  path="/mentor"
                  element={renderProtectedRoute(<MentorAssistantPage />)}
                />
                <Route
                  path="/assistant"
                  element={renderProtectedRoute(<WorkspaceAssistantPage />)}
                />
                <Route
                  path="/dashboard/:section"
                  element={renderProtectedRoute(<Dashboard />)}
                />
                <Route
                  path="/workspace/:slug/project/:projectKey"
                  element={renderProtectedRoute(<Dashboard />)}
                />

                {/* Отдельные URL для прямых переходов в разделы платформы. */}
                <Route path="/messenger" element={renderProtectedRoute(<Dashboard />)} />
                <Route path="/inbox" element={renderProtectedRoute(<Dashboard />)} />
                <Route path="/tasks" element={renderProtectedRoute(<Dashboard />)} />
                <Route path="/team" element={renderProtectedRoute(<Dashboard />)} />
                <Route path="/analytics" element={renderProtectedRoute(<Dashboard />)} />
                <Route path="/shop" element={renderProtectedRoute(<Dashboard />)} />
                <Route path="/awards" element={renderProtectedRoute(<Dashboard />)} />
                <Route path="/gamification" element={renderProtectedRoute(<Dashboard />)} />
                <Route
                  path="/profile"
                  element={renderProtectedRoute(<Dashboard />)}
                />
                <Route
                  path="/admin"
                  element={renderAdminRoute(<AdminLayout />)}
                >
                  <Route index element={renderLazyRoute(<AdminOverview />)} />
                  <Route path="users" element={renderLazyRoute(<AdminUsers />)} />
                  <Route path="workspaces" element={renderLazyRoute(<AdminWorkspaces />)} />
                  <Route path="ops" element={renderLazyRoute(<AdminOps />)} />
                  <Route path="landing" element={renderLazyRoute(<AdminLanding />)} />
                  <Route path="panels" element={renderLazyRoute(<AdminPanels />)} />
                  <Route path="operations/:id" element={renderLazyRoute(<AdminOperationDetail />)} />
                </Route>

                {/* Публичные информационные страницы. */}
                <Route path="/about" element={renderLazyRoute(<About />)} />
                <Route path="/contact" element={renderLazyRoute(<Contact />)} />
                <Route path="/privacy" element={renderLazyRoute(<PrivacyPolicy />)} />
                <Route path="/terms" element={renderLazyRoute(<TermsOfUse />)} />
                <Route path="/cookies" element={renderLazyRoute(<CookiePolicy />)} />
              </Routes>
            </Router>
          </FirstWorkspaceModalProvider>
        </TaskProvider>
      </PreferencesProvider>
    </AuthProvider>
  );
}
