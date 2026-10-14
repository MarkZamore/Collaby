import React from 'react';
import LegalPage from '../components/LegalPage';
import '../styles/CookiePolicy.css';

const CookiePolicy = () => (
  <LegalPage
    pageKey="cookies"
    className="cookie-page"
    containerClassName="cookie-container"
    headerClassName="cookie-header"
    titleClassName="cookie-title"
    dateClassName="cookie-date"
    contentClassName="cookie-content"
    sectionClassName="cookie-section"
    contactClassName="cookie-contact"
  />
);

export default CookiePolicy;
