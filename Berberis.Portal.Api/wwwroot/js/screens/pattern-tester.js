import { apiClient } from '../api-client.js';

let channels = [];

function wildcardToRegex(pattern) {
    const escaped = pattern.replace(/[.+?^${}()|[\]\\]/g, '\\$&');
    const regex = escaped.replace(/\*/g, '.*');
    return new RegExp(`^${regex}$`);
}

function testPattern(pattern, channelName) {
    try {
        const regex = wildcardToRegex(pattern);
        return regex.test(channelName);
    } catch (e) {
        return false;
    }
}

function findMatchingChannels(pattern) {
    if (!pattern) return [];
    return channels.filter(ch => testPattern(pattern, ch.channelName));
}

function getSuggestedPatterns() {
    if (channels.length === 0) return [];

    const suggestions = new Set();

    channels.forEach(ch => {
        const parts = ch.channelName.split('.');
        if (parts.length > 1) {
            suggestions.add(parts[0] + '.*');
            if (parts.length > 2) {
                suggestions.add(parts[0] + '.' + parts[1] + '.*');
            }
        }
    });

    suggestions.add('*');
    suggestions.add('*.created');
    suggestions.add('*.deleted');

    return Array.from(suggestions).slice(0, 10);
}

async function loadChannels() {
    try {
        const data = await apiClient.get('/api/channels');
        channels = data.channels || [];
    } catch (err) {
        console.error('Failed to load channels:', err);
        channels = [];
    }
}

function renderMatchResult(pattern, channelName, matches) {
    const resultDiv = document.getElementById('match-result');
    if (!pattern || !channelName) {
        resultDiv.innerHTML = '';
        return;
    }

    const icon = matches ? '✅' : '❌';
    const statusClass = matches ? 'match-success' : 'match-failure';
    const explanation = matches
        ? `Pattern <code>${pattern}</code> matches channel <code>${channelName}</code>`
        : `Pattern <code>${pattern}</code> does not match channel <code>${channelName}</code>`;

    resultDiv.innerHTML = `
        <div class="match-result ${statusClass}">
            <div class="match-result-icon">${icon}</div>
            <div class="match-result-text">${explanation}</div>
        </div>
    `;
}

function renderMatchingChannels(matchingChannels, pattern) {
    const listDiv = document.getElementById('matching-channels-list');

    if (matchingChannels.length === 0) {
        listDiv.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">🔍</div>
                <div class="empty-state-title">No matching channels</div>
                <div class="empty-state-description">Pattern <code>${pattern}</code> does not match any channels</div>
            </div>
        `;
        return;
    }

    const channelsHtml = matchingChannels.map(ch => `
        <div class="channel-match-item">
            <span class="channel-match-name">${ch.channelName}</span>
            <span class="channel-match-stats">
                ${ch.subscriptionCount} subs,
                ${ch.totalMessages.toLocaleString()} msgs
            </span>
        </div>
    `).join('');

    listDiv.innerHTML = `
        <div class="match-count">Found ${matchingChannels.length} matching channel${matchingChannels.length === 1 ? '' : 's'}</div>
        <div class="channel-match-list">
            ${channelsHtml}
        </div>
    `;
}

function setupPatternSuggestions() {
    const suggestionsDiv = document.getElementById('pattern-suggestions');
    const suggestions = getSuggestedPatterns();

    if (suggestions.length === 0) {
        suggestionsDiv.innerHTML = '<p class="text-muted">Loading suggestions...</p>';
        return;
    }

    const suggestionsHtml = suggestions.map(pattern =>
        `<button class="pattern-suggestion-btn" data-pattern="${pattern}">${pattern}</button>`
    ).join('');

    suggestionsDiv.innerHTML = `
        <div class="pattern-suggestions-label">Quick patterns:</div>
        <div class="pattern-suggestions-list">${suggestionsHtml}</div>
    `;

    suggestionsDiv.querySelectorAll('.pattern-suggestion-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.getElementById('pattern-input').value = btn.dataset.pattern;
            handleTestMatch();
        });
    });
}

function handleTestMatch() {
    const pattern = document.getElementById('pattern-input').value;
    const channelName = document.getElementById('channel-input').value;

    if (pattern && channelName) {
        const matches = testPattern(pattern, channelName);
        renderMatchResult(pattern, channelName, matches);
    } else {
        renderMatchResult('', '', false);
    }
}

function handleShowMatching() {
    const pattern = document.getElementById('pattern-input').value;

    if (!pattern) {
        alert('Please enter a pattern first');
        return;
    }

    const matchingChannels = findMatchingChannels(pattern);
    renderMatchingChannels(matchingChannels, pattern);
}

export async function render(container) {
    await loadChannels();

    container.innerHTML = `
        <div class="page-header">
            <h2 class="page-title">Pattern Matcher Testing Tool</h2>
            <p class="page-description">Test channel name patterns and see which channels match</p>
        </div>

        <div class="pattern-tester-container">
            <div class="pattern-tester-section">
                <h3>Pattern Matching</h3>

                <div class="form-group">
                    <label for="pattern-input">Pattern (with wildcards)</label>
                    <input
                        type="text"
                        id="pattern-input"
                        class="form-input"
                        placeholder="e.g., orders.*, user.*.created"
                        autocomplete="off"
                    />
                </div>

                <div class="form-group">
                    <label for="channel-input">Channel Name</label>
                    <input
                        type="text"
                        id="channel-input"
                        class="form-input"
                        placeholder="e.g., orders.created, user.123.created"
                        autocomplete="off"
                    />
                </div>

                <div class="button-group">
                    <button id="test-match-btn" class="btn btn-primary">Test Match</button>
                    <button id="show-matching-btn" class="btn btn-secondary">Show All Matching Channels</button>
                </div>

                <div id="match-result"></div>
            </div>

            <div class="pattern-tester-section">
                <h3>Wildcard Syntax Help</h3>
                <div class="help-content">
                    <p><strong>Wildcard patterns use <code>*</code> to match any sequence of characters:</strong></p>
                    <ul class="help-list">
                        <li><code>*</code> - Matches all channels</li>
                        <li><code>orders.*</code> - Matches <code>orders.created</code>, <code>orders.deleted</code>, etc.</li>
                        <li><code>user.*.created</code> - Matches <code>user.123.created</code>, <code>user.abc.created</code>, etc.</li>
                        <li><code>*.error</code> - Matches any channel ending with <code>.error</code></li>
                    </ul>

                    <p><strong>Examples:</strong></p>
                    <div class="example-table">
                        <div class="example-row example-header">
                            <div class="example-col">Pattern</div>
                            <div class="example-col">Channel Name</div>
                            <div class="example-col">Match?</div>
                        </div>
                        <div class="example-row">
                            <div class="example-col"><code>orders.*</code></div>
                            <div class="example-col"><code>orders.created</code></div>
                            <div class="example-col"><span class="match-yes">✅ Yes</span></div>
                        </div>
                        <div class="example-row">
                            <div class="example-col"><code>orders.*</code></div>
                            <div class="example-col"><code>products.created</code></div>
                            <div class="example-col"><span class="match-no">❌ No</span></div>
                        </div>
                        <div class="example-row">
                            <div class="example-col"><code>*.created</code></div>
                            <div class="example-col"><code>orders.created</code></div>
                            <div class="example-col"><span class="match-yes">✅ Yes</span></div>
                        </div>
                        <div class="example-row">
                            <div class="example-col"><code>user.*.profile</code></div>
                            <div class="example-col"><code>user.123.profile</code></div>
                            <div class="example-col"><span class="match-yes">✅ Yes</span></div>
                        </div>
                    </div>
                </div>

                <div id="pattern-suggestions"></div>
            </div>
        </div>

        <div class="pattern-tester-section">
            <div id="matching-channels-list"></div>
        </div>
    `;

    setupPatternSuggestions();

    document.getElementById('test-match-btn').addEventListener('click', handleTestMatch);
    document.getElementById('show-matching-btn').addEventListener('click', handleShowMatching);

    document.getElementById('pattern-input').addEventListener('input', () => {
        if (document.getElementById('channel-input').value) {
            handleTestMatch();
        }
    });

    document.getElementById('channel-input').addEventListener('input', handleTestMatch);
}

export async function cleanup() {
    channels = [];
}

export default { render, cleanup };
