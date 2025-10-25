import { apiClient } from '../api-client.js';

let errorLog = null;
let selectedError = null;
let currentErrorTypeFilter = null;
let currentSearchTerm = '';

async function loadErrors() {
    try {
        const params = new URLSearchParams();
        if (currentErrorTypeFilter) {
            params.append('errorType', currentErrorTypeFilter);
        }
        if (currentSearchTerm) {
            params.append('search', currentSearchTerm);
        }

        errorLog = await apiClient.get(`/api/errors?${params.toString()}`);
        renderErrorList();
        renderStatistics();
    } catch (err) {
        console.error('Failed to load errors:', err);
        errorLog = null;
    }
}

function getSeverityClass(severity) {
    switch (severity.toLowerCase()) {
        case 'critical': return 'severity-critical';
        case 'error': return 'severity-error';
        case 'warning': return 'severity-warning';
        default: return 'severity-info';
    }
}

function formatTimestamp(timestamp) {
    const date = new Date(timestamp);
    const now = new Date();
    const diffMs = now - date;
    const diffSec = Math.floor(diffMs / 1000);
    const diffMin = Math.floor(diffSec / 60);
    const diffHour = Math.floor(diffMin / 60);

    if (diffSec < 60) return `${diffSec}s ago`;
    if (diffMin < 60) return `${diffMin}m ago`;
    if (diffHour < 24) return `${diffHour}h ago`;

    return date.toLocaleString();
}

function renderStatistics() {
    const statsDiv = document.getElementById('error-statistics');
    if (!errorLog || !errorLog.statistics) {
        statsDiv.innerHTML = '<p class="no-data">No statistics available</p>';
        return;
    }

    const stats = errorLog.statistics;
    statsDiv.innerHTML = `
        <div class="error-stat-card">
            <div class="error-stat-label">Handler Timeouts</div>
            <div class="error-stat-value">${stats.totalTimeouts}</div>
        </div>
        <div class="error-stat-card">
            <div class="error-stat-label">Publish Failures</div>
            <div class="error-stat-value">${stats.totalPublishFailures}</div>
        </div>
        <div class="error-stat-card">
            <div class="error-stat-label">Type Mismatches</div>
            <div class="error-stat-value">${stats.totalTypeMismatches}</div>
        </div>
        <div class="error-stat-card">
            <div class="error-stat-label">Invalid Operations</div>
            <div class="error-stat-value">${stats.totalInvalidOperations}</div>
        </div>
        <div class="error-stat-card">
            <div class="error-stat-label">Other Errors</div>
            <div class="error-stat-value">${stats.totalOtherErrors}</div>
        </div>
    `;
}

function renderErrorList() {
    const listDiv = document.getElementById('error-list');

    if (!errorLog || errorLog.errors.length === 0) {
        listDiv.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">✅</div>
                <div class="empty-state-title">No Errors</div>
                <div class="empty-state-description">No errors logged in the system</div>
            </div>
        `;
        return;
    }

    const errorsHtml = errorLog.errors.map(error => `
        <tr class="error-row" data-error-id="${error.id}">
            <td>
                <span class="severity-badge ${getSeverityClass(error.severity)}">
                    ${error.severity}
                </span>
            </td>
            <td>${formatTimestamp(error.timestamp)}</td>
            <td><span class="error-type-badge">${error.errorType}</span></td>
            <td><code class="error-channel">${error.channelName || '-'}</code></td>
            <td><code class="error-subscription">${error.subscriptionId || '-'}</code></td>
            <td class="error-message-cell">${error.errorMessage}</td>
        </tr>
    `).join('');

    listDiv.innerHTML = `
        <div class="error-count-badge">
            Showing ${errorLog.errors.length} of ${errorLog.totalErrors} errors
        </div>
        <div class="error-table-container">
            <table class="error-table">
                <thead>
                    <tr>
                        <th>Severity</th>
                        <th>Time</th>
                        <th>Type</th>
                        <th>Channel</th>
                        <th>Subscription</th>
                        <th>Message</th>
                    </tr>
                </thead>
                <tbody>
                    ${errorsHtml}
                </tbody>
            </table>
        </div>
    `;

    document.querySelectorAll('.error-row').forEach(row => {
        row.addEventListener('click', () => {
            const errorId = parseInt(row.dataset.errorId);
            selectedError = errorLog.errors.find(e => e.id === errorId);
            showErrorDetailModal();
        });
    });
}

function showErrorDetailModal() {
    if (!selectedError) return;

    const modal = document.createElement('div');
    modal.className = 'modal';
    modal.innerHTML = `
        <div class="modal-overlay"></div>
        <div class="modal-content error-detail-modal">
            <div class="modal-header">
                <h3>Error Details</h3>
                <button class="modal-close">&times;</button>
            </div>
            <div class="modal-body">
                <div class="error-detail-section">
                    <div class="error-detail-row">
                        <div class="error-detail-label">Error ID</div>
                        <div class="error-detail-value">${selectedError.id}</div>
                    </div>
                    <div class="error-detail-row">
                        <div class="error-detail-label">Timestamp</div>
                        <div class="error-detail-value">${new Date(selectedError.timestamp).toLocaleString()}</div>
                    </div>
                    <div class="error-detail-row">
                        <div class="error-detail-label">Error Type</div>
                        <div class="error-detail-value"><span class="error-type-badge">${selectedError.errorType}</span></div>
                    </div>
                    <div class="error-detail-row">
                        <div class="error-detail-label">Severity</div>
                        <div class="error-detail-value">
                            <span class="severity-badge ${getSeverityClass(selectedError.severity)}">${selectedError.severity}</span>
                        </div>
                    </div>
                    ${selectedError.channelName ? `
                    <div class="error-detail-row">
                        <div class="error-detail-label">Channel</div>
                        <div class="error-detail-value"><code>${selectedError.channelName}</code></div>
                    </div>
                    ` : ''}
                    ${selectedError.subscriptionId ? `
                    <div class="error-detail-row">
                        <div class="error-detail-label">Subscription</div>
                        <div class="error-detail-value"><code>${selectedError.subscriptionId}</code></div>
                    </div>
                    ` : ''}
                </div>

                <div class="error-detail-section">
                    <h4>Error Message</h4>
                    <div class="error-message-box">${selectedError.errorMessage}</div>
                </div>

                ${selectedError.metadata ? `
                <div class="error-detail-section">
                    <h4>Metadata</h4>
                    <div class="metadata-grid">
                        ${Object.entries(selectedError.metadata).map(([key, value]) => `
                            <div class="metadata-item">
                                <div class="metadata-key">${key}:</div>
                                <div class="metadata-value">${value}</div>
                            </div>
                        `).join('')}
                    </div>
                </div>
                ` : ''}

                ${selectedError.stackTrace ? `
                <div class="error-detail-section">
                    <h4>Stack Trace</h4>
                    <pre class="stack-trace">${selectedError.stackTrace}</pre>
                </div>
                ` : ''}
            </div>
        </div>
    `;

    document.body.appendChild(modal);

    modal.querySelector('.modal-close').addEventListener('click', () => {
        document.body.removeChild(modal);
    });

    modal.querySelector('.modal-overlay').addEventListener('click', () => {
        document.body.removeChild(modal);
    });
}

async function handleClearErrors() {
    if (!confirm('Are you sure you want to clear all error logs?')) {
        return;
    }

    try {
        await apiClient.delete('/api/errors');
        await loadErrors();
    } catch (err) {
        console.error('Failed to clear errors:', err);
        alert('Failed to clear errors');
    }
}

function handleErrorTypeFilter(errorType) {
    currentErrorTypeFilter = currentErrorTypeFilter === errorType ? null : errorType;
    updateFilterButtons();
    loadErrors();
}

function handleSearch() {
    currentSearchTerm = document.getElementById('error-search-input').value;
    loadErrors();
}

function updateFilterButtons() {
    document.querySelectorAll('.filter-btn').forEach(btn => {
        const btnType = btn.dataset.errorType;
        if (btnType === currentErrorTypeFilter) {
            btn.classList.add('active');
        } else {
            btn.classList.remove('active');
        }
    });
}

export async function render(container) {
    await loadErrors();

    container.innerHTML = `
        <div class="page-header">
            <h2 class="page-title">Error Log</h2>
            <p class="page-description">System errors and warnings</p>
        </div>

        <div class="error-statistics" id="error-statistics"></div>

        <div class="error-controls">
            <div class="error-filters">
                <button class="filter-btn" data-error-type="HandlerTimeout">Timeouts</button>
                <button class="filter-btn" data-error-type="PublishFailure">Publish Failures</button>
                <button class="filter-btn" data-error-type="TypeMismatch">Type Mismatches</button>
                <button class="filter-btn" data-error-type="InvalidOperation">Invalid Operations</button>
            </div>

            <div class="error-search">
                <input
                    type="text"
                    id="error-search-input"
                    class="search-input"
                    placeholder="Search errors..."
                />
                <button id="search-btn" class="btn btn-secondary">Search</button>
            </div>

            <button id="clear-errors-btn" class="btn btn-danger">Clear All Errors</button>
        </div>

        <div id="error-list"></div>
    `;

    renderStatistics();
    renderErrorList();

    document.getElementById('clear-errors-btn').addEventListener('click', handleClearErrors);
    document.getElementById('search-btn').addEventListener('click', handleSearch);
    document.getElementById('error-search-input').addEventListener('keypress', (e) => {
        if (e.key === 'Enter') handleSearch();
    });

    document.querySelectorAll('.filter-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            handleErrorTypeFilter(btn.dataset.errorType);
        });
    });
}

export async function cleanup() {
    errorLog = null;
    selectedError = null;
    currentErrorTypeFilter = null;
    currentSearchTerm = '';
}

export default { render, cleanup };
