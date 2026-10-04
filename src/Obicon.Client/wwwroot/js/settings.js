// Settings page functionality
let settings = [];
let tokens = [];
let pools = [];
// Test types with their enabled state for the EnabledTestTypes toggles; null when the
// endpoint failed (the setting then falls back to its raw text input)
let testTypes = null;
let testTypesError = null;

// Labels selectable through TestMetricsLabels; test_id and the counter's status are
// always attached and not offered as choices
const metricLabels = ['test_type', 'test_name', 'node_id', 'node_name', 'node_labels'];

// DOM elements
const settingsList = document.getElementById('settingsList');
const settingsError = document.getElementById('settingsError');
const tokensList = document.getElementById('tokensList');
const noTokensMessage = document.getElementById('noTokensMessage');
const tokenCreated = document.getElementById('tokenCreated');

// Load everything on page load
loadSettings();
loadTokens();
loadPools();

// Pools are used to scope enroll tokens and to show the scope in the tokens table
async function loadPools() {
    try {
        pools = await apiCall('GET', '/v1/pools');
        const select = document.getElementById('tokenPool');
        const previous = select.value;
        select.innerHTML = '<option value="">Entire server (any pool)</option>' +
            pools.map(pool => `<option value="${pool.id}">${escapeHtml(pool.name)}</option>`).join('');
        if (previous) {
            select.value = previous;
        }
    } catch (error) {
        console.error('Could not load pools:', error);
    }
}

async function loadSettings() {
    settingsList.innerHTML = '<div class="text-center my-4"><div class="spinner-border"></div></div>';
    try {
        settings = await apiCall('GET', '/v1/settings');
    } catch (error) {
        settingsError.textContent = error.message;
        settingsError.style.display = 'block';
        return;
    }

    // The test type list backs the EnabledTestTypes toggles; a failure (e.g. a broken
    // setting value) must not take the whole settings page down with it
    testTypes = null;
    testTypesError = null;
    try {
        testTypes = await apiCall('GET', '/v1/tests/types');
    } catch (error) {
        testTypesError = error.message;
    }

    renderSettings();
}

function renderSettings() {
    // Settings arrive in definition order; group them into sections for display
    const groups = new Map();
    for (const setting of settings) {
        const group = setting.group || 'General';
        if (!groups.has(group)) {
            groups.set(group, []);
        }
        groups.get(group).push(setting);
    }

    settingsList.innerHTML = [...groups.entries()].map(([group, groupSettings]) => `
        <h6 class="text-uppercase text-muted small mt-4 mb-3 border-bottom pb-1">${escapeHtml(group)}</h6>
        ${groupSettings.map(renderSettingRow).join('')}
    `).join('');
}

function renderSettingRow(setting) {
    if (setting.key === 'EnabledTestTypes' && testTypes) {
        return renderTestTypesRow(setting);
    }
    if (setting.key === 'TestMetricsLabels') {
        return renderMetricLabelsRow(setting);
    }
    if (setting.key === 'TestResultStorageMode') {
        return renderStorageModeRow(setting);
    }
    if (setting.key === 'FrequencyPresetsSeconds') {
        return renderFrequencyPresetsRow(setting);
    }

    const locked = setting.isForced || setting.isReadOnly;
    const control = isBoolean(setting)
        ? `
            <div class="form-check form-switch">
                <input class="form-check-input setting-switch" type="checkbox" role="switch"
                       id="setting-input-${escapeHtml(setting.key)}"
                       ${setting.value === true ? 'checked' : ''}
                       ${locked ? 'disabled' : ''}
                       onchange="toggleSetting('${escapeHtml(setting.key)}', this.checked, this)">
            </div>`
        : `
            <input id="setting-input-${escapeHtml(setting.key)}"
                   class="form-control${locked ? ' bg-body-tertiary text-secondary' : ''}"
                   value="${escapeHtml(setting.value)}"
                   ${locked ? 'readonly' : ''}>`;

    const action = locked
        ? '<span class="text-muted small">Read-only</span>'
        : isBoolean(setting)
            ? ''
            : `<button class="btn btn-sm btn-primary" onclick="saveSetting('${escapeHtml(setting.key)}')">Save</button>`;

    const badge = setting.isReadOnly
        ? '<span class="badge bg-secondary ms-1" title="Derived from other settings; cannot be changed directly">Derived</span>'
        : setting.isForced
            ? '<span class="badge bg-secondary ms-1" title="Pinned by appsettings or an environment variable; cannot be changed here">Forced by configuration</span>'
            : `<span class="badge bg-light text-dark border ms-1">${escapeHtml(setting.source)}</span>`;

    return `
        <div class="row align-items-center mb-3 pb-3 border-bottom">
            <div class="col-md-5">
                <strong>${escapeHtml(setting.key)}</strong>
                ${badge}
                <div class="small text-muted">${escapeHtml(setting.description)}</div>
            </div>
            <div class="col-md-4">
                ${control}
            </div>
            <div class="col-md-3 text-end">
                ${action}
            </div>
        </div>
    `;
}

// One row of per-type switches for EnabledTestTypes; the enabled list is recomputed
// from every switch on each change, so concurrent toggles are kept
function renderTestTypesRow(setting) {
    const locked = setting.isForced || setting.isReadOnly;
    const switches = testTypes.map(t => `
        <div class="form-check form-switch mb-0">
            <input class="form-check-input test-type-switch" type="checkbox" role="switch"
                   id="test-type-${escapeHtml(t.name)}" ${t.enabled ? 'checked' : ''}
                   ${locked ? 'disabled' : ''}
                   onchange="toggleTestType('${escapeHtml(t.name)}', this.checked, this)">
            <label class="form-check-label" for="test-type-${escapeHtml(t.name)}">${escapeHtml(t.name)}</label>
        </div>`).join('');

    const badge = setting.isForced
        ? '<span class="badge bg-secondary ms-1" title="Pinned by appsettings or an environment variable; cannot be changed here">Forced by configuration</span>'
        : `<span class="badge bg-light text-dark border ms-1">${escapeHtml(setting.source)}</span>`;

    return `
        <div class="row align-items-center mb-3 pb-3 border-bottom">
            <div class="col-md-5">
                <strong>${escapeHtml(setting.key)}</strong>
                ${badge}
                <div class="small text-muted">${escapeHtml(setting.description)}</div>
            </div>
            <div class="col-md-7">
                <div class="d-flex flex-wrap gap-3">${switches}</div>
            </div>
        </div>
    `;
}

async function toggleTestType(name, checked, input) {
    // The switches are the source of truth: collect every checked type name
    const enabled = testTypes
        .filter(t => document.getElementById(`test-type-${t.name}`).checked)
        .map(t => t.name);

    try {
        await apiCall('PUT', '/v1/settings/EnabledTestTypes', { value: enabled });
    } catch (error) {
        input.checked = !checked;
        settingsError.textContent = error.message;
        settingsError.style.display = 'block';
        setTimeout(() => { settingsError.style.display = 'none'; }, 8000);
    }
}

// One row of per-label switches for TestMetricsLabels, plus the permanently disabled
// job_id switch that documents why it can never be a metric label
function renderMetricLabelsRow(setting) {
    const locked = setting.isForced || setting.isReadOnly;
    if (!Array.isArray(setting.value)) {
        // An unexpected shape falls back to the raw text input, like a broken setting
        return renderRawSettingRow(setting);
    }
    const selected = setting.value;

    const switches = metricLabels.map(label => `
        <div class="form-check form-switch mb-0">
            <input class="form-check-input metric-label-switch" type="checkbox" role="switch"
                   id="metric-label-${escapeHtml(label)}" ${selected.includes(label) ? 'checked' : ''}
                   ${locked ? 'disabled' : ''}
                   onchange="toggleMetricLabel('${escapeHtml(label)}', this.checked, this)">
            <label class="form-check-label" for="metric-label-${escapeHtml(label)}">${escapeHtml(label)}</label>
        </div>`).join('') + `
        <div class="form-check form-switch mb-0" title="High cardinality: one series per run - not supported">
            <input class="form-check-input" type="checkbox" role="switch" disabled>
            <label class="form-check-label text-muted">job_id</label>
        </div>
        <div class="small text-muted w-100 mt-1">test_id and status are always attached</div>`;

    const badge = setting.isForced
        ? '<span class="badge bg-secondary ms-1" title="Pinned by appsettings or an environment variable; cannot be changed here">Forced by configuration</span>'
        : `<span class="badge bg-light text-dark border ms-1">${escapeHtml(setting.source)}</span>`;

    return `
        <div class="row align-items-center mb-3 pb-3 border-bottom">
            <div class="col-md-5">
                <strong>${escapeHtml(setting.key)}</strong>
                ${badge}
                <div class="small text-muted">${escapeHtml(setting.description)}</div>
            </div>
            <div class="col-md-7">
                <div class="d-flex flex-wrap gap-3">${switches}</div>
            </div>
        </div>
    `;
}

async function toggleMetricLabel(label, checked, input) {
    // The switches are the source of truth: collect every checked label name
    const selected = metricLabels
        .filter(l => document.getElementById(`metric-label-${l}`).checked);

    try {
        await apiCall('PUT', '/v1/settings/TestMetricsLabels', { value: selected });
    } catch (error) {
        input.checked = !checked;
        settingsError.textContent = error.message;
        settingsError.style.display = 'block';
        setTimeout(() => { settingsError.style.display = 'none'; }, 8000);
    }
}

// A select for TestResultStorageMode: how finished jobs are stored in the database
function renderStorageModeRow(setting) {
    const locked = setting.isForced || setting.isReadOnly;
    const modes = [
        ['Full', 'Full - keep the complete result payload'],
        ['MetadataOnly', 'MetadataOnly - keep the row, drop the payload'],
        ['None', 'None - delete the row on completion']
    ];
    const control = `
        <select id="setting-input-${escapeHtml(setting.key)}"
                class="form-select${locked ? ' bg-body-tertiary text-secondary' : ''}"
                ${locked ? 'disabled' : ''}
                onchange="saveStorageMode('${escapeHtml(setting.key)}', this.value, this)">
            ${modes.map(([value, text]) =>
                `<option value="${value}" ${setting.value === value ? 'selected' : ''}>${text}</option>`).join('')}
        </select>`;

    const badge = setting.isForced
        ? '<span class="badge bg-secondary ms-1" title="Pinned by appsettings or an environment variable; cannot be changed here">Forced by configuration</span>'
        : `<span class="badge bg-light text-dark border ms-1">${escapeHtml(setting.source)}</span>`;

    return `
        <div class="row align-items-center mb-3 pb-3 border-bottom">
            <div class="col-md-5">
                <strong>${escapeHtml(setting.key)}</strong>
                ${badge}
                <div class="small text-muted">${escapeHtml(setting.description)}</div>
            </div>
            <div class="col-md-4">${control}</div>
            <div class="col-md-3 text-end">
                ${locked ? '<span class="text-muted small">Read-only</span>' : ''}
            </div>
        </div>
    `;
}

async function saveStorageMode(key, value, select) {
    try {
        await apiCall('PUT', `/v1/settings/${key}`, { value });
    } catch (error) {
        settingsError.textContent = error.message;
        settingsError.style.display = 'block';
        setTimeout(() => { settingsError.style.display = 'none'; }, 8000);
        await loadSettings();
    }
}

// Renders a setting as its plain text input, used as the fallback for special rows
// whose value cannot be rendered as switches
function renderRawSettingRow(setting) {
    const locked = setting.isForced || setting.isReadOnly;
    return `
        <div class="row align-items-center mb-3 pb-3 border-bottom">
            <div class="col-md-5">
                <strong>${escapeHtml(setting.key)}</strong>
                <div class="small text-muted">${escapeHtml(setting.description)}</div>
            </div>
            <div class="col-md-4">
                <input id="setting-input-${escapeHtml(setting.key)}"
                       class="form-control${locked ? ' bg-body-tertiary text-secondary' : ''}"
                       value="${escapeHtml(setting.value)}"
                       ${locked ? 'readonly' : ''}>
            </div>
            <div class="col-md-3 text-end">
                ${locked ? '<span class="text-muted small">Read-only</span>' : `<button class="btn btn-sm btn-primary" onclick="saveSetting('${escapeHtml(setting.key)}')">Save</button>`}
            </div>
        </div>
    `;
}

function isBoolean(setting) {
    return setting.value === 'true' || setting.value === 'false';
}

// Boolean settings save immediately on toggle; the switch reverts on failure
async function toggleSetting(key, checked, input) {
    try {
        await apiCall('PUT', `/v1/settings/${key}`, { value: checked });
    } catch (error) {
        input.checked = !checked;
        settingsError.textContent = error.message;
        settingsError.style.display = 'block';
        setTimeout(() => { settingsError.style.display = 'none'; }, 8000);
    }
}

// The frequency presets row: a comma-separated text field over the int-list setting
function renderFrequencyPresetsRow(setting) {
    const locked = setting.isForced || setting.isReadOnly;
    const values = Array.isArray(setting.value) ? setting.value.join(', ') : String(setting.value ?? '');
    return `
        <div class="row align-items-center mb-3 pb-3 border-bottom">
            <div class="col-md-5">
                <strong>${escapeHtml(setting.key)}</strong>
                ${setting.isForced ? '<span class="badge bg-secondary ms-1" title="Pinned by appsettings or an environment variable; cannot be changed here">Forced by configuration</span>' : `<span class="badge bg-light text-dark border ms-1">${escapeHtml(setting.source)}</span>`}
                <div class="small text-muted">${escapeHtml(setting.description)}</div>
            </div>
            <div class="col-md-4">
                <input id="setting-input-${escapeHtml(setting.key)}" class="form-control"
                       value="${escapeHtml(values)}" placeholder="e.g. 15, 45, 3600" ${locked ? 'readonly' : ''}>
            </div>
            <div class="col-md-3 text-end">
                ${locked ? '<span class="text-muted small">Read-only</span>' : `<button class="btn btn-sm btn-primary" onclick="saveSetting('${escapeHtml(setting.key)}')">Save</button>`}
            </div>
        </div>
    `;
}

async function saveSetting(key) {
    const input = document.getElementById(`setting-input-${key}`);
    let value = input.value;
    if (key === 'FrequencyPresetsSeconds') {
        value = value.split(',')
            .map(part => parseInt(part.trim(), 10))
            .filter(seconds => Number.isFinite(seconds) && seconds > 0);
    }
    try {
        await apiCall('PUT', `/v1/settings/${key}`, { value });
        await loadSettings();
    } catch (error) {
        settingsError.textContent = error.message;
        settingsError.style.display = 'block';
        setTimeout(() => { settingsError.style.display = 'none'; }, 8000);
    }
}

async function loadTokens() {
    try {
        tokens = await apiCall('GET', '/v1/enroll-tokens');
        renderTokens();
    } catch (error) {
        settingsError.textContent = error.message;
        settingsError.style.display = 'block';
    }
}

function renderTokens() {
    if (tokens.length === 0) {
        noTokensMessage.style.display = 'block';
        tokensList.style.display = 'none';
        return;
    }

    noTokensMessage.style.display = 'none';
    tokensList.style.display = 'block';

    tokensList.innerHTML = tokens.map(token => {
        const revoked = token.revokedAt != null;
        const expired = token.expiresAt != null && new Date(token.expiresAt) <= new Date();
        const state = revoked
            ? '<span class="badge bg-danger">Revoked</span>'
            : expired
                ? '<span class="badge bg-warning text-dark">Expired</span>'
                : '<span class="badge bg-success">Active</span>';

        return `
            <div class="row g-2 g-lg-3 list-row px-3">
                <div class="col-12 col-lg-3">
                    <div class="fw-semibold">${escapeHtml(token.name)}</div>
                    <div class="text-muted small">Created ${new Date(token.createdAt).toLocaleString()}</div>
                </div>
                <div class="col-6 col-lg-2">
                    <div class="field-label">Expires</div>
                    ${token.expiresAt ? new Date(token.expiresAt).toLocaleString() : 'Never'}
                </div>
                <div class="col-6 col-lg-2">
                    <div class="field-label">Scope</div>
                    ${renderTokenScope(token.poolId)}
                </div>
                <div class="col-6 col-lg-2">
                    <div class="field-label">State</div>
                    ${state}
                </div>
                <div class="col-12 col-lg-3 d-flex align-items-end justify-content-lg-end">
                    ${!revoked ? `<button class="btn btn-sm btn-outline-warning me-1" onclick="revokeToken('${token.id}')">Revoke</button>` : ''}
                    <button class="btn btn-sm btn-danger" onclick="deleteToken('${token.id}')">Delete</button>
                </div>
            </div>
        `;
    }).join('');
}

// Shows the pool a token is scoped to, or "Server" when it enrolls into any pool
function renderTokenScope(poolId) {
    if (!poolId) {
        return '<span class="badge bg-light text-dark border">Server</span>';
    }
    const pool = pools.find(p => p.id === poolId);
    return pool
        ? `<span class="badge bg-info text-dark" title="Enrolled nodes are always added to this pool">${escapeHtml(pool.name)}</span>`
        : `<span class="badge bg-info text-dark" title="${poolId}">Pool</span>`;
}

async function createToken() {
    const name = document.getElementById('tokenName').value.trim();
    const expiryInput = document.getElementById('tokenExpiry').value;
    const poolId = document.getElementById('tokenPool').value;

    const body = {};
    if (name) body.name = name;
    if (expiryInput) body.expiresAt = new Date(expiryInput).toISOString();
    if (poolId) body.poolId = poolId;

    try {
        const token = await apiCall('POST', '/v1/enroll-tokens', body);

        document.getElementById('tokenName').value = '';
        document.getElementById('tokenExpiry').value = '';
        document.getElementById('tokenPool').value = '';

        document.getElementById('tokenCreatedName').textContent = token.name;
        document.getElementById('tokenCreatedValue').textContent = token.token;
        tokenCreated.style.display = 'block';

        await loadTokens();
    } catch (error) {
        settingsError.textContent = error.message;
        settingsError.style.display = 'block';
        setTimeout(() => { settingsError.style.display = 'none'; }, 8000);
    }
}

async function revokeToken(id) {
    try {
        await apiCall('POST', `/v1/enroll-tokens/${id}/revoke`);
        await loadTokens();
    } catch (error) {
        settingsError.textContent = error.message;
        settingsError.style.display = 'block';
    }
}

async function deleteToken(id) {
    try {
        await apiCall('DELETE', `/v1/enroll-tokens/${id}`);
        await loadTokens();
    } catch (error) {
        settingsError.textContent = error.message;
        settingsError.style.display = 'block';
    }
}

async function copyNewToken() {
    await copyText(document.getElementById('tokenCreatedValue').textContent, document.getElementById('copyTokenBtn'));
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

// Helper to escape HTML
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
