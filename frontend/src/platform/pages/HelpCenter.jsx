import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Book, ChevronDown, Mail, MessageCircle, Search, Shield, Zap } from 'lucide-react';
import '../styles/HelpCenter.css';

const HelpCenter = () => {
  const { t } = useTranslation('platform');
  const [searchQuery, setSearchQuery] = useState('');
  const [activeFaq, setActiveFaq] = useState(null);

  const categories = useMemo(
    () => [
      {
        icon: <Book size={24} />,
        title: t('helpCenter.categories.gettingStarted.title'),
        description: t('helpCenter.categories.gettingStarted.description'),
      },
      {
        icon: <Zap size={24} />,
        title: t('helpCenter.categories.tasks.title'),
        description: t('helpCenter.categories.tasks.description'),
      },
      {
        icon: <Shield size={24} />,
        title: t('helpCenter.categories.security.title'),
        description: t('helpCenter.categories.security.description'),
      },
      {
        icon: <MessageCircle size={24} />,
        title: t('helpCenter.categories.chat.title'),
        description: t('helpCenter.categories.chat.description'),
      },
    ],
    [t],
  );

  const faqs = useMemo(
    () => [
      {
        question: t('helpCenter.faqs.createProject.question'),
        answer: t('helpCenter.faqs.createProject.answer'),
      },
      {
        question: t('helpCenter.faqs.inviteMembers.question'),
        answer: t('helpCenter.faqs.inviteMembers.answer'),
      },
      {
        question: t('helpCenter.faqs.changeTheme.question'),
        answer: t('helpCenter.faqs.changeTheme.answer'),
      },
      {
        question: t('helpCenter.faqs.coins.question'),
        answer: t('helpCenter.faqs.coins.answer'),
      },
    ],
    [t],
  );

  const filteredFaqs = faqs.filter(({ question, answer }) => {
    const query = searchQuery.trim().toLowerCase();
    if (!query) {
      return true;
    }

    return question.toLowerCase().includes(query) || answer.toLowerCase().includes(query);
  });

  return (
    <div className="help-center-container">
      <div className="flex items-start justify-between gap-4 flex-wrap mb-6">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2" style={{ color: 'var(--app-text-main)' }}>
            <Book size={22} style={{ color: 'var(--app-accent)' }} />
            {t('helpCenter.title')}
          </h1>
          <p className="text-sm mt-1" style={{ color: 'var(--app-text-secondary)' }}>
            {t('helpCenter.subtitle')}
          </p>
        </div>
      </div>

      <div className="help-search-container">
        <Search className="help-search-icon" size={20} />
        <input
          type="text"
          className="help-search-input"
          placeholder={t('helpCenter.searchPlaceholder')}
          value={searchQuery}
          onChange={(event) => setSearchQuery(event.target.value)}
        />
      </div>

      <div className="help-grid">
        {categories.map((category) => (
          <div key={category.title} className="help-card">
            <div className="help-card-icon">{category.icon}</div>
            <h3 className="help-card-title">{category.title}</h3>
            <p className="help-card-description">{category.description}</p>
          </div>
        ))}
      </div>

      <div className="faq-section">
        <h2 className="faq-title">{t('helpCenter.faqTitle')}</h2>
        <div className="faq-list">
          {filteredFaqs.map((faq, index) => (
            <div key={faq.question} className={`faq-item ${activeFaq === index ? 'active' : ''}`}>
              <button className="faq-question" onClick={() => setActiveFaq(activeFaq === index ? null : index)}>
                {faq.question}
                <ChevronDown className="faq-icon" size={20} />
              </button>
              <div className="faq-answer">{faq.answer}</div>
            </div>
          ))}
        </div>
      </div>

      <div className="help-header" style={{ marginTop: '60px' }}>
        <p className="help-subtitle">{t('helpCenter.noAnswer')}</p>
        <button
          className="help-contact-button"
          style={{ marginTop: '16px', display: 'inline-flex', alignItems: 'center', gap: '8px' }}
        >
          <Mail size={18} />
          {t('helpCenter.contactSupport')}
        </button>
      </div>
    </div>
  );
};

export default HelpCenter;
