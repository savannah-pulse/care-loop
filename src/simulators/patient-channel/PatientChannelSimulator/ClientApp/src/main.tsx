import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';

import { App } from './App';
import i18n from './i18n';
import './styles.css';

// Language, direction and title come from the i18n layer so no text is hard-coded.
document.documentElement.lang = i18n.language;
document.documentElement.dir = i18n.dir();
document.title = i18n.t('app.documentTitle');

const container = document.getElementById('root');
if (container) {
  createRoot(container).render(
    <StrictMode>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </StrictMode>,
  );
}
