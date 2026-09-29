// Queue page functionality
let jobs = [];
let autoRefreshTimer = null;

// DOM elements
const queueTable = document.getElementById('queueTable');
const queueTableBody = document.getElementById('queueTableBody');
const noJobsMessage = document.getElementById('noJobsMessage');
const queueLoadingMessage = document.getElementById('queueLoadingMessage');
const queueErrorMessage = document.getElementById('queueErrorMessage');
const queueRefreshSpinner = document.getElementById('queueRefreshSpinner');
const autoRefreshCheckbox = document.getElementById('autoRefresh');

// Job status to badge class
const jobStatusMap = {
    0: 'Queued',
    1: 'Assigned',
    2: 'Running',
    3: 'Completed',
    4: 'Failed',
    5: 'Timeout'
};

const jobStatusBadgeMap = {
    0: 'bg-secondary',
    1: 'bg-info text-dark',
    2: 'bg-primary',
    3: 'bg-success',
    4: 'bg-danger',
    5: 'bg-warning text-dark'
};

// Load queue on page load
toggleAutoRefresh();
loadQueue();

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

function renderJobs() {
    if (jobs.length === 0) {
        noJobsMessage.style.display = 'block';
        queueTable.style.display = 'none';
        return;
    }

    noJobsMessage.style.display = 'none';
    queueTable.style.display = 'table';

    queueTableBody.innerHTML = jobs.map(job => `
        <tr>
            <td title="${job.id}">${job.id.substring(0, 8)}</td>
            <td title="${job.testId}">${job.testId.substring(0, 8)}</td>
            <td title="${job.nodeId}">${job.nodeId.substring(0, 8)}</td>
            <td><span class="badge ${jobStatusBadgeMap[job.status] || 'bg-secondary'}">${jobStatusMap[job.status] || job.status}</span></td>
            <td>${new Date(job.createdAt).toLocaleString()}</td>
            <td>${job.durationMs != null ? job.durationMs + ' ms' : '-'}</td>
            <td class="text-truncate" style="max-width: 300px;" title="${escapeHtml(job.errorMessage || job.output || '')}">${escapeHtml(job.errorMessage || job.output || '-')}</td>
        </tr>
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
