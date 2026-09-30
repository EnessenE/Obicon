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

// Track the node being edited
let editingNode = null;
let editModal = null;

// Load nodes on page load
loadNodes();

async function loadNodes() {
    showLoading();
    try {
        const [nodeResults, statusResults] = await Promise.allSettled([
            apiCall('GET', '/v1/nodes'),
            apiCall('GET', '/v1/nodes/status')
        ]);

        if (nodeResults.status === 'fulfilled') {
            nodes = nodeResults.value;
        }

        // Merge live connectivity into the node rows
        const connectedById = {};
        if (statusResults.status === 'fulfilled') {
            statusResults.value.forEach(s => connectedById[s.id] = s.isConnected);
        }

        renderNodes(connectedById);
        showContent();

        const failure = [nodeResults, statusResults].find(r => r.status === 'rejected');
        if (failure) {
            showError(failure.reason.message);
        }
    } catch (error) {
        showError(error.message);
    }
}

function renderNodes(connectedById = {}) {
    if (nodes.length === 0) {
        noNodesMessage.style.display = 'block';
        nodesTable.style.display = 'none';
        return;
    }

    noNodesMessage.style.display = 'none';
    nodesTable.style.display = 'table';

    nodesTableBody.innerHTML = nodes.map(node => `
        <tr>
            <td title="${node.id}">${node.id.substring(0, 8)}</td>
            <td>${escapeHtml(node.name)}</td>
            <td>${renderLabels(node.labels)}</td>
            <td>${renderPools(node.id)}</td>
            <td>${node.enrollmentType === 'auto-enrollment' ? '<span class="badge bg-info text-dark" title="Enrolled itself with an enroll token; manages its own name, labels, and pools">Auto-enrolled</span>' : '<span class="badge bg-light text-dark border" title="Created by a user">Manual</span>'}</td>
            <td>${node.id in connectedById
                ? (connectedById[node.id] ? '<span class="badge bg-success">Connected</span>' : '<span class="badge bg-secondary">Offline</span>')
                : '-'}</td>
            <td>${node.isActive ? 'Yes' : 'No'}</td>
            <td>${new Date(node.createdAt).toLocaleString()}</td>
            <td>${node.lastSeenAt ? new Date(node.lastSeenAt).toLocaleString() : 'Never'}</td>
            <td>
                <button class="btn btn-sm btn-primary me-1" onclick="openEditModal('${node.id}')">Edit</button>
                <button class="btn btn-sm btn-danger" onclick="deleteNode('${node.id}')">Delete</button>
            </td>
        </tr>
    `).join('');

    // Fetch pool names per node after rendering, so the table appears fast
    nodes.forEach(fillNodePools);
}

function renderLabels(labels) {
    if (!labels || labels.length === 0) return '-';
    return labels.map(l => `<span class="badge bg-secondary me-1">${escapeHtml(l)}</span>`).join('');
}

function renderPools(nodeId) {
    return `<span class="node-pools text-muted" data-node-id="${nodeId}">...</span>`;
}

async function fillNodePools(node) {
    try {
        const pools = await apiCall('GET', `/v1/nodes/${node.id}/pools`);
        const span = document.querySelector(`.node-pools[data-node-id="${node.id}"]`);
        if (span) {
            span.innerHTML = pools.length === 0
                ? '-'
                : pools.map(p => `<span class="badge bg-info text-dark me-1">${escapeHtml(p.name)}</span>`).join('');
        }
    } catch (error) {
        console.error('Failed to load pools for node', node.id, error);
    }
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
        successMessage.querySelector('strong').textContent = 'Node created!';
        successMessage.style.display = 'block';
    } catch (error) {
        showError(error.message);
    }
}

async function openEditModal(nodeId) {
    editingNode = nodes.find(n => n.id === nodeId);
    if (!editingNode) return;

    document.getElementById('editNodeId').textContent = `(${editingNode.name})`;
    document.getElementById('editNodeName').value = editingNode.name;
    document.getElementById('editNodeLabels').value = (editingNode.labels || []).join(', ');
    document.getElementById('editRegenerateToken').checked = false;
    document.getElementById('editError').style.display = 'none';

    const poolsDiv = document.getElementById('editNodePools');
    poolsDiv.textContent = 'Loading...';
    try {
        const pools = await apiCall('GET', `/v1/nodes/${nodeId}/pools`);
        poolsDiv.innerHTML = pools.length === 0
            ? '<span class="text-muted">Not in any pool</span>'
            : pools.map(p => `<span class="badge bg-info text-dark me-1">${escapeHtml(p.name)}</span>`).join('');
    } catch (error) {
        poolsDiv.textContent = error.message;
    }

    if (!editModal) {
        editModal = new bootstrap.Modal(document.getElementById('editModal'));
    }
    editModal.show();
}

async function saveNodeEdit() {
    if (!editingNode) return;

    const name = document.getElementById('editNodeName').value.trim();
    const labels = document.getElementById('editNodeLabels').value
        .split(',')
        .map(l => l.trim())
        .filter(l => l.length > 0);
    const regenerateToken = document.getElementById('editRegenerateToken').checked;

    if (!name) {
        const err = document.getElementById('editError');
        err.textContent = 'Name is required.';
        err.style.display = 'block';
        return;
    }

    try {
        const updated = await apiCall('PUT', `/v1/nodes/${editingNode.id}`, { name, labels, regenerateToken });

        editModal.hide();

        await loadNodes();

        if (regenerateToken) {
            createdNodeName.textContent = updated.name;
            createdNodeToken.textContent = updated.authToken;
            successMessage.querySelector('strong').textContent = 'Token regenerated!';
            successMessage.style.display = 'block';
        }
    } catch (error) {
        const err = document.getElementById('editError');
        err.textContent = error.message;
        err.style.display = 'block';
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

function showLoading() {
    loadingMessage.style.display = 'block';
    nodesTable.style.display = 'none';
    noNodesMessage.style.display = 'none';
    errorMessage.style.display = 'none';
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
