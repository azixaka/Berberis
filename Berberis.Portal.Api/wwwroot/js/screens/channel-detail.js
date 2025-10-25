// Channel Detail Screen

import { api } from '../api-client.js';
import { signalRClient } from '../signalr-client.js';
import { createSparkline, MetricsHistory } from '../utils/sparkline.js';

let channelName = '';
let channel = null;
let metricsUnsubscribe = null;
const publishRateHistory = new MetricsHistory(30);

export async function render(container, params) {
    channelName = decodeURIComponent(params[0] || '');

    if (!channelName) {
        container.innerHTML = `
            <div class="error-message">Invalid channel name</div>
        `;
        return;
    }

    // Initial render with loading state
    container.innerHTML = `
        <div style="margin-bottom: 24px;">
            <nav style="margin-bottom: 16px;">
                <a href="#/channels" style="color: var(--primary-color); text-decoration: none;">← Back to Channels</a>
            </nav>
            <div class="page-header">
                <div>
                    <h2 class="page-title">${escapeHtml(channelName)}</h2>
                    <p class="page-description" id="channel-type-label">Loading channel details...</p>
                </div>
                <div style="display: flex; gap: 12px;">
                    <button id="refresh-channel-button" class="btn btn-secondary" title="Refresh">
                        <span style="font-size: 16px;">↻</span> Refresh
                    </button>
                    <button id="reset-channel-button" class="btn btn-danger" title="Reset channel">
                        Reset Channel
                    </button>
                </div>
            </div>
        </div>

        <div id="channel-content">
            <div class="loading">Loading channel details...</div>
        </div>
    `;

    // Set up event listeners
    setupEventListeners();

    // Load channel data
    await loadChannelData();

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

    // Clear history
    publishRateHistory.clear();
}

function setupEventListeners() {
    // Refresh button
    const refreshButton = document.getElementById('refresh-channel-button');
    if (refreshButton) {
        refreshButton.addEventListener('click', async () => {
            refreshButton.disabled = true;
            refreshButton.innerHTML = '<span style="font-size: 16px;">↻</span> Refreshing...';
            await loadChannelData();
            refreshButton.disabled = false;
            refreshButton.innerHTML = '<span style="font-size: 16px;">↻</span> Refresh';
        });
    }

    // Reset channel button
    const resetButton = document.getElementById('reset-channel-button');
    if (resetButton) {
        resetButton.addEventListener('click', () => showResetConfirmation());
    }
}

async function loadChannelData() {
    try {
        // Load channel details
        channel = await api.getChannelDetails(channelName);

        // Update type label
        const typeLabel = document.getElementById('channel-type-label');
        if (typeLabel) {
            typeLabel.textContent = `Type: ${channel.channelType || 'Unknown'}`;
        }

        // Render channel content
        renderChannelContent();
    } catch (error) {
        console.error('Failed to load channel:', error);
        const container = document.getElementById('channel-content');
        if (container) {
            container.innerHTML = `
                <div class="error-message">Failed to load channel details: ${error.message}</div>
            `;
        }
    }
}

function renderChannelContent() {
    const container = document.getElementById('channel-content');
    if (!container || !channel) return;

    // Track publish rate for sparkline
    publishRateHistory.add('rate', channel.publishRate || 0);

    // Generate publish rate sparkline
    const publishRateSparkline = createSparkline(publishRateHistory.get('rate'), {
        width: 200,
        height: 60,
        strokeColor: '#10b981',
        fillColor: 'rgba(16, 185, 129, 0.1)'
    });

    container.innerHTML = `
        <!-- Channel Statistics -->
        <div class="stats-grid" style="margin-bottom: 24px;">
            <div class="stat-card">
                <div class="stat-label">Publish Rate</div>
                <div class="stat-value">${formatRate(channel.publishRate || 0)}/s</div>
                <div class="stat-sparkline">${publishRateSparkline}</div>
            </div>

            <div class="stat-card">
                <div class="stat-label">Total Messages</div>
                <div class="stat-value">${formatNumber(channel.totalMessages || 0)}</div>
                <div class="stat-change">lifetime count</div>
            </div>

            <div class="stat-card">
                <div class="stat-label">Subscriptions</div>
                <div class="stat-value">${channel.subscriptionCount || 0}</div>
                <div class="stat-change">active subscribers</div>
            </div>

            <div class="stat-card">
                <div class="stat-label">Stored Messages</div>
                <div class="stat-value">${formatNumber(channel.storedMessagesCount || 0)}</div>
                <div class="stat-change">in message store</div>
            </div>
        </div>

        <!-- Channel Metadata -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">Channel Information</h3>
            <table style="width: 100%; border: none;">
                <tbody>
                    <tr style="background: none;">
                        <td style="border: none; padding: 8px 0; font-weight: 600; width: 200px;">Channel Name</td>
                        <td style="border: none; padding: 8px 0;">${escapeHtml(channel.channelName)}</td>
                    </tr>
                    <tr style="background: none;">
                        <td style="border: none; padding: 8px 0; font-weight: 600;">Channel Type</td>
                        <td style="border: none; padding: 8px 0;">${escapeHtml(channel.channelType || 'Unknown')}</td>
                    </tr>
                    <tr style="background: none;">
                        <td style="border: none; padding: 8px 0; font-weight: 600;">Last Publisher</td>
                        <td style="border: none; padding: 8px 0; font-family: monospace;">${escapeHtml(channel.lastPublisher || '-')}</td>
                    </tr>
                    <tr style="background: none;">
                        <td style="border: none; padding: 8px 0; font-weight: 600;">Creation Time</td>
                        <td style="border: none; padding: 8px 0;">${channel.createdAt ? formatDateTime(channel.createdAt) : '-'}</td>
                    </tr>
                </tbody>
            </table>
        </div>

        <!-- Subscriptions for this Channel -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">Subscriptions (${channel.subscriptions ? channel.subscriptions.length : 0})</h3>
            <div id="subscriptions-list">
                ${renderSubscriptionsList()}
            </div>
        </div>

        <!-- State Viewer for Stateful Channels -->
        ${channel.channelType && channel.channelType.includes('Stateful') ? renderStateViewer() : ''}
    `;

    // Add click handlers for subscription rows
    const subRows = container.querySelectorAll('.subscription-row');
    subRows.forEach(row => {
        const subId = row.getAttribute('data-subscription-id');
        if (subId) {
            row.style.cursor = 'pointer';
            row.addEventListener('click', () => {
                window.location.hash = `#/subscriptions/${subId}`;
            });
        }
    });
}

function renderSubscriptionsList() {
    if (!channel.subscriptions || channel.subscriptions.length === 0) {
        return `
            <div style="text-align: center; padding: 24px; color: var(--text-secondary);">
                No subscriptions for this channel
            </div>
        `;
    }

    return `
        <table>
            <thead>
                <tr>
                    <th>Subscription ID</th>
                    <th>Channel Pattern</th>
                    <th>Status</th>
                    <th>Queue Depth</th>
                    <th>Process Rate</th>
                    <th>Avg Latency</th>
                </tr>
            </thead>
            <tbody>
                ${channel.subscriptions.map(sub => `
                    <tr class="subscription-row" data-subscription-id="${sub.subscriptionId}">
                        <td><strong>${sub.subscriptionId}</strong></td>
                        <td><code>${escapeHtml(sub.channelPattern)}</code></td>
                        <td>
                            <span class="status-badge ${getSubscriptionStatusClass(sub)}">
                                ${getSubscriptionStatusLabel(sub)}
                            </span>
                        </td>
                        <td>${sub.queueDepth || 0}</td>
                        <td>${formatRate(sub.processRate || 0)}/s</td>
                        <td>${formatLatency(sub.avgLatency)}</td>
                    </tr>
                `).join('')}
            </tbody>
        </table>
    `;
}

function renderStateViewer() {
    // TODO: Implement state viewer when API supports it
    // For now, show placeholder
    return `
        <div class="card" style="margin-bottom: 24px;">
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
                <h3 class="card-title" style="margin: 0;">Channel State</h3>
                <button class="btn btn-secondary" onclick="alert('State viewer coming soon')">
                    View State
                </button>
            </div>
            <div style="text-align: center; padding: 24px; color: var(--text-secondary); background-color: var(--bg-tertiary); border-radius: 6px;">
                State viewer for stateful channels - Coming soon
            </div>
        </div>
    `;
}

function showResetConfirmation() {
    if (confirm(`Are you sure you want to reset channel "${channelName}"?\n\nThis will clear all stored messages and reset channel statistics. This action cannot be undone.`)) {
        resetChannel();
    }
}

async function resetChannel() {
    try {
        await api.resetChannel(channelName);
        alert(`Channel "${channelName}" has been reset successfully.`);
        await loadChannelData();
    } catch (error) {
        alert(`Failed to reset channel: ${error.message}`);
    }
}

function handleMetricsUpdate(metrics) {
    if (metrics.channels) {
        const updated = metrics.channels.find(c => c.channelName === channelName);
        if (updated) {
            channel = { ...channel, ...updated };
            renderChannelContent();
        }
    }
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

function formatDateTime(dateStr) {
    if (!dateStr) return '-';
    const date = new Date(dateStr);
    return date.toLocaleString();
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

export default { render, cleanup };
