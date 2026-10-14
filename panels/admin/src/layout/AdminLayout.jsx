import React from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { Activity, FileText, LayoutDashboard, Monitor, Shield, Users, Warehouse } from 'lucide-react';
import { useAuth } from '@shared/context/AuthContext';

const navItems = [
  { to: '/admin', label: 'Overview', icon: LayoutDashboard, end: true },
  { to: '/admin/users', label: 'Users', icon: Users },
  { to: '/admin/workspaces', label: 'Workspaces', icon: Warehouse },
  { to: '/admin/landing', label: 'Landing', icon: FileText },
  { to: '/admin/panels', label: 'Panels', icon: Monitor },
  { to: '/admin/ops', label: 'Ops', icon: Activity },
];

const AdminLayout = () => {
  const navigate = useNavigate();
  const { currentUser } = useAuth();

  return (
    <div className="app-admin-shell">
      <div className="app-admin-frame">
        <aside className="app-admin-rail">
          <div className="app-admin-brand">
            <div className="app-admin-brand-badge">
              <Shield size={20} />
            </div>
            <div>
              <div className="app-admin-brand-kicker">Collaby</div>
              <div className="app-admin-brand-title">Admin Panel</div>
            </div>
          </div>

          <div className="app-admin-identity">
            <div className="app-admin-meta">Signed in</div>
            <div className="app-admin-meta-value">
              {currentUser?.username || currentUser?.email || 'System admin'}
            </div>
            <div className="app-admin-meta-copy">
              {currentUser?.email || 'admin@collaby.local'}
            </div>
          </div>

          <nav className="app-admin-nav">
            {navItems.map((item) => {
              const Icon = item.icon;
              return (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.end}
                  className="app-admin-nav-link"
                >
                  <Icon size={18} />
                  <span>{item.label}</span>
                </NavLink>
              );
            })}
          </nav>

          <div className="mt-6 grid gap-2.5">
            <button
              type="button"
              onClick={() => navigate('/dashboard')}
              className="app-control-button"
            >
              Back to workspace
            </button>
          </div>
        </aside>

        <main className="app-admin-main">
          <Outlet />
        </main>
      </div>
    </div>
  );
};

export default AdminLayout;
