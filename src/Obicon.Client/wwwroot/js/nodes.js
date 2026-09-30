// Nodes page functionality
let nodes = [];

// DOM elements
const nodesList = document.getElementById('nodesList');
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
        nodesList.style.display = 'none';
        return;
    }

    noNodesMessage.style.display = 'none';
    nodesList.style.display = 'block';

    nodesList.innerHTML = nodes.map(node => `
        <div class="row g-2 g-lg-3 list-row px-3">
            <div class="col-12 col-lg-3">
                <div class="fw-semibold">${escapeHtml(node.name)}</div>
                <div class="text-muted small" title="Created ${new Date(node.createdAt).toLocaleString()}">${node.id}</div>
            </div>
            <div class="col-6 col-lg-1">
                <div class="field-label">Version</div>
                ${node.version ? `<code>v${escapeHtml(node.version)}</code>` : '-'}
            </div>
            <div class="col-6 col-lg-1">
                <div class="field-label">IP</div>
                ${renderIps(node)}
            </div>
            <div class="col-6 col-lg-1">
                <div class="field-label">Capacity</div>
                ${renderCapacity(node.settings)}
            </div>
            <div class="col-6 col-lg-1">
                <div class="field-label">State</div>
                ${node.id in connectedById
                    ? (connectedById[node.id] ? '<span class="badge bg-success">Connected</span>' : '<span class="badge bg-secondary">Offline</span>')
                    : '-'}
                <div class="small text-muted" title="Last seen">${node.lastSeenAt ? new Date(node.lastSeenAt).toLocaleString() : 'Never'}</div>
            </div>
            <div class="col-6 col-lg-1">
                <div class="field-label">Enrollment</div>
                ${node.enrollmentType === 'auto-enrollment'
                    ? '<span class="badge bg-info text-dark" title="Enrolled itself with an enroll token; manages its own name, labels, and pools">Auto-enrolled</span>'
                    : '<span class="badge bg-light text-dark border" title="Created by a user">Manual</span>'}
            </div>
            <div class="col-6 col-lg-1">
                <div class="field-label">Active</div>
                ${node.isActive ? 'Yes' : 'No'}
            </div>
            <div class="col-12 col-lg-1">
                <div class="field-label">Labels</div>
                ${renderLabels(node.labels)}
            </div>
            <div class="col-12 col-lg-1">
                <div class="field-label">Pools</div>
                ${renderPools(node.id)}
            </div>
            <div class="col-12 col-lg-2 d-flex align-items-end justify-content-lg-end">
                <button class="btn btn-sm btn-primary me-1" onclick="openEditModal('${node.id}')">Edit</button>
                <button class="btn btn-sm btn-danger" onclick="deleteNode('${node.id}')">Delete</button>
            </div>
        </div>
    `).join('');

    // Fetch pool names per node after rendering, so the list appears fast
    nodes.forEach(fillNodePools);
}

function renderLabels(labels) {
    if (!labels || labels.length === 0) return '-';
    return labels.map(l => `<span class="badge bg-secondary me-1">${escapeHtml(l)}</span>`).join('');
}

// IP column: the node-reported internal and external addresses per family, "unavailable"
// when a family is missing; the connection-observed address is the fallback before the
// node has reported anything
function renderIps(node) {
    const hasReported = node.internalIpv4 != null || node.internalIpv6 != null
        || node.externalIpv4 != null || node.externalIpv6 != null;
    if (!hasReported && !node.ipAddress) {
        return '-';
    }

    const rows = [
        ['int4', node.internalIpv4],
        ['int6', node.internalIpv6],
        ['ext4', node.externalIpv4],
        ['ext6', node.externalIpv6]
    ];

    if (!hasReported) {
        return `<code>${escapeHtml(node.ipAddress)}</code>`;
    }

    return rows.map(([label, value]) =>
        value
            ? `<code>${escapeHtml(value)}</code> <span class="text-muted small">${label}</span>`
            : `<span class="text-muted small" title="This address family is unavailable on the node">${label} unavailable</span>`
    ).join('<br>');
}

// Capacity column: how many tests the node runs in parallel
function renderCapacity(settings) {
    const maxConcurrent = settings && settings.MaxConcurrentTests;
    if (!maxConcurrent) return '-';
    return `<span class="badge bg-secondary" title="Maximum concurrent test executions">${escapeHtml(maxConcurrent)}x parallel</span>`;
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

    // Read-only settings the node reported on its last connection
    const settingsDiv = document.getElementById('editNodeSettings');
    const settings = editingNode.settings || {};
    const prettyNames = {
        MaxConcurrentTests: 'Max concurrent tests',
        HeartbeatIntervalSeconds: 'Heartbeat interval (s)',
        DefaultTestTimeoutSeconds: 'Default test timeout (s)',
        MaxTestTimeoutSeconds: 'Max test timeout (s)',
        ReconnectDelaySeconds: 'Reconnect delay (s)'
    };
    settingsDiv.innerHTML = Object.keys(settings).length === 0
        ? '<span class="text-muted">Not reported yet - the node reports its settings when it connects</span>'
        : Object.entries(settings).map(([key, value], index, entries) => {
            const last = index === entries.length - 1;
            return `
                <div class="d-flex justify-content-between gap-3 py-1${last ? '' : ' border-bottom'}">
                    <span class="text-muted" title="${escapeHtml(key)}">${escapeHtml(prettyNames[key] || key)}</span>
                    <span>${escapeHtml(value)}</span>
                </div>`;
        }).join('');

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
    nodesList.style.display = 'none';
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
