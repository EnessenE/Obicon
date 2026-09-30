// Server page functionality: health check + server statistics
let autoRefreshTimer = null;

// DOM elements
const serverSpinner = document.getElementById('serverSpinner');
const serverError = document.getElementById('serverError');
const autoRefreshCheckbox = document.getElementById('autoRefresh');

// Load everything on page load
toggleAutoRefresh();
loadAll();

function toggleAutoRefresh() {
    if (autoRefreshTimer) {
        clearInterval(autoRefreshTimer);
        autoRefreshTimer = null;
    }
    if (autoRefreshCheckbox.checked) {
        autoRefreshTimer = setInterval(loadAll, 5000);
    }
}

async function loadAll() {
    serverSpinner.style.display = 'inline-block';
    serverError.style.display = 'none';

    const [health, stats] = await Promise.allSettled([
        apiCall('GET', '/v1/health'),
        apiCall('GET', '/v1/server/stats')
    ]);

    if (health.status === 'fulfilled') {
        document.getElementById('healthStatus').textContent = health.value.status;
        document.getElementById('healthTimestamp').textContent = new Date().toLocaleString();
    }

    if (stats.status === 'fulfilled') {
        document.getElementById('healthUptime').textContent = formatUptime(stats.value.uptime);
        setText('statTotalNodes', stats.value.totalNodes);
        setText('statConnectedNodes', stats.value.connectedNodes);
        setText('statTotalTests', stats.value.totalTests);
        setText('statActiveTests', stats.value.activeTests);
        setText('statQueuedJobs', stats.value.queuedJobs);
        setText('statRunningJobs', stats.value.runningJobs);
        setText('statCompletedJobs', stats.value.completedJobs);
        setText('statFailedJobs', stats.value.failedJobs);
        setText('statTimedOutJobs', stats.value.timedOutJobs);
        setText('statNoRunJobs', stats.value.noRunJobs);
    }

    const failures = [health, stats].filter(r => r.status === 'rejected');
    if (failures.length > 0) {
        serverError.textContent = failures.map(f => f.reason.message).join(' / ');
        serverError.style.display = 'block';
    }

    serverSpinner.style.display = 'none';
}

function setText(id, value) {
    document.getElementById(id).textContent = value;
}

function formatUptime(uptime) {
    // "hh:mm:ss" or "d.hh:mm:ss" TimeSpan format
    const parts = uptime.split('.');
    const time = (parts.length > 1 ? parts[1] : parts[0]);
    return parts.length > 1 ? `${parts[0]}d ${time}` : time;
}
