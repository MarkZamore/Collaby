const GOOGLE_IDENTITY_SCRIPT_URL = 'https://accounts.google.com/gsi/client';

let googleIdentityScriptPromise = null;

export const loadGoogleIdentityScript = () => {
  if (typeof window === 'undefined') {
    return Promise.reject(new Error('Google Identity Services are unavailable on the server'));
  }

  if (window.google?.accounts?.id) {
    return Promise.resolve(window.google);
  }

  if (!googleIdentityScriptPromise) {
    googleIdentityScriptPromise = new Promise((resolve, reject) => {
      const existingScript = document.querySelector(`script[src="${GOOGLE_IDENTITY_SCRIPT_URL}"]`);
      const script = existingScript || document.createElement('script');

      const handleLoad = () => {
        if (window.google?.accounts?.id) {
          resolve(window.google);
          return;
        }

        googleIdentityScriptPromise = null;
        reject(new Error('Google Identity Services did not initialize correctly'));
      };

      const handleError = () => {
        googleIdentityScriptPromise = null;
        reject(new Error('Failed to load Google Identity Services'));
      };

      script.addEventListener('load', handleLoad, { once: true });
      script.addEventListener('error', handleError, { once: true });

      if (!existingScript) {
        script.src = GOOGLE_IDENTITY_SCRIPT_URL;
        script.async = true;
        script.defer = true;
        document.head.appendChild(script);
      }
    });
  }

  return googleIdentityScriptPromise;
};
