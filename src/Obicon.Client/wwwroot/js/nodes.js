// Nodes page functionality
let nodes = [];

// DOM elements
const nodesTable = document.getElementById('nodesTable');
const nodesTableBody = document.getElementById('nodesTableBody');
const noNodesMessage = document.getElementById('noNodesMessage');
const loadingMessage = document.getElementById('loadingMessage');
const errorMessage = document.getElementById('errorMessage');
const successMessage = document.getElementById('successMessage');
const createdNodeName = document.getElementById('createdNodeName');
const createdNodeToken = document.getElementById('createdNodeToken');
const refreshBtn = document.getElementById('refreshBtn');
const refreshSpinner = document.getElementById('refreshSpinner');

// Load nodes on page load
loadNodes();

async function loadNodes() {
    showLoading();
    try {
        nodes = await apiCall('GET', '/v1/nodes');
        renderNodes();
        showContent();
    } catch (error) {
        showError(error.message);
    }
}

function renderNodes() {
    if (nodes.length === 0) {
        noNodesMessage.style.display = 'block';
        nodesTable.style.display = 'none';
        return;
    }
    
    noNodesMessage.style.display = 'none';
    nodesTable.style.display = 'table';
    
    nodesTableBody.innerHTML = nodes.map(node => `
        <tr>
            <td>${node.id.substring(0, 8)}</td>
            <td>${escapeHtml(node.name)}</td>
            <td>
                <code style="word-break:break-all">${escapeHtml(node.authToken)}</code>
                <button class="btn btn-sm btn-outline-primary py-0" onclick="copyText('${node.authToken}', this)">Copy</button>
            </td>
            <td>${node.isActive ? 'Yes' : 'No'}</td>
            <td>${new Date(node.createdAt).toLocaleString()}</td>
            <td>${node.lastSeenAt ? new Date(node.lastSeenAt).toLocaleString() : 'Never'}</td>
            <td>
                <button class="btn btn-sm btn-danger" onclick="deleteNode('${node.id}')">Delete</button>
            </td>
        </tr>
    `).join('');
}

async function createNode() {
    const name = document.getElementById('newNodeName').value.trim();
    if (!name) return;

    try {
        const node = await apiCall('POST', '/v1/nodes', { name });
        document.getElementById('newNodeName').value = '';

        // Reload first: loadNodes() hides the success message while loading
        await loadNodes();

        // Then show the token until the user dismisses it
        createdNodeName.textContent = node.name;
        createdNodeToken.textContent = node.authToken;
        successMessage.style.display = 'block';
    } catch (error) {
        showError(error.message);
    }
}

async function copyToken() {
    await copyText(createdNodeToken.textContent, document.getElementById('copyTokenBtn'));
}

async function copyText(value, btn) {
    try {
        await navigator.clipboard.writeText(value);
        const original = btn.textContent;
        btn.textContent = 'Copied!';
        setTimeout(() => { btn.textContent = original; }, 2000);
    } catch (error) {
        console.error('Failed to copy to clipboard:', error);
    }
}

async function deleteNode(id) {
    try {
        await apiCall('DELETE', `/v1/nodes/${id}`);
        await loadNodes();
    } catch (error) {
        showError(error.message);
    }
}

function showLoading() {
    loadingMessage.style.display = 'block';
    nodesTable.style.display = 'none';
    noNodesMessage.style.display = 'none';
    errorMessage.style.display = 'none';
    successMessage.style.display = 'none';
}

function showContent() {
    loadingMessage.style.display = 'none';
}

function showError(message) {
    errorMessage.textContent = message;
    errorMessage.style.display = 'block';
    loadingMessage.style.display = 'none';
}

// Helper to escape HTML
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
