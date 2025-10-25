// Channels List Screen

import { api } from '../api-client.js';
import { signalRClient } from '../signalr-client.js';

let channels = [];
let filteredChannels = [];
let currentPage = 1;
let pageSize = 25;
let sortColumn = 'channelName';
let sortDirection = 'asc';
let searchQuery = '';
let metricsUnsubscribe = null;

export async function render(container) {
    container.innerHTML = `
        <div class="page-header">
            <div>
                <h2 class="page-title">Channels</h2>
                <p class="page-description">View and monitor all messaging channels</p>
            </div>
            <div style="display: flex; gap: 12px;">
                <input
                    type="text"
                    id="channel-search"
                    placeholder="Search channels..."
                    class="search-input"
                    style="padding: 8px 12px; border: 1px solid var(--border-color); border-radius: 6px; font-size: 14px; min-width: 250px;"
                >
                <button id="refresh-channels-button" class="btn btn-secondary" title="Refresh channels">
                    <span style="font-size: 16px;">↻</span> Refresh
                </button>
            </div>
        </div>

        <div class="card">
            <div id="channels-table-container">
                <div class="loading">Loading channels...</div>
            </div>

            <div id="pagination-container" style="margin-top: 16px; display: flex; justify-content: space-between; align-items: center;">
                <div id="pagination-info" style="color: var(--text-secondary); font-size: 13px;"></div>
                <div id="pagination-controls" style="display: flex; gap: 8px;"></div>
            </div>
        </div>
    `;

    // Set up event listeners
    setupEventListeners();

    // Load channels data
    await loadChannels();

    // Subscribe to real-time metrics for live updates
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
    const searchInput = document.getElementById('channel-search');
    if (searchInput) {
        searchInput.addEventListener('input', (e) => {
            searchQuery = e.target.value.toLowerCase();
            currentPage = 1; // Reset to first page
            applyFiltersAndSort();
        });
    }

    // Refresh button
    const refreshButton = document.getElementById('refresh-channels-button');
    if (refreshButton) {
        refreshButton.addEventListener('click', async () => {
            refreshButton.disabled = true;
            refreshButton.innerHTML = '<span style="font-size: 16px;">↻</span> Refreshing...';
            await loadChannels();
            refreshButton.disabled = false;
            refreshButton.innerHTML = '<span style="font-size: 16px;">↻</span> Refresh';
        });
    }
}

async function loadChannels() {
    try {
        const data = await api.getChannels();
        channels = data || [];
        applyFiltersAndSort();
    } catch (error) {
        console.error('Failed to load channels:', error);
        const container = document.getElementById('channels-table-container');
        if (container) {
            container.innerHTML = `
                <div class="error-message">Failed to load channels: ${error.message}</div>
            `;
        }
    }
}

function applyFiltersAndSort() {
    // Filter
    filteredChannels = channels.filter(channel => {
        if (!searchQuery) return true;
        return channel.channelName.toLowerCase().includes(searchQuery);
    });

    // Sort
    filteredChannels.sort((a, b) => {
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
    const container = document.getElementById('channels-table-container');
    if (!container) return;

    if (filteredChannels.length === 0) {
        container.innerHTML = `
            <div style="text-align: center; padding: 40px; color: var(--text-secondary);">
                ${searchQuery ? `No channels found matching "${searchQuery}"` : 'No channels available'}
            </div>
        `;
        updatePagination();
        return;
    }

    // Paginate
    const start = (currentPage - 1) * pageSize;
    const end = start + pageSize;
    const paginatedChannels = filteredChannels.slice(start, end);

    // Render table
    const tableHtml = `
        <table>
            <thead>
                <tr>
                    ${createSortableHeader('channelName', 'Channel Name')}
                    ${createSortableHeader('channelType', 'Type')}
                    ${createSortableHeader('publishRate', 'Publish Rate')}
                    ${createSortableHeader('totalMessages', 'Total Messages')}
                    ${createSortableHeader('subscriptionCount', 'Subscriptions')}
                    ${createSortableHeader('storedMessagesCount', 'Stored Messages')}
                    ${createSortableHeader('lastPublisher', 'Last Publisher')}
                    <th style="cursor: pointer;">Status</th>
                </tr>
            </thead>
            <tbody>
                ${paginatedChannels.map(channel => createChannelRow(channel)).join('')}
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
        const channel = paginatedChannels[index];
        row.style.cursor = 'pointer';
        row.addEventListener('click', () => {
            window.location.hash = `#/channels/${encodeURIComponent(channel.channelName)}`;
        });
    });

    updatePagination();
}

function createSortableHeader(column, label) {
    const isSorted = sortColumn === column;
    const arrow = isSorted ? (sortDirection === 'asc' ? ' ↑' : ' ↓') : '';
    return `<th data-column="${column}" style="cursor: pointer; user-select: none;">${label}${arrow}</th>`;
}

function createChannelRow(channel) {
    const publishRate = formatRate(channel.publishRate || 0);
    const totalMessages = formatNumber(channel.totalMessages || 0);
    const storedMessages = formatNumber(channel.storedMessagesCount || 0);
    const lastPublisher = channel.lastPublisher || '-';
    const channelType = channel.channelType || 'Unknown';
    const subscriptionCount = channel.subscriptionCount || 0;

    // Determine health status
    const isActive = channel.publishRate > 0;
    const hasSubscribers = subscriptionCount > 0;
    const healthClass = isActive ? 'status-healthy' : (hasSubscribers ? 'status-warning' : 'status-inactive');
    const healthLabel = isActive ? 'Active' : (hasSubscribers ? 'Idle' : 'Inactive');

    return `
        <tr>
            <td><strong>${escapeHtml(channel.channelName)}</strong></td>
            <td>${escapeHtml(channelType)}</td>
            <td>${publishRate}/s</td>
            <td>${totalMessages}</td>
            <td>${subscriptionCount}</td>
            <td>${storedMessages}</td>
            <td style="font-size: 12px; color: var(--text-secondary);">${escapeHtml(lastPublisher)}</td>
            <td><span class="status-badge ${healthClass}">${healthLabel}</span></td>
        </tr>
    `;
}

function updatePagination() {
    const totalPages = Math.ceil(filteredChannels.length / pageSize);
    const start = (currentPage - 1) * pageSize + 1;
    const end = Math.min(currentPage * pageSize, filteredChannels.length);

    // Update info
    const infoContainer = document.getElementById('pagination-info');
    if (infoContainer) {
        infoContainer.textContent = `Showing ${start}-${end} of ${filteredChannels.length} channels`;
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
    // Update channel metrics in real-time
    if (metrics.channels) {
        // Merge updated metrics with existing channels
        channels = channels.map(channel => {
            const updated = metrics.channels.find(c => c.channelName === channel.channelName);
            return updated ? { ...channel, ...updated } : channel;
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

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

export default { render, cleanup };
