// Health page functionality

// DOM elements
const healthSpinner = document.getElementById('healthSpinner');
const healthLoading = document.getElementById('healthLoading');
const healthError = document.getElementById('healthError');
const healthResult = document.getElementById('healthResult');
const healthStatus = document.getElementById('healthStatus');
const healthTimestamp = document.getElementById('healthTimestamp');

async function checkHealth() {
    healthSpinner.style.display = 'inline-block';
    healthLoading.style.display = 'block';
    healthError.style.display = 'none';
    healthResult.style.display = 'none';
    
    try {
        const health = await apiCall('GET', '/v1/health');
        healthStatus.textContent = health.status;
        healthTimestamp.textContent = new Date(health.timestamp).toLocaleString();
        healthResult.style.display = 'block';
    } catch (error) {
        healthError.textContent = error.message;
        healthError.style.display = 'block';
    } finally {
        healthSpinner.style.display = 'none';
        healthLoading.style.display = 'none';
    }
}
