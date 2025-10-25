// Performance Analytics Dashboard

import { api } from '../api-client.js';
import { signalRClient } from '../signalr-client.js';
import { createSparkline, MetricsHistory } from '../utils/sparkline.js';

let channels = [];
let subscriptions = [];
let metricsUnsubscribe = null;
let selectedTimeRange = '15m'; // Default: last 15 minutes
const systemThroughputHistory = new MetricsHistory(100);
const channelThroughputHistory = new Map(); // channelName -> MetricsHistory

export async function render(container) {
    container.innerHTML = `
        <div class="page-header">
            <div>
                <h2 class="page-title">Performance Analytics</h2>
                <p class="page-description">System-wide performance metrics and trends</p>
            </div>
            <div style="display: flex; gap: 12px; align-items: center;">
                <select
                    id="time-range-selector"
                    class="filter-select"
                    style="padding: 8px 12px; border: 1px solid var(--border-color); border-radius: 6px; font-size: 14px; background-color: white; cursor: pointer;"
                >
                    <option value="5m">Last 5 minutes</option>
                    <option value="15m" selected>Last 15 minutes</option>
                    <option value="1h">Last 1 hour</option>
                    <option value="6h">Last 6 hours</option>
                    <option value="24h">Last 24 hours</option>
                </select>
                <button id="refresh-analytics-button" class="btn btn-secondary" title="Refresh">
                    <span style="font-size: 16px;">↻</span> Refresh
                </button>
                <button id="export-charts-button" class="btn btn-secondary" title="Export data">
                    Export CSV
                </button>
            </div>
        </div>

        <div id="analytics-content">
            <div class="loading">Loading analytics data...</div>
        </div>
    `;

    // Set up event listeners
    setupEventListeners();

    // Load initial data
    await loadAnalyticsData();

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

    // Clear histories
    systemThroughputHistory.clear();
    channelThroughputHistory.clear();
}

function setupEventListeners() {
    // Time range selector
    const timeRangeSelector = document.getElementById('time-range-selector');
    if (timeRangeSelector) {
        timeRangeSelector.addEventListener('change', (e) => {
            selectedTimeRange = e.target.value;
            // In a real implementation, this would trigger re-loading historical data
            renderAnalyticsContent();
        });
    }

    // Refresh button
    const refreshButton = document.getElementById('refresh-analytics-button');
    if (refreshButton) {
        refreshButton.addEventListener('click', async () => {
            refreshButton.disabled = true;
            refreshButton.innerHTML = '<span style="font-size: 16px;">↻</span> Refreshing...';
            await loadAnalyticsData();
            refreshButton.disabled = false;
            refreshButton.innerHTML = '<span style="font-size: 16px;">↻</span> Refresh';
        });
    }

    // Export button
    const exportButton = document.getElementById('export-charts-button');
    if (exportButton) {
        exportButton.addEventListener('click', exportToCSV);
    }
}

async function loadAnalyticsData() {
    try {
        // Load channels and subscriptions
        const [channelsData, subscriptionsData] = await Promise.all([
            api.getChannels(),
            api.getSubscriptions()
        ]);

        channels = channelsData || [];
        subscriptions = subscriptionsData || [];

        renderAnalyticsContent();
    } catch (error) {
        console.error('Failed to load analytics data:', error);
        const container = document.getElementById('analytics-content');
        if (container) {
            container.innerHTML = `
                <div class="error-message">Failed to load analytics data: ${error.message}</div>
            `;
        }
    }
}

function renderAnalyticsContent() {
    const container = document.getElementById('analytics-content');
    if (!container) return;

    // Calculate system-wide throughput
    const totalThroughput = channels.reduce((sum, ch) => sum + (ch.publishRate || 0), 0);
    systemThroughputHistory.add('throughput', totalThroughput);

    // Track per-channel throughput
    channels.forEach(ch => {
        if (!channelThroughputHistory.has(ch.channelName)) {
            channelThroughputHistory.set(ch.channelName, new MetricsHistory(100));
        }
        channelThroughputHistory.get(ch.channelName).add('rate', ch.publishRate || 0);
    });

    container.innerHTML = `
        <!-- System-wide Latency Distribution -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">System-wide Latency Distribution</h3>
            <p style="color: var(--text-secondary); font-size: 13px; margin-top: 8px;">
                Latency distribution across all active subscriptions
            </p>
            ${renderSystemLatencyDistribution()}
        </div>

        <!-- Per-channel Latency Comparison -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">Per-Channel Latency Comparison</h3>
            <p style="color: var(--text-secondary); font-size: 13px; margin-top: 8px;">
                Average latency by channel (top 10 channels by traffic)
            </p>
            ${renderPerChannelLatencyComparison()}
        </div>

        <!-- Percentile Comparison Chart -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">Latency Percentiles Comparison</h3>
            <p style="color: var(--text-secondary); font-size: 13px; margin-top: 8px;">
                Comparing P50, P90, P99 latencies across all subscriptions
            </p>
            ${renderPercentileComparison()}
        </div>

        <!-- Latency Heatmap -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">Latency Heatmap Over Time</h3>
            <p style="color: var(--text-secondary); font-size: 13px; margin-top: 8px;">
                Visual representation of latency variations over time (lighter = lower latency)
            </p>
            ${renderLatencyHeatmap()}
        </div>

        <!-- Throughput Over Time -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">System Throughput Over Time</h3>
            <p style="color: var(--text-secondary); font-size: 13px; margin-top: 8px;">
                Total message throughput across all channels (messages/second)
            </p>
            ${renderSystemThroughputChart()}
        </div>

        <!-- Per-channel Throughput Comparison -->
        <div class="card" style="margin-bottom: 24px;">
            <h3 class="card-title">Per-Channel Throughput Comparison</h3>
            <p style="color: var(--text-secondary); font-size: 13px; margin-top: 8px;">
                Message throughput by channel (top 10 channels)
            </p>
            ${renderPerChannelThroughputComparison()}
        </div>

        <!-- Summary Statistics -->
        <div class="card">
            <h3 class="card-title">Summary Statistics</h3>
            ${renderSummaryStatistics()}
        </div>
    `;
}

function renderSystemLatencyDistribution() {
    if (subscriptions.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">No subscription data available</div>';
    }

    // Group subscriptions by latency ranges
    const ranges = [
        { label: '< 10ms', min: 0, max: 10, count: 0, color: '#10b981' },
        { label: '10-50ms', min: 10, max: 50, count: 0, color: '#3b82f6' },
        { label: '50-100ms', min: 50, max: 100, count: 0, color: '#f59e0b' },
        { label: '100-200ms', min: 100, max: 200, count: 0, color: '#ef4444' },
        { label: '> 200ms', min: 200, max: Infinity, count: 0, color: '#991b1b' }
    ];

    subscriptions.forEach(sub => {
        const latency = sub.avgLatencyMs || 0;
        const range = ranges.find(r => latency >= r.min && latency < r.max);
        if (range) range.count++;
    });

    const maxCount = Math.max(...ranges.map(r => r.count));

    return `
        <div style="margin-top: 24px;">
            <div style="display: flex; gap: 16px; align-items: flex-end; height: 200px;">
                ${ranges.map(range => {
                    const height = maxCount > 0 ? (range.count / maxCount) * 100 : 0;
                    return `
                        <div style="flex: 1; display: flex; flex-direction: column; align-items: center; gap: 8px;">
                            <div style="font-size: 14px; font-weight: 600; color: var(--text-primary);">
                                ${range.count}
                            </div>
                            <div
                                style="width: 100%; height: ${height}%; background-color: ${range.color}; border-radius: 6px 6px 0 0; min-height: 4px;"
                                title="${range.label}: ${range.count} subscriptions"
                            ></div>
                            <div style="font-size: 12px; color: var(--text-secondary); text-align: center;">
                                ${range.label}
                            </div>
                        </div>
                    `;
                }).join('')}
            </div>
        </div>
    `;
}

function renderPerChannelLatencyComparison() {
    if (subscriptions.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">No subscription data available</div>';
    }

    // Group subscriptions by channel and calculate average latency
    const channelLatencies = new Map();
    subscriptions.forEach(sub => {
        const channel = sub.channelPattern;
        if (!channelLatencies.has(channel)) {
            channelLatencies.set(channel, { sum: 0, count: 0 });
        }
        const data = channelLatencies.get(channel);
        data.sum += sub.avgLatencyMs || 0;
        data.count++;
    });

    // Convert to array and calculate averages
    const channelAvgs = Array.from(channelLatencies.entries())
        .map(([channel, data]) => ({
            channel,
            avgLatency: data.sum / data.count,
            count: data.count
        }))
        .sort((a, b) => b.avgLatency - a.avgLatency)
        .slice(0, 10); // Top 10

    if (channelAvgs.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">No data available</div>';
    }

    const maxLatency = Math.max(...channelAvgs.map(c => c.avgLatency));

    return `
        <div style="margin-top: 24px;">
            <div style="display: flex; flex-direction: column; gap: 12px;">
                ${channelAvgs.map(ch => {
                    const widthPercent = maxLatency > 0 ? (ch.avgLatency / maxLatency) * 100 : 0;
                    const color = ch.avgLatency > 100 ? '#ef4444' : ch.avgLatency > 50 ? '#f59e0b' : '#10b981';
                    return `
                        <div style="display: flex; align-items: center; gap: 12px;">
                            <div style="min-width: 200px; font-size: 13px; color: var(--text-primary); font-family: monospace;">
                                ${escapeHtml(ch.channel)}
                            </div>
                            <div style="flex: 1; background-color: var(--bg-tertiary); border-radius: 4px; height: 30px; position: relative;">
                                <div
                                    style="width: ${widthPercent}%; height: 100%; background-color: ${color}; border-radius: 4px; display: flex; align-items: center; padding-left: 8px; color: white; font-size: 12px; font-weight: 600;"
                                >
                                    ${widthPercent > 15 ? formatLatency(ch.avgLatency) : ''}
                                </div>
                                ${widthPercent <= 15 ? `<div style="position: absolute; left: calc(${widthPercent}% + 8px); top: 50%; transform: translateY(-50%); font-size: 12px; font-weight: 600;">${formatLatency(ch.avgLatency)}</div>` : ''}
                            </div>
                            <div style="min-width: 80px; text-align: right; font-size: 12px; color: var(--text-secondary);">
                                ${ch.count} sub${ch.count !== 1 ? 's' : ''}
                            </div>
                        </div>
                    `;
                }).join('')}
            </div>
        </div>
    `;
}

function renderPercentileComparison() {
    if (subscriptions.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">No subscription data available</div>';
    }

    // Calculate system-wide percentiles
    const avgLatencies = subscriptions.map(s => s.avgLatencyMs || 0).filter(l => l > 0).sort((a, b) => a - b);
    const p99Latencies = subscriptions.map(s => s.percentileLatencyMs || 0).filter(l => l > 0).sort((a, b) => a - b);

    if (avgLatencies.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">No latency data available</div>';
    }

    const p50Index = Math.floor(avgLatencies.length * 0.5);
    const p90Index = Math.floor(avgLatencies.length * 0.9);
    const p99Index = Math.floor(p99Latencies.length * 0.99);

    const p50 = avgLatencies[p50Index] || 0;
    const p90 = avgLatencies[p90Index] || 0;
    const p99 = p99Latencies.length > 0 ? (p99Latencies[p99Index] || p99Latencies[p99Latencies.length - 1]) : 0;

    const percentiles = [
        { label: 'P50', value: p50, color: '#10b981', description: '50% of subscriptions' },
        { label: 'P90', value: p90, color: '#3b82f6', description: '90% of subscriptions' },
        { label: 'P99', value: p99, color: '#f59e0b', description: '99% of subscriptions' }
    ];

    const maxValue = Math.max(...percentiles.map(p => p.value));

    return `
        <div style="margin-top: 24px;">
            <div style="display: flex; gap: 16px; align-items: flex-end; height: 200px;">
                ${percentiles.map(p => {
                    const height = maxValue > 0 ? (p.value / maxValue) * 100 : 0;
                    return `
                        <div style="flex: 1; display: flex; flex-direction: column; align-items: center; gap: 8px;">
                            <div style="font-size: 14px; font-weight: 600; color: var(--text-primary);">
                                ${formatLatency(p.value)}
                            </div>
                            <div
                                style="width: 100%; height: ${height}%; background: linear-gradient(to top, ${p.color}, ${p.color}dd); border-radius: 6px 6px 0 0; min-height: 4px;"
                                title="${p.label}: ${formatLatency(p.value)}"
                            ></div>
                            <div style="font-size: 16px; font-weight: 600; color: ${p.color};">
                                ${p.label}
                            </div>
                            <div style="font-size: 11px; color: var(--text-secondary); text-align: center;">
                                ${p.description}
                            </div>
                        </div>
                    `;
                }).join('')}
            </div>
        </div>
    `;
}

function renderLatencyHeatmap() {
    if (subscriptions.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">No subscription data available</div>';
    }

    // Simple heatmap: group subscriptions into cells based on latency ranges over "time buckets"
    // For now, we'll simulate this with current data grouped by channel
    const topChannels = channels
        .sort((a, b) => (b.publishRate || 0) - (a.publishRate || 0))
        .slice(0, 10);

    if (topChannels.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">No channel data available</div>';
    }

    // Get subscriptions for each channel
    const channelSubs = topChannels.map(ch => {
        const subs = subscriptions.filter(s => s.channelPattern === ch.channelName);
        const avgLatency = subs.length > 0
            ? subs.reduce((sum, s) => sum + (s.avgLatencyMs || 0), 0) / subs.length
            : 0;
        return {
            channel: ch.channelName,
            latency: avgLatency
        };
    });

    const maxLatency = Math.max(...channelSubs.map(c => c.latency), 1);

    // Time buckets (simulated - in real implementation would be historical data)
    const timeBuckets = ['5m ago', '4m ago', '3m ago', '2m ago', '1m ago', 'Now'];

    return `
        <div style="margin-top: 24px; overflow-x: auto;">
            <div style="display: inline-block; min-width: 100%;">
                <!-- Header row -->
                <div style="display: flex; margin-bottom: 4px;">
                    <div style="width: 150px;"></div>
                    ${timeBuckets.map(bucket => `
                        <div style="flex: 1; min-width: 60px; text-align: center; font-size: 11px; color: var(--text-secondary);">
                            ${bucket}
                        </div>
                    `).join('')}
                </div>

                <!-- Data rows -->
                ${channelSubs.map(ch => {
                    const intensity = ch.latency / maxLatency;
                    // Simulate variation over time (in real implementation, use historical data)
                    return `
                        <div style="display: flex; margin-bottom: 4px; align-items: center;">
                            <div style="width: 150px; font-size: 12px; font-family: monospace; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">
                                ${escapeHtml(ch.channel)}
                            </div>
                            ${timeBuckets.map((_, i) => {
                                // Simulate slight variation
                                const variance = 0.8 + (Math.sin(i) * 0.2);
                                const cellIntensity = Math.min(intensity * variance, 1);
                                const bgColor = getHeatmapColor(cellIntensity);
                                const textColor = cellIntensity > 0.5 ? 'white' : 'var(--text-primary)';
                                return `
                                    <div
                                        style="flex: 1; min-width: 60px; height: 40px; background-color: ${bgColor}; margin-right: 2px; border-radius: 3px; display: flex; align-items: center; justify-content: center; font-size: 10px; color: ${textColor}; font-weight: 600;"
                                        title="${ch.channel} at ${timeBuckets[i]}: ~${formatLatency(ch.latency * variance)}"
                                    >
                                        ${cellIntensity > 0.3 ? formatLatency(ch.latency * variance) : ''}
                                    </div>
                                `;
                            }).join('')}
                        </div>
                    `;
                }).join('')}

                <!-- Legend -->
                <div style="margin-top: 16px; display: flex; align-items: center; gap: 8px; font-size: 12px;">
                    <span style="color: var(--text-secondary);">Lower latency</span>
                    ${[0, 0.25, 0.5, 0.75, 1].map(intensity => `
                        <div style="width: 40px; height: 20px; background-color: ${getHeatmapColor(intensity)}; border-radius: 3px;"></div>
                    `).join('')}
                    <span style="color: var(--text-secondary);">Higher latency</span>
                </div>
            </div>
        </div>
    `;
}

function renderSystemThroughputChart() {
    const throughputData = systemThroughputHistory.get('throughput');

    if (throughputData.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">Collecting data...</div>';
    }

    const sparkline = createSparkline(throughputData, {
        width: 800,
        height: 200,
        strokeWidth: 2,
        strokeColor: '#3b82f6',
        fillColor: 'rgba(59, 130, 246, 0.1)'
    });

    const current = throughputData[throughputData.length - 1] || 0;
    const avg = throughputData.reduce((sum, v) => sum + v, 0) / throughputData.length;
    const max = Math.max(...throughputData);
    const min = Math.min(...throughputData);

    return `
        <div style="margin-top: 24px;">
            <div style="display: flex; justify-content: center; margin-bottom: 16px;">
                ${sparkline}
            </div>
            <div style="display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px;">
                <div style="text-align: center;">
                    <div style="font-size: 12px; color: var(--text-secondary);">Current</div>
                    <div style="font-size: 20px; font-weight: 600; color: #3b82f6;">${formatRate(current)}/s</div>
                </div>
                <div style="text-align: center;">
                    <div style="font-size: 12px; color: var(--text-secondary);">Average</div>
                    <div style="font-size: 20px; font-weight: 600;">${formatRate(avg)}/s</div>
                </div>
                <div style="text-align: center;">
                    <div style="font-size: 12px; color: var(--text-secondary);">Peak</div>
                    <div style="font-size: 20px; font-weight: 600; color: #10b981;">${formatRate(max)}/s</div>
                </div>
                <div style="text-align: center;">
                    <div style="font-size: 12px; color: var(--text-secondary);">Minimum</div>
                    <div style="font-size: 20px; font-weight: 600; color: #f59e0b;">${formatRate(min)}/s</div>
                </div>
            </div>
        </div>
    `;
}

function renderPerChannelThroughputComparison() {
    if (channels.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">No channel data available</div>';
    }

    const topChannels = channels
        .filter(ch => (ch.publishRate || 0) > 0)
        .sort((a, b) => (b.publishRate || 0) - (a.publishRate || 0))
        .slice(0, 10);

    if (topChannels.length === 0) {
        return '<div style="text-align: center; padding: 40px; color: var(--text-secondary);">No active channels</div>';
    }

    const maxRate = Math.max(...topChannels.map(ch => ch.publishRate || 0));

    return `
        <div style="margin-top: 24px;">
            <div style="display: flex; flex-direction: column; gap: 12px;">
                ${topChannels.map((ch, index) => {
                    const widthPercent = maxRate > 0 ? ((ch.publishRate || 0) / maxRate) * 100 : 0;
                    const colors = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6'];
                    const color = colors[index % colors.length];
                    return `
                        <div style="display: flex; align-items: center; gap: 12px;">
                            <div style="min-width: 200px; font-size: 13px; color: var(--text-primary); font-family: monospace;">
                                ${escapeHtml(ch.channelName)}
                            </div>
                            <div style="flex: 1; background-color: var(--bg-tertiary); border-radius: 4px; height: 30px; position: relative;">
                                <div
                                    style="width: ${widthPercent}%; height: 100%; background-color: ${color}; border-radius: 4px; display: flex; align-items: center; padding-left: 8px; color: white; font-size: 12px; font-weight: 600;"
                                >
                                    ${widthPercent > 20 ? formatRate(ch.publishRate || 0) + '/s' : ''}
                                </div>
                                ${widthPercent <= 20 ? `<div style="position: absolute; left: calc(${widthPercent}% + 8px); top: 50%; transform: translateY(-50%); font-size: 12px; font-weight: 600;">${formatRate(ch.publishRate || 0)}/s</div>` : ''}
                            </div>
                            <div style="min-width: 100px; text-align: right; font-size: 12px; color: var(--text-secondary);">
                                ${formatNumber(ch.totalMessages || 0)} total
                            </div>
                        </div>
                    `;
                }).join('')}
            </div>
        </div>
    `;
}

function renderSummaryStatistics() {
    const totalChannels = channels.length;
    const activeChannels = channels.filter(ch => (ch.publishRate || 0) > 0).length;
    const totalSubscriptions = subscriptions.length;
    const totalThroughput = channels.reduce((sum, ch) => sum + (ch.publishRate || 0), 0);

    const avgLatencies = subscriptions.map(s => s.avgLatencyMs || 0).filter(l => l > 0);
    const avgSystemLatency = avgLatencies.length > 0
        ? avgLatencies.reduce((sum, l) => sum + l, 0) / avgLatencies.length
        : 0;

    const totalTimeouts = subscriptions.reduce((sum, s) => sum + (s.timeoutCount || 0), 0);

    return `
        <div style="margin-top: 16px; display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px;">
            <div style="padding: 16px; background-color: var(--bg-tertiary); border-radius: 6px;">
                <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 4px;">Total Channels</div>
                <div style="font-size: 24px; font-weight: 600;">${totalChannels}</div>
                <div style="font-size: 11px; color: var(--text-secondary); margin-top: 4px;">${activeChannels} active</div>
            </div>
            <div style="padding: 16px; background-color: var(--bg-tertiary); border-radius: 6px;">
                <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 4px;">Total Subscriptions</div>
                <div style="font-size: 24px; font-weight: 600;">${totalSubscriptions}</div>
            </div>
            <div style="padding: 16px; background-color: var(--bg-tertiary); border-radius: 6px;">
                <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 4px;">System Throughput</div>
                <div style="font-size: 24px; font-weight: 600; color: #3b82f6;">${formatRate(totalThroughput)}/s</div>
            </div>
            <div style="padding: 16px; background-color: var(--bg-tertiary); border-radius: 6px;">
                <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 4px;">Avg System Latency</div>
                <div style="font-size: 24px; font-weight: 600; color: ${avgSystemLatency > 100 ? '#f59e0b' : '#10b981'};">${formatLatency(avgSystemLatency)}</div>
            </div>
            <div style="padding: 16px; background-color: var(--bg-tertiary); border-radius: 6px;">
                <div style="font-size: 12px; color: var(--text-secondary); margin-bottom: 4px;">Total Timeouts</div>
                <div style="font-size: 24px; font-weight: 600; color: ${totalTimeouts > 0 ? '#ef4444' : '#10b981'};">${formatNumber(totalTimeouts)}</div>
            </div>
        </div>
    `;
}

function handleMetricsUpdate(metrics) {
    if (metrics.channels) {
        channels = channels.map(ch => {
            const updated = metrics.channels.find(c => c.channelName === ch.channelName);
            return updated ? { ...ch, ...updated } : ch;
        });
    }

    if (metrics.subscriptions) {
        subscriptions = subscriptions.map(sub => {
            const updated = metrics.subscriptions.find(s => s.id === sub.id);
            return updated ? { ...sub, ...updated } : sub;
        });
    }

    renderAnalyticsContent();
}

function exportToCSV() {
    // Build CSV data
    const csvLines = [];

    // Header
    csvLines.push('Export Type,Metric,Value');
    csvLines.push('');

    // Channels
    csvLines.push('Channel Data');
    csvLines.push('Channel Name,Publish Rate,Total Messages,Subscriptions');
    channels.forEach(ch => {
        csvLines.push(`"${ch.channelName}",${ch.publishRate || 0},${ch.totalMessages || 0},${ch.subscriptionCount || 0}`);
    });
    csvLines.push('');

    // Subscriptions
    csvLines.push('Subscription Data');
    csvLines.push('Subscription ID,Channel Pattern,Avg Latency (ms),P99 Latency (ms),Queue Depth,Process Rate,Timeouts');
    subscriptions.forEach(sub => {
        csvLines.push(`"${sub.id}","${sub.channelPattern}",${sub.avgLatencyMs || 0},${sub.percentileLatencyMs || 0},${sub.queueDepth || 0},${sub.processRate || 0},${sub.timeoutCount || 0}`);
    });

    // Create and download file
    const csvContent = csvLines.join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    const url = URL.createObjectURL(blob);
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').substring(0, 19);
    link.setAttribute('href', url);
    link.setAttribute('download', `berberis-analytics-${timestamp}.csv`);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

function getHeatmapColor(intensity) {
    // Green (low) to Red (high)
    if (intensity < 0.2) return '#d1fae5'; // Very light green
    if (intensity < 0.4) return '#6ee7b7'; // Light green
    if (intensity < 0.6) return '#fde68a'; // Yellow
    if (intensity < 0.8) return '#fbbf24'; // Orange
    return '#ef4444'; // Red
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
