import React, { useEffect, useState } from 'react';
import { useSearchParams, useParams } from 'react-router-dom';
import { getRestartOperation, getUserDeletionOperation, retryUserDeletionOperation } from '../utils/adminApi';
import {
  EmptyState,
  PageHeader,
  Panel,
  StatusBadge,
  controlButtonClass,
  mutedTextClass,
  pageContainerClass,
  tableClass,
  tableWrapClass,
} from '../components/AdminPrimitives';
import { useAuth } from '@shared/context/AuthContext';

const AdminOperationDetail = () => {
  const { id } = useParams();
  const [searchParams] = useSearchParams();
  const { accessToken } = useAuth();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [operation, setOperation] = useState(null);
  const [retrying, setRetrying] = useState(false);
  const kind = searchParams.get('kind');

  const load = async (background = false) => {
    if (!id || !kind) {
      setLoading(false);
      setError('Operation kind is required.');
      return;
    }

    if (!background) setLoading(true);
    setError(null);
    try {
      if (kind === 'user-deletion') {
        setOperation(await getUserDeletionOperation(id, accessToken));
      } else if (kind === 'service-restart') {
        setOperation(await getRestartOperation(id, accessToken));
      } else {
        throw new Error(`Unknown operation kind: ${kind}`);
      }
    } catch (nextError) {
      setError(nextError.message || 'Failed to load operation.');
      if (!background) setOperation(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, [id, kind, accessToken]);

  const pending = kind === 'service-restart' && operation && ['queued', 'triggered', 'running'].includes(operation.status);
  useEffect(() => {
    if (!pending) return undefined;
    const timer = window.setInterval(() => load(true), 3000);
    return () => window.clearInterval(timer);
  }, [pending, id, kind, accessToken]);

  const retry = async () => {
    setRetrying(true);
    try {
      await retryUserDeletionOperation(id, accessToken);
      await load();
    } catch (nextError) {
      window.alert(nextError.message || 'Retry failed.');
    } finally {
      setRetrying(false);
    }
  };

  return (
    <div className={pageContainerClass}>
      <PageHeader
        title="Operation detail"
        subtitle={`Kind: ${kind || 'unknown'} • deep-link friendly admin trace page.`}
        actions={<button type="button" className={controlButtonClass} onClick={() => load()}>Refresh</button>}
      />

      <Panel title="Operation">
        {loading ? (
          <div>Loading operation...</div>
        ) : error ? (
          <EmptyState>{error}</EmptyState>
        ) : !operation ? (
          <EmptyState>No operation found.</EmptyState>
        ) : (
          <div className="grid gap-4">
            <div className="flex flex-wrap items-center gap-2.5">
              <StatusBadge value={operation.status} />
              {'targetService' in operation ? <span>{operation.targetService}</span> : <span>{operation.userEmail}</span>}
            </div>

            {'steps' in operation ? (
              <>
                {operation.canRetry ? (
                  <div>
                    <button type="button" className={controlButtonClass} disabled={retrying} onClick={retry}>
                      {retrying
                        ? 'Retrying...'
                        : operation.status === 'failed'
                          ? 'Retry failed steps'
                          : 'Resume deletion'}
                    </button>
                  </div>
                ) : null}
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
                      {operation.steps.map((step) => (
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
              </>
            ) : (
              <div className={`grid gap-2 ${mutedTextClass}`}>
                <div>Deployment: {operation.deploymentNamespace || 'pending'}/{operation.targetService}</div>
                <div>Generation: {operation.deploymentGeneration ?? 'pending'}</div>
                <div>UID: {operation.deploymentUid || 'pending'}</div>
                {operation.failureMessage ? <div role="alert">{operation.failureMessage}</div> : null}
                <div>Rollout status: {operation.rolloutStatus || 'unknown'}</div>
                <div>Owned state units: {(operation.ownedStateUnits || []).join(', ') || 'none'}</div>
              </div>
            )}
          </div>
        )}
      </Panel>
    </div>
  );
};

export default AdminOperationDetail;
