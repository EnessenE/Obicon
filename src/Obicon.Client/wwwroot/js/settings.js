// Settings page functionality
let settings = [];
let tokens = [];

// DOM elements
const settingsList = document.getElementById('settingsList');
const settingsError = document.getElementById('settingsError');
const tokensTable = document.getElementById('tokensTable');
const tokensTableBody = document.getElementById('tokensTableBody');
const noTokensMessage = document.getElementById('noTokensMessage');
const tokenCreated = document.getElementById('tokenCreated');

// Load everything on page load
loadSettings();
loadTokens();

async function loadSettings() {
    settingsList.innerHTML = '<div class="text-center my-4"><div class="spinner-border"></div></div>';
    try {
        settings = await apiCall('GET', '/v1/settings');
        renderSettings();
    } catch (error) {
        settingsError.textContent = error.message;
        settingsError.style.display = 'block';
    }
}

function renderSettings() {
    settingsList.innerHTML = settings.map(setting => {
        const locked = setting.isForced || setting.isReadOnly;
        const control = isBoolean(setting)
            ? `
                <div class="form-check form-switch">
                    <input class="form-check-input setting-switch" type="checkbox" role="switch"
                           id="setting-input-${escapeHtml(setting.key)}"
                           ${setting.value === 'true' ? 'checked' : ''}
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
    }).join('');
}

function isBoolean(setting) {
    return setting.value === 'true' || setting.value === 'false';
}

// Boolean settings save immediately on toggle; the switch reverts on failure
async function toggleSetting(key, checked, input) {
    try {
        await apiCall('PUT', `/v1/settings/${key}`, { value: checked ? 'true' : 'false' });
    } catch (error) {
        input.checked = !checked;
        settingsError.textContent = error.message;
        settingsError.style.display = 'block';
        setTimeout(() => { settingsError.style.display = 'none'; }, 8000);
    }
}

async function saveSetting(key) {
    const input = document.getElementById(`setting-input-${key}`);
    try {
        await apiCall('PUT', `/v1/settings/${key}`, { value: input.value });
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
        tokensTable.style.display = 'none';
        return;
    }

    noTokensMessage.style.display = 'none';
    tokensTable.style.display = 'table';

    tokensTableBody.innerHTML = tokens.map(token => {
        const revoked = token.revokedAt != null;
        const expired = token.expiresAt != null && new Date(token.expiresAt) <= new Date();
        const state = revoked
            ? '<span class="badge bg-danger">Revoked</span>'
            : expired
                ? '<span class="badge bg-warning text-dark">Expired</span>'
                : '<span class="badge bg-success">Active</span>';

        return `
            <tr>
                <td>${escapeHtml(token.name)}</td>
                <td>${new Date(token.createdAt).toLocaleString()}</td>
                <td>${token.expiresAt ? new Date(token.expiresAt).toLocaleString() : 'Never'}</td>
                <td>${state}</td>
                <td>
                    ${!revoked ? `<button class="btn btn-sm btn-outline-warning me-1" onclick="revokeToken('${token.id}')">Revoke</button>` : ''}
                    <button class="btn btn-sm btn-danger" onclick="deleteToken('${token.id}')">Delete</button>
                </td>
            </tr>
        `;
    }).join('');
}

async function createToken() {
    const name = document.getElementById('tokenName').value.trim();
    const expiryInput = document.getElementById('tokenExpiry').value;

    const body = {};
    if (name) body.name = name;
    if (expiryInput) body.expiresAt = new Date(expiryInput).toISOString();

    try {
        const token = await apiCall('POST', '/v1/enroll-tokens', body);

        document.getElementById('tokenName').value = '';
        document.getElementById('tokenExpiry').value = '';

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
