import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { createRestartOperation, getAdminOpsOverview, getRestartOperations } from '../utils/adminApi';
import {
  EmptyState,
  PageHeader,
  Panel,
  StatusBadge,
  controlButtonClass,
  destructiveButtonClass,
  linkClass,
  mutedTextClass,
  pageContainerClass,
  tableClass,
  tableWrapClass,
} from '../components/AdminPrimitives';
import { useAuth } from '@shared/context/AuthContext';

export const getProbeStatusValue = (probe) => {
  if (!probe) {
    return 'not_probed';
  }

  return probe.isHealthy ? 'succeeded' : 'failed';
};

export const getHealthText = (probe) => {
  if (!probe) {
    return 'not probed';
  }

  if (probe.error) {
    return probe.error;
  }

  return probe.statusCode ? `HTTP ${probe.statusCode}` : probe.isHealthy ? 'Healthy' : 'Unavailable';
};

const AdminOps = () => {
  const { accessToken } = useAuth();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [overview, setOverview] = useState({ services: [], panelLinks: [] });
  const [operations, setOperations] = useState([]);
  const [busyTarget, setBusyTarget] = useState(null);

  const load = async (background = false) => {
    if (!background) setLoading(true);
    setError(null);
    try {
      const [overviewPayload, operationsPayload] = await Promise.all([
        getAdminOpsOverview(accessToken),
        getRestartOperations(accessToken),
      ]);
      setOverview(overviewPayload || { services: [], panelLinks: [] });
      setOperations(operationsPayload?.items || []);
    } catch (nextError) {
      setError(nextError.message || 'Failed to load admin ops.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, [accessToken]);

  const hasPendingRestarts = operations.some((operation) => ['queued', 'triggered', 'running'].includes(operation.status));
  useEffect(() => {
    if (!hasPendingRestarts) return undefined;
    const timer = window.setInterval(() => load(true), 3000);
    return () => window.clearInterval(timer);
  }, [hasPendingRestarts, accessToken]);

  const restartService = async (targetService) => {
    if (!window.confirm(`Restart ${targetService} through Kubernetes API?`)) {
      return;
    }

    setBusyTarget(targetService);
    try {
      const created = await createRestartOperation(targetService, accessToken);
      setOperations((current) => [created, ...current.filter((operation) => operation.id !== created.id)]);
      await load(true);
    } catch (nextError) {
      window.alert(nextError.message || 'Failed to trigger restart.');
    } finally {
      setBusyTarget(null);
    }
  };

  return (
    <div className={pageContainerClass}>
      <PageHeader
        title="Ops"
        subtitle="Catalog-backed health summary, panel links and restart actions that go only through the Kubernetes API."
        actions={<button type="button" className={controlButtonClass} onClick={() => load()}>Refresh</button>}
      />

      <Panel title="Panel links">
        {overview.panelLinks?.length ? (
          <div className="flex flex-wrap gap-3">
            {overview.panelLinks.map((link) => (
              <a
                key={link.key}
                href={link.url}
                target="_blank"
                rel="noreferrer"
                className={`${controlButtonClass} no-underline`}
              >
                {link.key}
              </a>
            ))}
          </div>
        ) : (
          <EmptyState>No local panel links are configured.</EmptyState>
        )}
      </Panel>

      <Panel title="Runtime services">
        {loading ? (
          <div>Loading ops overview...</div>
        ) : error ? (
          <EmptyState>{error}</EmptyState>
        ) : (
          <div className={tableWrapClass}>
            <table className={tableClass}>
              <thead>
                <tr>
                  <th>Service</th>
                  <th>Kind</th>
                  <th>Direct</th>
                  <th>Gateway</th>
                  <th>State units</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {overview.services?.map((service) => (
                  <tr key={service.composeService}>
                    <td>
                      <div className="font-extrabold">{service.composeService}</div>
                    </td>
                    <td><StatusBadge value={service.kind} /></td>
                    <td>
                      <StatusBadge value={getProbeStatusValue(service.directHealth)} />
                      <div className={`mt-1.5 text-xs ${mutedTextClass}`}>{getHealthText(service.directHealth)}</div>
                    </td>
                    <td>
                      <StatusBadge value={getProbeStatusValue(service.gatewayHealth)} />
                      <div className={`mt-1.5 text-xs ${mutedTextClass}`}>{getHealthText(service.gatewayHealth)}</div>
                    </td>
                    <td>
                      {service.ownedStateUnits?.length ? service.ownedStateUnits.join(', ') : 'none'}
                    </td>
                    <td>
                      {service.restartEligible ? (
                        <button
                          type="button"
                          className={destructiveButtonClass}
                          disabled={busyTarget === service.composeService || operations.some((operation) => operation.targetService === service.composeService && ['queued', 'triggered', 'running'].includes(operation.status))}
                          onClick={() => restartService(service.composeService)}
                        >
                          {busyTarget === service.composeService ? 'Starting...' : 'Restart'}
                        </button>
                      ) : (
                        <span style={{ fontSize: 13, opacity: 0.62 }}>Not eligible</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>

      <Panel title="Restart history">
        {operations.length === 0 ? (
          <EmptyState>No restart operations yet.</EmptyState>
        ) : (
          <div className={tableWrapClass}>
            <table className={tableClass}>
              <thead>
                <tr>
                  <th>Target</th>
                  <th>Status</th>
                  <th>Deployment</th>
                  <th>Requested</th>
                </tr>
              </thead>
              <tbody>
                {operations.map((operation) => (
                  <tr key={operation.id}>
                    <td>
                      <Link to={`/admin/operations/${operation.id}?kind=service-restart`} className={linkClass}>
                        {operation.targetService}
                      </Link>
                    </td>
                    <td><StatusBadge value={operation.status} /></td>
                    <td>
                      {operation.deploymentNamespace ? `${operation.deploymentNamespace}/${operation.targetService}: generation ${operation.deploymentGeneration ?? 'pending'}` : 'queued'}
                    </td>
                    <td>{new Date(operation.requestedAtUtc).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>
    </div>
  );
};

export default AdminOps;
