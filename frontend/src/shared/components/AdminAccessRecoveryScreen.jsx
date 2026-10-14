import React from 'react';
import { ShieldAlert } from 'lucide-react';

import { redirectToCloudflareAccessLogout } from '../utils/cloudflareAccess';

const AdminAccessRecoveryScreen = ({
  message = 'Admin session could not be established.',
}) => (
  <div
    className="platform-layout flex min-h-screen w-full items-center justify-center px-6"
    style={{
      backgroundColor: 'var(--app-bg, #ffffff)',
      color: 'var(--app-text-main, #1A202C)',
    }}
  >
    <div
      className="glass-panel flex w-full max-w-xl flex-col gap-4 rounded-3xl px-6 py-6 shadow-sm"
      style={{
        backgroundColor: 'var(--app-layer-surface, #ffffff)',
        border: '1px solid var(--app-border, rgba(0, 0, 0, 0.08))',
      }}
    >
      <div className="flex items-start gap-3">
        <ShieldAlert
          className="mt-0.5 h-5 w-5 shrink-0"
          style={{ color: 'var(--app-accent, #ff6b35)' }}
        />
        <div className="space-y-2">
          <h1 className="text-base font-semibold">Admin panel sign-in needs attention</h1>
          <p className="text-sm leading-6 opacity-80">{message}</p>
          <p className="text-sm leading-6 opacity-80">
            The page stopped the automatic retry loop so we can recover the session cleanly.
          </p>
        </div>
      </div>

      <div className="flex flex-wrap gap-3">
        <button
          type="button"
          className="rounded-full px-4 py-2 text-sm font-medium"
          style={{
            backgroundColor: 'var(--app-accent, #ff6b35)',
            color: '#ffffff',
          }}
          onClick={() => window.location.reload()}
        >
          Retry this page
        </button>
        <button
          type="button"
          className="rounded-full border px-4 py-2 text-sm font-medium"
          style={{
            borderColor: 'var(--app-border, rgba(0, 0, 0, 0.08))',
          }}
          onClick={() => redirectToCloudflareAccessLogout()}
        >
          Re-enter Cloudflare Access
        </button>
      </div>
    </div>
  </div>
);

export default AdminAccessRecoveryScreen;
