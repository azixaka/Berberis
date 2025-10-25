import { apiClient } from '../api-client.js';

let configuration = null;

async function loadConfiguration() {
    try {
        configuration = await apiClient.get('/api/configuration');
    } catch (err) {
        console.error('Failed to load configuration:', err);
        configuration = null;
    }
}

function handleExportJson() {
    if (!configuration) return;

    const json = JSON.stringify(configuration, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'berberis-config.json';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

function handleDownloadConfig() {
    window.location.href = '/api/configuration/export';
}

function renderCrossBarOptions(options) {
    if (!options) return '<p class="no-data">No CrossBar options available</p>';

    return `
        <div class="config-section">
            <h3>CrossBar Options</h3>
            <div class="config-grid">
                <div class="config-item">
                    <div class="config-label">Default Buffer Capacity</div>
                    <div class="config-value">${options.defaultBufferCapacity || 'Unbounded'}</div>
                    <div class="config-help">Default buffer size for subscriptions (null = unbounded)</div>
                </div>

                <div class="config-item">
                    <div class="config-label">Default Slow Consumer Strategy</div>
                    <div class="config-value">${options.defaultSlowConsumerStrategy}</div>
                    <div class="config-help">Behavior when subscription buffer is full</div>
                </div>

                <div class="config-item">
                    <div class="config-label">Default Conflation Interval</div>
                    <div class="config-value">${options.defaultConflationInterval}</div>
                    <div class="config-help">Time window for message conflation (None = no conflation)</div>
                </div>

                <div class="config-item">
                    <div class="config-label">Max Channels</div>
                    <div class="config-value">${options.maxChannels || 'Unlimited'}</div>
                    <div class="config-help">Maximum number of channels allowed</div>
                </div>

                <div class="config-item">
                    <div class="config-label">Max Channel Name Length</div>
                    <div class="config-value">${options.maxChannelNameLength}</div>
                    <div class="config-help">Maximum length for channel names</div>
                </div>

                <div class="config-item">
                    <div class="config-label">Message Tracing</div>
                    <div class="config-value">
                        <span class="config-badge ${options.enableMessageTracing ? 'enabled' : 'disabled'}">
                            ${options.enableMessageTracing ? 'Enabled' : 'Disabled'}
                        </span>
                    </div>
                    <div class="config-help">Trace all published messages to system channel</div>
                </div>

                <div class="config-item">
                    <div class="config-label">Lifecycle Tracking</div>
                    <div class="config-value">
                        <span class="config-badge ${options.enableLifecycleTracking ? 'enabled' : 'disabled'}">
                            ${options.enableLifecycleTracking ? 'Enabled' : 'Disabled'}
                        </span>
                    </div>
                    <div class="config-help">Track channel and subscription lifecycle events</div>
                </div>

                <div class="config-item">
                    <div class="config-label">Publish Logging</div>
                    <div class="config-value">
                        <span class="config-badge ${options.enablePublishLogging ? 'enabled' : 'disabled'}">
                            ${options.enablePublishLogging ? 'Enabled' : 'Disabled'}
                        </span>
                    </div>
                    <div class="config-help">Log all publish operations (verbose, impacts performance)</div>
                </div>

                <div class="config-item">
                    <div class="config-label">System Channel Prefix</div>
                    <div class="config-value"><code>${options.systemChannelPrefix}</code></div>
                    <div class="config-help">Prefix for system channels (e.g., $lifecycle, $message.traces)</div>
                </div>

                <div class="config-item">
                    <div class="config-label">System Channel Buffer Capacity</div>
                    <div class="config-value">${options.systemChannelBufferCapacity}</div>
                    <div class="config-help">Buffer size for system channels</div>
                </div>
            </div>
        </div>
    `;
}

function renderSubscriptionOptions(options) {
    if (!options) return '<p class="no-data">No default subscription options available</p>';

    return `
        <div class="config-section">
            <h3>Default Subscription Options</h3>
            <div class="config-grid">
                <div class="config-item">
                    <div class="config-label">Handler Timeout</div>
                    <div class="config-value">${options.handlerTimeout || 'None (infinite)'}</div>
                    <div class="config-help">Maximum time allowed for handler execution per message</div>
                </div>

                <div class="config-item">
                    <div class="config-label">Timeout Callback</div>
                    <div class="config-value">
                        <span class="config-badge ${options.hasTimeoutCallback ? 'enabled' : 'disabled'}">
                            ${options.hasTimeoutCallback ? 'Configured' : 'Not Configured'}
                        </span>
                    </div>
                    <div class="config-help">Callback invoked when handler times out</div>
                </div>
            </div>
        </div>
    `;
}

function renderStatsOptions(options) {
    if (!options) return '<p class="no-data">No stats options available</p>';

    return `
        <div class="config-section">
            <h3>Statistics Options</h3>
            <div class="config-grid">
                <div class="config-item">
                    <div class="config-label">Percentile</div>
                    <div class="config-value">${options.percentile ? (options.percentile * 100).toFixed(1) + '%' : 'Not configured'}</div>
                    <div class="config-help">Percentile to track (e.g., P99 = 0.99)</div>
                </div>

                <div class="config-item">
                    <div class="config-label">Percentile Enabled</div>
                    <div class="config-value">
                        <span class="config-badge ${options.percentileEnabled ? 'enabled' : 'disabled'}">
                            ${options.percentileEnabled ? 'Yes' : 'No'}
                        </span>
                    </div>
                    <div class="config-help">Whether percentile tracking is active</div>
                </div>

                <div class="config-item">
                    <div class="config-label">Alpha (Moving Percentile)</div>
                    <div class="config-value">${options.alpha}</div>
                    <div class="config-help">Moving percentile alpha parameter (smoothing factor)</div>
                </div>

                <div class="config-item">
                    <div class="config-label">Delta (Moving Percentile)</div>
                    <div class="config-value">${options.delta}</div>
                    <div class="config-help">Moving percentile delta parameter (adaptation rate)</div>
                </div>

                <div class="config-item">
                    <div class="config-label">EWMA Window Size</div>
                    <div class="config-value">${options.ewmaWindowSize}</div>
                    <div class="config-help">Exponentially Weighted Moving Average window size</div>
                </div>
            </div>
        </div>
    `;
}

function renderDocumentationLinks() {
    return `
        <div class="config-section">
            <h3>Documentation</h3>
            <div class="doc-links">
                <div class="doc-link-item">
                    <div class="doc-link-icon">📖</div>
                    <div class="doc-link-content">
                        <div class="doc-link-title">CrossBar Options</div>
                        <div class="doc-link-description">
                            Configuration options for message broker behavior, system channels, and observability
                        </div>
                    </div>
                </div>
                <div class="doc-link-item">
                    <div class="doc-link-icon">📊</div>
                    <div class="doc-link-content">
                        <div class="doc-link-title">Statistics Options</div>
                        <div class="doc-link-description">
                            Configuring percentile tracking, EWMA parameters, and performance metrics
                        </div>
                    </div>
                </div>
                <div class="doc-link-item">
                    <div class="doc-link-icon">⏱️</div>
                    <div class="doc-link-content">
                        <div class="doc-link-title">Subscription Options</div>
                        <div class="doc-link-description">
                            Handler timeouts, backpressure strategies, and subscription behavior
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;
}

export async function render(container) {
    await loadConfiguration();

    if (!configuration) {
        container.innerHTML = `
            <div class="page-header">
                <h2 class="page-title">Configuration</h2>
                <p class="page-description">System configuration and settings</p>
            </div>
            <div class="error-state">
                <div class="error-state-icon">⚠️</div>
                <div class="error-state-title">Failed to load configuration</div>
                <div class="error-state-description">Unable to retrieve system configuration</div>
            </div>
        `;
        return;
    }

    container.innerHTML = `
        <div class="page-header">
            <h2 class="page-title">Configuration</h2>
            <p class="page-description">System configuration and settings (read-only)</p>
        </div>

        <div class="config-actions">
            <button id="export-json-btn" class="btn btn-primary">Export to JSON</button>
            <button id="download-config-btn" class="btn btn-secondary">Download Configuration</button>
        </div>

        <div class="config-container">
            ${renderCrossBarOptions(configuration.crossBarOptions)}
            ${renderSubscriptionOptions(configuration.defaultSubscriptionOptions)}
            ${renderStatsOptions(configuration.defaultStatsOptions)}
            ${renderDocumentationLinks()}
        </div>
    `;

    document.getElementById('export-json-btn').addEventListener('click', handleExportJson);
    document.getElementById('download-config-btn').addEventListener('click', handleDownloadConfig);
}

export async function cleanup() {
    configuration = null;
}

export default { render, cleanup };
