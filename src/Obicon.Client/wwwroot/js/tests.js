// Tests page functionality
let tests = [];
let nodes = [];
let pools = [];

// DOM elements
const testsList = document.getElementById('testsList');
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
    0: 'Ping', 1: 'Traceroute', 2: 'HTTP', 3: 'HTTPS', 4: 'TCP', 5: 'DNS', 6: 'TLS'
};

// All selectable test types; hidden ones come from the EnabledTestTypes server setting
const allTestTypes = [
    { value: 0, label: 'Ping' }, { value: 1, label: 'Traceroute' }, { value: 2, label: 'HTTP' },
    { value: 3, label: 'HTTPS' }, { value: 4, label: 'TCP' }, { value: 5, label: 'DNS' }, { value: 6, label: 'TLS' }
];
let enabledTestTypes = null;

const jobStatusMap = {
    0: 'Queued', 1: 'Assigned', 2: 'Running', 3: 'Completed', 4: 'Failed', 5: 'Timeout'
};

// Allowed test frequencies in seconds; loaded from the FrequencyPresetsSeconds server setting,
// falling back to the server's default list if it cannot be read
let frequencyPresets = [10, 30, 60, 120, 300, 600, 3600];

function formatFrequencyLabel(seconds) {
    if (!Number.isFinite(seconds) || seconds <= 0) return String(seconds);
    if (seconds < 60) return `${seconds} seconds`;
    if (seconds % 3600 === 0) return `${seconds / 3600} hour${seconds === 3600 ? '' : 's'}`;
    if (seconds % 60 === 0) return `${seconds / 60} minute${seconds === 60 ? '' : 's'}`;
    return `${seconds} seconds`;
}

async function loadFrequencyPresets() {
    try {
        const settings = await apiCall('GET', '/v1/settings');
        const setting = settings.find(s => s.key === 'FrequencyPresetsSeconds');
        const parsed = (setting ? setting.value : '')
            .split(',')
            .map(part => parseInt(part.trim(), 10))
            .filter(seconds => Number.isFinite(seconds) && seconds > 0);
        if (parsed.length > 0) {
            frequencyPresets = [...new Set(parsed)].sort((a, b) => a - b);
        }
    } catch (error) {
        console.error('Could not load frequency presets, using defaults:', error);
    }

    const enabledSetting = settings.find(s => s.key === 'EnabledTestTypes');
    enabledTestTypes = parseEnabledTestTypes(enabledSetting ? enabledSetting.value : '');
    applyEnabledTestTypes();

    populateFrequencySelect(document.getElementById('testFrequency'));
    populateFrequencySelect(document.getElementById('editFrequency'));
    updateEstimate();
}

// Parses the EnabledTestTypes setting: a JSON array of TestType names or numbers,
// e.g. ["Ping","Http","Dns"] or [0,2,5]. Null means all types (the default, an
// empty array, or an unparseable value)
function parseEnabledTestTypes(raw) {
    if (!raw || !raw.trim()) {
        return null;
    }

    try {
        const parsed = JSON.parse(raw);
        if (!Array.isArray(parsed)) {
            return null;
        }

        const enabled = new Set();
        for (const entry of parsed) {
            const match = allTestTypes.find(t =>
                t.value === entry || String(entry).toLowerCase() === t.label.toLowerCase());
            if (match) {
                enabled.add(match.value);
            }
        }
        return enabled.size > 0 ? enabled : null;
    } catch (error) {
        console.error('Could not parse EnabledTestTypes, using all types:', error);
        return null;
    }
}

// Hides type options the server disabled; an edit of an existing test of a disabled
// type keeps that type selectable so the modal can still show it
function applyEnabledTestTypes(extraType = null) {
    const enabled = new Set(enabledTestTypes ?? allTestTypes.map(t => t.value));
    if (extraType != null) {
        enabled.add(extraType);
    }
    for (const option of document.querySelectorAll('#testType option, #editType option')) {
        option.style.display = enabled.has(parseInt(option.value, 10)) ? '' : 'none';
    }
}

function populateFrequencySelect(select, extraValue = null) {
    if (!select) return;
    const values = extraValue && !frequencyPresets.includes(extraValue)
        ? [...frequencyPresets, extraValue].sort((a, b) => a - b)
        : frequencyPresets;
    select.innerHTML = values.map(seconds =>
        `<option value="${seconds}">${formatFrequencyLabel(seconds)}</option>`).join('');
}

const ipVersionMap = {
    0: 'Any', 1: 'IPv4', 2: 'IPv6'
};

// Load tests, nodes, pools and the frequency presets on page load
loadTests();
loadNodeCheckboxes();
loadPoolCheckboxes();
loadFrequencyPresets();
testTypeSelect.addEventListener('change', updateExpectationVisibility);
document.getElementById('editType').addEventListener('change', updateEditExpectationVisibility);
updateExpectationVisibility();

// Adds one key/value header row to the given container
function addHeaderRow(containerId, name = '', value = '') {
    const row = document.createElement('div');
    row.className = 'input-group input-group-sm mb-1';
    row.innerHTML = `
        <input class="form-control header-name" placeholder="Name">
        <input class="form-control header-value" placeholder="Value">
        <button class="btn btn-outline-danger" type="button" title="Remove this header"
                onclick="this.closest('.input-group').remove()">Remove</button>`;
    row.querySelector('.header-name').value = name;
    row.querySelector('.header-value').value = value;
    document.getElementById(containerId).appendChild(row);
}

// Collects the header rows into a dictionary; a filled row without a name is an error
function collectHeaderRows(containerId) {
    const headers = {};
    for (const row of document.querySelectorAll(`#${containerId} .input-group`)) {
        const name = row.querySelector('.header-name').value.trim();
        const value = row.querySelector('.header-value').value.trim();
        if (!name && !value) continue;
        if (!name) {
            return { error: 'A header row has an empty name.' };
        }
        headers[name] = value;
    }
    return { headers };
}

function clearHeaderRows(containerId) {
    document.getElementById(containerId).innerHTML = '';
}

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

    const seconds = parseInt(document.getElementById('testFrequency').value) || 60;
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
    // The certificate expiry threshold applies to HTTPS and the TLS test type
    document.querySelectorAll('.https-expectation').forEach(el =>
        el.style.display = (type === 3 || type === 6) ? '' : 'none');
    document.querySelectorAll('.dns-expectation').forEach(el =>
        el.style.display = (type === 5) ? '' : 'none');
    document.querySelectorAll('.traceroute-expectation').forEach(el =>
        el.style.display = (type === 1) ? '' : 'none');
    document.querySelectorAll('.ping-expectation').forEach(el =>
        el.style.display = (type === 0) ? '' : 'none');
}

// Same for the edit modal, driven by its own type select
function updateEditExpectationVisibility() {
    const type = parseInt(document.getElementById('editType').value);
    document.querySelectorAll('#editTestModal .http-expectation').forEach(el =>
        el.style.display = (type === 2 || type === 3) ? '' : 'none');
    document.querySelectorAll('#editTestModal .https-expectation').forEach(el =>
        el.style.display = (type === 3 || type === 6) ? '' : 'none');
    document.querySelectorAll('#editTestModal .dns-expectation').forEach(el =>
        el.style.display = (type === 5) ? '' : 'none');
    document.querySelectorAll('#editTestModal .traceroute-expectation').forEach(el =>
        el.style.display = (type === 1) ? '' : 'none');
    document.querySelectorAll('#editTestModal .ping-expectation').forEach(el =>
        el.style.display = (type === 0) ? '' : 'none');
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

    const timeoutInput = document.getElementById('testTimeout').value.trim();
    const timeoutSeconds = timeoutInput === '' ? 60 : parseInt(timeoutInput);
    if (isNaN(timeoutSeconds) || timeoutSeconds < 1) {
        showFormError('Timeout must be at least 1 second.');
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

    // HTTP-only extras: body regex, headers, proxy, cache busting
    let expectedBodyPattern = null;
    let headers = null;
    let proxyUrl = null;
    let cacheBust = false;
    if (type === 2 || type === 3) {
        expectedBodyPattern = document.getElementById('expectedBodyPattern').value.trim() || null;
        if (expectedBodyPattern) {
            try {
                new RegExp(expectedBodyPattern);
            } catch (error) {
                showFormError('Body pattern is not a valid regular expression.');
                return null;
            }
        }

        const parsed = collectHeaderRows('headerRows');
        if (parsed.error) {
            showFormError(parsed.error);
            return null;
        }
        headers = Object.keys(parsed.headers).length > 0 ? parsed.headers : null;

        proxyUrl = document.getElementById('proxyUrl').value.trim() || null;
        if (proxyUrl && !/^https?:\/\/.+/.test(proxyUrl)) {
            showFormError('Proxy must be an absolute http:// or https:// URL.');
            return null;
        }
        cacheBust = document.getElementById('cacheBust').checked;
    }

    // Traceroute extras: empty inputs mean the node defaults
    let tracerouteMaxHops = null;
    let tracerouteQueriesPerHop = null;
    let tracerouteQueryTimeoutMs = null;
    let tracerouteResolveHostnames = null;
    if (type === 1) {
        tracerouteMaxHops = parseIntOrNull(document.getElementById('tracerouteMaxHops').value);
        tracerouteQueriesPerHop = parseIntOrNull(document.getElementById('tracerouteQueriesPerHop').value);
        tracerouteQueryTimeoutMs = parseIntOrNull(document.getElementById('tracerouteQueryTimeoutMs').value);
        tracerouteResolveHostnames = document.getElementById('tracerouteResolveHostnames').checked ? null : false;
    }

    // Ping extras: empty inputs mean the node defaults
    let pingCount = null;
    let pingTimeoutMs = null;
    let pingIntervalMs = null;
    if (type === 0) {
        pingCount = parseIntOrNull(document.getElementById('pingCount').value);
        pingTimeoutMs = parseIntOrNull(document.getElementById('pingTimeoutMs').value);
        pingIntervalMs = parseIntOrNull(document.getElementById('pingIntervalMs').value);
    }

    // HTTP method and redirect handling ride along with the HTTP extras
    let httpMethod = null;
    let followRedirects = null;
    if (type === 2 || type === 3) {
        httpMethod = document.getElementById('httpMethod').value || null;
        followRedirects = document.getElementById('followRedirects').checked ? null : false;
    }

    const dnsNameserver = document.getElementById('dnsNameserver').value.trim() || null;
    const dnsQueryType = document.getElementById('dnsQueryType').value || null;

    return {
        name, target, type, frequency, isActive, nodeIds, poolIds, ipVersion, timeoutSeconds,
        expectedStatusCodes,
        checkCertExpiryDays,
        expectedDnsResult: document.getElementById('expectedDnsResult').value.trim() || null,
        expectedBodyPattern,
        headers,
        proxyUrl,
        cacheBust,
        tracerouteMaxHops,
        tracerouteQueriesPerHop,
        tracerouteQueryTimeoutMs,
        tracerouteResolveHostnames,
        pingCount,
        pingTimeoutMs,
        pingIntervalMs,
        httpMethod,
        followRedirects,
        dnsNameserver,
        dnsQueryType
    };
}

// Parses an input's value as an integer, null for empty or invalid input
function parseIntOrNull(value) {
    const parsed = parseInt(value, 10);
    return Number.isNaN(parsed) ? null : parsed;
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
    const seconds = test.frequency || 60;
    const perHour = targets * 3600 / seconds;
    return `${formatNumber(perHour)}/hr`;
}

function estimateTooltip(test) {
    const targets = testTargetCount(test);
    if (targets === 0) return 'No targets';
    const seconds = test.frequency || 60;
    const perHour = targets * 3600 / seconds;
    return `${targets} target(s): ${formatNumber(perHour)}/hour, ${formatNumber(perHour * 24)}/day, ${formatNumber(perHour * 24 * 30)}/month (30d)`;
}

function renderTests() {
    if (tests.length === 0) {
        noTestsMessage.style.display = 'block';
        testsList.style.display = 'none';
        return;
    }

    noTestsMessage.style.display = 'none';
    testsList.style.display = 'block';

    testsList.innerHTML = tests.map(test => `
        <div class="row g-2 g-lg-3 list-row px-3">
            <div class="col-12 col-lg-4">
                <div class="fw-semibold">${escapeHtml(test.name)}</div>
                <div class="text-muted small" title="${test.id}"><code class="small">${escapeHtml(test.target)}</code></div>
            </div>
            <div class="col-6 col-lg-1">
                <div class="field-label">Type</div>
                ${testTypeMap[test.type] || test.type}
            </div>
            <div class="col-6 col-lg-2">
                <div class="field-label">Frequency</div>
                ${formatFrequencyLabel(test.frequency)}${test.ipVersion ? ' <span class="text-muted">(' + (ipVersionMap[test.ipVersion] || '') + ')</span>' : ''}
            </div>
            <div class="col-6 col-lg-1">
                <div class="field-label">Est. checks</div>
                <span title="${estimateTooltip(test)}">${estimateShort(test)}</span>
            </div>
            <div class="col-6 col-lg-1">
                <div class="field-label">State</div>
                <span class="badge ${test.isActive ? 'bg-success' : 'bg-secondary'}">${test.isActive ? 'Active' : 'Inactive'}</span>
            </div>
            <div class="col-12 col-lg-3 d-flex flex-wrap align-items-end justify-content-lg-end">
                <button class="btn btn-sm btn-outline-primary me-1 mb-1" onclick="openTestEdit('${test.id}')">Edit</button>
                <button class="btn btn-sm ${test.isActive ? 'btn-outline-warning' : 'btn-outline-success'} me-1 mb-1" onclick="toggleTest('${test.id}')">${test.isActive ? 'Disable' : 'Enable'}</button>
                <button class="btn btn-sm btn-success me-1 mb-1" onclick="triggerRun('${test.id}')">Run</button>
                <button class="btn btn-sm btn-danger mb-1" onclick="deleteTest('${test.id}')">Delete</button>
            </div>
        </div>
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
            ipVersion: values.ipVersion,
            timeoutSeconds: values.timeoutSeconds,
            expectedBodyPattern: values.expectedBodyPattern,
            headers: values.headers,
            proxyUrl: values.proxyUrl,
            cacheBust: values.cacheBust,
            tracerouteMaxHops: values.tracerouteMaxHops,
            tracerouteQueriesPerHop: values.tracerouteQueriesPerHop,
            tracerouteQueryTimeoutMs: values.tracerouteQueryTimeoutMs,
            tracerouteResolveHostnames: values.tracerouteResolveHostnames,
            pingCount: values.pingCount,
            pingTimeoutMs: values.pingTimeoutMs,
            pingIntervalMs: values.pingIntervalMs,
            httpMethod: values.httpMethod,
            followRedirects: values.followRedirects,
            dnsNameserver: values.dnsNameserver,
            dnsQueryType: values.dnsQueryType
        });

        // Reset form
        document.getElementById('testName').value = '';
        document.getElementById('testTarget').value = '';
        document.getElementById('testType').value = '0';
        document.getElementById('testFrequency').value = String(frequencyPresets.find(s => s >= 60) ?? frequencyPresets[0]);
        document.getElementById('testIsActive').checked = true;
        document.getElementById('testTimeout').value = '60';
        document.getElementById('expectedStatusCodes').value = '200-399';
        document.getElementById('checkCertExpiryDays').value = '';
        document.getElementById('expectedDnsResult').value = '';
        document.getElementById('expectedBodyPattern').value = '';
        document.getElementById('testHeaders').value = '';
        document.getElementById('proxyUrl').value = '';
        document.getElementById('cacheBust').checked = false;
        document.getElementById('tracerouteMaxHops').value = '';
        document.getElementById('tracerouteQueriesPerHop').value = '';
        document.getElementById('tracerouteQueryTimeoutMs').value = '';
        document.getElementById('tracerouteResolveHostnames').checked = true;
        document.getElementById('pingCount').value = '';
        document.getElementById('pingTimeoutMs').value = '';
        document.getElementById('pingIntervalMs').value = '';
        document.getElementById('httpMethod').value = '';
        document.getElementById('followRedirects').checked = true;
        document.getElementById('dnsNameserver').value = '';
        document.getElementById('dnsQueryType').value = '';
        clearHeaderRows('headerRows');
        document.querySelectorAll('.node-checkbox:checked, .pool-checkbox:checked').forEach(cb => cb.checked = false);
        updateExpectationVisibility();

        await loadTests();
    } catch (error) {
        showTestsError(error.message);
    }
}

// Runs the current form values once on the selected nodes, before the test is created;
// falls back to a single random node when nothing is selected
async function runOnRandomNode() {
    const values = validateForm({ requireName: false, requireTargets: false });
    if (!values) return;

    const selectedIds = getSelectedNodeIds();
    const selectedPoolIds = getSelectedPoolIds();
    let nodesToRun = nodes.filter(n => selectedIds.includes(n.id));
    if (nodesToRun.length === 0 && selectedPoolIds.length === 0) {
        if (nodes.length === 0) {
            showFormError('No nodes available to run the test on.');
            return;
        }
        nodesToRun = [nodes[Math.floor(Math.random() * nodes.length)]];
    }

    dryRunBtn.disabled = true;
    dryRunBtn.textContent = 'Running...';
    dryRunResult.style.display = 'none';
    dryRunResult.className = 'alert mt-3 mb-0 alert-info';
    const poolNote = selectedPoolIds.length > 0 ? ` (plus the top ${3} least-busy nodes from ${selectedPoolIds.length} selected pool${selectedPoolIds.length === 1 ? '' : 's'})` : '';
    dryRunResult.innerHTML = `<strong>Running ${testTypeMap[values.type]}</strong> against <code>${escapeHtml(values.target)}</code> on ${nodesToRun.map(n => `<strong>${escapeHtml(n.name)}</strong>`).join(', ') || 'pool nodes'}${poolNote}...`;

    try {
        const jobs = await apiCall('POST', '/v1/tests/run-once', {
            type: values.type,
            target: values.target,
            nodeIds: nodesToRun.map(n => n.id),
            poolIds: selectedPoolIds,
            expectedStatusCodes: values.expectedStatusCodes,
            checkCertificateExpiryDays: values.checkCertExpiryDays,
            expectedDnsResult: values.expectedDnsResult,
            ipVersion: values.ipVersion,
            timeoutSeconds: values.timeoutSeconds,
            expectedBodyPattern: values.expectedBodyPattern,
            headers: values.headers,
            proxyUrl: values.proxyUrl,
            cacheBust: values.cacheBust,
            tracerouteMaxHops: values.tracerouteMaxHops,
            tracerouteQueriesPerHop: values.tracerouteQueriesPerHop,
            tracerouteQueryTimeoutMs: values.tracerouteQueryTimeoutMs,
            tracerouteResolveHostnames: values.tracerouteResolveHostnames,
            pingCount: values.pingCount,
            pingTimeoutMs: values.pingTimeoutMs,
            pingIntervalMs: values.pingIntervalMs,
            httpMethod: values.httpMethod,
            followRedirects: values.followRedirects,
            dnsNameserver: values.dnsNameserver,
            dnsQueryType: values.dnsQueryType
        });

        // One job per selected node (direct picks plus the top pool members); poll all
        // and pair each with its node for display
        const finished = await Promise.all(jobs.map(job => pollJob(job.id, 75)));
        const nodeById = Object.fromEntries(nodes.map(n => [n.id, n]));
        showDryRunResult(finished.map(job => ({
            job,
            node: nodeById[job.nodeId] || { name: job.nodeId.substring(0, 8) }
        })));
    } catch (error) {
        dryRunResult.className = 'alert mt-3 mb-0 alert-danger';
        dryRunResult.textContent = error.message;
        dryRunResult.style.display = 'block';
    } finally {
        dryRunBtn.disabled = false;
        dryRunBtn.textContent = 'Dry run on selected nodes';
    }
}

function showDryRunResult(results) {
    const allSuccess = results.every(r => r.job.success);
    dryRunResult.style.display = 'block';
    dryRunResult.className = `alert mt-3 mb-0 ${allSuccess ? 'alert-success' : 'alert-danger'}`;
    dryRunResult.innerHTML = results.map(({ job, node }) => `
        <div>
            <strong>${job.success ? 'Success' : (jobStatusMap[job.status] || 'Failed')}</strong>
            on ${escapeHtml(node.name)} in ${job.durationMs != null ? job.durationMs + ' ms' : '-'}<br>
            <code class="text-break">${escapeHtml(job.output || job.errorMessage || '')}</code>
            ${renderTestDetails(job.details)}
        </div>
    `).join('<hr class="my-2">');
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

// Test editing: opens the edit modal with all fields of the test
let editingTest = null;
let editTestModal = null;

function openTestEditModal(test) {
    editingTest = test;

    document.getElementById('editTestName').textContent = `(${test.name})`;
    document.getElementById('editName').value = test.name;
    applyEnabledTestTypes(test.type);
    document.getElementById('editType').value = String(test.type);
    document.getElementById('editTarget').value = test.target;
    // Keep the test's frequency selectable even if it is no longer part of the presets
    populateFrequencySelect(document.getElementById('editFrequency'), test.frequency);
    document.getElementById('editFrequency').value = String(test.frequency);
    document.getElementById('editTimeout').value = test.timeoutSeconds || 60;
    document.getElementById('editIpVersion').value = String(test.ipVersion || 0);
    document.getElementById('editExpectedStatusCodes').value = test.expectedStatusCodes || '200-399';
    document.getElementById('editCertExpiryDays').value = test.checkCertificateExpiryDays ?? '';
    document.getElementById('editExpectedDnsResult').value = test.expectedDnsResult || '';
    document.getElementById('editExpectedBodyPattern').value = test.expectedBodyPattern || '';
    clearHeaderRows('editHeaderRows');
    for (const [name, value] of Object.entries(test.headers || {})) {
        addHeaderRow('editHeaderRows', name, value);
    }
    document.getElementById('editProxyUrl').value = test.proxyUrl || '';
    document.getElementById('editCacheBust').checked = !!test.cacheBust;
    document.getElementById('editTracerouteMaxHops').value = test.tracerouteMaxHops ?? '';
    document.getElementById('editTracerouteQueriesPerHop').value = test.tracerouteQueriesPerHop ?? '';
    document.getElementById('editTracerouteQueryTimeoutMs').value = test.tracerouteQueryTimeoutMs ?? '';
    document.getElementById('editTracerouteResolveHostnames').checked = test.tracerouteResolveHostnames !== false;
    document.getElementById('editPingCount').value = test.pingCount ?? '';
    document.getElementById('editPingTimeoutMs').value = test.pingTimeoutMs ?? '';
    document.getElementById('editPingIntervalMs').value = test.pingIntervalMs ?? '';
    document.getElementById('editHttpMethod').value = test.httpMethod || '';
    document.getElementById('editFollowRedirects').checked = test.followRedirects !== false;
    document.getElementById('editDnsNameserver').value = test.dnsNameserver || '';
    document.getElementById('editDnsQueryType').value = test.dnsQueryType || '';
    document.getElementById('editIsActive').checked = test.isActive;
    document.getElementById('editError').style.display = 'none';
    updateEditExpectationVisibility();

    document.getElementById('editNodeList').innerHTML = nodes.map(node => `
        <div class="form-check">
            <input class="form-check-input edit-node-checkbox" type="checkbox" value="${node.id}" id="edit-node-${node.id}"
                   ${test.nodeIds.includes(node.id) ? 'checked' : ''}>
            <label class="form-check-label" for="edit-node-${node.id}">${escapeHtml(node.name)}</label>
        </div>
    `).join('') || '<span class="text-muted">No nodes exist.</span>';

    document.getElementById('editPoolList').innerHTML = pools.map(pool => `
        <div class="form-check">
            <input class="form-check-input edit-pool-checkbox" type="checkbox" value="${pool.id}" id="edit-pool-${pool.id}"
                   ${test.poolIds.includes(pool.id) ? 'checked' : ''}>
            <label class="form-check-label" for="edit-pool-${pool.id}">${escapeHtml(pool.name)} <span class="text-muted">(${pool.nodeIds.length})</span></label>
        </div>
    `).join('') || '<span class="text-muted">No pools exist.</span>';

    if (!editTestModal) {
        editTestModal = new bootstrap.Modal(document.getElementById('editTestModal'));
    }
    editTestModal.show();
}

function openTestEdit(testId) {
    const test = tests.find(t => t.id === testId);
    if (test) {
        openTestEditModal(test);
    }
}

async function saveTestEdit() {
    if (!editingTest) return;

    const name = document.getElementById('editName').value.trim();
    const target = document.getElementById('editTarget').value.trim();
    const timeoutSeconds = parseInt(document.getElementById('editTimeout').value);
    const nodeIds = Array.from(document.querySelectorAll('.edit-node-checkbox:checked')).map(cb => cb.value);
    const poolIds = Array.from(document.querySelectorAll('.edit-pool-checkbox:checked')).map(cb => cb.value);
    const type = parseInt(document.getElementById('editType').value);

    const error = document.getElementById('editError');
    if (!name) { error.textContent = 'Name is required.'; error.style.display = 'block'; return; }
    if (!target) { error.textContent = 'Target is required.'; error.style.display = 'block'; return; }
    if (isNaN(timeoutSeconds) || timeoutSeconds < 1) { error.textContent = 'Timeout must be at least 1 second.'; error.style.display = 'block'; return; }
    if (nodeIds.length === 0 && poolIds.length === 0) { error.textContent = 'Select at least one node or pool.'; error.style.display = 'block'; return; }

    // HTTP-only extras, read from the modal's own fields
    let expectedBodyPattern = null;
    let headers = null;
    let proxyUrl = null;
    let cacheBust = false;
    if (type === 2 || type === 3) {
        expectedBodyPattern = document.getElementById('editExpectedBodyPattern').value.trim() || null;
        if (expectedBodyPattern) {
            try {
                new RegExp(expectedBodyPattern);
            } catch (regexError) {
                error.textContent = 'Body pattern is not a valid regular expression.';
                error.style.display = 'block';
                return;
            }
        }

        const parsed = collectHeaderRows('editHeaderRows');
        if (parsed.error) {
            error.textContent = parsed.error;
            error.style.display = 'block';
            return;
        }
        headers = Object.keys(parsed.headers).length > 0 ? parsed.headers : null;

        proxyUrl = document.getElementById('editProxyUrl').value.trim() || null;
        if (proxyUrl && !/^https?:\/\/.+/.test(proxyUrl)) {
            error.textContent = 'Proxy must be an absolute http:// or https:// URL.';
            error.style.display = 'block';
            return;
        }
        cacheBust = document.getElementById('editCacheBust').checked;
    }

    // Traceroute extras, read from the modal's own fields
    let tracerouteMaxHops = null;
    let tracerouteQueriesPerHop = null;
    let tracerouteQueryTimeoutMs = null;
    let tracerouteResolveHostnames = null;
    if (type === 1) {
        tracerouteMaxHops = parseIntOrNull(document.getElementById('editTracerouteMaxHops').value);
        tracerouteQueriesPerHop = parseIntOrNull(document.getElementById('editTracerouteQueriesPerHop').value);
        tracerouteQueryTimeoutMs = parseIntOrNull(document.getElementById('editTracerouteQueryTimeoutMs').value);
        tracerouteResolveHostnames = document.getElementById('editTracerouteResolveHostnames').checked ? null : false;
    }

    // Ping extras, read from the modal's own fields
    let pingCount = null;
    let pingTimeoutMs = null;
    let pingIntervalMs = null;
    if (type === 0) {
        pingCount = parseIntOrNull(document.getElementById('editPingCount').value);
        pingTimeoutMs = parseIntOrNull(document.getElementById('editPingTimeoutMs').value);
        pingIntervalMs = parseIntOrNull(document.getElementById('editPingIntervalMs').value);
    }

    // HTTP method and redirect handling, read from the modal's own fields
    let httpMethod = null;
    let followRedirects = null;
    if (type === 2 || type === 3) {
        httpMethod = document.getElementById('editHttpMethod').value || null;
        followRedirects = document.getElementById('editFollowRedirects').checked ? null : false;
    }

    const dnsNameserver = document.getElementById('editDnsNameserver').value.trim() || null;
    const dnsQueryType = document.getElementById('editDnsQueryType').value || null;

    try {
        await apiCall('PUT', `/v1/tests/${editingTest.id}`, {
            type,
            target,
            nodeIds,
            poolIds,
            frequency: parseInt(document.getElementById('editFrequency').value),
            isActive: document.getElementById('editIsActive').checked,
            expectedStatusCodes: document.getElementById('editExpectedStatusCodes').value.trim() || '200-399',
            checkCertificateExpiryDays: document.getElementById('editCertExpiryDays').value.trim() === ''
                ? null
                : parseInt(document.getElementById('editCertExpiryDays').value),
            expectedDnsResult: document.getElementById('editExpectedDnsResult').value.trim() || null,
            ipVersion: parseInt(document.getElementById('editIpVersion').value),
            timeoutSeconds,
            expectedBodyPattern,
            headers,
            proxyUrl,
            cacheBust,
            tracerouteMaxHops,
            tracerouteQueriesPerHop,
            tracerouteQueryTimeoutMs,
            tracerouteResolveHostnames,
            pingCount,
            pingTimeoutMs,
            pingIntervalMs,
            httpMethod,
            followRedirects,
            dnsNameserver,
            dnsQueryType
        });

        editTestModal.hide();
        await loadTests();
    } catch (requestError) {
        error.textContent = requestError.message;
        error.style.display = 'block';
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
