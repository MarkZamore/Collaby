import React, { useEffect } from 'react';

import AppLoadingScreen from './AppLoadingScreen';
import { redirectToCloudflareAccessLogin } from '../utils/cloudflareAccess';

const CloudflareAccessRedirect = ({ label = 'Redirecting to Cloudflare Access...' }) => {
  useEffect(() => {
    redirectToCloudflareAccessLogin();
  }, []);

  return <AppLoadingScreen label={label} />;
};

export default CloudflareAccessRedirect;
