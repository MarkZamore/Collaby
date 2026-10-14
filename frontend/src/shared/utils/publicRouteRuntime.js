const PUBLIC_STYLESHEET_RE =
  /\/assets\/(?:Landing|Auth|About|Contact|PrivacyPolicy|TermsOfUse|CookiePolicy|VerifyEmail(?:Sent)?|ForgotPassword|ResetPassword|Header|Footer|Navbar|AnimatedCounter|ScrollReveal|ImagePlaceholder|GlassCard|AnimatedBackground|usePublicPageState)-[\w-]+\.css(?:\?.*)?$/i;

const setPublicClassState = (isPublicRoute) => {
  if (typeof document === 'undefined') {
    return;
  }

  const { documentElement, body } = document;
  documentElement.classList.toggle('public-page', isPublicRoute);
  body?.classList.toggle('public-page', isPublicRoute);
};

const setPublicStylesheetState = (isPublicRoute) => {
  if (typeof document === 'undefined') {
    return;
  }

  document.querySelectorAll('link[rel="stylesheet"][href]').forEach((link) => {
    const href = link.getAttribute('href') || '';
    if (!PUBLIC_STYLESHEET_RE.test(href)) {
      return;
    }

    link.dataset.publicRouteStylesheet = 'true';
    link.disabled = !isPublicRoute;
    link.media = isPublicRoute ? 'all' : 'not all';
  });
};

export const syncPublicRouteRuntime = (isPublicRoute) => {
  setPublicClassState(isPublicRoute);
  setPublicStylesheetState(isPublicRoute);
};

export const teardownPublicRouteRuntime = () => {
  syncPublicRouteRuntime(false);
};

