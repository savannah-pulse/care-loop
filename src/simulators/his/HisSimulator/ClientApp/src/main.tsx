import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { I18nextProvider } from 'react-i18next';
import { HashRouter } from 'react-router-dom';

import { App } from './App';
import { i18n } from './i18n';
import './styles.css';

const container = document.getElementById('root');
if (container === null) {
  throw new Error('Root element #root is missing from index.html');
}

createRoot(container).render(
  <StrictMode>
    <I18nextProvider i18n={i18n}>
      <HashRouter>
        <App />
      </HashRouter>
    </I18nextProvider>
  </StrictMode>,
);
