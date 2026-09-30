// Pools page functionality
let pools = [];
let allNodes = [];
let editingPool = null;
let membersModal = null;

// DOM elements
const poolsTable = document.getElementById('poolsTable');
const poolsTableBody = document.getElementById('poolsTableBody');
const noPoolsMessage = document.getElementById('noPoolsMessage');
const poolsError = document.getElementById('poolsError');
const poolsRefreshSpinner = document.getElementById('poolsRefreshSpinner');

// Load pools and nodes on page load
loadAll();

async function loadAll() {
    poolsRefreshSpinner.style.display = 'inline-block';
    poolsError.style.display = 'none';
    try {
        const [poolResults, nodeResults] = await Promise.allSettled([
            apiCall('GET', '/v1/pools'),
            apiCall('GET', '/v1/nodes')
        ]);

        if (poolResults.status === 'fulfilled') {
            pools = poolResults.value;
            renderPools();
        }
        if (nodeResults.status === 'fulfilled') {
            allNodes = nodeResults.value;
        }

        const failures = [poolResults, nodeResults].filter(r => r.status === 'rejected');
        if (failures.length > 0) {
            showPoolsError(failures.map(f => f.reason.message).join(' / '));
        }
    } finally {
        poolsRefreshSpinner.style.display = 'none';
    }
}

function renderPools() {
    if (pools.length === 0) {
        noPoolsMessage.style.display = 'block';
        poolsTable.style.display = 'none';
        return;
    }

    noPoolsMessage.style.display = 'none';
    poolsTable.style.display = 'table';

    poolsTableBody.innerHTML = pools.map(pool => `
        <tr>
            <td>${escapeHtml(pool.name)}</td>
            <td>${pool.nodeIds.length} node${pool.nodeIds.length === 1 ? '' : 's'}</td>
            <td>
                <button class="btn btn-sm btn-primary me-1" onclick="openMembersModal('${pool.id}')">Nodes</button>
                <button class="btn btn-sm btn-danger" onclick="deletePool('${pool.id}')">Delete</button>
            </td>
        </tr>
    `).join('');
}

async function createPool() {
    const name = document.getElementById('newPoolName').value.trim();
    if (!name) {
        showPoolsError('Pool name is required.');
        return;
    }

    try {
        await apiCall('POST', '/v1/pools', { name });
        document.getElementById('newPoolName').value = '';
        await loadAll();
    } catch (error) {
        showPoolsError(error.message);
    }
}

async function deletePool(id) {
    try {
        await apiCall('DELETE', `/v1/pools/${id}`);
        await loadAll();
    } catch (error) {
        showPoolsError(error.message);
    }
}

function openMembersModal(poolId) {
    editingPool = pools.find(p => p.id === poolId);
    if (!editingPool) return;

    document.getElementById('membersPoolName').textContent = editingPool.name;

    if (allNodes.length === 0) {
        document.getElementById('membersNodeList').innerHTML =
            '<span class="text-muted">No nodes exist yet. Create nodes on the Nodes page first.</span>';
    } else {
        document.getElementById('membersNodeList').innerHTML = allNodes.map(node => `
            <div class="form-check">
                <input class="form-check-input member-checkbox" type="checkbox" value="${node.id}"
                       id="member-${node.id}" ${editingPool.nodeIds.includes(node.id) ? 'checked' : ''}>
                <label class="form-check-label" for="member-${node.id}">${escapeHtml(node.name)}</label>
            </div>
        `).join('');
    }

    if (!membersModal) {
        membersModal = new bootstrap.Modal(document.getElementById('membersModal'));
    }
    membersModal.show();
}

async function saveMembers() {
    if (!editingPool) return;

    const nodeIds = Array.from(document.querySelectorAll('.member-checkbox:checked')).map(cb => cb.value);

    try {
        await apiCall('PUT', `/v1/pools/${editingPool.id}/nodes`, { nodeIds });
        membersModal.hide();
        await loadAll();
    } catch (error) {
        showPoolsError(error.message);
        membersModal.hide();
    }
}

function showPoolsError(message) {
    poolsError.textContent = message;
    poolsError.style.display = 'block';
}

// Helper to escape HTML
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
