import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getAdminUsers, getAdminWorkspaces, getRestartOperations, getSystemAdminRoster } from '../utils/adminApi';
import {
  EmptyState,
  PageHeader,
  Panel,
  StatusBadge,
  controlButtonClass,
  linkClass,
  listClass,
  listRowClass,
  mutedTextClass,
  pageContainerClass,
  splitGridClass,
  statCardClass,
  statGridClass,
} from '../components/AdminPrimitives';
import { useAuth } from '@shared/context/AuthContext';

const AdminOverview = () => {
  const { accessToken } = useAuth();
  const [state, setState] = useState({
    loading: true,
    error: null,
    users: 0,
    workspaces: 0,
    operations: 0,
    latestOperation: null,
    roster: [],
  });

  const load = async () => {
    setState((prev) => ({ ...prev, loading: true, error: null }));
    try {
      const [users, workspaces, operations, roster] = await Promise.all([
        getAdminUsers({ page: 1, pageSize: 1 }, accessToken),
        getAdminWorkspaces({ page: 1, pageSize: 1 }, accessToken),
        getRestartOperations(accessToken),
        getSystemAdminRoster(accessToken),
      ]);

      setState({
        loading: false,
        error: null,
        users: users?.totalCount || 0,
        workspaces: workspaces?.totalCount || 0,
        operations: operations?.items?.length || 0,
        latestOperation: operations?.items?.[0] || null,
        roster: roster?.entries || [],
      });
    } catch (error) {
      setState((prev) => ({
        ...prev,
        loading: false,
        error: error.message || 'Failed to load admin overview.',
      }));
    }
  };

  useEffect(() => {
    load();
  }, [accessToken]);

  return (
    <div className={pageContainerClass}>
      <PageHeader
        title="System overview"
        subtitle="Read-only snapshot of admin scope: current roster, global object counts and the latest restart activity."
        actions={<button type="button" className={controlButtonClass} onClick={load}>Refresh</button>}
      />

      <div className={statGridClass}>
        {[
          ['Users', state.users],
          ['Workspaces', state.workspaces],
          ['Restart ops', state.operations],
        ].map(([label, value]) => (
          <div key={label} className={statCardClass}>
            <div className="app-admin-stat-label">{label}</div>
            <div className="app-admin-stat-value">{value}</div>
          </div>
        ))}
      </div>

      <div className={splitGridClass}>
        <Panel title="System admin roster">
          {state.loading ? (
            <div>Loading roster...</div>
          ) : state.error ? (
            <EmptyState>{state.error}</EmptyState>
          ) : state.roster.length === 0 ? (
            <EmptyState>`SYSTEM_ADMIN_EMAILS` is empty.</EmptyState>
          ) : (
            <div className={listClass}>
              {state.roster.map((entry) => (
                <div
                  key={entry.email}
                  className={listRowClass}
                >
                  <div>
                    <div className="font-bold">{entry.email}</div>
                    <div className={mutedTextClass}>
                      {entry.isRegistered
                        ? `${entry.username || 'Registered user'}${entry.isActive === false ? ' • inactive' : ''}`
                        : 'Allowlisted email without registered account'}
                    </div>
                  </div>
                  <StatusBadge value={entry.isRegistered ? 'registered' : 'allowlisted'} />
                </div>
              ))}
            </div>
          )}
        </Panel>

        <Panel title="Latest restart activity">
          {state.latestOperation ? (
            <div className="grid gap-3">
              <div className={mutedTextClass}>Target service</div>
              <div className="text-[22px] font-extrabold">{state.latestOperation.targetService}</div>
              <StatusBadge value={state.latestOperation.status} />
              <div className={mutedTextClass}>
                Requested at {new Date(state.latestOperation.requestedAtUtc).toLocaleString()}
              </div>
              <Link
                to={`/admin/operations/${state.latestOperation.id}?kind=service-restart`}
                className={`${controlButtonClass} inline-flex justify-center no-underline`}
              >
                Open operation
              </Link>
            </div>
          ) : (
            <EmptyState>No restart operations yet.</EmptyState>
          )}
        </Panel>
      </div>

      <Panel title="Quick actions">
        <div className="flex flex-wrap gap-3">
          <Link to="/admin/landing" className={`${controlButtonClass} inline-flex justify-center no-underline`}>
            Open landing CMS
          </Link>
          <Link to="/admin/panels" className={`${controlButtonClass} inline-flex justify-center no-underline`}>
            Open panels catalog
          </Link>
          <Link to="/admin/ops" className={`${controlButtonClass} inline-flex justify-center no-underline`}>
            Open ops overview
          </Link>
        </div>
      </Panel>
    </div>
  );
};

export default AdminOverview;
