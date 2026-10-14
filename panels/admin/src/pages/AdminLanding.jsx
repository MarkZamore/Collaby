import React, { useEffect, useMemo, useState } from 'react';
import { ArrowDown, ArrowUp, Plus, RefreshCw, Save, Trash2 } from 'lucide-react';
import { getAdminLandingLocale, saveAdminLandingLocale } from '../utils/adminApi';
import {
  EmptyState,
  PageHeader,
  Panel,
  controlButtonClass,
  destructiveButtonClass,
  inputClass,
  mutedTextClass,
  pageContainerClass,
} from '../components/AdminPrimitives';
import { useAuth } from '@shared/context/AuthContext';

const LOCALE_LABELS = { en: 'EN', ru: 'RU', kk: 'KZ' };
const cloneValue = (value) => JSON.parse(JSON.stringify(value));
const linkTemplate = () => ({ label: '', url: '' });
const featureTemplate = () => ({ icon: '', title: '', description: '' });
const screenshotTemplate = () => ({ type: 'dashboard', label: '', imageUrl: '', imageAlt: '', caption: '' });
const faqTemplate = () => ({ question: '', answer: '' });
const roadmapTemplate = () => ({ date: '', title: '', description: '' });
const valueTemplate = () => ({ icon: '', title: '', description: '' });
const timelineTemplate = () => ({ date: '', title: '', description: '' });
const teamTemplate = () => ({ name: '', position: '', description: '', initials: '', avatarUrl: '', profileUrl: '' });
const infoTemplate = () => ({ icon: '', title: '', info: '', url: '' });
const socialLinkTemplate = () => ({ key: '', label: '', url: '' });

const cardStyle = { border: '1px solid rgba(148, 163, 184, 0.18)', borderRadius: 18, padding: 18, display: 'grid', gap: 14, background: 'rgba(255, 255, 255, 0.03)' };
const gridStyle = { display: 'grid', gap: 14, gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))' };
const arrayHeaderStyle = { display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12 };
const buttonRowStyle = { display: 'flex', gap: 8, flexWrap: 'wrap' };

const getByPath = (target, path) => path.reduce((value, segment) => value?.[segment], target);
const setByPath = (target, path, value) => {
  let cursor = target;
  for (let index = 0; index < path.length - 1; index += 1) {
    cursor = cursor[path[index]];
  }
  cursor[path[path.length - 1]] = value;
};

const formatUpdatedAt = (value) => (value ? new Date(value).toLocaleString() : 'Not saved yet');

const TextInput = ({ label, value, onChange, placeholder = '' }) => (
  <label style={{ display: 'grid', gap: 8 }}>
    <span>{label}</span>
    <input className={inputClass} value={value || ''} onChange={(event) => onChange(event.target.value)} placeholder={placeholder} />
  </label>
);

const TextAreaInput = ({ label, value, onChange, rows = 4, placeholder = '' }) => (
  <label style={{ display: 'grid', gap: 8 }}>
    <span>{label}</span>
    <textarea className={inputClass} value={value || ''} onChange={(event) => onChange(event.target.value)} rows={rows} placeholder={placeholder} style={{ minHeight: rows * 24 }} />
  </label>
);

const LinkFields = ({ label, item, onChange }) => (
  <div style={gridStyle}>
    <TextInput label={`${label} label`} value={item?.label} onChange={(value) => onChange({ ...item, label: value })} />
    <TextInput label={`${label} URL`} value={item?.url} onChange={(value) => onChange({ ...item, url: value })} />
  </div>
);

const ImageFields = ({ label, item, onChange }) => (
  <div style={gridStyle}>
    <TextInput label={`${label} imageUrl`} value={item?.imageUrl} onChange={(value) => onChange({ ...item, imageUrl: value })} />
    <TextInput label={`${label} imageAlt`} value={item?.imageAlt} onChange={(value) => onChange({ ...item, imageAlt: value })} />
    <TextInput label={`${label} caption`} value={item?.caption} onChange={(value) => onChange({ ...item, caption: value })} />
  </div>
);

const ArrayItemToolbar = ({ canMoveUp, canMoveDown, onMoveUp, onMoveDown, onRemove }) => (
  <div style={buttonRowStyle}>
    <button type="button" className={controlButtonClass} disabled={!canMoveUp} onClick={onMoveUp}><ArrowUp size={16} /></button>
    <button type="button" className={controlButtonClass} disabled={!canMoveDown} onClick={onMoveDown}><ArrowDown size={16} /></button>
    <button type="button" className={destructiveButtonClass} onClick={onRemove}><Trash2 size={16} /></button>
  </div>
);

const AdminLanding = () => {
  const { accessToken } = useAuth();
  const [activeLocale, setActiveLocale] = useState('en');
  const [documentsByLocale, setDocumentsByLocale] = useState({});
  const [metaByLocale, setMetaByLocale] = useState({});
  const [dirtyByLocale, setDirtyByLocale] = useState({});
  const [loadingLocale, setLoadingLocale] = useState(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState('');

  const currentDocument = documentsByLocale[activeLocale] || null;
  const currentMeta = metaByLocale[activeLocale] || null;
  const localeTabs = useMemo(() => Object.entries(LOCALE_LABELS).map(([locale, label]) => ({ locale, label })), []);

  const loadLocale = async (locale, { force = false } = {}) => {
    if (!force && documentsByLocale[locale]) return;
    setLoadingLocale(locale);
    setError(null);
    try {
      const response = await getAdminLandingLocale(locale, accessToken);
      setDocumentsByLocale((prev) => ({ ...prev, [locale]: cloneValue(response.content) }));
      setMetaByLocale((prev) => ({ ...prev, [locale]: { source: response.source, updatedAtUtc: response.updatedAtUtc, updatedByEmail: response.updatedByEmail, updatedByUserId: response.updatedByUserId } }));
      setDirtyByLocale((prev) => ({ ...prev, [locale]: false }));
    } catch (nextError) {
      setError(nextError.message || 'Failed to load landing locale.');
    } finally {
      setLoadingLocale(null);
    }
  };

  useEffect(() => {
    loadLocale(activeLocale);
  }, [activeLocale, accessToken]);

  const mutateActiveDocument = (mutator) => {
    setDocumentsByLocale((prev) => {
      const current = prev[activeLocale];
      if (!current) return prev;
      const next = cloneValue(current);
      mutator(next);
      return { ...prev, [activeLocale]: next };
    });
    setDirtyByLocale((prev) => ({ ...prev, [activeLocale]: true }));
    setNotice('');
  };

  const setValue = (path, value) => mutateActiveDocument((draft) => setByPath(draft, path, value));
  const updateArrayItem = (path, index, value) => mutateActiveDocument((draft) => { getByPath(draft, path)[index] = value; });
  const addArrayItem = (path, factory) => mutateActiveDocument((draft) => { getByPath(draft, path).push(factory()); });
  const removeArrayItem = (path, index) => mutateActiveDocument((draft) => { getByPath(draft, path).splice(index, 1); });
  const moveArrayItem = (path, index, direction) => mutateActiveDocument((draft) => {
    const items = getByPath(draft, path);
    const nextIndex = index + direction;
    if (nextIndex < 0 || nextIndex >= items.length) return;
    const [entry] = items.splice(index, 1);
    items.splice(nextIndex, 0, entry);
  });

  const saveActiveLocale = async () => {
    if (!currentDocument) return;
    setSaving(true);
    setError(null);
    setNotice('');
    try {
      const response = await saveAdminLandingLocale(activeLocale, currentDocument, accessToken);
      setDocumentsByLocale((prev) => ({ ...prev, [activeLocale]: cloneValue(response.content) }));
      setMetaByLocale((prev) => ({ ...prev, [activeLocale]: { source: response.source, updatedAtUtc: response.updatedAtUtc, updatedByEmail: response.updatedByEmail, updatedByUserId: response.updatedByUserId } }));
      setDirtyByLocale((prev) => ({ ...prev, [activeLocale]: false }));
      setNotice(`Locale ${activeLocale.toUpperCase()} saved and published.`);
    } catch (nextError) {
      setError(nextError.message || 'Failed to save landing locale.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className={pageContainerClass}>
      <PageHeader
        title="Landing CMS"
        subtitle="Structured live editor for the public landing content in EN, RU, and KZ. Saving a locale publishes it immediately."
        actions={
          <div style={buttonRowStyle}>
            <button type="button" className={controlButtonClass} onClick={() => loadLocale(activeLocale, { force: true })} disabled={loadingLocale === activeLocale}><RefreshCw size={16} /><span>Reload</span></button>
            <button type="button" className={controlButtonClass} onClick={saveActiveLocale} disabled={!currentDocument || saving}><Save size={16} /><span>{saving ? 'Saving...' : 'Save locale'}</span></button>
          </div>
        }
      />

      <Panel title="Locale">
        <div style={buttonRowStyle}>
          {localeTabs.map((tab) => (
            <button key={tab.locale} type="button" className={controlButtonClass} onClick={() => setActiveLocale(tab.locale)} style={tab.locale === activeLocale ? { outline: '2px solid var(--app-accent)' } : undefined}>
              {tab.label}{dirtyByLocale[tab.locale] ? ' *' : ''}
            </button>
          ))}
        </div>
        <div className={mutedTextClass} style={{ marginTop: 12 }}>
          Source: {currentMeta?.source || 'loading'} | Updated: {formatUpdatedAt(currentMeta?.updatedAtUtc)} | By: {currentMeta?.updatedByEmail || 'fallback defaults'}
        </div>
        {notice ? <div style={{ marginTop: 12 }}>{notice}</div> : null}
        {error ? <div style={{ marginTop: 12 }}>{error}</div> : null}
      </Panel>

      {loadingLocale === activeLocale || !currentDocument ? (
        <Panel title="Landing content">
          {loadingLocale === activeLocale ? 'Loading locale...' : <EmptyState>Landing locale is unavailable.</EmptyState>}
        </Panel>
      ) : (
        <>
          <Panel title="Navbar">
            <div style={gridStyle}>
              <TextInput label="About menu title" value={currentDocument.navbar.aboutMenu} onChange={(value) => setValue(['navbar', 'aboutMenu'], value)} />
              <TextInput label="Theme label" value={currentDocument.navbar.theme} onChange={(value) => setValue(['navbar', 'theme'], value)} />
            </div>
            <div style={{ display: 'grid', gap: 18, marginTop: 18 }}>
              <LinkFields label="About team" item={currentDocument.navbar.aboutTeam} onChange={(value) => setValue(['navbar', 'aboutTeam'], value)} />
              <LinkFields label="Development stages" item={currentDocument.navbar.developmentStages} onChange={(value) => setValue(['navbar', 'developmentStages'], value)} />
              <LinkFields label="Contact" item={currentDocument.navbar.contact} onChange={(value) => setValue(['navbar', 'contact'], value)} />
              <LinkFields label="Features" item={currentDocument.navbar.features} onChange={(value) => setValue(['navbar', 'features'], value)} />
              <LinkFields label="Benefits" item={currentDocument.navbar.benefits} onChange={(value) => setValue(['navbar', 'benefits'], value)} />
              <LinkFields label="Progress" item={currentDocument.navbar.progress} onChange={(value) => setValue(['navbar', 'progress'], value)} />
              <LinkFields label="Login" item={currentDocument.navbar.login} onChange={(value) => setValue(['navbar', 'login'], value)} />
              <LinkFields label="Register" item={currentDocument.navbar.register} onChange={(value) => setValue(['navbar', 'register'], value)} />
            </div>
          </Panel>

          <Panel title="Footer">
            <div style={gridStyle}>
              <TextAreaInput label="Description" value={currentDocument.footer.description} onChange={(value) => setValue(['footer', 'description'], value)} rows={4} />
              <TextInput label="Project title" value={currentDocument.footer.project} onChange={(value) => setValue(['footer', 'project'], value)} />
              <TextInput label="Pages title" value={currentDocument.footer.pages} onChange={(value) => setValue(['footer', 'pages'], value)} />
              <TextInput label="Documents title" value={currentDocument.footer.documents} onChange={(value) => setValue(['footer', 'documents'], value)} />
              <TextInput label="Follow title" value={currentDocument.footer.follow} onChange={(value) => setValue(['footer', 'follow'], value)} />
              <TextAreaInput label="Follow description" value={currentDocument.footer.followDescription} onChange={(value) => setValue(['footer', 'followDescription'], value)} rows={3} />
              <TextInput label="Copyright" value={currentDocument.footer.copyright} onChange={(value) => setValue(['footer', 'copyright'], value)} />
            </div>

            {[
              ['Project links', ['footer', 'projectLinks'], linkTemplate],
              ['Page links', ['footer', 'pageLinks'], linkTemplate],
              ['Document links', ['footer', 'documentLinks'], linkTemplate],
              ['Follow links', ['footer', 'followLinks'], linkTemplate],
              ['Legal links', ['footer', 'legalLinks'], linkTemplate],
            ].map(([title, path, factory]) => (
              <div key={title} style={{ marginTop: 24 }}>
                <div style={arrayHeaderStyle}>
                  <h3>{title}</h3>
                  <button type="button" className={controlButtonClass} onClick={() => addArrayItem(path, factory)}>
                    <Plus size={16} />
                    <span>Add</span>
                  </button>
                </div>
                <div style={{ display: 'grid', gap: 14, marginTop: 14 }}>
                  {getByPath(currentDocument, path).map((item, index, array) => (
                    <div key={`${title}-${index}`} style={cardStyle}>
                      <ArrayItemToolbar
                        canMoveUp={index > 0}
                        canMoveDown={index < array.length - 1}
                        onMoveUp={() => moveArrayItem(path, index, -1)}
                        onMoveDown={() => moveArrayItem(path, index, 1)}
                        onRemove={() => removeArrayItem(path, index)}
                      />
                      <LinkFields label={title.slice(0, -1)} item={item} onChange={(value) => updateArrayItem(path, index, value)} />
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </Panel>

          <Panel title="Home">
            <div style={gridStyle}>
              <TextInput label="Badge" value={currentDocument.home.badge} onChange={(value) => setValue(['home', 'badge'], value)} />
              <TextInput label="Title" value={currentDocument.home.title} onChange={(value) => setValue(['home', 'title'], value)} />
              <TextAreaInput label="Description" value={currentDocument.home.description} onChange={(value) => setValue(['home', 'description'], value)} rows={4} />
              <TextInput label="Stats team" value={currentDocument.home.stats.team} onChange={(value) => setValue(['home', 'stats', 'team'], value)} />
              <TextInput label="Stats modules" value={currentDocument.home.stats.modules} onChange={(value) => setValue(['home', 'stats', 'modules'], value)} />
              <TextInput label="Stats months" value={currentDocument.home.stats.months} onChange={(value) => setValue(['home', 'stats', 'months'], value)} />
            </div>
            <div style={{ display: 'grid', gap: 18, marginTop: 18 }}>
              <LinkFields label="Primary CTA" item={currentDocument.home.primaryCta} onChange={(value) => setValue(['home', 'primaryCta'], value)} />
              <LinkFields label="Secondary CTA" item={currentDocument.home.secondaryCta} onChange={(value) => setValue(['home', 'secondaryCta'], value)} />
              <ImageFields label="Hero media" item={currentDocument.home.heroMedia} onChange={(value) => setValue(['home', 'heroMedia'], value)} />
              <ImageFields label="Benefits media" item={currentDocument.home.benefitsMedia} onChange={(value) => setValue(['home', 'benefitsMedia'], value)} />
            </div>
            <div style={{ ...gridStyle, marginTop: 18 }}>
              <TextInput label="Features title" value={currentDocument.home.featuresTitle} onChange={(value) => setValue(['home', 'featuresTitle'], value)} />
              <TextAreaInput label="Features description" value={currentDocument.home.featuresDescription} onChange={(value) => setValue(['home', 'featuresDescription'], value)} rows={3} />
              <TextInput label="Benefits title" value={currentDocument.home.benefitsTitle} onChange={(value) => setValue(['home', 'benefitsTitle'], value)} />
              <TextInput label="Screenshots title" value={currentDocument.home.screenshotsTitle} onChange={(value) => setValue(['home', 'screenshotsTitle'], value)} />
              <TextAreaInput label="Screenshots description" value={currentDocument.home.screenshotsDescription} onChange={(value) => setValue(['home', 'screenshotsDescription'], value)} rows={3} />
              <TextInput label="FAQ title" value={currentDocument.home.faqTitle} onChange={(value) => setValue(['home', 'faqTitle'], value)} />
              <TextAreaInput label="FAQ description" value={currentDocument.home.faqDescription} onChange={(value) => setValue(['home', 'faqDescription'], value)} rows={3} />
              <TextInput label="Roadmap title" value={currentDocument.home.roadmapTitle} onChange={(value) => setValue(['home', 'roadmapTitle'], value)} />
              <TextAreaInput label="Roadmap description" value={currentDocument.home.roadmapDescription} onChange={(value) => setValue(['home', 'roadmapDescription'], value)} rows={3} />
            </div>

            {[
              ['Features', ['home', 'features'], featureTemplate],
              ['Benefits', ['home', 'benefits'], featureTemplate],
              ['Screenshots', ['home', 'screenshots'], screenshotTemplate],
              ['FAQ', ['home', 'faq'], faqTemplate],
              ['Roadmap', ['home', 'roadmap'], roadmapTemplate],
            ].map(([title, path, factory]) => (
              <div key={title} style={{ marginTop: 24 }}>
                <div style={arrayHeaderStyle}>
                  <h3>{title}</h3>
                  <button type="button" className={controlButtonClass} onClick={() => addArrayItem(path, factory)}>
                    <Plus size={16} />
                    <span>Add</span>
                  </button>
                </div>
                <div style={{ display: 'grid', gap: 14, marginTop: 14 }}>
                  {getByPath(currentDocument, path).map((item, index, array) => (
                    <div key={`${title}-${index}`} style={cardStyle}>
                      <ArrayItemToolbar
                        canMoveUp={index > 0}
                        canMoveDown={index < array.length - 1}
                        onMoveUp={() => moveArrayItem(path, index, -1)}
                        onMoveDown={() => moveArrayItem(path, index, 1)}
                        onRemove={() => removeArrayItem(path, index)}
                      />
                      {'label' in item ? (
                        <>
                          {'type' in item ? <TextInput label="Type" value={item.type} onChange={(value) => updateArrayItem(path, index, { ...item, type: value })} /> : null}
                          <TextInput label="Label" value={item.label} onChange={(value) => updateArrayItem(path, index, { ...item, label: value })} />
                          {'imageUrl' in item ? <ImageFields label="Media" item={item} onChange={(value) => updateArrayItem(path, index, value)} /> : null}
                        </>
                      ) : null}
                      {'icon' in item ? <TextInput label="Icon" value={item.icon} onChange={(value) => updateArrayItem(path, index, { ...item, icon: value })} /> : null}
                      {'question' in item ? <TextInput label="Question" value={item.question} onChange={(value) => updateArrayItem(path, index, { ...item, question: value })} /> : null}
                      {'date' in item ? <TextInput label="Date" value={item.date} onChange={(value) => updateArrayItem(path, index, { ...item, date: value })} /> : null}
                      {'title' in item ? <TextInput label="Title" value={item.title} onChange={(value) => updateArrayItem(path, index, { ...item, title: value })} /> : null}
                      {'description' in item ? <TextAreaInput label="Description" value={item.description} onChange={(value) => updateArrayItem(path, index, { ...item, description: value })} rows={3} /> : null}
                      {'answer' in item ? <TextAreaInput label="Answer" value={item.answer} onChange={(value) => updateArrayItem(path, index, { ...item, answer: value })} rows={3} /> : null}
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </Panel>

          <Panel title="About">
            <div style={gridStyle}>
              <TextInput label="Badge" value={currentDocument.about.badge} onChange={(value) => setValue(['about', 'badge'], value)} />
              <TextInput label="Title" value={currentDocument.about.title} onChange={(value) => setValue(['about', 'title'], value)} />
              <TextAreaInput label="Description" value={currentDocument.about.description} onChange={(value) => setValue(['about', 'description'], value)} rows={4} />
              <TextInput label="Stats team" value={currentDocument.about.stats.team} onChange={(value) => setValue(['about', 'stats', 'team'], value)} />
              <TextInput label="Stats scenarios" value={currentDocument.about.stats.scenarios} onChange={(value) => setValue(['about', 'stats', 'scenarios'], value)} />
              <TextInput label="Stats months" value={currentDocument.about.stats.months} onChange={(value) => setValue(['about', 'stats', 'months'], value)} />
              <TextInput label="Stats goal" value={currentDocument.about.stats.goal} onChange={(value) => setValue(['about', 'stats', 'goal'], value)} />
              <TextInput label="Mission title" value={currentDocument.about.missionTitle} onChange={(value) => setValue(['about', 'missionTitle'], value)} />
              <TextAreaInput label="Mission description" value={currentDocument.about.missionDescription} onChange={(value) => setValue(['about', 'missionDescription'], value)} rows={3} />
              <TextInput label="Vision title" value={currentDocument.about.visionTitle} onChange={(value) => setValue(['about', 'visionTitle'], value)} />
              <TextAreaInput label="Vision description" value={currentDocument.about.visionDescription} onChange={(value) => setValue(['about', 'visionDescription'], value)} rows={3} />
              <TextInput label="Values title" value={currentDocument.about.valuesTitle} onChange={(value) => setValue(['about', 'valuesTitle'], value)} />
              <TextAreaInput label="Values description" value={currentDocument.about.valuesDescription} onChange={(value) => setValue(['about', 'valuesDescription'], value)} rows={3} />
              <TextInput label="Story title" value={currentDocument.about.storyTitle} onChange={(value) => setValue(['about', 'storyTitle'], value)} />
              <TextAreaInput label="Quote" value={currentDocument.about.quote} onChange={(value) => setValue(['about', 'quote'], value)} rows={3} />
              <TextInput label="Quote author" value={currentDocument.about.quoteAuthor} onChange={(value) => setValue(['about', 'quoteAuthor'], value)} />
              <TextInput label="Timeline title" value={currentDocument.about.timelineTitle} onChange={(value) => setValue(['about', 'timelineTitle'], value)} />
              <TextAreaInput label="Timeline description" value={currentDocument.about.timelineDescription} onChange={(value) => setValue(['about', 'timelineDescription'], value)} rows={3} />
              <TextInput label="Team title" value={currentDocument.about.teamTitle} onChange={(value) => setValue(['about', 'teamTitle'], value)} />
              <TextAreaInput label="Team description" value={currentDocument.about.teamDescription} onChange={(value) => setValue(['about', 'teamDescription'], value)} rows={3} />
              <TextInput label="CTA title" value={currentDocument.about.ctaTitle} onChange={(value) => setValue(['about', 'ctaTitle'], value)} />
              <TextAreaInput label="CTA description" value={currentDocument.about.ctaDescription} onChange={(value) => setValue(['about', 'ctaDescription'], value)} rows={3} />
            </div>
            <div style={{ display: 'grid', gap: 18, marginTop: 18 }}>
              <ImageFields label="Story media" item={currentDocument.about.storyMedia} onChange={(value) => setValue(['about', 'storyMedia'], value)} />
              <LinkFields label="Primary CTA" item={currentDocument.about.primaryCta} onChange={(value) => setValue(['about', 'primaryCta'], value)} />
              <LinkFields label="Secondary CTA" item={currentDocument.about.secondaryCta} onChange={(value) => setValue(['about', 'secondaryCta'], value)} />
            </div>

            {[
              ['Values', ['about', 'values'], valueTemplate],
              ['Story paragraphs', ['about', 'story'], () => ''],
              ['Timeline', ['about', 'timeline'], timelineTemplate],
              ['Team', ['about', 'team'], teamTemplate],
            ].map(([title, path, factory]) => (
              <div key={title} style={{ marginTop: 24 }}>
                <div style={arrayHeaderStyle}>
                  <h3>{title}</h3>
                  <button type="button" className={controlButtonClass} onClick={() => addArrayItem(path, factory)}>
                    <Plus size={16} />
                    <span>Add</span>
                  </button>
                </div>
                <div style={{ display: 'grid', gap: 14, marginTop: 14 }}>
                  {getByPath(currentDocument, path).map((item, index, array) => (
                    <div key={`${title}-${index}`} style={cardStyle}>
                      <ArrayItemToolbar
                        canMoveUp={index > 0}
                        canMoveDown={index < array.length - 1}
                        onMoveUp={() => moveArrayItem(path, index, -1)}
                        onMoveDown={() => moveArrayItem(path, index, 1)}
                        onRemove={() => removeArrayItem(path, index)}
                      />
                      {typeof item === 'string' ? (
                        <TextAreaInput label="Paragraph" value={item} onChange={(value) => updateArrayItem(path, index, value)} rows={3} />
                      ) : (
                        <>
                          {'icon' in item ? <TextInput label="Icon" value={item.icon} onChange={(value) => updateArrayItem(path, index, { ...item, icon: value })} /> : null}
                          {'name' in item ? <TextInput label="Name" value={item.name} onChange={(value) => updateArrayItem(path, index, { ...item, name: value })} /> : null}
                          {'position' in item ? <TextInput label="Position" value={item.position} onChange={(value) => updateArrayItem(path, index, { ...item, position: value })} /> : null}
                          {'initials' in item ? <TextInput label="Initials" value={item.initials} onChange={(value) => updateArrayItem(path, index, { ...item, initials: value })} /> : null}
                          {'avatarUrl' in item ? <TextInput label="Avatar URL" value={item.avatarUrl} onChange={(value) => updateArrayItem(path, index, { ...item, avatarUrl: value })} /> : null}
                          {'profileUrl' in item ? <TextInput label="Profile URL" value={item.profileUrl} onChange={(value) => updateArrayItem(path, index, { ...item, profileUrl: value })} /> : null}
                          {'date' in item ? <TextInput label="Date" value={item.date} onChange={(value) => updateArrayItem(path, index, { ...item, date: value })} /> : null}
                          {'title' in item ? <TextInput label="Title" value={item.title} onChange={(value) => updateArrayItem(path, index, { ...item, title: value })} /> : null}
                          {'description' in item ? <TextAreaInput label="Description" value={item.description} onChange={(value) => updateArrayItem(path, index, { ...item, description: value })} rows={3} /> : null}
                        </>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </Panel>

          <Panel title="Contact">
            <div style={gridStyle}>
              <TextInput label="Badge" value={currentDocument.contact.badge} onChange={(value) => setValue(['contact', 'badge'], value)} />
              <TextInput label="Title" value={currentDocument.contact.title} onChange={(value) => setValue(['contact', 'title'], value)} />
              <TextAreaInput label="Description" value={currentDocument.contact.description} onChange={(value) => setValue(['contact', 'description'], value)} rows={4} />
              <TextInput label="Form title" value={currentDocument.contact.formTitle} onChange={(value) => setValue(['contact', 'formTitle'], value)} />
              <TextAreaInput label="Form description" value={currentDocument.contact.formDescription} onChange={(value) => setValue(['contact', 'formDescription'], value)} rows={3} />
              <TextInput label="Name placeholder" value={currentDocument.contact.form.namePlaceholder} onChange={(value) => setValue(['contact', 'form', 'namePlaceholder'], value)} />
              <TextInput label="Email placeholder" value={currentDocument.contact.form.emailPlaceholder} onChange={(value) => setValue(['contact', 'form', 'emailPlaceholder'], value)} />
              <TextInput label="Subject placeholder" value={currentDocument.contact.form.subjectPlaceholder} onChange={(value) => setValue(['contact', 'form', 'subjectPlaceholder'], value)} />
              <TextAreaInput label="Message placeholder" value={currentDocument.contact.form.messagePlaceholder} onChange={(value) => setValue(['contact', 'form', 'messagePlaceholder'], value)} rows={3} />
              <TextInput label="Real links title" value={currentDocument.contact.realLinksTitle} onChange={(value) => setValue(['contact', 'realLinksTitle'], value)} />
              <TextAreaInput label="Real links description" value={currentDocument.contact.realLinksDescription} onChange={(value) => setValue(['contact', 'realLinksDescription'], value)} rows={3} />
              <TextInput label="Important title" value={currentDocument.contact.importantTitle} onChange={(value) => setValue(['contact', 'importantTitle'], value)} />
              <TextAreaInput label="Demo status" value={currentDocument.contact.demoStatus} onChange={(value) => setValue(['contact', 'demoStatus'], value)} rows={3} />
            </div>

            {[
              ['Contact info', ['contact', 'info'], infoTemplate],
              ['Social links', ['contact', 'socialLinks'], socialLinkTemplate],
              ['FAQ', ['contact', 'faq'], faqTemplate],
            ].map(([title, path, factory]) => (
              <div key={title} style={{ marginTop: 24 }}>
                <div style={arrayHeaderStyle}>
                  <h3>{title}</h3>
                  <button type="button" className={controlButtonClass} onClick={() => addArrayItem(path, factory)}>
                    <Plus size={16} />
                    <span>Add</span>
                  </button>
                </div>
                <div style={{ display: 'grid', gap: 14, marginTop: 14 }}>
                  {getByPath(currentDocument, path).map((item, index, array) => (
                    <div key={`${title}-${index}`} style={cardStyle}>
                      <ArrayItemToolbar
                        canMoveUp={index > 0}
                        canMoveDown={index < array.length - 1}
                        onMoveUp={() => moveArrayItem(path, index, -1)}
                        onMoveDown={() => moveArrayItem(path, index, 1)}
                        onRemove={() => removeArrayItem(path, index)}
                      />
                      {'key' in item ? <TextInput label="Key" value={item.key} onChange={(value) => updateArrayItem(path, index, { ...item, key: value })} /> : null}
                      {'icon' in item ? <TextInput label="Icon" value={item.icon} onChange={(value) => updateArrayItem(path, index, { ...item, icon: value })} /> : null}
                      {'title' in item ? <TextInput label="Title" value={item.title} onChange={(value) => updateArrayItem(path, index, { ...item, title: value })} /> : null}
                      {'info' in item ? <TextInput label="Info" value={item.info} onChange={(value) => updateArrayItem(path, index, { ...item, info: value })} /> : null}
                      {'label' in item ? <TextInput label="Label" value={item.label} onChange={(value) => updateArrayItem(path, index, { ...item, label: value })} /> : null}
                      {'url' in item ? <TextInput label="URL" value={item.url} onChange={(value) => updateArrayItem(path, index, { ...item, url: value })} /> : null}
                      {'question' in item ? <TextInput label="Question" value={item.question} onChange={(value) => updateArrayItem(path, index, { ...item, question: value })} /> : null}
                      {'answer' in item ? <TextAreaInput label="Answer" value={item.answer} onChange={(value) => updateArrayItem(path, index, { ...item, answer: value })} rows={3} /> : null}
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </Panel>
        </>
      )}
    </div>
  );
};

export default AdminLanding;
