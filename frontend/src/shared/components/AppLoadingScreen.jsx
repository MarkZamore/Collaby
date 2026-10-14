import React from 'react';
import { Loader2 } from 'lucide-react';

const AppLoadingScreen = ({ label = 'Loading...' }) => (
  <div
    className="platform-layout flex min-h-screen w-full items-center justify-center px-6"
    style={{
      backgroundColor: 'var(--app-bg, #ffffff)',
      color: 'var(--app-text-main, #1A202C)',
    }}
  >
    <div
      className="glass-panel flex items-center gap-3 rounded-2xl px-5 py-4 shadow-sm"
      style={{
        backgroundColor: 'var(--app-layer-surface, #ffffff)',
        border: '1px solid var(--app-border, rgba(0, 0, 0, 0.08))',
      }}
    >
      <Loader2
        className="h-4 w-4 animate-spin"
        style={{ color: 'var(--app-accent, #ff6b35)' }}
      />
      <span className="text-sm font-medium">{label}</span>
    </div>
  </div>
);

export default AppLoadingScreen;
