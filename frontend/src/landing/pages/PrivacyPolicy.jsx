import React from 'react';
import LegalPage from '../components/LegalPage';
import '../styles/PrivacyPolicy.css';

const PrivacyPolicy = () => (
  <LegalPage
    pageKey="privacy"
    className="privacy-page"
    containerClassName="privacy-container"
    headerClassName="privacy-header"
    titleClassName="privacy-title"
    dateClassName="privacy-date"
    contentClassName="privacy-content"
    sectionClassName="privacy-section"
    contactClassName="privacy-contact"
  />
);

export default PrivacyPolicy;
