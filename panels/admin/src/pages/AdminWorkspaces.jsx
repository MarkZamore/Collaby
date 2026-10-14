import React, { useEffect, useState } from 'react';
import {
  archiveAdminWorkspace,
  forceDeleteAdminWorkspace,
  getAdminWorkspace,
  getAdminWorkspaces,
  unarchiveAdminWorkspace,
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

const AdminWorkspaces = () => {
  const { accessToken } = useAuth();
  const [query, setQuery] = useState('');
  const [isArchived, setIsArchived] = useState('');
  const [state, setState] = useState({ loading: true, error: null, items: [], totalCount: 0 });
  const [selectedId, setSelectedId] = useState(null);
  const [selectedWorkspace, setSelectedWorkspace] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [busyAction, setBusyAction] = useState(null);

  const loadList = async () => {
    setState((prev) => ({ ...prev, loading: true, error: null }));
    try {
      const payload = await getAdminWorkspaces(
        {
          q: query,
          isArchived: isArchived === '' ? undefined : isArchived === 'true',
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
    } catch (error) {
      setState({ loading: false, error: error.message || 'Failed to load workspaces.', items: [], totalCount: 0 });
    }
  };

  const loadDetail = async (workspaceId) => {
    setSelectedId(workspaceId);
    setDetailLoading(true);
    try {
      setSelectedWorkspace(await getAdminWorkspace(workspaceId, accessToken));
    } catch (error) {
      setSelectedWorkspace({ error: error.message || 'Failed to load workspace details.' });
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    loadList();
  }, [accessToken]);

  const refreshSelected = async () => {
    if (selectedId) {
      await loadDetail(selectedId);
    }
    await loadList();
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
        title="Workspaces"
        subtitle="Global workspace governance with archive, unarchive and force delete outside обычных membership/owner ограничений."
        actions={<button type="button" className={controlButtonClass} onClick={loadList}>Refresh</button>}
      />

      <Panel title={`Global workspace list • ${state.totalCount}`}>
        <div className="mb-4 flex flex-wrap gap-3">
          <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search workspace name" className={`${inputClass} min-w-[260px]`} />
          <select value={isArchived} onChange={(event) => setIsArchived(event.target.value)} className={inputClass}>
            <option value="">All archive states</option>
            <option value="true">Archived</option>
            <option value="false">Active</option>
          </select>
          <button type="button" className={controlButtonClass} onClick={loadList}>Apply</button>
        </div>

        {state.loading ? (
          <div>Loading workspaces...</div>
        ) : state.error ? (
          <EmptyState>{state.error}</EmptyState>
        ) : (
          <div className={tableWrapClass}>
            <table className={tableClass}>
              <thead>
                <tr>
                  <th>Workspace</th>
                  <th>Owner</th>
                  <th>Members</th>
                  <th>Projects</th>
                  <th>Tasks</th>
                </tr>
              </thead>
              <tbody>
                {state.items.map((workspace) => (
                  <tr key={workspace.id} onClick={() => loadDetail(workspace.id)} className={tableRowClass({ interactive: true })}>
                    <td>
                      <div className="font-extrabold">{workspace.name}</div>
                      <div className="mt-1.5 flex gap-2.5">
                        <StatusBadge value={workspace.isArchived ? 'archived' : 'active'} />
                      </div>
                    </td>
                    <td>{workspace.owner?.displayName || workspace.owner?.username || 'Unknown owner'}</td>
                    <td>{workspace.memberCount}</td>
                    <td>{workspace.projectCount}</td>
                    <td>{workspace.taskCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>

      <Panel title="Details">
        {!selectedId ? (
          <EmptyState>Select a workspace to inspect owner/member summary and destructive actions.</EmptyState>
        ) : detailLoading ? (
          <div>Loading details...</div>
        ) : selectedWorkspace?.error ? (
          <EmptyState>{selectedWorkspace.error}</EmptyState>
        ) : selectedWorkspace ? (
          <div className="grid gap-4">
            <div className={detailHeaderClass}>
              <div>
                <div className="text-[28px] font-black">{selectedWorkspace.name}</div>
                <div className={`mt-1.5 ${mutedTextClass}`}>{selectedWorkspace.description || 'No description'}</div>
              </div>
              <div className={actionsClass}>
                {selectedWorkspace.isArchived ? (
                  <button
                    type="button"
                    className={controlButtonClass}
                    disabled={busyAction === 'unarchive'}
                    onClick={() => runAction('unarchive', () => unarchiveAdminWorkspace(selectedWorkspace.id, accessToken))}
                  >
                    Unarchive
                  </button>
                ) : (
                  <button
                    type="button"
                    className={controlButtonClass}
                    disabled={busyAction === 'archive'}
                    onClick={() => runAction('archive', () => archiveAdminWorkspace(selectedWorkspace.id, accessToken))}
                  >
                    Archive
                  </button>
                )}
                <button
                  type="button"
                  className={destructiveButtonClass}
                  disabled={busyAction === 'force-delete'}
                  onClick={() => {
                    if (!window.confirm(`Force delete workspace "${selectedWorkspace.name}"?`)) {
                      return;
                    }
                    runAction('force-delete', () => forceDeleteAdminWorkspace(selectedWorkspace.id, accessToken));
                  }}
                >
                  Force delete
                </button>
              </div>
            </div>

            <div className={badgesClass}>
              <StatusBadge value={selectedWorkspace.isArchived ? 'archived' : 'active'} />
              <span className={mutedTextClass}>Owner: {selectedWorkspace.owner?.displayName || selectedWorkspace.owner?.username || 'Unknown owner'}</span>
            </div>

            <div className={tableWrapClass}>
              <table className={tableClass}>
                <thead>
                  <tr>
                    <th>Member</th>
                    <th>Role</th>
                    <th>Status</th>
                    <th>Joined</th>
                  </tr>
                </thead>
                <tbody>
                  {selectedWorkspace.members.map((member) => (
                    <tr key={`${member.userId}-${member.joinedAt}`}>
                      <td>{member.user?.displayName || member.user?.username || member.userId}</td>
                      <td>{member.role}</td>
                      <td><StatusBadge value={member.status} /></td>
                      <td>{new Date(member.joinedAt).toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        ) : null}
      </Panel>
    </div>
  );
};

export default AdminWorkspaces;
