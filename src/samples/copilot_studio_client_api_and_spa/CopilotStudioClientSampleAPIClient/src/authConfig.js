export const msalConfig = {
    auth: {
        clientId: process.env.REACT_APP_CLIENTID, // Replace with Application (client) ID
        authority: `https://login.microsoftonline.com/${process.env.REACT_APP_TENANTID}`, // Replace with Directory (tenant) ID
        redirectUri: "http://localhost:3000", // Replace with your app's redirect URI
    },
    cache: {
        cacheLocation: "localStorage", // This can be "localStorage" or "sessionStorage"
        storeAuthStateInCookie: false,
    },
};

export const loginRequest = {
    scopes: [process.env.REACT_APP_API_SCOPE],
};
