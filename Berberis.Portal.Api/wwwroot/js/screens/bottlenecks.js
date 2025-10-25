// Bottleneck Detection View

import { api } from '../api-client.js';
import { signalRClient } from '../signalr-client.js';

let subscriptions = [];
let metricsUnsubscribe = null;

// Configurable thresholds
let thresholds = {
    queueDepthWarning: 1000,
    queueDepthCritical: 5000,
    processRateWarning: 100, // msg/s - below this is slow
    latencyWarning: 100, // ms
    latencyCritical: 500, // ms
    timeoutWarningPercent: 80, // % of timeout threshold
    conflationEffectivenessMin: 0.2 // 20% minimum effectiveness
};

const bottleneckHistory = []; // Track bottlenecks over time

export async function render(container) {
    container.innerHTML = `
        <div class="page-header">
            <div>
                <h2 class="page-title">Bottleneck Detection</h2>
                <p class="page-description">Identify and diagnose performance bottlenecks in real-time</p>
            </div>
            <div style="display: flex; gap: 12px;">
                <button id="config-thresholds-button" class="btn btn-secondary" title="Configure alert thresholds">
                    ⚙ Configure Thresholds
                </button>
                <button id="refresh-bottlenecks-button" class="btn btn-secondary" title="Refresh">
                    <span style="font-size: 16px;">↻</span> Refresh
                </button>
                <button id="export-bottlenecks-button" class="btn btn-secondary" title="Export bottleneck report">
                    Export Report
                </button>
            </div>
        </div>

        <!-- Alert Summary -->
        <div id="alert-summary" style="margin-bottom: 24px;"></div>

        <div id="bottlenecks-content">
            <div class="loading">Loading bottleneck analysis...</div>
        </div>
    `;

    // Set up event listeners
    setupEventListeners();

    // Load initial data
    await loadBottlenecksData();

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
    // Config button
    const configButton = document.getElementById('config-thresholds-button');
    if (configButton) {
        configButton.addEventListener('click', showThresholdsConfig);
    }

    // Refresh button
    const refreshButton = document.getElementById('refresh-bottlenecks-button');
    if (refreshButton) {
        refreshButton.addEventListener('click', async () => {
            refreshButton.disabled = true;
            refreshButton.innerHTML = '<span style="font-size: 16px;">↻</span> Refreshing...';
            await loadBottlenecksData();
            refreshButton.disabled = false;
            refreshButton.innerHTML = '<span style="font-size: 16px;">↻</span> Refresh';
        });
    }

    // Export button
    const exportButton = document.getElementById('export-bottlenecks-button');
    if (exportButton) {
        exportButton.addEventListener('click', exportBottleneckReport);
    }
}

async function loadBottlenecksData() {
    try {
        subscriptions = await api.getSubscriptions() || [];
        renderBottlenecksContent();
    } catch (error) {
        console.error('Failed to load bottlenecks data:', error);
        const container = document.getElementById('bottlenecks-content');
        if (container) {
            container.innerHTML = `
                <div class="error-message">Failed to load bottleneck analysis: ${error.message}</div>
            `;
        }
    }
}

function renderBottlenecksContent() {
    // Analyze bottlenecks
    const analysis = analyzeBottlenecks();

    // Render alert summary
    renderAlertSummary(analysis);

    // Render main content
    const container = document.getElementById('bottlenecks-content');
    if (!container) return;

    container.innerHTML = `
        <!-- High Queue Depth -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">⚠ Top Subscriptions by Queue Depth</h3>
            <p style="color: var(--text-secondary); font-size: 13px; margin-top: 8px;">
                Subscriptions with the highest backlog of pending messages
            </p>
            ${renderHighQueueDepthWidget(analysis.highQueueDepth)}
        </div>

        <!-- Slow Subscriptions -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">🐌 Slowest Subscriptions by Process Rate</h3>
            <p style="color: var(--text-secondary); font-size: 13px; margin-top: 8px;">
                Subscriptions processing messages at the slowest rates
            </p>
            ${renderSlowSubscriptionsWidget(analysis.slowSubscriptions)}
        </div>

        <!-- Approaching Timeout -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">⏰ Subscriptions Approaching Timeout</h3>
            <p style="color: var(--text-secondary); font-size: 13px; margin-top: 8px;">
                Subscriptions with high latency relative to timeout threshold
            </p>
            ${renderApproachingTimeoutWidget(analysis.approachingTimeout)}
        </div>

        <!-- Low Conflation Effectiveness -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">📊 Low Conflation Effectiveness</h3>
            <p style="color: var(--text-secondary); font-size: 13px; margin-top: 8px;">
                Conflated subscriptions with low effectiveness ratios
            </p>
            ${renderLowConflationWidget(analysis.lowConflation)}
        </div>

        <!-- Historical Bottleneck Tracking -->
        <div class="card">
            <h3 class="card-title">📈 Historical Bottleneck Tracking</h3>
            <p style="color: var(--text-secondary); font-size: 13px; margin-top: 8px;">
                Subscriptions that have been bottlenecks over time
            </p>
            ${renderHistoricalBottlenecks()}
        </div>
    `;

    // Add click handlers for subscription links
    container.querySelectorAll('[data-subscription-id]').forEach(el => {
        el.style.cursor = 'pointer';
        el.addEventListener('click', (e) => {
            e.preventDefault();
            const subId = el.getAttribute('data-subscription-id');
            window.location.hash = `#/subscriptions/${subId}`;
        });
    });
}

function analyzeBottlenecks() {
    // High queue depth
    const highQueueDepth = subscriptions
        .filter(sub => (sub.queueDepth || 0) > 0)
        .sort((a, b) => (b.queueDepth || 0) - (a.queueDepth || 0))
        .slice(0, 10)
        .map(sub => ({
            ...sub,
            severity: getSeverity('queueDepth', sub.queueDepth)
        }));

    // Slow process rates
    const slowSubscriptions = subscriptions
        .filter(sub => (sub.processRate || 0) > 0 && (sub.processRate || 0) < thresholds.processRateWarning)
        .sort((a, b) => (a.processRate || 0) - (b.processRate || 0))
        .slice(0, 10)
        .map(sub => ({
            ...sub,
            severity: 'warning'
        }));

    // Approaching timeout
    const approachingTimeout = subscriptions
        .filter(sub => {
            const latency = sub.avgLatencyMs || 0;
            return latency > thresholds.latencyWarning;
        })
        .sort((a, b) => (b.avgLatencyMs || 0) - (a.avgLatencyMs || 0))
        .slice(0, 10)
        .map(sub => ({
            ...sub,
            severity: getSeverity('latency', sub.avgLatencyMs)
        }));

    // Low conflation effectiveness
    const lowConflation = subscriptions
        .filter(sub =>
            sub.conflationInterval &&
            sub.conflationRatio !== null &&
            sub.conflationRatio !== undefined &&
            sub.conflationRatio < thresholds.conflationEffectivenessMin
        )
        .sort((a, b) => (a.conflationRatio || 0) - (b.conflationRatio || 0))
        .slice(0, 10)
        .map(sub => ({
            ...sub,
            severity: 'warning'
        }));

    // Track historical bottlenecks
    const now = Date.now();
    [...highQueueDepth, ...slowSubscriptions, ...approachingTimeout].forEach(sub => {
        const existing = bottleneckHistory.find(b => b.subscriptionId === sub.id);
        if (existing) {
            existing.lastSeen = now;
            existing.occurrences++;
        } else {
            bottleneckHistory.push({
                subscriptionId: sub.id,
                channelPattern: sub.channelPattern,
                firstSeen: now,
                lastSeen: now,
                occurrences: 1
            });
        }
    });

    // Clean old history (> 1 hour)
    const oneHourAgo = now - (60 * 60 * 1000);
    bottleneckHistory.splice(0, bottleneckHistory.length, ...bottleneckHistory.filter(b => b.lastSeen > oneHourAgo));

    return {
        highQueueDepth,
        slowSubscriptions,
        approachingTimeout,
        lowConflation
    };
}

function renderAlertSummary(analysis) {
    const container = document.getElementById('alert-summary');
    if (!container) return;

    const criticalCount = [
        ...analysis.highQueueDepth,
        ...analysis.slowSubscriptions,
        ...analysis.approachingTimeout,
        ...analysis.lowConflation
    ].filter(item => item.severity === 'critical').length;

    const warningCount = [
        ...analysis.highQueueDepth,
        ...analysis.slowSubscriptions,
        ...analysis.approachingTimeout,
        ...analysis.lowConflation
    ].filter(item => item.severity === 'warning').length;

    if (criticalCount === 0 && warningCount === 0) {
        container.innerHTML = `
            <div style="padding: 16px; background-color: #d1fae5; border: 1px solid #10b981; border-radius: 8px; display: flex; align-items: center; gap: 12px;">
                <span style="font-size: 24px;">✓</span>
                <div>
                    <div style="font-weight: 600; color: #065f46;">All Systems Healthy</div>
                    <div style="font-size: 13px; color: #047857;">No bottlenecks detected</div>
                </div>
            </div>
        `;
        return;
    }

    container.innerHTML = `
        <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px;">
            ${criticalCount > 0 ? `
                <div style="padding: 16px; background-color: #fee2e2; border: 2px solid #ef4444; border-radius: 8px;">
                    <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 4px;">
                        <span style="font-size: 20px;">🔴</span>
                        <span style="font-weight: 600; color: #991b1b;">Critical Alerts</span>
                    </div>
                    <div style="font-size: 28px; font-weight: 700; color: #dc2626;">${criticalCount}</div>
                    <div style="font-size: 12px; color: #991b1b; margin-top: 4px;">Immediate attention required</div>
                </div>
            ` : ''}
            ${warningCount > 0 ? `
                <div style="padding: 16px; background-color: #fef3c7; border: 2px solid #f59e0b; border-radius: 8px;">
                    <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 4px;">
                        <span style="font-size: 20px;">⚠</span>
                        <span style="font-weight: 600; color: #92400e;">Warning Alerts</span>
                    </div>
                    <div style="font-size: 28px; font-weight: 700; color: #d97706;">${warningCount}</div>
                    <div style="font-size: 12px; color: #92400e; margin-top: 4px;">Monitor closely</div>
                </div>
            ` : ''}
        </div>
    `;
}

function renderHighQueueDepthWidget(items) {
    if (items.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">No subscriptions with high queue depth</div>';
    }

    const maxQueueDepth = Math.max(...items.map(item => item.queueDepth || 0));

    return `
        <div style="margin-top: 16px;">
            <table style="width: 100%;">
                <thead>
                    <tr>
                        <th style="text-align: left;">Severity</th>
                        <th style="text-align: left;">Subscription</th>
                        <th style="text-align: left;">Channel Pattern</th>
                        <th style="text-align: right;">Queue Depth</th>
                        <th style="text-align: right;">Process Rate</th>
                        <th style="text-align: center;">Action</th>
                    </tr>
                </thead>
                <tbody>
                    ${items.map(item => {
                        const widthPercent = maxQueueDepth > 0 ? ((item.queueDepth || 0) / maxQueueDepth) * 100 : 0;
                        return `
                            <tr>
                                <td>
                                    ${renderSeverityBadge(item.severity)}
                                </td>
                                <td>
                                    <a href="#" data-subscription-id="${item.id}" style="color: var(--primary-color); text-decoration: none; font-weight: 600;">
                                        ${escapeHtml(item.id)}
                                    </a>
                                </td>
                                <td>
                                    <code style="font-size: 12px;">${escapeHtml(item.channelPattern)}</code>
                                </td>
                                <td style="text-align: right;">
                                    <div style="display: flex; align-items: center; justify-content: flex-end; gap: 8px;">
                                        <div style="flex: 0 0 60px; height: 20px; background-color: var(--bg-tertiary); border-radius: 3px; position: relative; overflow: hidden;">
                                            <div style="width: ${widthPercent}%; height: 100%; background-color: ${item.severity === 'critical' ? '#ef4444' : '#f59e0b'};"></div>
                                        </div>
                                        <strong style="min-width: 60px; text-align: right;">${formatNumber(item.queueDepth || 0)}</strong>
                                    </div>
                                </td>
                                <td style="text-align: right;">${formatRate(item.processRate || 0)}/s</td>
                                <td style="text-align: center;">
                                    <button
                                        class="btn btn-sm btn-secondary"
                                        data-subscription-id="${item.id}"
                                        style="padding: 4px 8px; font-size: 12px;"
                                    >
                                        View Details
                                    </button>
                                </td>
                            </tr>
                        `;
                    }).join('')}
                </tbody>
            </table>
        </div>
    `;
}

function renderSlowSubscriptionsWidget(items) {
    if (items.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">No slow subscriptions detected</div>';
    }

    return `
        <div style="margin-top: 16px;">
            <table style="width: 100%;">
                <thead>
                    <tr>
                        <th style="text-align: left;">Severity</th>
                        <th style="text-align: left;">Subscription</th>
                        <th style="text-align: left;">Channel Pattern</th>
                        <th style="text-align: right;">Process Rate</th>
                        <th style="text-align: right;">Queue Depth</th>
                        <th style="text-align: right;">Avg Latency</th>
                        <th style="text-align: center;">Action</th>
                    </tr>
                </thead>
                <tbody>
                    ${items.map(item => `
                        <tr>
                            <td>
                                ${renderSeverityBadge(item.severity)}
                            </td>
                            <td>
                                <a href="#" data-subscription-id="${item.id}" style="color: var(--primary-color); text-decoration: none; font-weight: 600;">
                                    ${escapeHtml(item.id)}
                                </a>
                            </td>
                            <td>
                                <code style="font-size: 12px;">${escapeHtml(item.channelPattern)}</code>
                            </td>
                            <td style="text-align: right;">
                                <strong style="color: #f59e0b;">${formatRate(item.processRate || 0)}/s</strong>
                            </td>
                            <td style="text-align: right;">${formatNumber(item.queueDepth || 0)}</td>
                            <td style="text-align: right;">${formatLatency(item.avgLatencyMs)}</td>
                            <td style="text-align: center;">
                                <button
                                    class="btn btn-sm btn-secondary"
                                    data-subscription-id="${item.id}"
                                    style="padding: 4px 8px; font-size: 12px;"
                                >
                                    View Details
                                </button>
                            </td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        </div>
    `;
}

function renderApproachingTimeoutWidget(items) {
    if (items.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">No subscriptions approaching timeout</div>';
    }

    return `
        <div style="margin-top: 16px;">
            <table style="width: 100%;">
                <thead>
                    <tr>
                        <th style="text-align: left;">Severity</th>
                        <th style="text-align: left;">Subscription</th>
                        <th style="text-align: left;">Channel Pattern</th>
                        <th style="text-align: right;">Avg Latency</th>
                        <th style="text-align: right;">P99 Latency</th>
                        <th style="text-align: right;">Timeouts</th>
                        <th style="text-align: center;">Action</th>
                    </tr>
                </thead>
                <tbody>
                    ${items.map(item => `
                        <tr>
                            <td>
                                ${renderSeverityBadge(item.severity)}
                            </td>
                            <td>
                                <a href="#" data-subscription-id="${item.id}" style="color: var(--primary-color); text-decoration: none; font-weight: 600;">
                                    ${escapeHtml(item.id)}
                                </a>
                            </td>
                            <td>
                                <code style="font-size: 12px;">${escapeHtml(item.channelPattern)}</code>
                            </td>
                            <td style="text-align: right;">
                                <strong style="color: ${item.severity === 'critical' ? '#ef4444' : '#f59e0b'};">${formatLatency(item.avgLatencyMs)}</strong>
                            </td>
                            <td style="text-align: right;">${formatLatency(item.percentileLatencyMs)}</td>
                            <td style="text-align: right;">
                                <strong style="color: ${(item.timeoutCount || 0) > 0 ? '#ef4444' : 'inherit'};">
                                    ${formatNumber(item.timeoutCount || 0)}
                                </strong>
                            </td>
                            <td style="text-align: center;">
                                <button
                                    class="btn btn-sm btn-secondary"
                                    data-subscription-id="${item.id}"
                                    style="padding: 4px 8px; font-size: 12px;"
                                >
                                    View Details
                                </button>
                            </td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        </div>
    `;
}

function renderLowConflationWidget(items) {
    if (items.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">No conflated subscriptions with low effectiveness</div>';
    }

    return `
        <div style="margin-top: 16px;">
            <table style="width: 100%;">
                <thead>
                    <tr>
                        <th style="text-align: left;">Severity</th>
                        <th style="text-align: left;">Subscription</th>
                        <th style="text-align: left;">Channel Pattern</th>
                        <th style="text-align: right;">Conflation Ratio</th>
                        <th style="text-align: right;">Conflation Interval</th>
                        <th style="text-align: center;">Action</th>
                    </tr>
                </thead>
                <tbody>
                    ${items.map(item => `
                        <tr>
                            <td>
                                ${renderSeverityBadge(item.severity)}
                            </td>
                            <td>
                                <a href="#" data-subscription-id="${item.id}" style="color: var(--primary-color); text-decoration: none; font-weight: 600;">
                                    ${escapeHtml(item.id)}
                                </a>
                            </td>
                            <td>
                                <code style="font-size: 12px;">${escapeHtml(item.channelPattern)}</code>
                            </td>
                            <td style="text-align: right;">
                                <strong style="color: #f59e0b;">${formatPercentage(item.conflationRatio)}</strong>
                                <span style="font-size: 11px; color: var(--text-secondary); margin-left: 4px;">(low)</span>
                            </td>
                            <td style="text-align: right;">${formatDuration(item.conflationInterval)}</td>
                            <td style="text-align: center;">
                                <button
                                    class="btn btn-sm btn-secondary"
                                    data-subscription-id="${item.id}"
                                    style="padding: 4px 8px; font-size: 12px;"
                                >
                                    View Details
                                </button>
                            </td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        </div>
    `;
}

function renderHistoricalBottlenecks() {
    if (bottleneckHistory.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">No historical bottleneck data available yet</div>';
    }

    // Sort by occurrences
    const sorted = [...bottleneckHistory].sort((a, b) => b.occurrences - a.occurrences).slice(0, 10);

    return `
        <div style="margin-top: 16px;">
            <table style="width: 100%;">
                <thead>
                    <tr>
                        <th style="text-align: left;">Subscription</th>
                        <th style="text-align: left;">Channel Pattern</th>
                        <th style="text-align: right;">Occurrences</th>
                        <th style="text-align: right;">First Seen</th>
                        <th style="text-align: right;">Last Seen</th>
                        <th style="text-align: center;">Action</th>
                    </tr>
                </thead>
                <tbody>
                    ${sorted.map(item => `
                        <tr>
                            <td>
                                <a href="#" data-subscription-id="${item.subscriptionId}" style="color: var(--primary-color); text-decoration: none; font-weight: 600;">
                                    ${escapeHtml(item.subscriptionId)}
                                </a>
                            </td>
                            <td>
                                <code style="font-size: 12px;">${escapeHtml(item.channelPattern)}</code>
                            </td>
                            <td style="text-align: right;">
                                <strong>${item.occurrences}</strong>
                                <span style="font-size: 11px; color: var(--text-secondary); margin-left: 4px;">times</span>
                            </td>
                            <td style="text-align: right; font-size: 12px; color: var(--text-secondary);">
                                ${formatTimeAgo(item.firstSeen)}
                            </td>
                            <td style="text-align: right; font-size: 12px; color: var(--text-secondary);">
                                ${formatTimeAgo(item.lastSeen)}
                            </td>
                            <td style="text-align: center;">
                                <button
                                    class="btn btn-sm btn-secondary"
                                    data-subscription-id="${item.subscriptionId}"
                                    style="padding: 4px 8px; font-size: 12px;"
                                >
                                    View Details
                                </button>
                            </td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
        </div>
    `;
}

function showThresholdsConfig() {
    const html = `
        <div style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background-color: rgba(0,0,0,0.5); display: flex; align-items: center; justify-content: center; z-index: 1000;" id="config-modal">
            <div style="background: white; border-radius: 8px; padding: 24px; max-width: 500px; width: 90%;">
                <h3 style="margin-top: 0;">Configure Alert Thresholds</h3>

                <div style="margin-bottom: 16px;">
                    <label style="display: block; font-weight: 600; margin-bottom: 4px; font-size: 13px;">Queue Depth Warning</label>
                    <input type="number" id="threshold-queue-warning" value="${thresholds.queueDepthWarning}" style="width: 100%; padding: 8px; border: 1px solid var(--border-color); border-radius: 4px;" />
                </div>

                <div style="margin-bottom: 16px;">
                    <label style="display: block; font-weight: 600; margin-bottom: 4px; font-size: 13px;">Queue Depth Critical</label>
                    <input type="number" id="threshold-queue-critical" value="${thresholds.queueDepthCritical}" style="width: 100%; padding: 8px; border: 1px solid var(--border-color); border-radius: 4px;" />
                </div>

                <div style="margin-bottom: 16px;">
                    <label style="display: block; font-weight: 600; margin-bottom: 4px; font-size: 13px;">Process Rate Warning (msg/s - below this is slow)</label>
                    <input type="number" id="threshold-process-rate" value="${thresholds.processRateWarning}" style="width: 100%; padding: 8px; border: 1px solid var(--border-color); border-radius: 4px;" />
                </div>

                <div style="margin-bottom: 16px;">
                    <label style="display: block; font-weight: 600; margin-bottom: 4px; font-size: 13px;">Latency Warning (ms)</label>
                    <input type="number" id="threshold-latency-warning" value="${thresholds.latencyWarning}" style="width: 100%; padding: 8px; border: 1px solid var(--border-color); border-radius: 4px;" />
                </div>

                <div style="margin-bottom: 16px;">
                    <label style="display: block; font-weight: 600; margin-bottom: 4px; font-size: 13px;">Latency Critical (ms)</label>
                    <input type="number" id="threshold-latency-critical" value="${thresholds.latencyCritical}" style="width: 100%; padding: 8px; border: 1px solid var(--border-color); border-radius: 4px;" />
                </div>

                <div style="margin-bottom: 24px;">
                    <label style="display: block; font-weight: 600; margin-bottom: 4px; font-size: 13px;">Min Conflation Effectiveness (%)</label>
                    <input type="number" id="threshold-conflation" value="${thresholds.conflationEffectivenessMin * 100}" min="0" max="100" step="1" style="width: 100%; padding: 8px; border: 1px solid var(--border-color); border-radius: 4px;" />
                </div>

                <div style="display: flex; gap: 12px; justify-content: flex-end;">
                    <button class="btn btn-secondary" onclick="document.getElementById('config-modal').remove();">Cancel</button>
                    <button class="btn btn-primary" id="save-thresholds-button">Save</button>
                </div>
            </div>
        </div>
    `;

    document.body.insertAdjacentHTML('beforeend', html);

    document.getElementById('save-thresholds-button').addEventListener('click', () => {
        thresholds.queueDepthWarning = parseInt(document.getElementById('threshold-queue-warning').value) || 1000;
        thresholds.queueDepthCritical = parseInt(document.getElementById('threshold-queue-critical').value) || 5000;
        thresholds.processRateWarning = parseInt(document.getElementById('threshold-process-rate').value) || 100;
        thresholds.latencyWarning = parseInt(document.getElementById('threshold-latency-warning').value) || 100;
        thresholds.latencyCritical = parseInt(document.getElementById('threshold-latency-critical').value) || 500;
        thresholds.conflationEffectivenessMin = parseFloat(document.getElementById('threshold-conflation').value) / 100 || 0.2;

        document.getElementById('config-modal').remove();
        renderBottlenecksContent();
    });
}

function exportBottleneckReport() {
    const analysis = analyzeBottlenecks();
    const csvLines = [];

    // Header
    csvLines.push('Berberis Portal - Bottleneck Report');
    csvLines.push(`Generated: ${new Date().toISOString()}`);
    csvLines.push('');

    // High Queue Depth
    csvLines.push('HIGH QUEUE DEPTH SUBSCRIPTIONS');
    csvLines.push('Subscription ID,Channel Pattern,Queue Depth,Process Rate,Severity');
    analysis.highQueueDepth.forEach(item => {
        csvLines.push(`"${item.id}","${item.channelPattern}",${item.queueDepth},${item.processRate},${item.severity}`);
    });
    csvLines.push('');

    // Slow Subscriptions
    csvLines.push('SLOW SUBSCRIPTIONS');
    csvLines.push('Subscription ID,Channel Pattern,Process Rate,Queue Depth,Avg Latency');
    analysis.slowSubscriptions.forEach(item => {
        csvLines.push(`"${item.id}","${item.channelPattern}",${item.processRate},${item.queueDepth},${item.avgLatencyMs}`);
    });
    csvLines.push('');

    // Approaching Timeout
    csvLines.push('SUBSCRIPTIONS APPROACHING TIMEOUT');
    csvLines.push('Subscription ID,Channel Pattern,Avg Latency,P99 Latency,Timeouts');
    analysis.approachingTimeout.forEach(item => {
        csvLines.push(`"${item.id}","${item.channelPattern}",${item.avgLatencyMs},${item.percentileLatencyMs},${item.timeoutCount}`);
    });
    csvLines.push('');

    // Low Conflation
    csvLines.push('LOW CONFLATION EFFECTIVENESS');
    csvLines.push('Subscription ID,Channel Pattern,Conflation Ratio');
    analysis.lowConflation.forEach(item => {
        csvLines.push(`"${item.id}","${item.channelPattern}",${item.conflationRatio}`);
    });

    // Create and download file
    const csvContent = csvLines.join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    const url = URL.createObjectURL(blob);
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').substring(0, 19);
    link.setAttribute('href', url);
    link.setAttribute('download', `berberis-bottleneck-report-${timestamp}.csv`);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

function handleMetricsUpdate(metrics) {
    if (metrics.subscriptions) {
        subscriptions = subscriptions.map(sub => {
            const updated = metrics.subscriptions.find(s => s.id === sub.id);
            return updated ? { ...sub, ...updated } : sub;
        });

        renderBottlenecksContent();
    }
}

function getSeverity(metric, value) {
    if (metric === 'queueDepth') {
        if (value >= thresholds.queueDepthCritical) return 'critical';
        if (value >= thresholds.queueDepthWarning) return 'warning';
    } else if (metric === 'latency') {
        if (value >= thresholds.latencyCritical) return 'critical';
        if (value >= thresholds.latencyWarning) return 'warning';
    }
    return 'info';
}

function renderSeverityBadge(severity) {
    const config = {
        critical: { icon: '🔴', label: 'CRITICAL', color: '#dc2626', bg: '#fee2e2' },
        warning: { icon: '⚠', label: 'WARNING', color: '#d97706', bg: '#fef3c7' },
        info: { icon: 'ℹ', label: 'INFO', color: '#2563eb', bg: '#dbeafe' }
    };

    const c = config[severity] || config.info;

    return `
        <span style="display: inline-flex; align-items: center; gap: 4px; padding: 4px 8px; background-color: ${c.bg}; border-radius: 4px; font-size: 11px; font-weight: 600; color: ${c.color};">
            <span>${c.icon}</span>
            <span>${c.label}</span>
        </span>
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

function formatLatency(ms) {
    if (ms === null || ms === undefined) return '-';
    if (ms < 1) return '<1ms';
    return Math.round(ms) + 'ms';
}

function formatDuration(duration) {
    if (!duration) return '-';
    if (typeof duration === 'string') return duration;
    return duration.toString();
}

function formatPercentage(ratio) {
    if (ratio === null || ratio === undefined) return '-';
    return (ratio * 100).toFixed(1) + '%';
}

function formatTimeAgo(timestamp) {
    const seconds = Math.floor((Date.now() - timestamp) / 1000);
    if (seconds < 60) return `${seconds}s ago`;
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    return `${days}d ago`;
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

export default { render, cleanup };
