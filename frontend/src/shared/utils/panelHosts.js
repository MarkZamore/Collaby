export const isDedicatedAdminHost = () => {
  if (typeof window === 'undefined') {
    return false;
  }

  const { hostname } = window.location;
  return hostname === 'admin.localhost' || hostname.startsWith('admin.');
};
