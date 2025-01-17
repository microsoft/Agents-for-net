import React from "react";
import ReactDOM from "react-dom";
import { PublicClientApplication } from "@azure/msal-browser";
import { MsalProvider } from "@azure/msal-react";
import App from "./App";
import SamplePage from "./SamplePage";
import { msalConfig } from "./authConfig";
import { BrowserRouter as Router } from "react-router-dom"; // Import BrowserRouter

const msalInstance = new PublicClientApplication(msalConfig);

const container = document.getElementById('root');
const root = ReactDOM.createRoot(container);

root.render(
    <MsalProvider instance={msalInstance}>
        <SamplePage />
        <Router>
            <App />
        </Router>
    </MsalProvider>
);
