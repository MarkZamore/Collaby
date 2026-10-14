import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  deactivateAdminUser,
  getAdminUser,
  getAdminUsers,
  hardDeleteAdminUser,
  reactivateAdminUser,
  retryUserDeletionOperation,
} from '../utils/adminApi';
import {
  EmptyState,
  PageHeader,
  Panel,
  StatusBadge,
  actionsClass,
  badgesClass,
  controlButtonClass,
  destructiveButtonClass,
  detailHeaderClass,
  inputClass,
  mutedTextClass,
  pageContainerClass,
  tableClass,
  tableRowClass,
  tableWrapClass,
} from '../components/AdminPrimitives';
import { useAuth } from '@shared/context/AuthContext';

const AdminUsers = () => {
  const { accessToken } = useAuth();
  const [query, setQuery] = useState('');
  const [isActive, setIsActive] = useState('');
  const [isSystemAdmin, setIsSystemAdmin] = useState('');
  const [state, setState] = useState({ loading: true, error: null, items: [], totalCount: 0 });
  const [selectedId, setSelectedId] = useState(null);
  const [selectedUser, setSelectedUser] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [busyAction, setBusyAction] = useState(null);

  const clearSelection = () => {
    setSelectedId(null);
    setSelectedUser(null);
    setDetailLoading(false);
  };

  const loadList = async () => {
    setState((prev) => ({ ...prev, loading: true, error: null }));
    try {
      const payload = await getAdminUsers(
        {
          q: query,
          isActive: isActive === '' ? undefined : isActive === 'true',
          isSystemAdmin: isSystemAdmin === '' ? undefined : isSystemAdmin === 'true',
          page: 1,
          pageSize: 50,
        },
        accessToken,
      );
      setState({
        loading: false,
        error: null,
        items: payload?.items || [],
        totalCount: payload?.totalCount || 0,
      });
      return payload || { items: [], totalCount: 0 };
    } catch (error) {
      setState({ loading: false, error: error.message || 'Failed to load users.', items: [], totalCount: 0 });
      return null;
    }
  };

  const loadDetail = async (userId) => {
    setSelectedId(userId);
    setDetailLoading(true);
    try {
      const payload = await getAdminUser(userId, accessToken);
      setSelectedUser(payload);
    } catch (error) {
      setSelectedUser({ error: error.message || 'Failed to load user details.' });
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    loadList();
  }, [accessToken]);

  useEffect(() => {
    if (state.loading || !selectedId) {
      return;
    }

    const selectedStillExists = (state.items || []).some((user) => user.id === selectedId);
    if (!selectedStillExists) {
      clearSelection();
    }
  }, [selectedId, state.items, state.loading]);

  const refreshSelected = async () => {
    const payload = await loadList();
    if (!selectedId || !payload) {
      return;
    }

    const selectedStillExists = (payload.items || []).some((user) => user.id === selectedId);
    if (!selectedStillExists) {
      clearSelection();
      return;
    }

    await loadDetail(selectedId);
  };

  const runAction = async (actionKey, action) => {
    setBusyAction(actionKey);
    try {
      await action();
      await refreshSelected();
    } catch (error) {
      window.alert(error.message || 'Action failed.');
    } finally {
      setBusyAction(null);
    }
  };

  return (
    <div className={pageContainerClass}>
      <PageHeader
        title="Users"
        subtitle="Global user governance for system admins only: search, inspect, deactivate, reactivate and orchestrate hard delete."
        actions={<button type="button" className={controlButtonClass} onClick={refreshSelected}>Refresh</button>}
      />

      <Panel title={`Directory • ${state.totalCount}`}>
        <div className="mb-4 flex flex-wrap gap-3">
          <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search by email or username" className={`${inputClass} min-w-[260px]`} />
          <select value={isActive} onChange={(event) => setIsActive(event.target.value)} className={inputClass}>
            <option value="">All activity states</option>
            <option value="true">Active</option>
            <option value="false">Inactive</option>
          </select>
          <select value={isSystemAdmin} onChange={(event) => setIsSystemAdmin(event.target.value)} className={inputClass}>
            <option value="">All admin states</option>
            <option value="true">System admins</option>
            <option value="false">Non-admins</option>
          </select>
          <button type="button" className={controlButtonClass} onClick={refreshSelected}>Apply</button>
        </div>

        {state.loading ? (
          <div>Loading users...</div>
        ) : state.error ? (
          <EmptyState>{state.error}</EmptyState>
        ) : (
          <div className={tableWrapClass}>
            <table className={tableClass}>
              <thead>
                <tr>
                  <th>User</th>
                  <th>State</th>
                  <th>Admin</th>
                  <th>Deletion</th>
                </tr>
              </thead>
              <tbody>
                {state.items.map((user) => (
                  <tr key={user.id} onClick={() => loadDetail(user.id)} className={tableRowClass({ interactive: true })}>
                    <td>
                      <div className="font-extrabold">{user.username || 'Unnamed user'}</div>
                      <div className={mutedTextClass}>{user.email}</div>
                    </td>
                    <td><StatusBadge value={user.isActive ? 'active' : 'inactive'} /></td>
                    <td><StatusBadge value={user.isSystemAdmin ? 'system_admin' : 'workspace_only'} /></td>
                    <td>{user.deletionStatus ? <StatusBadge value={user.deletionStatus} /> : 'none'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>

      <Panel title="Details">
        {!selectedId ? (
          <EmptyState>Select a user to inspect global state and hard-delete steps.</EmptyState>
        ) : detailLoading ? (
          <div>Loading details...</div>
        ) : selectedUser?.error ? (
          <EmptyState>{selectedUser.error}</EmptyState>
        ) : selectedUser ? (
          (() => {
            const deletionStatus = selectedUser.deletionOperation?.status || null;
            const canRetryDeletion = Boolean(selectedUser.deletionOperation?.canRetry);
            const hasInFlightDeletion = ['pending', 'running', 'retry_requested'].includes(deletionStatus || '');
            const retryLabel = deletionStatus === 'failed' ? 'Retry failed steps' : 'Resume deletion';
            const hardDeleteDisabled =
              busyAction === 'hard-delete' ||
              canRetryDeletion ||
              selectedUser.isProtectedAccount ||
              (hasInFlightDeletion && !canRetryDeletion);

            return (
          <div className="grid gap-4">
            <div className={detailHeaderClass}>
              <div>
                <div className="text-[28px] font-black">{selectedUser.username || 'Unnamed user'}</div>
                <div className={`mt-1.5 ${mutedTextClass}`}>{selectedUser.email}</div>
              </div>
              <div className={actionsClass}>
                {canRetryDeletion ? (
                  <button
                    type="button"
                    className={controlButtonClass}
                    disabled={busyAction === 'retry'}
                    onClick={() => runAction('retry', () => retryUserDeletionOperation(selectedUser.deletionOperation.id, accessToken))}
                  >
                    {retryLabel}
                  </button>
                ) : selectedUser.isActive ? (
                  <button
                    type="button"
                    className={controlButtonClass}
                    disabled={busyAction === 'deactivate' || selectedUser.isProtectedAccount}
                    onClick={() => runAction('deactivate', () => deactivateAdminUser(selectedUser.id, accessToken))}
                  >
                    Deactivate
                  </button>
                ) : (
                  <button
                    type="button"
                    className={controlButtonClass}
                    disabled={busyAction === 'reactivate' || hasInFlightDeletion}
                    onClick={() => runAction('reactivate', () => reactivateAdminUser(selectedUser.id, accessToken))}
                  >
                    Reactivate
                  </button>
                )}
                <button
                  type="button"
                  className={destructiveButtonClass}
                  disabled={hardDeleteDisabled}
                  onClick={() => {
                    if (!window.confirm(`Start hard delete for ${selectedUser.email}?`)) {
                      return;
                    }
                    runAction('hard-delete', () => hardDeleteAdminUser(selectedUser.id, accessToken));
                  }}
                >
                  Hard delete
                </button>
              </div>
            </div>

            <div className={badgesClass}>
              <StatusBadge value={selectedUser.isActive ? 'active' : 'inactive'} />
              <StatusBadge value={selectedUser.isSystemAdmin ? 'system_admin' : 'workspace_only'} />
              {selectedUser.isProtectedAccount ? <StatusBadge value="protected_account" /> : null}
            </div>

            {selectedUser.deletionOperation ? (
              <div className="grid gap-3">
                <div className={detailHeaderClass}>
                  <div className="text-lg font-extrabold">Deletion operation</div>
                  <div className={actionsClass}>
                    <Link to={`/admin/operations/${selectedUser.deletionOperation.id}?kind=user-deletion`} className={`${controlButtonClass} no-underline`}>
                      Open operation
                    </Link>
                    {selectedUser.deletionOperation.canRetry ? (
                      <button
                        type="button"
                        className={controlButtonClass}
                        disabled={busyAction === 'retry'}
                        onClick={() => runAction('retry', () => retryUserDeletionOperation(selectedUser.deletionOperation.id, accessToken))}
                      >
                        {retryLabel}
                      </button>
                    ) : null}
                  </div>
                </div>
                <StatusBadge value={selectedUser.deletionOperation.status} />
                <div className={tableWrapClass}>
                  <table className={tableClass}>
                    <thead>
                      <tr>
                        <th>Service</th>
                        <th>Status</th>
                        <th>Attempts</th>
                        <th>Error</th>
                      </tr>
                    </thead>
                    <tbody>
                      {selectedUser.deletionOperation.steps.map((step) => (
                        <tr key={step.serviceName}>
                          <td>{step.serviceName}</td>
                          <td><StatusBadge value={step.status} /></td>
                          <td>{step.attemptCount}</td>
                          <td>{step.lastError || 'none'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            ) : (
              <EmptyState>No hard-delete operation for this user.</EmptyState>
            )}
          </div>
            );
          })()
        ) : null}
      </Panel>
    </div>
  );
};

export default AdminUsers;
