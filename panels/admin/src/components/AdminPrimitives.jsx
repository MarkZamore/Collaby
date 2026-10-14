import React from 'react';

const joinClasses = (...classes) => classes.filter(Boolean).join(' ');

export const pageContainerClass = 'app-admin-stack';
export const controlButtonClass = 'app-control-button';
export const destructiveButtonClass = 'app-control-button app-control-button--destructive';
export const inputClass = 'app-input-control';
export const tableWrapClass = 'app-table-wrap';
export const tableClass = 'app-table';
export const linkClass = 'app-admin-link';
export const splitGridClass = 'app-admin-split';
export const statGridClass = 'app-admin-stat-grid';
export const statCardClass = 'app-admin-stat-card';
export const listClass = 'app-admin-list';
export const listRowClass = 'app-admin-list-row';
export const detailHeaderClass = 'app-admin-detail-header';
export const actionsClass = 'app-admin-actions';
export const badgesClass = 'app-admin-badges';
export const mutedTextClass = 'app-admin-muted';

export const StatusBadge = ({ value }) => {
  const normalized = String(value || 'unknown').toLowerCase();
  const palette = {
    succeeded: ['#dff7e6', '#1a6a36'],
    completed: ['#dff7e6', '#1a6a36'],
    active: ['#dff7e6', '#1a6a36'],
    registered: ['#dff7e6', '#1a6a36'],
    running: ['#fff1cf', '#8f5d06'],
    triggered: ['#fff1cf', '#8f5d06'],
    queued: ['#e6efff', '#2656a8'],
    pending: ['#e6efff', '#2656a8'],
    allowlisted: ['#e6efff', '#2656a8'],
    retry_requested: ['#efe4ff', '#5f35a8'],
    failed: ['#ffe2de', '#9d3523'],
    inactive: ['#ffe2de', '#9d3523'],
    protected_account: ['#efe4ff', '#5f35a8'],
    system_admin: ['#efe4ff', '#5f35a8'],
    noop: ['#ebe6dd', '#6d5a43'],
    archived: ['#ebe6dd', '#6d5a43'],
    workspace_only: ['#ebe6dd', '#6d5a43'],
    not_probed: ['#e8eef5', '#40566f'],
  };
  const [background, color] = palette[normalized] || ['#ece6d9', '#5b4630'];

  return (
    <span
      className="app-status-badge"
      style={{
        '--badge-bg': background,
        '--badge-color': color,
      }}
    >
      {String(value || 'unknown').replaceAll('_', ' ')}
    </span>
  );
};

export const PageHeader = ({ title, subtitle, actions = null }) => (
  <div className="app-page-header">
    <div>
      <h1 className="app-page-title">{title}</h1>
      {subtitle ? <p className="app-page-subtitle">{subtitle}</p> : null}
    </div>
    {actions ? <div className="app-page-actions">{actions}</div> : null}
  </div>
);

export const Panel = ({ title, children, aside = null }) => (
  <section className="app-panel">
    {(title || aside) ? (
      <div className="app-panel-header">
        {title ? <h2 className="app-panel-title">{title}</h2> : <span />}
        {aside}
      </div>
    ) : null}
    {children}
  </section>
);

export const EmptyState = ({ children }) => (
  <div className="app-empty-state">
    {children}
  </div>
);

export const tableRowClass = ({ interactive = false } = {}) => joinClasses(
  interactive ? 'cursor-pointer transition-colors hover:bg-white/25 dark:hover:bg-white/[0.03]' : null,
);
