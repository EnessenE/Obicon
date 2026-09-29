// Tests page functionality
let tests = [];
let nodes = [];
let pools = [];

// DOM elements
const testsTable = document.getElementById('testsTable');
const testsTableBody = document.getElementById('testsTableBody');
const noTestsMessage = document.getElementById('noTestsMessage');
const testsLoadingMessage = document.getElementById('testsLoadingMessage');
const testsErrorMessage = document.getElementById('testsErrorMessage');
const testsRefreshSpinner = document.getElementById('testsRefreshSpinner');
const nodeSelectList = document.getElementById('nodeSelectList');
const poolSelectList = document.getElementById('poolSelectList');
const testFormError = document.getElementById('testFormError');
const dryRunResult = document.getElementById('dryRunResult');
const dryRunBtn = document.getElementById('dryRunBtn');
const testTypeSelect = document.getElementById('testType');

// Enum mappings
const testTypeMap = {
    0: 'Ping', 1: 'Traceroute', 2: 'HTTP', 3: 'HTTPS', 4: 'TCP', 5: 'DNS'
};

const frequencyMap = {
    0: '10 seconds', 1: '30 seconds', 2: '1 minute',
    3: '2 minutes', 4: '5 minutes', 5: '10 minutes', 6: '1 hour'
};

const jobStatusMap = {
    0: 'Queued', 1: 'Assigned', 2: 'Running', 3: 'Completed', 4: 'Failed', 5: 'Timeout'
};

const frequencySeconds = {
    0: 10, 1: 30, 2: 60, 3: 120, 4: 300, 5: 600, 6: 3600
};

const ipVersionMap = {
    0: 'Any', 1: 'IPv4', 2: 'IPv6'
};

// Load tests, nodes and pools on page load
loadTests();
loadNodeCheckboxes();
loadPoolCheckboxes();
testTypeSelect.addEventListener('change', updateExpectationVisibility);
updateExpectationVisibility();

// Live estimate: recompute whenever targeting or frequency changes
['change', 'click', 'input'].forEach(evt => {
    document.addEventListener(evt, updateEstimate);
});
updateEstimate();

// Number of distinct nodes targeted: selected nodes plus members of selected pools
function estimateTargetCount() {
    const nodeIds = new Set(getSelectedNodeIds());
    pools.forEach(pool => {
        if (getSelectedPoolIds().includes(pool.id)) {
            pool.nodeIds.forEach(id => nodeIds.add(id));
        }
    });
    return nodeIds.size;
}

function formatNumber(n) {
    return n.toLocaleString(undefined, { maximumFractionDigits: 0 });
}

function updateEstimate() {
    const estimateEl = document.getElementById('checkEstimate');
    const targets = estimateTargetCount();
    if (targets === 0) {
        estimateEl.textContent = '-';
        return;
    }

    const seconds = frequencySeconds[parseInt(document.getElementById('testFrequency').value)] || 60;
    const perHour = targets * 3600 / seconds;
    estimateEl.textContent = `${formatNumber(perHour)}/hour, ${formatNumber(perHour * 24)}/day, ${formatNumber(perHour * 24 * 30)}/month (30d)`;
}

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

async function loadNodeCheckboxes() {
    try {
        nodes = await apiCall('GET', '/v1/nodes');
        renderNodeCheckboxes();
    } catch (error) {
        nodeSelectList.innerHTML = `<span class="text-danger">${escapeHtml(error.message)}</span>`;
    }
}

async function loadPoolCheckboxes() {
    try {
        pools = await apiCall('GET', '/v1/pools');
        renderPoolCheckboxes();
    } catch (error) {
        poolSelectList.innerHTML = `<span class="text-danger">${escapeHtml(error.message)}</span>`;
    }
}

function renderNodeCheckboxes() {
    if (nodes.length === 0) {
        nodeSelectList.innerHTML = '<span class="text-muted">No nodes yet. Create one on the Nodes page first.</span>';
        return;
    }

    nodeSelectList.innerHTML = nodes.map(node => `
        <div class="form-check">
            <input class="form-check-input node-checkbox" type="checkbox" value="${node.id}" id="node-${node.id}">
            <label class="form-check-label" for="node-${node.id}">${escapeHtml(node.name)}</label>
        </div>
    `).join('');
}

function renderPoolCheckboxes() {
    if (pools.length === 0) {
        poolSelectList.innerHTML = '<span class="text-muted">No pools yet. Create one on the Pools page first.</span>';
        return;
    }

    poolSelectList.innerHTML = pools.map(pool => `
        <div class="form-check">
            <input class="form-check-input pool-checkbox" type="checkbox" value="${pool.id}" id="pool-${pool.id}">
            <label class="form-check-label" for="pool-${pool.id}">${escapeHtml(pool.name)} <span class="text-muted">(${pool.nodeIds.length})</span></label>
        </div>
    `).join('');
}

// Shows only the expectation inputs relevant for the selected test type
function updateExpectationVisibility() {
    const type = parseInt(testTypeSelect.value);
    document.querySelectorAll('.http-expectation').forEach(el =>
        el.style.display = (type === 2 || type === 3) ? '' : 'none');
    document.querySelectorAll('.https-expectation').forEach(el =>
        el.style.display = (type === 3) ? '' : 'none');
    document.querySelectorAll('.dns-expectation').forEach(el =>
        el.style.display = (type === 5) ? '' : 'none');
}

function getSelectedNodeIds() {
    return Array.from(document.querySelectorAll('.node-checkbox:checked')).map(cb => cb.value);
}

function getSelectedPoolIds() {
    return Array.from(document.querySelectorAll('.pool-checkbox:checked')).map(cb => cb.value);
}

function showFormError(message) {
    testFormError.textContent = message;
    testFormError.style.display = 'block';
    setTimeout(() => { testFormError.style.display = 'none'; }, 8000);
}

// Validates the form; returns the values or null
function validateForm({ requireName = true, requireTargets = true } = {}) {
    const name = document.getElementById('testName').value.trim();
    const target = document.getElementById('testTarget').value.trim();
    const type = parseInt(testTypeSelect.value);
    const frequency = parseInt(document.getElementById('testFrequency').value);
    const isActive = document.getElementById('testIsActive').checked;
    const nodeIds = getSelectedNodeIds();
    const poolIds = getSelectedPoolIds();

    if (requireName && !name) {
        showFormError('Name is required.');
        return null;
    }
    if (!target) {
        showFormError('Target is required.');
        return null;
    }
    if (isNaN(type) || !testTypeMap[type]) {
        showFormError('Invalid test type.');
        return null;
    }
    if (requireTargets && nodeIds.length === 0 && poolIds.length === 0) {
        showFormError('Select at least one node or pool.');
        return null;
    }

    const expectedStatusCodes = document.getElementById('expectedStatusCodes').value.trim() || '200-399';
    if ((type === 2 || type === 3) && !/^\d{3}(-\d{3})?(,\d{3}(-\d{3})?)*$/.test(expectedStatusCodes)) {
        showFormError('Expected status codes must look like 200-399 or 200,301.');
        return null;
    }

    const certDaysInput = document.getElementById('checkCertExpiryDays').value.trim();
    const checkCertExpiryDays = certDaysInput === '' ? null : parseInt(certDaysInput);
    if (checkCertExpiryDays !== null && (isNaN(checkCertExpiryDays) || checkCertExpiryDays < 0)) {
        showFormError('TLS expiry days must be a non-negative number.');
        return null;
    }

    const ipVersion = parseInt(document.getElementById("testIpVersion").value);

    return {
        name, target, type, frequency, isActive, nodeIds, poolIds, ipVersion,
        expectedStatusCodes,
        checkCertExpiryDays,
        expectedDnsResult: document.getElementById('expectedDnsResult').value.trim() || null
    };
}

// Estimated checks for a saved test, based on its direct nodes plus pool members
function testTargetCount(test) {
    const nodeIds = new Set(test.nodeIds || []);
    (test.poolIds || []).forEach(poolId => {
        const pool = pools.find(p => p.id === poolId);
        if (pool) {
            pool.nodeIds.forEach(id => nodeIds.add(id));
        }
    });
    return nodeIds.size;
}

function estimateShort(test) {
    const targets = testTargetCount(test);
    if (targets === 0) return '-';
    const seconds = frequencySeconds[test.frequency] || 60;
    const perHour = targets * 3600 / seconds;
    return `${formatNumber(perHour)}/hr`;
}

function estimateTooltip(test) {
    const targets = testTargetCount(test);
    if (targets === 0) return 'No targets';
    const seconds = frequencySeconds[test.frequency] || 60;
    const perHour = targets * 3600 / seconds;
    return `${targets} target(s): ${formatNumber(perHour)}/hour, ${formatNumber(perHour * 24)}/day, ${formatNumber(perHour * 24 * 30)}/month (30d)`;
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
            <td title="${test.id}">${test.id.substring(0, 8)}</td>
            <td>${escapeHtml(test.name)}</td>
            <td>${testTypeMap[test.type] || test.type}</td>
            <td><code>${escapeHtml(test.target)}</code></td>
            <td>${frequencyMap[test.frequency] || test.frequency}${test.ipVersion ? ' <span class="text-muted">(' + (ipVersionMap[test.ipVersion] || '') + ')</span>' : ''}</td>
            <td title="${estimateTooltip(test)}">${estimateShort(test)}</td>
            <td>
                <span class="badge ${test.isActive ? 'bg-success' : 'bg-secondary'}">${test.isActive ? 'Active' : 'Inactive'}</span>
            </td>
            <td>
                <button class="btn btn-sm ${test.isActive ? 'btn-outline-warning' : 'btn-outline-success'} me-1" onclick="toggleTest('${test.id}')">${test.isActive ? 'Disable' : 'Enable'}</button>
                <button class="btn btn-sm btn-success me-1" onclick="triggerRun('${test.id}')">Run</button>
                <button class="btn btn-sm btn-danger" onclick="deleteTest('${test.id}')">Delete</button>
            </td>
        </tr>
    `).join('');
}

async function createTest() {
    const values = validateForm();
    if (!values) return;

    try {
        await apiCall('POST', '/v1/tests', {
            name: values.name,
            type: values.type,
            target: values.target,
            nodeIds: values.nodeIds,
            poolIds: values.poolIds,
            frequency: values.frequency,
            isActive: values.isActive,
            expectedStatusCodes: values.expectedStatusCodes,
            checkCertificateExpiryDays: values.checkCertExpiryDays,
            expectedDnsResult: values.expectedDnsResult,
            ipVersion: values.ipVersion
        });

        // Reset form
        document.getElementById('testName').value = '';
        document.getElementById('testTarget').value = '';
        document.getElementById('testType').value = '0';
        document.getElementById('testFrequency').value = '2';
        document.getElementById('testIsActive').checked = true;
        document.getElementById('expectedStatusCodes').value = '200-399';
        document.getElementById('checkCertExpiryDays').value = '';
        document.getElementById('expectedDnsResult').value = '';
        document.querySelectorAll('.node-checkbox:checked, .pool-checkbox:checked').forEach(cb => cb.checked = false);
        updateExpectationVisibility();

        await loadTests();
    } catch (error) {
        showTestsError(error.message);
    }
}

// Runs the current form values once on a random node, before the test is created
async function runOnRandomNode() {
    const values = validateForm({ requireName: false, requireTargets: false });
    if (!values) return;

    if (nodes.length === 0) {
        showFormError('No nodes available to run the test on.');
        return;
    }

    const node = nodes[Math.floor(Math.random() * nodes.length)];

    dryRunBtn.disabled = true;
    dryRunBtn.textContent = 'Running...';
    dryRunResult.style.display = 'none';
    dryRunResult.className = 'alert mt-3 mb-0 alert-info';
    dryRunResult.innerHTML = `<strong>Running ${testTypeMap[values.type]}</strong> against <code>${escapeHtml(values.target)}</code> on <strong>${escapeHtml(node.name)}</strong>...`;

    try {
        const job = await apiCall('POST', '/v1/tests/run-once', {
            type: values.type,
            target: values.target,
            nodeId: node.id,
            expectedStatusCodes: values.expectedStatusCodes,
            checkCertificateExpiryDays: values.checkCertExpiryDays,
            expectedDnsResult: values.expectedDnsResult,
            ipVersion: values.ipVersion
        });

        const finished = await pollJob(job.id, 75);
        showDryRunResult(finished, node);
    } catch (error) {
        dryRunResult.className = 'alert mt-3 mb-0 alert-danger';
        dryRunResult.textContent = error.message;
        dryRunResult.style.display = 'block';
    } finally {
        dryRunBtn.disabled = false;
        dryRunBtn.textContent = 'Run once on a random node';
    }
}

function showDryRunResult(job, node) {
    dryRunResult.style.display = 'block';
    dryRunResult.className = `alert mt-3 mb-0 ${job.success ? 'alert-success' : 'alert-danger'}`;
    dryRunResult.innerHTML = `
        <strong>${job.success ? 'Success' : (jobStatusMap[job.status] || 'Failed')}</strong>
        on ${escapeHtml(node.name)} in ${job.durationMs != null ? job.durationMs + ' ms' : '-'}<br>
        <code class="text-break">${escapeHtml(job.output || job.errorMessage || '')}</code>
    `;
}

// Polls a job until it reaches a final state (Completed, Failed, Timeout)
async function pollJob(jobId, maxSeconds) {
    const deadline = Date.now() + maxSeconds * 1000;
    while (Date.now() < deadline) {
        await new Promise(resolve => setTimeout(resolve, 1000));
        const job = await apiCall('GET', `/v1/queue/${jobId}`);
        if (job.status >= 3) {
            return job;
        }
    }
    throw new Error(`Job did not finish within ${maxSeconds}s`);
}

async function deleteTest(id) {
    try {
        await apiCall('DELETE', `/v1/tests/${id}`);
        await loadTests();
    } catch (error) {
        showTestsError(error.message);
    }
}

async function toggleTest(testId) {
    try {
        await apiCall('POST', `/v1/tests/${testId}/toggle`);
        await loadTests();
    } catch (error) {
        showTestsError(error.message);
    }
}

async function triggerRun(testId) {
    try {
        await apiCall('POST', `/v1/tests/${testId}/run`);
        await loadTests();
    } catch (error) {
        showTestsError(error.message);
    }
}

function showTestsLoading() {
    testsLoadingMessage.style.display = 'block';
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
