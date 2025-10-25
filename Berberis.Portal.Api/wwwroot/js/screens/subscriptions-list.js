// Subscriptions List Screen

import { api } from '../api-client.js';
import { signalRClient } from '../signalr-client.js';

let subscriptions = [];
let filteredSubscriptions = [];
let currentPage = 1;
let pageSize = 25;
let sortColumn = 'subscriptionId';
let sortDirection = 'asc';
let searchQuery = '';
let statusFilter = 'all'; // 'all', 'active', 'suspended', 'detached'
let metricsUnsubscribe = null;

export async function render(container) {
    container.innerHTML = `
        <div class="page-header">
            <div>
                <h2 class="page-title">Subscriptions</h2>
                <p class="page-description">Monitor and manage all subscriptions</p>
            </div>
            <div style="display: flex; gap: 12px;">
                <select
                    id="status-filter"
                    class="filter-select"
                    style="padding: 8px 12px; border: 1px solid var(--border-color); border-radius: 6px; font-size: 14px; background-color: white; cursor: pointer;"
                >
                    <option value="all">All Status</option>
                    <option value="active">Active</option>
                    <option value="suspended">Suspended</option>
                    <option value="detached">Detached</option>
                </select>
                <input
                    type="text"
                    id="subscription-search"
                    placeholder="Search by channel or ID..."
                    class="search-input"
                    style="padding: 8px 12px; border: 1px solid var(--border-color); border-radius: 6px; font-size: 14px; min-width: 250px;"
                >
                <button id="refresh-subscriptions-button" class="btn btn-secondary" title="Refresh">
                    <span style="font-size: 16px;">↻</span> Refresh
                </button>
            </div>
        </div>

        <div class="card">
            <div id="subscriptions-table-container">
                <div class="loading">Loading subscriptions...</div>
            </div>

            <div id="pagination-container" style="margin-top: 16px; display: flex; justify-content: space-between; align-items: center;">
                <div id="pagination-info" style="color: var(--text-secondary); font-size: 13px;"></div>
                <div id="pagination-controls" style="display: flex; gap: 8px;"></div>
            </div>
        </div>
    `;

    // Set up event listeners
    setupEventListeners();

    // Load subscriptions
    await loadSubscriptions();

    // Subscribe to real-time metrics
    try {
        await signalRClient.subscribeToMetrics(5000);
        metricsUnsubscribe = signalRClient.onMetricsUpdate(handleMetricsUpdate);
    } catch (error) {
        console.error('Failed to subscribe to metrics:', error);
    }
}

export async function cleanup() {
    if (metricsUnsubscribe) {
        metricsUnsubscribe();
        metricsUnsubscribe = null;
    }

    try {
        await signalRClient.unsubscribeFromMetrics();
    } catch (error) {
        console.error('Failed to unsubscribe from metrics:', error);
    }
}

function setupEventListeners() {
    // Search input
    const searchInput = document.getElementById('subscription-search');
    if (searchInput) {
        searchInput.addEventListener('input', (e) => {
            searchQuery = e.target.value.toLowerCase();
            currentPage = 1;
            applyFiltersAndSort();
        });
    }

    // Status filter
    const statusFilterSelect = document.getElementById('status-filter');
    if (statusFilterSelect) {
        statusFilterSelect.addEventListener('change', (e) => {
            statusFilter = e.target.value;
            currentPage = 1;
            applyFiltersAndSort();
        });
    }

    // Refresh button
    const refreshButton = document.getElementById('refresh-subscriptions-button');
    if (refreshButton) {
        refreshButton.addEventListener('click', async () => {
            refreshButton.disabled = true;
            refreshButton.innerHTML = '<span style="font-size: 16px;">↻</span> Refreshing...';
            await loadSubscriptions();
            refreshButton.disabled = false;
            refreshButton.innerHTML = '<span style="font-size: 16px;">↻</span> Refresh';
        });
    }
}

async function loadSubscriptions() {
    try {
        const data = await api.getSubscriptions();
        subscriptions = data || [];
        applyFiltersAndSort();
    } catch (error) {
        console.error('Failed to load subscriptions:', error);
        const container = document.getElementById('subscriptions-table-container');
        if (container) {
            container.innerHTML = `
                <div class="error-message">Failed to load subscriptions: ${error.message}</div>
            `;
        }
    }
}

function applyFiltersAndSort() {
    // Filter
    filteredSubscriptions = subscriptions.filter(sub => {
        // Search filter
        if (searchQuery) {
            const matchesSearch =
                sub.channelPattern.toLowerCase().includes(searchQuery) ||
                sub.subscriptionId.toString().includes(searchQuery);
            if (!matchesSearch) return false;
        }

        // Status filter
        if (statusFilter !== 'all') {
            const status = getSubscriptionStatus(sub);
            if (status !== statusFilter) return false;
        }

        return true;
    });

    // Sort
    filteredSubscriptions.sort((a, b) => {
        let aVal = a[sortColumn];
        let bVal = b[sortColumn];

        // Handle null/undefined
        if (aVal === null || aVal === undefined) aVal = '';
        if (bVal === null || bVal === undefined) bVal = '';

        // String comparison
        if (typeof aVal === 'string') {
            aVal = aVal.toLowerCase();
            bVal = bVal.toLowerCase();
        }

        if (sortDirection === 'asc') {
            return aVal > bVal ? 1 : aVal < bVal ? -1 : 0;
        } else {
            return aVal < bVal ? 1 : aVal > bVal ? -1 : 0;
        }
    });

    renderTable();
}

function renderTable() {
    const container = document.getElementById('subscriptions-table-container');
    if (!container) return;

    if (filteredSubscriptions.length === 0) {
        container.innerHTML = `
            <div style="text-align: center; padding: 40px; color: var(--text-secondary);">
                ${searchQuery || statusFilter !== 'all' ? 'No subscriptions match the current filters' : 'No subscriptions available'}
            </div>
        `;
        updatePagination();
        return;
    }

    // Paginate
    const start = (currentPage - 1) * pageSize;
    const end = start + pageSize;
    const paginatedSubscriptions = filteredSubscriptions.slice(start, end);

    // Render table
    const tableHtml = `
        <table>
            <thead>
                <tr>
                    ${createSortableHeader('subscriptionId', 'ID')}
                    ${createSortableHeader('channelPattern', 'Channel Pattern')}
                    <th style="cursor: pointer;">Status</th>
                    ${createSortableHeader('queueDepth', 'Queue Depth')}
                    ${createSortableHeader('processRate', 'Process Rate')}
                    ${createSortableHeader('avgLatency', 'Avg Latency')}
                    ${createSortableHeader('p99Latency', 'P99 Latency')}
                    ${createSortableHeader('timeoutCount', 'Timeouts')}
                    <th style="cursor: pointer;">Health</th>
                </tr>
            </thead>
            <tbody>
                ${paginatedSubscriptions.map(sub => createSubscriptionRow(sub)).join('')}
            </tbody>
        </table>
    `;

    container.innerHTML = tableHtml;

    // Add click handlers for sortable headers
    container.querySelectorAll('th[data-column]').forEach(th => {
        th.addEventListener('click', () => {
            const column = th.getAttribute('data-column');
            if (sortColumn === column) {
                sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
            } else {
                sortColumn = column;
                sortDirection = 'asc';
            }
            applyFiltersAndSort();
        });
    });

    // Add click handlers for rows
    container.querySelectorAll('tbody tr').forEach((row, index) => {
        const sub = paginatedSubscriptions[index];
        row.style.cursor = 'pointer';
        row.addEventListener('click', () => {
            window.location.hash = `#/subscriptions/${sub.subscriptionId}`;
        });
    });

    updatePagination();
}

function createSortableHeader(column, label) {
    const isSorted = sortColumn === column;
    const arrow = isSorted ? (sortDirection === 'asc' ? ' ↑' : ' ↓') : '';
    return `<th data-column="${column}" style="cursor: pointer; user-select: none;">${label}${arrow}</th>`;
}

function createSubscriptionRow(sub) {
    const queueDepth = sub.queueDepth || 0;
    const processRate = formatRate(sub.processRate || 0);
    const avgLatency = formatLatency(sub.avgLatency);
    const p99Latency = formatLatency(sub.p99Latency);
    const timeoutCount = sub.timeoutCount || 0;

    // Determine status
    const status = getSubscriptionStatus(sub);
    const statusClass = getSubscriptionStatusClass(sub);
    const statusLabel = getSubscriptionStatusLabel(sub);

    // Determine health indicators
    const hasHighQueueDepth = queueDepth > 1000;
    const hasHighLatency = (sub.avgLatency || 0) > 100;
    const hasTimeouts = timeoutCount > 0;
    const healthWarning = hasHighQueueDepth || hasHighLatency || hasTimeouts;

    let healthIndicator = '';
    if (healthWarning) {
        const warnings = [];
        if (hasHighQueueDepth) warnings.push('High queue depth');
        if (hasHighLatency) warnings.push('High latency');
        if (hasTimeouts) warnings.push('Timeouts detected');
        healthIndicator = `<span style="color: var(--warning-color); font-weight: 600;" title="${warnings.join(', ')}">⚠</span>`;
    } else {
        healthIndicator = `<span style="color: var(--success-color);" title="Healthy">✓</span>`;
    }

    return `
        <tr>
            <td><strong>${sub.subscriptionId}</strong></td>
            <td><code>${escapeHtml(sub.channelPattern)}</code></td>
            <td><span class="status-badge ${statusClass}">${statusLabel}</span></td>
            <td ${hasHighQueueDepth ? 'style="color: var(--warning-color); font-weight: 600;"' : ''}>${formatNumber(queueDepth)}</td>
            <td>${processRate}/s</td>
            <td ${hasHighLatency ? 'style="color: var(--warning-color); font-weight: 600;"' : ''}>${avgLatency}</td>
            <td>${p99Latency}</td>
            <td ${hasTimeouts ? 'style="color: var(--danger-color); font-weight: 600;"' : ''}>${timeoutCount}</td>
            <td style="text-align: center; font-size: 16px;">${healthIndicator}</td>
        </tr>
    `;
}

function getSubscriptionStatus(sub) {
    if (sub.isDetached) return 'detached';
    if (sub.isProcessingSuspended) return 'suspended';
    return 'active';
}

function getSubscriptionStatusClass(sub) {
    if (sub.isDetached) return 'status-error';
    if (sub.isProcessingSuspended) return 'status-warning';
    return 'status-healthy';
}

function getSubscriptionStatusLabel(sub) {
    if (sub.isDetached) return 'Detached';
    if (sub.isProcessingSuspended) return 'Suspended';
    return 'Active';
}

function updatePagination() {
    const totalPages = Math.ceil(filteredSubscriptions.length / pageSize);
    const start = (currentPage - 1) * pageSize + 1;
    const end = Math.min(currentPage * pageSize, filteredSubscriptions.length);

    // Update info
    const infoContainer = document.getElementById('pagination-info');
    if (infoContainer) {
        infoContainer.textContent = `Showing ${start}-${end} of ${filteredSubscriptions.length} subscriptions`;
    }

    // Update controls
    const controlsContainer = document.getElementById('pagination-controls');
    if (controlsContainer) {
        if (totalPages <= 1) {
            controlsContainer.innerHTML = '';
            return;
        }

        const buttons = [];

        // Previous button
        buttons.push(`
            <button
                class="btn btn-secondary"
                ${currentPage === 1 ? 'disabled' : ''}
                data-page="${currentPage - 1}"
            >← Previous</button>
        `);

        // Page numbers
        for (let i = 1; i <= totalPages; i++) {
            if (i === 1 || i === totalPages || (i >= currentPage - 1 && i <= currentPage + 1)) {
                buttons.push(`
                    <button
                        class="btn ${i === currentPage ? 'btn-primary' : 'btn-secondary'}"
                        data-page="${i}"
                    >${i}</button>
                `);
            } else if (i === currentPage - 2 || i === currentPage + 2) {
                buttons.push(`<span style="padding: 8px;">...</span>`);
            }
        }

        // Next button
        buttons.push(`
            <button
                class="btn btn-secondary"
                ${currentPage === totalPages ? 'disabled' : ''}
                data-page="${currentPage + 1}"
            >Next →</button>
        `);

        controlsContainer.innerHTML = buttons.join('');

        // Add click handlers
        controlsContainer.querySelectorAll('button[data-page]').forEach(btn => {
            btn.addEventListener('click', () => {
                const page = parseInt(btn.getAttribute('data-page'));
                if (page >= 1 && page <= totalPages) {
                    currentPage = page;
                    renderTable();
                }
            });
        });
    }
}

function handleMetricsUpdate(metrics) {
    // Update subscription metrics in real-time
    if (metrics.subscriptions) {
        subscriptions = subscriptions.map(sub => {
            const updated = metrics.subscriptions.find(s => s.subscriptionId === sub.subscriptionId);
            return updated ? { ...sub, ...updated } : sub;
        });

        // Re-apply filters and re-render
        applyFiltersAndSort();
    }
}

function formatNumber(num) {
    if (num >= 1000000) {
        return (num / 1000000).toFixed(1) + 'M';
    } else if (num >= 1000) {
        return (num / 1000).toFixed(1) + 'K';
    }
    return num.toString();
}

function formatRate(rate) {
    if (rate >= 1000000) {
        return (rate / 1000000).toFixed(2) + 'M';
    } else if (rate >= 1000) {
        return (rate / 1000).toFixed(1) + 'K';
    }
    return rate.toFixed(1);
}

function formatLatency(ms) {
    if (ms === null || ms === undefined) return '-';
    if (ms < 1) return '<1ms';
    return Math.round(ms) + 'ms';
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

export default { render, cleanup };
