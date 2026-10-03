// API Configuration
const API_BASE_URL = 'http://localhost:5000';
// The API auth key matches the server's ServerSettings:AuthHeader (default: 'secureobiconkey').
// A browser talking to a server with a changed key stores its own value in
// localStorage, prompted on the first 401, so deployments can use a real secret.
const DEFAULT_AUTH_KEY = 'secureobiconkey';
const AUTH_KEY_STORAGE = 'obicon.authKey';

function getAuthKey() {
    return localStorage.getItem(AUTH_KEY_STORAGE) || DEFAULT_AUTH_KEY;
}

// Helper for API calls
async function apiCall(method, endpoint, body = null, isRetry = false) {
    const headers = {
        'Authorization': getAuthKey(),
        'Content-Type': 'application/json'
    };

    const options = {
        method,
        headers
    };

    if (body) {
        options.body = JSON.stringify(body);
    }

    try {
        const response = await fetch(`${API_BASE_URL}${endpoint}`, options);
        // The server rejected the key: ask for the configured one and retry once
        if (response.status === 401 && !isRetry) {
            const key = prompt('API key rejected. Enter the Authorization header value (ServerSettings:AuthHeader on the server):');
            if (key) {
                localStorage.setItem(AUTH_KEY_STORAGE, key);
                return apiCall(method, endpoint, body, true);
            }
        }
        if (!response.ok) {
            const error = await response.text();
            throw new Error(error || `HTTP ${response.status}`);
        }
        // 204 No Content (e.g. DELETE) has no body to parse
        if (response.status === 204) {
            return null;
        }
        return response.json();
    } catch (error) {
        console.error('API call failed:', error);
        throw error;
    }
}
