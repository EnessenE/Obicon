// Tests page functionality
let tests = [];

// DOM elements
const testsTable = document.getElementById('testsTable');
const testsTableBody = document.getElementById('testsTableBody');
const noTestsMessage = document.getElementById('noTestsMessage');
const testsLoadingMessage = document.getElementById('testsLoadingMessage');
const testsErrorMessage = document.getElementById('testsErrorMessage');
const testsRefreshSpinner = document.getElementById('testsRefreshSpinner');

// Enum mappings
const testTypeMap = {
    0: 'Ping', 1: 'HTTP', 2: 'HTTPS', 3: 'TCP', 4: 'DNS', 5: 'Traceroute'
};

const frequencyMap = {
    0: '10 seconds', 1: '30 seconds', 2: '1 minute',
    3: '2 minutes', 4: '5 minutes', 5: '10 minutes', 6: '1 hour'
};

// Load tests on page load
loadTests();

async function loadTests() {
    showTestsLoading();
    try {
        tests = await apiCall('GET', '/v1/tests');
        renderTests();
        showTestsContent();
    } catch (error) {
        showTestsError(error.message);
    }
}

function renderTests() {
    if (tests.length === 0) {
        noTestsMessage.style.display = 'block';
        testsTable.style.display = 'none';
        return;
    }
    
    noTestsMessage.style.display = 'none';
    testsTable.style.display = 'table';
    
    testsTableBody.innerHTML = tests.map(test => `
        <tr>
            <td>${test.id.substring(0, 8)}</td>
            <td>${escapeHtml(test.name)}</td>
            <td>${testTypeMap[test.type] || test.type}</td>
            <td>${frequencyMap[test.frequency] || test.frequency}</td>
            <td>${test.isActive ? 'Yes' : 'No'}</td>
            <td>
                <button class="btn btn-sm btn-success me-1" onclick="triggerRun('${test.id}')">Run</button>
                <button class="btn btn-sm btn-danger" onclick="deleteTest('${test.id}')">Delete</button>
            </td>
        </tr>
    `).join('');
}

async function createTest() {
    const name = document.getElementById('testName').value.trim();
    const type = parseInt(document.getElementById('testType').value);
    const frequency = parseInt(document.getElementById('testFrequency').value);
    const isActive = document.getElementById('testIsActive').checked;
    
    if (!name) return;
    
    try {
        await apiCall('POST', '/v1/tests', {
            name,
            type,
            nodeIds: [],
            frequency,
            isActive
        });
        
        // Reset form
        document.getElementById('testName').value = '';
        document.getElementById('testType').value = '0';
        document.getElementById('testFrequency').value = '2';
        document.getElementById('testIsActive').checked = true;
        
        await loadTests();
    } catch (error) {
        showTestsError(error.message);
    }
}

async function deleteTest(id) {
    try {
        await apiCall('DELETE', `/v1/tests/${id}`);
        await loadTests();
    } catch (error) {
        showTestsError(error.message);
    }
}

async function triggerRun(testId) {
    try {
        await apiCall('POST', `/v1/tests/${testId}/run`);
    } catch (error) {
        showTestsError(error.message);
    }
}

function showTestsLoading() {
    testsLoadingMessage.style.display = 'block';
    testsTable.style.display = 'none';
    noTestsMessage.style.display = 'none';
    testsErrorMessage.style.display = 'none';
}

function showTestsContent() {
    testsLoadingMessage.style.display = 'none';
}

function showTestsError(message) {
    testsErrorMessage.textContent = message;
    testsErrorMessage.style.display = 'block';
    testsLoadingMessage.style.display = 'none';
}

// Helper to escape HTML
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
