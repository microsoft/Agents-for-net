export const msalConfig = {
    auth: {
        clientId: "0a370991-8a1a-4cbf-9ef1-4ce10791b954", // Replace with Application (client) ID
        authority: "https://login.microsoftonline.com/9e021cc9-7821-437d-af4f-41ae85cc1ca5", // Replace with Directory (tenant) ID
        redirectUri: "http://localhost:3000", // Replace with your app's redirect URI
    },
    cache: {
        cacheLocation: "localStorage", // This can be "localStorage" or "sessionStorage"
        storeAuthStateInCookie: false,
    },
};

export const loginRequest = {
    scopes: ["api://4b787ca0-7d16-483d-a566-0f9a3bbfe588/signin_as_user"],
};
