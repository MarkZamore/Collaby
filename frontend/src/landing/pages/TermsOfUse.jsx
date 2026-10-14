import React from 'react';
import LegalPage from '../components/LegalPage';
import '../styles/TermsOfUse.css';

const TermsOfUse = () => (
  <LegalPage
    pageKey="terms"
    className="terms-page"
    containerClassName="terms-container"
    headerClassName="terms-header"
    titleClassName="terms-title"
    dateClassName="terms-date"
    contentClassName="terms-content"
    sectionClassName="terms-section"
    contactClassName="terms-contact"
  />
);

export default TermsOfUse;
