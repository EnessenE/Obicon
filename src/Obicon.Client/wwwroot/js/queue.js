// Queue page functionality
let jobs = [];
let nodes = [];
let autoRefreshTimer = null;

// DOM elements
const queueList = document.getElementById('queueList');
const noJobsMessage = document.getElementById('noJobsMessage');
const queueLoadingMessage = document.getElementById('queueLoadingMessage');
const queueErrorMessage = document.getElementById('queueErrorMessage');
const queueRefreshSpinner = document.getElementById('queueRefreshSpinner');
const autoRefreshCheckbox = document.getElementById('autoRefresh');
const statusFilter = document.getElementById('statusFilter');
const nodeFilter = document.getElementById('nodeFilter');
const searchFilter = document.getElementById('searchFilter');
const queueFilterCount = document.getElementById('queueFilterCount');

// Job status to badge class
const jobStatusMap = {
    0: 'Queued',
    1: 'Assigned',
    2: 'Running',
    3: 'Completed',
    4: 'Failed',
    5: 'Timeout',
    6: 'No run'
};

const jobStatusBadgeMap = {
    0: 'bg-secondary',
    1: 'bg-info text-dark',
    2: 'bg-primary',
    3: 'bg-success',
    4: 'bg-danger',
    5: 'bg-warning text-dark',
    6: 'bg-dark'
};

// Load queue on page load
toggleAutoRefresh();
loadQueue();
loadNodes();

function toggleAutoRefresh() {
    if (autoRefreshTimer) {
        clearInterval(autoRefreshTimer);
        autoRefreshTimer = null;
    }
    if (autoRefreshCheckbox.checked) {
        autoRefreshTimer = setInterval(loadQueue, 5000);
    }
}

async function loadQueue() {
    showQueueLoading();
    try {
        jobs = await apiCall('GET', '/v1/queue');
        renderJobs();
        showQueueContent();
    } catch (error) {
        showQueueError(error.message);
    }
}

// Node names for the filter dropdown; the list rarely changes, so it loads once
async function loadNodes() {
    try {
        nodes = await apiCall('GET', '/v1/nodes');
        const selected = nodeFilter.value;
        nodeFilter.innerHTML = '<option value="">All nodes</option>' + nodes.map(node =>
            `<option value="${node.id}">${escapeHtml(node.name)}</option>`).join('');
        nodeFilter.value = selected;
    } catch (error) {
        // The queue itself still works without the node filter
        nodeFilter.innerHTML = '<option value="">All nodes</option>';
    }
}

function jobMatchesFilters(job) {
    if (statusFilter.value !== '' && job.status !== Number(statusFilter.value)) {
        return false;
    }
    if (nodeFilter.value !== '' && job.nodeId !== nodeFilter.value) {
        return false;
    }
    const term = searchFilter.value.trim().toLowerCase();
    if (term !== '') {
        const haystack = [job.id, job.testId, job.nodeId, job.target, job.output, job.errorMessage]
            .filter(v => v != null)
            .join(' ').toLowerCase();
        if (!haystack.includes(term)) {
            return false;
        }
    }
    return true;
}

function renderJobs() {
    if (jobs.length === 0) {
        queueFilterCount.textContent = '';
        noJobsMessage.textContent = 'Queue is empty.';
        noJobsMessage.style.display = 'block';
        queueList.style.display = 'none';
        return;
    }

    const filtered = jobs.filter(jobMatchesFilters);
    queueFilterCount.textContent = `${filtered.length} of ${jobs.length}`;
    if (filtered.length === 0) {
        noJobsMessage.textContent = 'No jobs match the current filters.';
        noJobsMessage.style.display = 'block';
        queueList.style.display = 'none';
        return;
    }

    noJobsMessage.style.display = 'none';
    queueList.style.display = 'block';

    queueList.innerHTML = filtered.map(job => `
        <div class="row g-2 g-lg-3 list-row px-3">
            <div class="col-12 col-lg-2">
                <div class="field-label">Job ID</div>
                <span title="${job.id}">${job.id.substring(0, 8)}</span>
                <div class="small text-muted">${new Date(job.createdAt).toLocaleString()}</div>
            </div>
            <div class="col-6 col-lg-2">
                <div class="field-label">Test / Node</div>
                <span title="${job.testId}">${job.testId === '00000000-0000-0000-0000-000000000000' ? 'run-once' : job.testId.substring(0, 8)}</span>
                <span class="text-muted"> · </span>
                <span title="${job.nodeId}">${job.nodeId.substring(0, 8)}</span>
                ${job.ipVersion === 1 || job.ipVersion === 2 ? `<span class="badge bg-light text-dark border" title="This run is pinned to one IP family">${job.ipVersion === 1 ? 'v4' : 'v6'}</span>` : ''}
            </div>
            <div class="col-6 col-lg-1">
                <div class="field-label">Status</div>
                <span class="badge ${jobStatusBadgeMap[job.status] || 'bg-secondary'}">${jobStatusMap[job.status] || job.status}</span>
            </div>
            <div class="col-6 col-lg-1">
                <div class="field-label">Duration</div>
                ${job.durationMs != null ? job.durationMs + ' ms' : '-'}
            </div>
            <div class="col-12 col-lg-6">
                <div class="field-label">Result</div>
                <code class="text-break">${escapeHtml(job.errorMessage || job.output || '-')}</code>
                ${renderTestDetails(job.details)}
            </div>
        </div>
    `).join('');
}

function showQueueLoading() {
    queueLoadingMessage.style.display = 'block';
    queueErrorMessage.style.display = 'none';
}

function showQueueContent() {
    queueLoadingMessage.style.display = 'none';
}

function showQueueError(message) {
    queueErrorMessage.textContent = message;
    queueErrorMessage.style.display = 'block';
    queueLoadingMessage.style.display = 'none';
}

// Helper to escape HTML
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
