import React, { useState, useRef, useEffect } from "react";
import { useMsal } from "@azure/msal-react";
import { loginRequest } from "./authConfig";
import './App.css';
import { FaUser, FaRecycle } from 'react-icons/fa'; // Import the user icon from FontAwesome
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { useLocation } from 'react-router-dom';

const App = () => {
  const { instance, accounts } = useMsal();
  const [inputValue, setInputValue] = useState("");
  const [chatHistory, setChatHistory] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isDisabled, setIsDisabled] = useState(false);
  const [isMenuOpen, setIsMenuOpen] = useState(false); // State to manage menu visibility
  const [error, setError] = useState(null);
  const inputRef = useRef(null); // Create a reference for the input element
  const chatHistoryRef = useRef(null); // Create a reference for the chat history container

  const location = useLocation(); // Get the location object
  const queryParams = new URLSearchParams(location.search); // Parse the query string
  const botIdentifier = queryParams.get('botIdentifier') || process.env.REACT_APP_BOT_IDENTIFIER; // Get the botIdentifier parameter
  const apiBaseUrl = process.env.REACT_APP_API_BASE_URL || ""; // Get the API base URL


  useEffect(() => {
    // Scroll to the bottom of the chat history when it updates
    if (chatHistoryRef.current) {
        chatHistoryRef.current.scrollTop = chatHistoryRef.current.scrollHeight;
    }
  }, [chatHistory]);
  
  const handleLogin = () => {
      console.log("Login Request:", loginRequest); 
      instance.loginPopup(loginRequest)
      .then(response => {
          console.log("Login Response:", response);
      })
      .catch(error => {
          console.error("Login Error:", error);
      });

  };

  const handleLogout = () => {
      instance.logoutPopup();
  };

  const sendChat = () => {
    setError(null);
    setIsLoading(true);
    setIsDisabled(true);
    callApiWithMessage();
  }

  const handleInputChange = (event) => {
    setInputValue(event.target.value);
  };

  const handleKeyDown = (event) => {
    if (event.key === 'Enter') {
        sendChat();
    }
  };

  const callApiWithMessage = (message) => {
    const request = {
        ...loginRequest,
        account: accounts[0]
    };

    const body = {
        message: message ?? inputValue,
        botIdentifier: botIdentifier
    };

    instance.acquireTokenSilent(request)
    .then(response => {
        const token = response.accessToken;
        fetch(`${apiBaseUrl}/api/Chat`, {
            method: "POST",
            headers: {
                "Authorization": `Bearer ${token}`,
                "Content-Type": "application/json"
            },
            body: JSON.stringify(body)
        })
        .then(response => response.json())
        .then(data => {
            console.log("API Response:", data);
            setChatHistory(prevHistory => [...prevHistory, ...data]); // Update chat history with API response
            setIsLoading(false);
            setIsDisabled(false);
            setInputValue(""); // Clear input field 
            inputRef.current.focus(); // Set focus on the input element
        })
        .catch(error => {
            console.error("API Error:", error);
            setError("An error occurred. Please try again.");
            setIsLoading(false);
            setIsDisabled(false);
        });
    })
    .catch(error => {
        console.error("Token Acquisition Error:", error);
        setError("An error occurred. Please try again.");
    });
  };

  const callApiToDeleteConversation = () => {
    const request = {
        ...loginRequest,
        account: accounts[0]
    };

    instance.acquireTokenSilent(request)
    .then(response => {
        const token = response.accessToken;
        fetch(`${apiBaseUrl}/api/Chat?botIdentifier=${botIdentifier}`, {
            method: "DELETE",
            headers: {
                "Authorization": `Bearer ${token}`,
                "Content-Type": "application/json"
            },
        })
        .then(data => {
            console.log("Converstation Deleted:");
            setChatHistory([]); // Clear chat history
            callApiWithMessage(""); // Call the API to start a new conversation
        })
        .catch(error => {
            console.error("API Error:", error);
            setError("An error occurred. Please try again.");
            setIsLoading(false);
            setIsDisabled(false);
        });
    })
    .catch(error => {
        console.error("Token Acquisition Error:", error);
        setError("An error occurred. Please try again.");
    });
  };

    const renderChatHistory = () => {
        return chatHistory.map((message, index) => (
            <div key={index} className={`message ${message.Role}`}>
                {message.Content.map((content, idx) => (
                    <p key={idx} className="common-text-style">
                    {content.Type === "text" ? content.Text : 
                        <ReactMarkdown remarkPlugins={[remarkGfm]}>{content.Text}</ReactMarkdown>
                    }
                </p>
                ))}
            </div>
        ));
    };

    const toggleMenu = () => {
        setIsMenuOpen(!isMenuOpen);
    };

    const handleRecycleClick = () => {
        console.log("Recycle icon clicked");
        callApiToDeleteConversation(); // Call the API to delete the conversation
    };

    return (
        <div className="app-container">
        {accounts.length > 0 ? (
            <>
                <div className="header">
                    <FaRecycle className="recycle-icon" onClick={handleRecycleClick} /> 
                    <FaUser className="user-menu" onClick={toggleMenu} />
                    {isMenuOpen && (
                        <div className="menu">
                            <p>{accounts[0].name}</p>
                            <button className="logout-button" onClick={handleLogout}>Logout</button>
                        </div>
                    )}
                </div>
                <div className="chat-container">
                    <div className="chat-history" ref={chatHistoryRef}>
                        {renderChatHistory()}
                    </div>
                    <div className="chat-input">
                        <input
                            type="text"
                            value={inputValue}
                            onChange={handleInputChange}
                            onKeyDown={handleKeyDown}
                            placeholder="Enter your question here"
                            disabled={isDisabled}
                            ref={inputRef} // Attach the reference to the input element
                        />   
                        <button className="chat-input-button" onClick={sendChat} disabled={isDisabled}>
                            {isLoading ? <div className="spinner"></div> : <p>Send</p>}
                        </button>
                    </div>
                    <div className="footer">
                        {error !== null ? <p className="footer-text">{error}</p>: null}
                    </div>
                </div>
            </>
        ) : (
            <button onClick={handleLogin}>Login</button>
        )}
    </div>
    );
};

export default App;
