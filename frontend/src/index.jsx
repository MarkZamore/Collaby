import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.css';
import './shared/styles/global.css';
import './shared/styles/semantic.css';
import './shared/i18n/config';
import App from './App.jsx';
import {
  isDynamicImportError,
  rememberChunkRecoveryAttempt,
  shouldTriggerChunkRecovery,
} from './shared/utils/chunkRecovery';
import {
  cleanupRuntimeRefreshUrl,
  CURRENT_FRONTEND_BUILD_ID,
  startRuntimeVersionMonitor,
} from './shared/utils/runtimeVersion';

const triggerChunkRecovery = () => {
  const locationKey = `${window.location.pathname}${window.location.search}`;
  if (!shouldTriggerChunkRecovery(window.sessionStorage, locationKey)) {
    return false;
  }

  rememberChunkRecoveryAttempt(window.sessionStorage, locationKey);
  window.location.reload();
  return true;
};

const handlePotentialChunkError = (candidate, preventDefault) => {
  if (!isDynamicImportError(candidate)) {
    return;
  }

  preventDefault?.();
  triggerChunkRecovery();
};

window.addEventListener('vite:preloadError', (event) => {
  handlePotentialChunkError(event?.payload ?? event?.error ?? event, () => event.preventDefault());
});

window.addEventListener('error', (event) => {
  handlePotentialChunkError(event?.error ?? event?.message);
});

window.addEventListener('unhandledrejection', (event) => {
  handlePotentialChunkError(event?.reason, () => event.preventDefault());
});

if (import.meta.env.MODE !== 'test') {
  cleanupRuntimeRefreshUrl(window.location, window.history, CURRENT_FRONTEND_BUILD_ID);
  startRuntimeVersionMonitor();
}

const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
