import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { App } from './app/App';
import { AppProviders } from './app/providers';
import { resolveSessionConfig } from './services/session';
import './styles.css';

// Resolve the Tauri session config (launch token + dynamic port) before mounting
// so that the HTTP client and SignalR factory have the correct values from the first render.
resolveSessionConfig().then(() => {
  ReactDOM.createRoot(document.getElementById('root') as HTMLElement).render(
    <React.StrictMode>
      <AppProviders>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </AppProviders>
    </React.StrictMode>,
  );
}).catch((err) => {
  console.error('Failed to resolve session config:', err);
});