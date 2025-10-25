// Overview Screen - System Dashboard

import { api } from '../api-client.js';
import { signalRClient } from '../signalr-client.js';
import { createSparkline, MetricsHistory } from '../utils/sparkline.js';

let metricsUnsubscribe = null;
let refreshInterval = null;
const metricsHistory = new MetricsHistory(20); // Keep last 20 data points
let autoRefreshEnabled = true; // Auto-refresh is on by default

export async function render(container) {
    // Initial render
    container.innerHTML = `
        <div class="page-header">
            <div>
                <h2 class="page-title">System Overview</h2>
                <p class="page-description">Monitor the health and performance of your Berberis CrossBar instance</p>
            </div>
            <div style="display: flex; gap: 12px; align-items: center;">
                <label style="display: flex; align-items: center; gap: 8px; cursor: pointer;">
                    <input type="checkbox" id="auto-refresh-toggle" checked style="cursor: pointer;">
                    <span style="font-size: 14px; color: var(--text-secondary);">Auto-refresh</span>
                </label>
                <button id="refresh-button" class="btn btn-secondary" title="Refresh now">
                    <span style="font-size: 16px;">↻</span> Refresh
                </button>
            </div>
        </div>

        <div id="overview-stats" class="stats-grid">
            <div class="loading">Loading metrics...</div>
        </div>

        <div class="card">
            <h3 class="card-title">Quick Links</h3>
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px;">
                <a href="#/channels" class="btn btn-secondary">View All Channels</a>
                <a href="#/subscriptions" class="btn btn-secondary">View All Subscriptions</a>
                <a href="#/bottlenecks" class="btn btn-secondary">Check Bottlenecks</a>
                <a href="#/lifecycle" class="btn btn-secondary">Lifecycle Events</a>
            </div>
        </div>
    `;

    // Load initial data
    await loadOverviewData();

    // Set up event listeners
    setupEventListeners();

    // Subscribe to real-time metrics updates if auto-refresh is enabled
    if (autoRefreshEnabled) {
        startAutoRefresh();
    }
}

function setupEventListeners() {
    // Refresh button
    const refreshButton = document.getElementById('refresh-button');
    if (refreshButton) {
        refreshButton.addEventListener('click', async () => {
            refreshButton.disabled = true;
            refreshButton.innerHTML = '<span style="font-size: 16px;">↻</span> Refreshing...';

            await loadOverviewData();

            refreshButton.disabled = false;
            refreshButton.innerHTML = '<span style="font-size: 16px;">↻</span> Refresh';
        });
    }

    // Auto-refresh toggle
    const autoRefreshToggle = document.getElementById('auto-refresh-toggle');
    if (autoRefreshToggle) {
        autoRefreshToggle.addEventListener('change', (e) => {
            autoRefreshEnabled = e.target.checked;

            if (autoRefreshEnabled) {
                startAutoRefresh();
            } else {
                stopAutoRefresh();
            }
        });
    }
}

async function startAutoRefresh() {
    // Subscribe to real-time metrics updates
    try {
        await signalRClient.subscribeToMetrics(5000); // Update every 5 seconds
        metricsUnsubscribe = signalRClient.onMetricsUpdate(handleMetricsUpdate);
    } catch (error) {
        console.error('Failed to subscribe to metrics:', error);
    }

    // Fallback: Poll every 10 seconds if SignalR fails
    refreshInterval = setInterval(loadOverviewData, 10000);
}

function stopAutoRefresh() {
    // Unsubscribe from SignalR
    if (metricsUnsubscribe) {
        metricsUnsubscribe();
        metricsUnsubscribe = null;
    }

    signalRClient.unsubscribeFromMetrics().catch(err => {
        console.error('Failed to unsubscribe from metrics:', err);
    });

    // Clear polling interval
    if (refreshInterval) {
        clearInterval(refreshInterval);
        refreshInterval = null;
    }
}

export async function cleanup() {
    stopAutoRefresh();
}

async function loadOverviewData() {
    try {
        const data = await api.getOverview();
        renderStats(data);
    } catch (error) {
        console.error('Failed to load overview:', error);
        document.getElementById('overview-stats').innerHTML = `
            <div class="error-message">Failed to load system overview: ${error.message}</div>
        `;
    }
}

function handleMetricsUpdate(metrics) {
    // Update stats in real-time from SignalR
    if (metrics.overview) {
        renderStats(metrics.overview);
    }
}

function renderStats(data) {
    const statsContainer = document.getElementById('overview-stats');
    if (!statsContainer) return;

    const activeChannels = data.activeChannels || 0;
    const totalChannels = data.totalChannels || 0;
    const activeSubscriptions = data.activeSubscriptions || 0;
    const totalSubscriptions = data.totalSubscriptions || 0;
    const suspendedSubscriptions = data.suspendedSubscriptions || 0;
    const detachedSubscriptions = data.detachedSubscriptions || 0;
    const throughput = data.systemThroughput || 0;
    const totalPublished = data.totalMessagesPublished || 0;
    const totalProcessed = data.totalMessagesProcessed || 0;
    const errorRate = data.systemErrorRate || 0;

    // Track metrics history for sparklines
    metricsHistory.add('throughput', throughput);
    metricsHistory.add('errorRate', errorRate * 100); // Store as percentage
    metricsHistory.add('activeChannels', activeChannels);
    metricsHistory.add('activeSubscriptions', activeSubscriptions);

    // Generate sparklines
    const throughputSparkline = createSparkline(metricsHistory.get('throughput'), {
        width: 120,
        height: 30,
        strokeColor: '#10b981',
        fillColor: 'rgba(16, 185, 129, 0.1)'
    });

    const errorRateSparkline = createSparkline(metricsHistory.get('errorRate'), {
        width: 120,
        height: 30,
        strokeColor: errorRate > 0.05 ? '#ef4444' : '#10b981',
        fillColor: errorRate > 0.05 ? 'rgba(239, 68, 68, 0.1)' : 'rgba(16, 185, 129, 0.1)'
    });

    const channelsSparkline = createSparkline(metricsHistory.get('activeChannels'), {
        width: 120,
        height: 30,
        strokeColor: '#60a5fa',
        fillColor: 'rgba(96, 165, 250, 0.1)'
    });

    const subscriptionsSparkline = createSparkline(metricsHistory.get('activeSubscriptions'), {
        width: 120,
        height: 30,
        strokeColor: '#a78bfa',
        fillColor: 'rgba(167, 139, 250, 0.1)'
    });

    statsContainer.innerHTML = `
        <div class="stat-card">
            <div class="stat-label">Total Channels</div>
            <div class="stat-value">${totalChannels}</div>
            <div class="stat-change">${activeChannels} active</div>
            <div class="stat-sparkline">${channelsSparkline}</div>
        </div>

        <div class="stat-card">
            <div class="stat-label">Total Subscriptions</div>
            <div class="stat-value">${totalSubscriptions}</div>
            <div class="stat-change">
                ${activeSubscriptions} active
                ${suspendedSubscriptions > 0 ? `, ${suspendedSubscriptions} suspended` : ''}
                ${detachedSubscriptions > 0 ? `, ${detachedSubscriptions} detached` : ''}
            </div>
            <div class="stat-sparkline">${subscriptionsSparkline}</div>
        </div>

        <div class="stat-card">
            <div class="stat-label">System Throughput</div>
            <div class="stat-value">${formatRate(throughput)}</div>
            <div class="stat-change">messages per second</div>
            <div class="stat-sparkline">${throughputSparkline}</div>
        </div>

        <div class="stat-card">
            <div class="stat-label">Messages Published</div>
            <div class="stat-value">${formatNumber(totalPublished)}</div>
            <div class="stat-change">total lifetime</div>
        </div>

        <div class="stat-card">
            <div class="stat-label">Messages Processed</div>
            <div class="stat-value">${formatNumber(totalProcessed)}</div>
            <div class="stat-change">total lifetime</div>
        </div>

        <div class="stat-card">
            <div class="stat-label">Error Rate</div>
            <div class="stat-value ${errorRate > 0.05 ? 'stat-change negative' : ''}">${(errorRate * 100).toFixed(2)}%</div>
            <div class="stat-change ${errorRate > 0.05 ? 'negative' : ''}">
                ${errorRate > 0.05 ? 'Needs attention' : 'Healthy'}
            </div>
            <div class="stat-sparkline">${errorRateSparkline}</div>
        </div>
    `;
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

export default { render, cleanup };
