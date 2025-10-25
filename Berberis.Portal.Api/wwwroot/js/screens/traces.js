// Message Trace Viewer Screen

import { signalRClient } from '../signalr-client.js';

let unsubscribeTraces = null;
let traces = [];
let filteredTraces = [];
let isPaused = false;
let maxTraces = 1000;
let samplingRate = 0.01; // 1%
let messageDetailModal = null;

const opTypeNames = {
    0: 'Channel Publish',
    1: 'Subscription Dequeue',
    2: 'Subscription Processed'
};

const opTypeIcons = {
    0: '📤', // Publish
    1: '📥', // Dequeue
    2: '✓'  // Processed
};

const opTypeColors = {
    0: '#3b82f6', // blue
    1: '#f59e0b', // amber
    2: '#10b981'  // green
};

const timestampFormats = {
    relative: 'Relative',
    absolute: 'Absolute',
    ticks: 'Ticks'
};

let currentTimestampFormat = 'relative';

export async function render(container) {
    container.innerHTML = `
        <div class="page-header">
            <h2 class="page-title">
                Message Traces
                <span id="trace-count-badge" class="badge">0</span>
            </h2>
            <p class="page-description">Real-time message traces with sampling</p>
        </div>

        <div class="controls-panel">
            <div class="controls-row">
                <div class="search-box">
                    <input type="text" id="search-channel" placeholder="Search by channel..." />
                </div>

                <div class="search-box">
                    <input type="text" id="search-correlation" placeholder="Search by correlation ID..." />
                </div>

                <div class="search-box">
                    <input type="text" id="search-from" placeholder="Search by source/from..." />
                </div>

                <div class="search-box">
                    <input type="text" id="search-key" placeholder="Search by message key..." />
                </div>
            </div>

            <div class="controls-row">
                <select id="op-type-filter">
                    <option value="">All Operation Types</option>
                    <option value="0">Channel Publish</option>
                    <option value="1">Subscription Dequeue</option>
                    <option value="2">Subscription Processed</option>
                </select>

                <div class="sampling-control">
                    <label>Sampling Rate: <span id="sampling-rate-display">1%</span></label>
                    <input type="range" id="sampling-slider" min="0.001" max="1" step="0.001" value="0.01" />
                </div>

                <select id="timestamp-format">
                    <option value="relative">Relative Time</option>
                    <option value="absolute">Absolute Time</option>
                    <option value="ticks">Ticks</option>
                </select>

                <button id="pause-btn" class="btn btn-secondary">
                    <span class="btn-icon">⏸️</span> Pause
                </button>

                <button id="clear-btn" class="btn btn-secondary">
                    <span class="btn-icon">🗑️</span> Clear
                </button>

                <button id="export-json-btn" class="btn btn-secondary">
                    <span class="btn-icon">📄</span> JSON
                </button>

                <button id="export-csv-btn" class="btn btn-secondary">
                    <span class="btn-icon">📊</span> CSV
                </button>

                <div class="max-traces-control">
                    <label>Max Traces:</label>
                    <input type="number" id="max-traces" value="1000" min="100" max="10000" step="100" />
                </div>
            </div>
        </div>

        <div class="trace-stats">
            <div class="stat-item">
                <span class="stat-label">Total:</span>
                <span id="total-count" class="stat-value">0</span>
            </div>
            <div class="stat-item">
                <span class="stat-label">Published:</span>
                <span id="publish-count" class="stat-value">0</span>
            </div>
            <div class="stat-item">
                <span class="stat-label">Dequeued:</span>
                <span id="dequeue-count" class="stat-value">0</span>
            </div>
            <div class="stat-item">
                <span class="stat-label">Processed:</span>
                <span id="processed-count" class="stat-value">0</span>
            </div>
        </div>

        <div id="traces-container" class="traces-table-wrapper">
            <table class="traces-table">
                <thead>
                    <tr>
                        <th>Time</th>
                        <th>Operation</th>
                        <th>Channel</th>
                        <th>Subscription</th>
                        <th>From</th>
                        <th>Correlation ID</th>
                        <th>Message Key</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody id="traces-tbody">
                    <tr>
                        <td colspan="8" class="empty-message">
                            <div class="empty-state">
                                <div class="empty-state-icon">⏳</div>
                                <div class="empty-state-title">Waiting for Traces</div>
                                <div class="empty-state-description">Message traces will appear here in real-time</div>
                            </div>
                        </td>
                    </tr>
                </tbody>
            </table>
        </div>

        <div id="message-detail-modal" class="modal" style="display: none;">
            <div class="modal-content">
                <div class="modal-header">
                    <h3>Message Trace Details</h3>
                    <button class="modal-close" id="close-modal">&times;</button>
                </div>
                <div class="modal-body" id="modal-body">
                </div>
            </div>
        </div>
    `;

    // Initialize
    await connectToTracesStream();
    setupEventHandlers(container);
}

async function connectToTracesStream() {
    try {
        if (!signalRClient.isConnected) {
            await signalRClient.connect();
        }

        // Subscribe to message traces
        unsubscribeTraces = signalRClient.onMessageTrace(handleMessageTrace);
        await signalRClient.subscribeToTraces(samplingRate);

        console.log('Connected to message traces stream with sampling rate:', samplingRate);
    } catch (error) {
        console.error('Failed to connect to traces stream:', error);
    }
}

function handleMessageTrace(trace) {
    if (isPaused) return;

    // Add to traces array
    traces.unshift(trace); // Add to beginning

    // Trim to max traces
    if (traces.length > maxTraces) {
        traces = traces.slice(0, maxTraces);
    }

    // Reapply filters and update display
    applyFilters();
    updateStats();
}

function setupEventHandlers(container) {
    // Search inputs
    const searchChannel = container.querySelector('#search-channel');
    const searchCorrelation = container.querySelector('#search-correlation');
    const searchFrom = container.querySelector('#search-from');
    const searchKey = container.querySelector('#search-key');

    searchChannel.addEventListener('input', applyFilters);
    searchCorrelation.addEventListener('input', applyFilters);
    searchFrom.addEventListener('input', applyFilters);
    searchKey.addEventListener('input', applyFilters);

    // Operation type filter
    const opTypeFilter = container.querySelector('#op-type-filter');
    opTypeFilter.addEventListener('change', applyFilters);

    // Sampling rate slider
    const samplingSlider = container.querySelector('#sampling-slider');
    const samplingDisplay = container.querySelector('#sampling-rate-display');
    samplingSlider.addEventListener('input', async (e) => {
        samplingRate = parseFloat(e.target.value);
        const percentage = (samplingRate * 100).toFixed(samplingRate < 0.01 ? 2 : 1);
        samplingDisplay.textContent = `${percentage}%`;

        // Update sampling rate on server
        try {
            await signalRClient.unsubscribeFromTraces();
            await signalRClient.subscribeToTraces(samplingRate);
        } catch (error) {
            console.error('Failed to update sampling rate:', error);
        }
    });

    // Timestamp format selector
    const timestampFormat = container.querySelector('#timestamp-format');
    timestampFormat.addEventListener('change', (e) => {
        currentTimestampFormat = e.target.value;
        renderTraces(container);
    });

    // Pause/Resume button
    const pauseBtn = container.querySelector('#pause-btn');
    pauseBtn.addEventListener('click', () => {
        isPaused = !isPaused;
        pauseBtn.innerHTML = isPaused
            ? '<span class="btn-icon">▶️</span> Resume'
            : '<span class="btn-icon">⏸️</span> Pause';
        pauseBtn.classList.toggle('btn-primary', isPaused);
    });

    // Clear button
    const clearBtn = container.querySelector('#clear-btn');
    clearBtn.addEventListener('click', () => {
        traces = [];
        filteredTraces = [];
        renderTraces(container);
        updateStats();
    });

    // Export buttons
    const exportJsonBtn = container.querySelector('#export-json-btn');
    exportJsonBtn.addEventListener('click', () => exportToJSON());

    const exportCsvBtn = container.querySelector('#export-csv-btn');
    exportCsvBtn.addEventListener('click', () => exportToCSV());

    // Max traces input
    const maxTracesInput = container.querySelector('#max-traces');
    maxTracesInput.addEventListener('change', (e) => {
        maxTraces = parseInt(e.target.value) || 1000;
        if (traces.length > maxTraces) {
            traces = traces.slice(0, maxTraces);
            applyFilters();
        }
    });

    // Modal close
    const closeModal = container.querySelector('#close-modal');
    closeModal.addEventListener('click', () => {
        container.querySelector('#message-detail-modal').style.display = 'none';
    });

    // Click outside modal to close
    const modal = container.querySelector('#message-detail-modal');
    modal.addEventListener('click', (e) => {
        if (e.target === modal) {
            modal.style.display = 'none';
        }
    });
}

function applyFilters() {
    const searchChannel = document.querySelector('#search-channel')?.value.toLowerCase() || '';
    const searchCorrelation = document.querySelector('#search-correlation')?.value.toLowerCase() || '';
    const searchFrom = document.querySelector('#search-from')?.value.toLowerCase() || '';
    const searchKey = document.querySelector('#search-key')?.value.toLowerCase() || '';
    const opTypeFilter = document.querySelector('#op-type-filter')?.value || '';

    filteredTraces = traces.filter(trace => {
        // Filter by operation type
        if (opTypeFilter !== '' && trace.opType.toString() !== opTypeFilter) {
            return false;
        }

        // Filter by channel
        if (searchChannel && !trace.channel?.toLowerCase().includes(searchChannel)) {
            return false;
        }

        // Filter by correlation ID
        if (searchCorrelation && !trace.correlationId?.toString().includes(searchCorrelation)) {
            return false;
        }

        // Filter by source/from
        if (searchFrom && !trace.from?.toLowerCase().includes(searchFrom)) {
            return false;
        }

        // Filter by message key
        if (searchKey && !trace.messageKey?.toLowerCase().includes(searchKey)) {
            return false;
        }

        return true;
    });

    renderTraces(document.querySelector('.page-header').closest('.screen-content'));
    updateTraceCount(filteredTraces.length);
}

function renderTraces(container) {
    const tbody = container.querySelector('#traces-tbody');

    if (filteredTraces.length === 0) {
        tbody.innerHTML = `
            <tr>
                <td colspan="8" class="empty-message">
                    <div class="empty-state">
                        <div class="empty-state-icon">⏳</div>
                        <div class="empty-state-title">No Traces</div>
                        <div class="empty-state-description">
                            ${traces.length === 0
                                ? 'Message traces will appear here in real-time'
                                : 'No traces match the current filters'
                            }
                        </div>
                    </div>
                </td>
            </tr>
        `;
        return;
    }

    const rowsHtml = filteredTraces.map(trace => renderTraceRow(trace)).join('');
    tbody.innerHTML = rowsHtml;

    // Add click handlers for detail buttons
    tbody.querySelectorAll('.view-detail-btn').forEach((btn, idx) => {
        btn.addEventListener('click', () => showTraceDetail(filteredTraces[idx]));
    });
}

function renderTraceRow(trace) {
    const opTypeName = opTypeNames[trace.opType] || 'Unknown';
    const opTypeIcon = opTypeIcons[trace.opType] || '❓';
    const opTypeColor = opTypeColors[trace.opType] || '#6b7280';
    const timestamp = formatTimestamp(trace.ticks);

    return `
        <tr>
            <td class="trace-time">${timestamp}</td>
            <td>
                <span class="op-type-badge" style="background-color: ${opTypeColor}">
                    ${opTypeIcon} ${opTypeName}
                </span>
            </td>
            <td class="trace-channel">${trace.channel || 'N/A'}</td>
            <td class="trace-subscription">${trace.subscriptionName || 'N/A'}</td>
            <td class="trace-from">${trace.from || 'N/A'}</td>
            <td class="trace-correlation">${trace.correlationId || 'N/A'}</td>
            <td class="trace-key">${trace.messageKey || 'N/A'}</td>
            <td>
                <button class="btn btn-sm view-detail-btn">View</button>
            </td>
        </tr>
    `;
}

function formatTimestamp(ticks) {
    // Convert .NET ticks to JavaScript timestamp
    // .NET ticks start from 0001-01-01, JS from 1970-01-01
    const ticksTo1970 = 621355968000000000n;
    const ticksPerMs = 10000n;

    const jsTimestamp = Number((BigInt(ticks) - ticksTo1970) / ticksPerMs);
    const date = new Date(jsTimestamp);

    switch (currentTimestampFormat) {
        case 'relative':
            return formatRelativeTime(date);
        case 'absolute':
            return date.toLocaleString();
        case 'ticks':
            return ticks.toString();
        default:
            return formatRelativeTime(date);
    }
}

function formatRelativeTime(date) {
    const now = new Date();
    const diffMs = now - date;
    const diffSec = Math.floor(diffMs / 1000);
    const diffMin = Math.floor(diffSec / 60);
    const diffHour = Math.floor(diffMin / 60);

    if (diffSec < 1) {
        return 'just now';
    } else if (diffSec < 60) {
        return `${diffSec}s ago`;
    } else if (diffMin < 60) {
        return `${diffMin}m ago`;
    } else if (diffHour < 24) {
        return `${diffHour}h ago`;
    } else {
        return date.toLocaleString();
    }
}

function showTraceDetail(trace) {
    const modal = document.querySelector('#message-detail-modal');
    const modalBody = document.querySelector('#modal-body');

    const timestamp = formatTimestamp(trace.ticks);
    const opTypeName = opTypeNames[trace.opType] || 'Unknown';

    modalBody.innerHTML = `
        <div class="detail-section">
            <h4>Trace Information</h4>
            <div class="detail-grid">
                <div class="detail-item">
                    <span class="detail-item-label">Operation Type:</span>
                    <span class="detail-item-value">${opTypeName}</span>
                </div>
                <div class="detail-item">
                    <span class="detail-item-label">Timestamp:</span>
                    <span class="detail-item-value">${timestamp}</span>
                </div>
                <div class="detail-item">
                    <span class="detail-item-label">Channel:</span>
                    <span class="detail-item-value">${trace.channel || 'N/A'}</span>
                </div>
                <div class="detail-item">
                    <span class="detail-item-label">Subscription:</span>
                    <span class="detail-item-value">${trace.subscriptionName || 'N/A'}</span>
                </div>
                <div class="detail-item">
                    <span class="detail-item-label">Source/From:</span>
                    <span class="detail-item-value">${trace.from || 'N/A'}</span>
                </div>
                <div class="detail-item">
                    <span class="detail-item-label">Correlation ID:</span>
                    <span class="detail-item-value">${trace.correlationId || 'N/A'}</span>
                </div>
                <div class="detail-item">
                    <span class="detail-item-label">Message Key:</span>
                    <span class="detail-item-value">${trace.messageKey || 'N/A'}</span>
                </div>
                <div class="detail-item">
                    <span class="detail-item-label">Ticks:</span>
                    <span class="detail-item-value">${trace.ticks}</span>
                </div>
            </div>
        </div>

        <div class="detail-section">
            <h4>Raw JSON</h4>
            <pre class="json-viewer"><code>${JSON.stringify(trace, null, 2)}</code></pre>
        </div>
    `;

    modal.style.display = 'flex';
}

function updateStats() {
    const totalCount = traces.length;
    const publishCount = traces.filter(t => t.opType === 0).length;
    const dequeueCount = traces.filter(t => t.opType === 1).length;
    const processedCount = traces.filter(t => t.opType === 2).length;

    document.querySelector('#total-count').textContent = totalCount;
    document.querySelector('#publish-count').textContent = publishCount;
    document.querySelector('#dequeue-count').textContent = dequeueCount;
    document.querySelector('#processed-count').textContent = processedCount;
}

function updateTraceCount(count) {
    const badge = document.querySelector('#trace-count-badge');
    if (badge) {
        badge.textContent = count;
    }
}

function exportToJSON() {
    const data = JSON.stringify(filteredTraces, null, 2);
    const blob = new Blob([data], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `message-traces-${new Date().toISOString()}.json`;
    a.click();
    URL.revokeObjectURL(url);
}

function exportToCSV() {
    const headers = ['Timestamp', 'Operation', 'Channel', 'Subscription', 'From', 'Correlation ID', 'Message Key', 'Ticks'];
    const rows = filteredTraces.map(trace => [
        formatTimestamp(trace.ticks),
        opTypeNames[trace.opType] || 'Unknown',
        trace.channel || '',
        trace.subscriptionName || '',
        trace.from || '',
        trace.correlationId || '',
        trace.messageKey || '',
        trace.ticks
    ]);

    const csvContent = [
        headers.join(','),
        ...rows.map(row => row.map(cell => `"${cell}"`).join(','))
    ].join('\n');

    const blob = new Blob([csvContent], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `message-traces-${new Date().toISOString()}.csv`;
    a.click();
    URL.revokeObjectURL(url);
}

export async function cleanup() {
    // Unsubscribe from traces
    if (unsubscribeTraces) {
        unsubscribeTraces();
        unsubscribeTraces = null;
    }

    try {
        await signalRClient.unsubscribeFromTraces();
    } catch (error) {
        console.error('Error unsubscribing from traces:', error);
    }

    // Clear traces
    traces = [];
    filteredTraces = [];
    isPaused = false;
}

export default { render, cleanup };
