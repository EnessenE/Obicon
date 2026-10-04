// Pools page functionality
let pools = [];
let allNodes = [];
let editingPool = null;
let membersModal = null;
let editPoolModal = null;

// DOM elements
const poolsList = document.getElementById('poolsList');
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
            pools = poolResults.value.items;
            renderPools();
        }
        if (nodeResults.status === 'fulfilled') {
            allNodes = nodeResults.value.items;
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
        poolsList.style.display = 'none';
        return;
    }

    noPoolsMessage.style.display = 'none';
    poolsList.style.display = 'block';

    poolsList.innerHTML = pools.map(pool => `
        <div class="row g-2 g-lg-3 list-row px-3">
            <div class="col-12 col-lg-3">
                <div class="fw-semibold">${escapeHtml(pool.name)}</div>
                <div class="text-muted small">Created ${new Date(pool.createdAt).toLocaleString()}</div>
            </div>
            <div class="col-12 col-lg-5">
                <div class="field-label">Description</div>
                <span class="text-muted">${escapeHtml(pool.description || '-')}</span>
            </div>
            <div class="col-6 col-lg-1">
                <div class="field-label">Nodes</div>
                ${pool.nodeIds.length} node${pool.nodeIds.length === 1 ? '' : 's'}
            </div>
            <div class="col-6 col-lg-3 d-flex align-items-end justify-content-end">
                <button class="btn btn-sm btn-primary me-1" onclick="openMembersModal('${pool.id}')">Nodes</button>
                <button class="btn btn-sm btn-outline-primary me-1" onclick="openEditPoolModal('${pool.id}')">Edit</button>
                <button class="btn btn-sm btn-danger" onclick="deletePool('${pool.id}')">Delete</button>
            </div>
        </div>
    `).join('');
}

async function createPool() {
    const name = document.getElementById('newPoolName').value.trim();
    const description = document.getElementById('newPoolDescription').value.trim();
    if (!name) {
        showPoolsError('Pool name is required.');
        return;
    }

    try {
        await apiCall('POST', '/v1/pools', { name, description });
        document.getElementById('newPoolName').value = '';
        document.getElementById('newPoolDescription').value = '';
        await loadAll();
    } catch (error) {
        showPoolsError(error.message);
    }
}

function openEditPoolModal(poolId) {
    editingPool = pools.find(p => p.id === poolId);
    if (!editingPool) return;

    document.getElementById('editPoolName').value = editingPool.name;
    document.getElementById('editPoolDescription').value = editingPool.description || '';
    document.getElementById('editPoolError').style.display = 'none';

    if (!editPoolModal) {
        editPoolModal = new bootstrap.Modal(document.getElementById('editPoolModal'));
    }
    editPoolModal.show();
}

async function savePoolEdit() {
    if (!editingPool) return;

    const name = document.getElementById('editPoolName').value.trim();
    const description = document.getElementById('editPoolDescription').value.trim();

    if (!name) {
        const error = document.getElementById('editPoolError');
        error.textContent = 'Pool name is required.';
        error.style.display = 'block';
        return;
    }

    try {
        await apiCall('PUT', `/v1/pools/${editingPool.id}`, { name, description });
        editPoolModal.hide();
        await loadAll();
    } catch (error) {
        const errorDiv = document.getElementById('editPoolError');
        errorDiv.textContent = error.message;
        errorDiv.style.display = 'block';
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
