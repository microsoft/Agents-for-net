import React from 'react';
import './SamplePage.css';

function SamplePage() {
  return (
    <div className="sample-page">
      <header className="sample-page-header">
        <h1>Copilot Studio Client Sample API Client</h1>
      </header>
      <section className="sample-page-content">
        <h2>Overview</h2>
        <p>
          The Copilot Studio Client Sample API Client is a sophisticated React-based application designed to demonstrate the seamless integration with the Copilot Studio Client Sample API. This application allows users to log in with their Microsoft Work or School account, providing access to a Microsoft Copilot Studio agent.
        </p>
        <h2>Key Features</h2>
        <ul>
          <li><strong>Agent Interaction:</strong> Engage in conversations with a Microsoft Copilot Studio agent through a user-friendly web interface.</li>
          <li><strong>User Authentication:</strong> Securely log in and log out using Azure Active Directory with MSAL.</li>
          <li><strong>Change Agent:</strong> Easily switch between different agents by modifying the URL with the query string parameter <code>?botidentifier=&lt;schema name of agent&gt;</code>. This directs the API to use the specified agent within the user's tenant.</li>
          <li><strong>Token Acquisition:</strong> Silently acquire access tokens for authenticated API requests.</li>
          <li><strong>API Interaction:</strong> Demonstrates how to interact with the Copilot Studio Client Sample API, including sending messages and managing conversations.</li>
        </ul>
        <h2>How to Use</h2>
        <p>
          To use this application, follow these steps:
        </p>
        <ol>
          <li>Log in with your Microsoft Work or School account that has access to the agent. Default agent is **sldc_weather** as set in .env variable REACT_APP_BOT_IDENTIFIER</li>
          <li>Start a conversation with the agent by typing a message in the chat input field and pressing Enter.</li>
          <li>To change the bot, add the query string parameter <code>?botidentifier=&lt;schema name of agent&gt;</code> to the URL. This will direct the API to use the specified agent within your tenant.</li>
        </ol>
      </section>
    </div>
  );
}

export default SamplePage;