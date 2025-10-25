// Subscription Detail Screen

import { api } from '../api-client.js';
import { signalRClient } from '../signalr-client.js';
import { createSparkline, MetricsHistory } from '../utils/sparkline.js';

let subscriptionId = '';
let subscription = null;
let metricsUnsubscribe = null;
const queueDepthHistory = new MetricsHistory(30);
const processRateHistory = new MetricsHistory(30);

export async function render(container, params) {
    subscriptionId = params[0] || '';

    if (!subscriptionId) {
        container.innerHTML = `
            <div class="error-message">Invalid subscription ID</div>
        `;
        return;
    }

    // Initial render with loading state
    container.innerHTML = `
        <div style="margin-bottom: 24px;">
            <nav style="margin-bottom: 16px;">
                <a href="#/subscriptions" style="color: var(--primary-color); text-decoration: none;">← Back to Subscriptions</a>
            </nav>
            <div class="page-header">
                <div>
                    <h2 class="page-title">Subscription ${escapeHtml(subscriptionId)}</h2>
                    <p class="page-description" id="subscription-pattern-label">Loading subscription details...</p>
                </div>
                <div style="display: flex; gap: 12px;">
                    <button id="refresh-subscription-button" class="btn btn-secondary" title="Refresh">
                        <span style="font-size: 16px;">↻</span> Refresh
                    </button>
                    <button id="suspend-button" class="btn btn-warning" title="Suspend subscription">
                        Suspend
                    </button>
                    <button id="resume-button" class="btn btn-success" title="Resume subscription">
                        Resume
                    </button>
                    <button id="detach-button" class="btn btn-danger" title="Detach subscription">
                        Detach
                    </button>
                </div>
            </div>
        </div>

        <div id="subscription-content">
            <div class="loading">Loading subscription details...</div>
        </div>
    `;

    // Set up event listeners
    setupEventListeners();

    // Load subscription data
    await loadSubscriptionData();

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
    queueDepthHistory.clear();
    processRateHistory.clear();
}

function setupEventListeners() {
    // Refresh button
    const refreshButton = document.getElementById('refresh-subscription-button');
    if (refreshButton) {
        refreshButton.addEventListener('click', async () => {
            refreshButton.disabled = true;
            refreshButton.innerHTML = '<span style="font-size: 16px;">↻</span> Refreshing...';
            await loadSubscriptionData();
            refreshButton.disabled = false;
            refreshButton.innerHTML = '<span style="font-size: 16px;">↻</span> Refresh';
        });
    }

    // Suspend button
    const suspendButton = document.getElementById('suspend-button');
    if (suspendButton) {
        suspendButton.addEventListener('click', () => showSuspendConfirmation());
    }

    // Resume button
    const resumeButton = document.getElementById('resume-button');
    if (resumeButton) {
        resumeButton.addEventListener('click', () => showResumeConfirmation());
    }

    // Detach button
    const detachButton = document.getElementById('detach-button');
    if (detachButton) {
        detachButton.addEventListener('click', () => showDetachConfirmation());
    }
}

async function loadSubscriptionData() {
    try {
        // Load subscription details
        subscription = await api.getSubscription(subscriptionId);

        if (!subscription) {
            const container = document.getElementById('subscription-content');
            if (container) {
                container.innerHTML = `
                    <div class="error-message">Subscription not found</div>
                `;
            }
            return;
        }

        // Update pattern label
        const patternLabel = document.getElementById('subscription-pattern-label');
        if (patternLabel) {
            patternLabel.innerHTML = `
                Channel Pattern: <code style="font-size: 14px;">${escapeHtml(subscription.channelPattern)}</code>
                &nbsp;<span class="status-badge ${getStatusClass(subscription)}">${subscription.status || 'Active'}</span>
            `;
        }

        // Render subscription content
        renderSubscriptionContent();
    } catch (error) {
        console.error('Failed to load subscription:', error);
        const container = document.getElementById('subscription-content');
        if (container) {
            container.innerHTML = `
                <div class="error-message">Failed to load subscription details: ${error.message}</div>
            `;
        }
    }
}

function renderSubscriptionContent() {
    const container = document.getElementById('subscription-content');
    if (!container || !subscription) return;

    // Track metrics for sparklines
    queueDepthHistory.add('depth', subscription.queueDepth || 0);
    processRateHistory.add('rate', subscription.processRate || 0);

    // Generate sparklines
    const queueDepthSparkline = createSparkline(queueDepthHistory.get('depth'), {
        width: 200,
        height: 60,
        strokeColor: '#f59e0b',
        fillColor: 'rgba(245, 158, 11, 0.1)'
    });

    const processRateSparkline = createSparkline(processRateHistory.get('rate'), {
        width: 200,
        height: 60,
        strokeColor: '#10b981',
        fillColor: 'rgba(16, 185, 129, 0.1)'
    });

    container.innerHTML = `
        <!-- Key Metrics -->
        <div class="stats-grid" style="margin-bottom: 24px;">
            <div class="stat-card">
                <div class="stat-label">Queue Depth</div>
                <div class="stat-value ${(subscription.queueDepth || 0) > 1000 ? 'text-warning' : ''}">${formatNumber(subscription.queueDepth || 0)}</div>
                <div class="stat-sparkline">${queueDepthSparkline}</div>
            </div>

            <div class="stat-card">
                <div class="stat-label">Process Rate</div>
                <div class="stat-value">${formatRate(subscription.processRate || 0)}/s</div>
                <div class="stat-sparkline">${processRateSparkline}</div>
            </div>

            <div class="stat-card">
                <div class="stat-label">Avg Latency</div>
                <div class="stat-value ${(subscription.avgLatencyMs || 0) > 100 ? 'text-warning' : ''}">${formatLatency(subscription.avgLatencyMs)}</div>
                <div class="stat-change">average response time</div>
            </div>

            <div class="stat-card">
                <div class="stat-label">P99 Latency</div>
                <div class="stat-value ${(subscription.percentileLatencyMs || 0) > 200 ? 'text-warning' : ''}">${formatLatency(subscription.percentileLatencyMs)}</div>
                <div class="stat-change">99th percentile</div>
            </div>

            <div class="stat-card">
                <div class="stat-label">Timeouts</div>
                <div class="stat-value ${(subscription.timeoutCount || 0) > 0 ? 'text-danger' : ''}">${formatNumber(subscription.timeoutCount || 0)}</div>
                <div class="stat-change">handler timeouts</div>
            </div>

            <div class="stat-card">
                <div class="stat-label">Total Processed</div>
                <div class="stat-value">${formatNumber(subscription.totalProcessed || 0)}</div>
                <div class="stat-change">lifetime messages</div>
            </div>
        </div>

        <!-- Subscription Metadata -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">Subscription Information</h3>
            <table style="width: 100%; border: none;">
                <tbody>
                    <tr style="background: none;">
                        <td style="border: none; padding: 8px 0; font-weight: 600; width: 220px;">Subscription ID</td>
                        <td style="border: none; padding: 8px 0;">${escapeHtml(subscription.id)}</td>
                    </tr>
                    <tr style="background: none;">
                        <td style="border: none; padding: 8px 0; font-weight: 600;">Channel Pattern</td>
                        <td style="border: none; padding: 8px 0;"><code>${escapeHtml(subscription.channelPattern)}</code></td>
                    </tr>
                    <tr style="background: none;">
                        <td style="border: none; padding: 8px 0; font-weight: 600;">Wildcard Subscription</td>
                        <td style="border: none; padding: 8px 0;">${subscription.isWildcard ? 'Yes' : 'No'}</td>
                    </tr>
                    <tr style="background: none;">
                        <td style="border: none; padding: 8px 0; font-weight: 600;">Status</td>
                        <td style="border: none; padding: 8px 0;">
                            <span class="status-badge ${getStatusClass(subscription)}">${subscription.status || 'Active'}</span>
                        </td>
                    </tr>
                    <tr style="background: none;">
                        <td style="border: none; padding: 8px 0; font-weight: 600;">Subscribed On</td>
                        <td style="border: none; padding: 8px 0;">${formatDateTime(subscription.subscribedOn)}</td>
                    </tr>
                    ${subscription.estimatedActiveMessages !== undefined ? `
                        <tr style="background: none;">
                            <td style="border: none; padding: 8px 0; font-weight: 600;">Estimated Concurrent Messages</td>
                            <td style="border: none; padding: 8px 0;">
                                <strong>${formatNumber(Math.round(subscription.estimatedActiveMessages))}</strong>
                                <span style="color: var(--text-secondary); font-size: 13px; margin-left: 8px;">
                                    (Little's Law: λ × W = ${formatRate(subscription.processRate || 0)}/s × ${formatLatency(subscription.avgLatencyMs)})
                                </span>
                            </td>
                        </tr>
                    ` : ''}
                </tbody>
            </table>
        </div>

        <!-- Latency Metrics Panel -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">Latency Metrics</h3>
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; margin-top: 16px;">
                <div style="padding: 12px; background-color: var(--bg-tertiary); border-radius: 6px;">
                    <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 4px;">Average</div>
                    <div style="font-size: 24px; font-weight: 600;">${formatLatency(subscription.avgLatencyMs)}</div>
                </div>
                <div style="padding: 12px; background-color: var(--bg-tertiary); border-radius: 6px;">
                    <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 4px;">Minimum</div>
                    <div style="font-size: 24px; font-weight: 600;">${formatLatency(subscription.minLatencyMs)}</div>
                </div>
                <div style="padding: 12px; background-color: var(--bg-tertiary); border-radius: 6px;">
                    <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 4px;">Maximum</div>
                    <div style="font-size: 24px; font-weight: 600;">${formatLatency(subscription.maxLatencyMs)}</div>
                </div>
                <div style="padding: 12px; background-color: var(--bg-tertiary); border-radius: 6px;">
                    <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 4px;">P99 (Percentile)</div>
                    <div style="font-size: 24px; font-weight: 600;">${formatLatency(subscription.percentileLatencyMs)}</div>
                </div>
            </div>
            ${renderLatencyHistogram()}
        </div>

        <!-- Service Time Metrics Panel -->
        ${subscription.avgServiceTimeMs !== undefined ? `
            <div class="card" style="margin-bottom: 24px;">
                <h3 class="card-title">Service Time Metrics</h3>
                <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; margin-top: 16px;">
                    <div style="padding: 12px; background-color: var(--bg-tertiary); border-radius: 6px;">
                        <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 4px;">Average</div>
                        <div style="font-size: 24px; font-weight: 600;">${formatLatency(subscription.avgServiceTimeMs)}</div>
                    </div>
                    <div style="padding: 12px; background-color: var(--bg-tertiary); border-radius: 6px;">
                        <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 4px;">Minimum</div>
                        <div style="font-size: 24px; font-weight: 600;">${formatLatency(subscription.minServiceTimeMs)}</div>
                    </div>
                    <div style="padding: 12px; background-color: var(--bg-tertiary); border-radius: 6px;">
                        <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 4px;">Maximum</div>
                        <div style="font-size: 24px; font-weight: 600;">${formatLatency(subscription.maxServiceTimeMs)}</div>
                    </div>
                    <div style="padding: 12px; background-color: var(--bg-tertiary); border-radius: 6px;">
                        <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 4px;">P99 (Percentile)</div>
                        <div style="font-size: 24px; font-weight: 600;">${formatLatency(subscription.percentileServiceTimeMs)}</div>
                    </div>
                </div>
                ${renderServiceTimeHistogram()}
            </div>
        ` : ''}

        <!-- Latency vs Service Time Breakdown -->
        ${subscription.latencyToResponseRatio !== undefined ? `
            <div class="card" style="margin-bottom: 24px;">
                <h3 class="card-title">Latency vs Service Time Breakdown</h3>
                <p style="color: var(--text-secondary); font-size: 13px; margin-top: 8px;">
                    Understanding where time is spent: waiting (latency) vs processing (service time)
                </p>
                <div style="margin-top: 16px;">
                    ${renderLatencyServiceTimeBreakdown()}
                </div>
            </div>
        ` : ''}

        <!-- Queue & Processing Statistics -->
        ${subscription.totalEnqueued !== undefined ? `
            <div class="card" style="margin-bottom: 24px;">
                <h3 class="card-title">Queue & Processing Statistics</h3>
                <table style="width: 100%; border: none; margin-top: 16px;">
                    <tbody>
                        <tr style="background: none;">
                            <td style="border: none; padding: 8px 0; font-weight: 600; width: 220px;">Current Queue Depth</td>
                            <td style="border: none; padding: 8px 0;">${formatNumber(subscription.queueDepth || 0)}</td>
                        </tr>
                        <tr style="background: none;">
                            <td style="border: none; padding: 8px 0; font-weight: 600;">Total Enqueued</td>
                            <td style="border: none; padding: 8px 0;">${formatNumber(subscription.totalEnqueued || 0)}</td>
                        </tr>
                        <tr style="background: none;">
                            <td style="border: none; padding: 8px 0; font-weight: 600;">Total Dequeued</td>
                            <td style="border: none; padding: 8px 0;">${formatNumber(subscription.totalDequeued || 0)}</td>
                        </tr>
                        <tr style="background: none;">
                            <td style="border: none; padding: 8px 0; font-weight: 600;">Process Rate</td>
                            <td style="border: none; padding: 8px 0;">${formatRate(subscription.processRate || 0)}/s</td>
                        </tr>
                        <tr style="background: none;">
                            <td style="border: none; padding: 8px 0; font-weight: 600;">Dequeue Rate</td>
                            <td style="border: none; padding: 8px 0;">${formatRate(subscription.dequeueRate || 0)}/s</td>
                        </tr>
                        <tr style="background: none;">
                            <td style="border: none; padding: 8px 0; font-weight: 600;">Average Response Time</td>
                            <td style="border: none; padding: 8px 0;">${formatLatency(subscription.avgResponseTimeMs)}</td>
                        </tr>
                    </tbody>
                </table>
            </div>
        ` : ''}

        <!-- Subscription Options -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">Subscription Options</h3>
            <table style="width: 100%; border: none; margin-top: 16px;">
                <tbody>
                    ${subscription.handlerTimeout ? `
                        <tr style="background: none;">
                            <td style="border: none; padding: 8px 0; font-weight: 600; width: 220px;">Handler Timeout</td>
                            <td style="border: none; padding: 8px 0;">${formatDuration(subscription.handlerTimeout)}</td>
                        </tr>
                    ` : ''}
                    ${subscription.backpressureStrategy ? `
                        <tr style="background: none;">
                            <td style="border: none; padding: 8px 0; font-weight: 600;">Backpressure Strategy</td>
                            <td style="border: none; padding: 8px 0;">${escapeHtml(subscription.backpressureStrategy)}</td>
                        </tr>
                    ` : ''}
                    ${subscription.conflationInterval ? `
                        <tr style="background: none;">
                            <td style="border: none; padding: 8px 0; font-weight: 600;">Conflation Interval</td>
                            <td style="border: none; padding: 8px 0;">${formatDuration(subscription.conflationInterval)}</td>
                        </tr>
                        <tr style="background: none;">
                            <td style="border: none; padding: 8px 0; font-weight: 600;">Conflation Ratio</td>
                            <td style="border: none; padding: 8px 0;">
                                <strong>${formatPercentage(subscription.conflationRatio)}</strong>
                                <span style="color: var(--text-secondary); font-size: 13px; margin-left: 8px;">
                                    (${subscription.conflationRatio > 0.5 ? 'Highly effective' : subscription.conflationRatio > 0.2 ? 'Moderately effective' : 'Low effectiveness'})
                                </span>
                            </td>
                        </tr>
                    ` : ''}
                </tbody>
            </table>
        </div>
    `;
}

function renderLatencyHistogram() {
    if (!subscription || subscription.minLatencyMs === undefined || subscription.maxLatencyMs === undefined) {
        return '';
    }

    // Simple histogram visualization using available data points
    const min = subscription.minLatencyMs || 0;
    const avg = subscription.avgLatencyMs || 0;
    const p99 = subscription.percentileLatencyMs || 0;
    const max = subscription.maxLatencyMs || 0;

    // Create bins for visualization (simplified)
    const bins = [
        { label: 'Min', value: min, color: '#10b981' },
        { label: 'Avg', value: avg, color: '#3b82f6' },
        { label: 'P99', value: p99, color: '#f59e0b' },
        { label: 'Max', value: max, color: '#ef4444' }
    ];

    const maxVal = Math.max(...bins.map(b => b.value));

    return `
        <div style="margin-top: 24px;">
            <div style="font-size: 13px; color: var(--text-secondary); margin-bottom: 12px;">Latency Distribution</div>
            <div style="display: flex; gap: 16px; align-items: flex-end; height: 150px;">
                ${bins.map(bin => {
                    const height = maxVal > 0 ? (bin.value / maxVal) * 100 : 0;
                    return `
                        <div style="flex: 1; display: flex; flex-direction: column; align-items: center; gap: 8px;">
                            <div style="font-size: 11px; color: var(--text-secondary);">${formatLatency(bin.value)}</div>
                            <div
                                style="width: 100%; height: ${height}%; background-color: ${bin.color}; border-radius: 4px 4px 0 0; min-height: 2px;"
                                title="${bin.label}: ${formatLatency(bin.value)}"
                            ></div>
                            <div style="font-size: 12px; font-weight: 600; color: var(--text-primary);">${bin.label}</div>
                        </div>
                    `;
                }).join('')}
            </div>
        </div>
    `;
}

function renderServiceTimeHistogram() {
    if (!subscription || subscription.minServiceTimeMs === undefined || subscription.maxServiceTimeMs === undefined) {
        return '';
    }

    // Simple histogram visualization using available data points
    const min = subscription.minServiceTimeMs || 0;
    const avg = subscription.avgServiceTimeMs || 0;
    const p99 = subscription.percentileServiceTimeMs || 0;
    const max = subscription.maxServiceTimeMs || 0;

    // Create bins for visualization (simplified)
    const bins = [
        { label: 'Min', value: min, color: '#10b981' },
        { label: 'Avg', value: avg, color: '#3b82f6' },
        { label: 'P99', value: p99, color: '#f59e0b' },
        { label: 'Max', value: max, color: '#ef4444' }
    ];

    const maxVal = Math.max(...bins.map(b => b.value));

    return `
        <div style="margin-top: 24px;">
            <div style="font-size: 13px; color: var(--text-secondary); margin-bottom: 12px;">Service Time Distribution</div>
            <div style="display: flex; gap: 16px; align-items: flex-end; height: 150px;">
                ${bins.map(bin => {
                    const height = maxVal > 0 ? (bin.value / maxVal) * 100 : 0;
                    return `
                        <div style="flex: 1; display: flex; flex-direction: column; align-items: center; gap: 8px;">
                            <div style="font-size: 11px; color: var(--text-secondary);">${formatLatency(bin.value)}</div>
                            <div
                                style="width: 100%; height: ${height}%; background-color: ${bin.color}; border-radius: 4px 4px 0 0; min-height: 2px;"
                                title="${bin.label}: ${formatLatency(bin.value)}"
                            ></div>
                            <div style="font-size: 12px; font-weight: 600; color: var(--text-primary);">${bin.label}</div>
                        </div>
                    `;
                }).join('')}
            </div>
        </div>
    `;
}

function renderLatencyServiceTimeBreakdown() {
    if (!subscription || subscription.latencyToResponseRatio === undefined) {
        return '<div style="color: var(--text-secondary);">No breakdown data available</div>';
    }

    const latencyRatio = subscription.latencyToResponseRatio;
    const serviceRatio = 1 - latencyRatio;
    const latencyPercent = latencyRatio * 100;
    const servicePercent = serviceRatio * 100;

    return `
        <div style="display: flex; flex-direction: column; gap: 16px;">
            <!-- Bar chart -->
            <div style="display: flex; height: 60px; background-color: var(--bg-tertiary); border-radius: 6px; overflow: hidden;">
                <div
                    style="background: linear-gradient(90deg, #f59e0b, #fbbf24); display: flex; align-items: center; justify-content: center; color: white; font-weight: 600; font-size: 14px;"
                    style="width: ${latencyPercent}%;"
                    title="Latency (Waiting): ${formatPercentage(latencyRatio)}"
                >
                    ${latencyPercent > 10 ? `Latency ${latencyPercent.toFixed(1)}%` : ''}
                </div>
                <div
                    style="background: linear-gradient(90deg, #3b82f6, #60a5fa); display: flex; align-items: center; justify-content: center; color: white; font-weight: 600; font-size: 14px; width: ${servicePercent}%;"
                    title="Service Time (Processing): ${formatPercentage(serviceRatio)}"
                >
                    ${servicePercent > 10 ? `Service ${servicePercent.toFixed(1)}%` : ''}
                </div>
            </div>

            <!-- Legend -->
            <div style="display: flex; gap: 24px; justify-content: center;">
                <div style="display: flex; align-items: center; gap: 8px;">
                    <div style="width: 16px; height: 16px; background: linear-gradient(90deg, #f59e0b, #fbbf24); border-radius: 3px;"></div>
                    <div style="font-size: 13px;">
                        <strong>Latency (Waiting)</strong>: ${formatLatency(subscription.avgLatencyMs)} (${formatPercentage(latencyRatio)})
                    </div>
                </div>
                <div style="display: flex; align-items: center; gap: 8px;">
                    <div style="width: 16px; height: 16px; background: linear-gradient(90deg, #3b82f6, #60a5fa); border-radius: 3px;"></div>
                    <div style="font-size: 13px;">
                        <strong>Service Time (Processing)</strong>: ${formatLatency(subscription.avgServiceTimeMs)} (${formatPercentage(serviceRatio)})
                    </div>
                </div>
            </div>

            <!-- Interpretation -->
            <div style="padding: 12px; background-color: var(--bg-tertiary); border-radius: 6px; font-size: 13px; color: var(--text-secondary);">
                ${latencyRatio > 0.7
                    ? '⚠ Most time is spent waiting in the queue. Consider increasing processing capacity or reducing message rate.'
                    : serviceRatio > 0.7
                    ? '⚠ Most time is spent in handler processing. Consider optimizing handler logic or reducing handler complexity.'
                    : '✓ Balanced distribution between waiting and processing time.'}
            </div>
        </div>
    `;
}

function showSuspendConfirmation() {
    if (confirm(`Are you sure you want to suspend subscription "${subscriptionId}"?\n\nThis will pause message processing. Messages will continue to queue.`)) {
        suspendSubscription();
    }
}

async function suspendSubscription() {
    try {
        await api.suspendSubscription(subscriptionId);
        alert(`Subscription "${subscriptionId}" has been suspended successfully.`);
        await loadSubscriptionData();
    } catch (error) {
        alert(`Failed to suspend subscription: ${error.message}`);
    }
}

function showResumeConfirmation() {
    if (confirm(`Are you sure you want to resume subscription "${subscriptionId}"?\n\nThis will resume message processing.`)) {
        resumeSubscription();
    }
}

async function resumeSubscription() {
    try {
        await api.resumeSubscription(subscriptionId);
        alert(`Subscription "${subscriptionId}" has been resumed successfully.`);
        await loadSubscriptionData();
    } catch (error) {
        alert(`Failed to resume subscription: ${error.message}`);
    }
}

function showDetachConfirmation() {
    if (confirm(`Are you sure you want to detach subscription "${subscriptionId}"?\n\nThis will permanently remove the subscription. This action cannot be undone.`)) {
        detachSubscription();
    }
}

async function detachSubscription() {
    try {
        await api.detachSubscription(subscriptionId);
        alert(`Subscription "${subscriptionId}" has been detached successfully.`);
        // Navigate back to subscriptions list
        window.location.hash = '#/subscriptions';
    } catch (error) {
        alert(`Failed to detach subscription: ${error.message}`);
    }
}

function handleMetricsUpdate(metrics) {
    if (metrics.subscriptions) {
        const updated = metrics.subscriptions.find(s => s.id === subscriptionId);
        if (updated) {
            subscription = { ...subscription, ...updated };
            renderSubscriptionContent();
        }
    }
}

function getStatusClass(sub) {
    const status = (sub.status || 'Active').toLowerCase();
    if (status === 'detached') return 'status-error';
    if (status === 'suspended') return 'status-warning';
    return 'status-healthy';
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

function formatDuration(duration) {
    if (!duration) return '-';
    // duration might be a string like "00:00:30" or an object
    if (typeof duration === 'string') {
        return duration;
    }
    // If it's a TimeSpan-like object, format it
    return duration.toString();
}

function formatPercentage(ratio) {
    if (ratio === null || ratio === undefined) return '-';
    return (ratio * 100).toFixed(1) + '%';
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

export default { render, cleanup };
