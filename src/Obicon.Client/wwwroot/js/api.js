// API Configuration
const API_BASE_URL = 'http://localhost:5000';
const API_AUTH_TOKEN = 'uwu';

// Helper for API calls
async function apiCall(method, endpoint, body = null) {
    const headers = {
        'Authorization': API_AUTH_TOKEN,
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
