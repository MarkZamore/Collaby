export const CLOUDFLARE_ACCESS_LOGOUT_PATH = '/cdn-cgi/access/logout';

export const getCloudflareAccessReturnPath = (locationObject = typeof window === 'undefined' ? null : window.location) => {
  if (!locationObject) {
    return '/';
  }

  return `${locationObject.pathname}${locationObject.search}${locationObject.hash}` || '/';
};

const normalizeCloudflareAccessReturnPath = (returnPath) => {
  if (typeof returnPath !== 'string' || returnPath.length === 0) {
    return '/';
  }

  if (!returnPath.startsWith('/') || returnPath.startsWith('//')) {
    return '/';
  }

  return returnPath;
};

export const redirectToCloudflareAccessLogin = (
  returnPath = getCloudflareAccessReturnPath(),
  locationObject = typeof window === 'undefined' ? null : window.location,
) => {
  if (!locationObject) {
    return;
  }

  const normalizedReturnPath = normalizeCloudflareAccessReturnPath(returnPath);
  locationObject.replace(normalizedReturnPath);
};

export const redirectToCloudflareAccessLogout = (locationObject = typeof window === 'undefined' ? null : window.location) => {
  if (!locationObject) {
    return;
  }

  locationObject.replace(CLOUDFLARE_ACCESS_LOGOUT_PATH);
};