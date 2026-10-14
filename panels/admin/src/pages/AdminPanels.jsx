import React, { useEffect, useState } from 'react';
import { ExternalLink } from 'lucide-react';
import { getAdminOpsOverview } from '../utils/adminApi';
import {
  EmptyState,
  PageHeader,
  Panel,
  controlButtonClass,
  mutedTextClass,
  pageContainerClass,
  splitGridClass,
} from '../components/AdminPrimitives';
import { useAuth } from '@shared/context/AuthContext';

const AdminPanels = () => {
  const { accessToken } = useAuth();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [panelLinks, setPanelLinks] = useState([]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const overview = await getAdminOpsOverview(accessToken);
      setPanelLinks(Array.isArray(overview?.panelLinks) ? overview.panelLinks : []);
    } catch (nextError) {
      setError(nextError.message || 'Failed to load panel links.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, [accessToken]);

  return (
    <div className={pageContainerClass}>
      <PageHeader
        title="Panels"
        subtitle="Dedicated catalog of runtime panel links. This page reuses the same backend panel-link roster that already powers Admin Ops."
        actions={<button type="button" className={controlButtonClass} onClick={load}>Refresh</button>}
      />

      <Panel title="Runtime panel links">
        {loading ? (
          <div>Loading panel links...</div>
        ) : error ? (
          <EmptyState>{error}</EmptyState>
        ) : panelLinks.length === 0 ? (
          <EmptyState>`CF_ZONE_NAME` is not configured, so public panel links are unavailable.</EmptyState>
        ) : (
          <div className={splitGridClass}>
            {panelLinks.map((link) => (
              <article key={link.key} className="app-panel">
                <div className="app-panel-header">
                  <h2 className="app-panel-title">{link.key}</h2>
                </div>
                <p className={mutedTextClass}>{link.url}</p>
                <div className="mt-4">
                  <a
                    href={link.url}
                    target="_blank"
                    rel="noreferrer"
                    className={`${controlButtonClass} inline-flex items-center gap-2 no-underline`}
                  >
                    <span>Open panel</span>
                    <ExternalLink size={16} />
                  </a>
                </div>
              </article>
            ))}
          </div>
        )}
      </Panel>
    </div>
  );
};

export default AdminPanels;
