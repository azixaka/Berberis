// Lifecycle Event Viewer Screen

import { signalRClient } from '../signalr-client.js';

let unsubscribeLifecycle = null;
let events = [];
let filteredEvents = [];
let isPaused = false;
let maxEvents = 500;

const eventTypeNames = {
    0: 'Channel Created',
    1: 'Channel Deleted',
    2: 'Subscription Created',
    3: 'Subscription Disposed'
};

const eventTypeIcons = {
    0: '➕', // Channel Created
    1: '➖', // Channel Deleted
    2: '🔔', // Subscription Created
    3: '🔕'  // Subscription Disposed
};

export async function render(container) {
    container.innerHTML = `
        <div class="page-header">
            <h2 class="page-title">
                Lifecycle Events
                <span id="event-count-badge" class="badge">0</span>
            </h2>
            <p class="page-description">Real-time channel and subscription lifecycle events</p>
        </div>

        <div class="controls-panel">
            <div class="controls-row">
                <div class="search-box">
                    <input type="text" id="search-input" placeholder="Search by channel or subscription name..." />
                </div>

                <select id="event-type-filter">
                    <option value="">All Event Types</option>
                    <option value="0">Channel Created</option>
                    <option value="1">Channel Deleted</option>
                    <option value="2">Subscription Created</option>
                    <option value="3">Subscription Disposed</option>
                </select>

                <button id="pause-btn" class="btn btn-secondary">
                    <span class="btn-icon">⏸️</span> Pause
                </button>

                <button id="clear-btn" class="btn btn-secondary">
                    <span class="btn-icon">🗑️</span> Clear
                </button>

                <div class="max-events-control">
                    <label>Max Events:</label>
                    <input type="number" id="max-events" value="500" min="50" max="5000" step="50" />
                </div>
            </div>
        </div>

        <div id="events-container" class="events-list">
            <div class="empty-state">
                <div class="empty-state-icon">⏳</div>
                <div class="empty-state-title">Waiting for Events</div>
                <div class="empty-state-description">Lifecycle events will appear here in real-time</div>
            </div>
        </div>
    `;

    // Initialize
    await connectToLifecycleStream();
    setupEventHandlers(container);
}

async function connectToLifecycleStream() {
    try {
        if (!signalRClient.isConnected) {
            await signalRClient.connect();
        }

        // Subscribe to lifecycle events
        unsubscribeLifecycle = signalRClient.onLifecycleEvent(handleLifecycleEvent);
        await signalRClient.subscribeToLifecycle();

        console.log('Connected to lifecycle stream');
    } catch (error) {
        console.error('Failed to connect to lifecycle stream:', error);
    }
}

function handleLifecycleEvent(event) {
    if (isPaused) return;

    // Add to events array
    events.unshift(event); // Add to beginning

    // Trim to max events
    if (events.length > maxEvents) {
        events = events.slice(0, maxEvents);
    }

    // Reapply filters
    applyFilters();
}

function setupEventHandlers(container) {
    // Search input
    const searchInput = container.querySelector('#search-input');
    searchInput.addEventListener('input', applyFilters);

    // Event type filter
    const eventTypeFilter = container.querySelector('#event-type-filter');
    eventTypeFilter.addEventListener('change', applyFilters);

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
        events = [];
        filteredEvents = [];
        renderEvents(container);
        updateEventCount(0);
    });

    // Max events input
    const maxEventsInput = container.querySelector('#max-events');
    maxEventsInput.addEventListener('change', (e) => {
        maxEvents = parseInt(e.target.value) || 500;
        if (events.length > maxEvents) {
            events = events.slice(0, maxEvents);
            applyFilters();
        }
    });
}

function applyFilters() {
    const searchInput = document.querySelector('#search-input');
    const eventTypeFilter = document.querySelector('#event-type-filter');

    const searchTerm = searchInput?.value.toLowerCase() || '';
    const selectedType = eventTypeFilter?.value || '';

    filteredEvents = events.filter(event => {
        // Filter by event type
        if (selectedType !== '' && event.eventType.toString() !== selectedType) {
            return false;
        }

        // Filter by search term (channel or subscription name)
        if (searchTerm) {
            const channelMatch = event.channelName?.toLowerCase().includes(searchTerm);
            const subMatch = event.subscriptionName?.toLowerCase().includes(searchTerm);
            return channelMatch || subMatch;
        }

        return true;
    });

    renderEvents(document.querySelector('.page-header').closest('.screen-content'));
    updateEventCount(filteredEvents.length);
}

function renderEvents(container) {
    const eventsContainer = container.querySelector('#events-container');

    if (filteredEvents.length === 0) {
        eventsContainer.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">⏳</div>
                <div class="empty-state-title">No Events</div>
                <div class="empty-state-description">
                    ${events.length === 0
                        ? 'Lifecycle events will appear here in real-time'
                        : 'No events match the current filters'
                    }
                </div>
            </div>
        `;
        return;
    }

    const eventsHtml = filteredEvents.map(event => renderEventCard(event)).join('');
    eventsContainer.innerHTML = eventsHtml;
}

function renderEventCard(event) {
    const eventTypeName = eventTypeNames[event.eventType] || 'Unknown';
    const eventIcon = eventTypeIcons[event.eventType] || '❓';
    const timestamp = new Date(event.timestamp);
    const isChannelEvent = event.eventType === 0 || event.eventType === 1;
    const isCreation = event.eventType === 0 || event.eventType === 2;

    return `
        <div class="event-card ${isCreation ? 'event-creation' : 'event-deletion'}">
            <div class="event-icon">${eventIcon}</div>
            <div class="event-content">
                <div class="event-header">
                    <span class="event-type">${eventTypeName}</span>
                    <span class="event-time">${formatTimestamp(timestamp)}</span>
                </div>
                <div class="event-details">
                    <div class="event-detail-row">
                        <span class="detail-label">Channel:</span>
                        <span class="detail-value">${event.channelName}</span>
                    </div>
                    ${!isChannelEvent && event.subscriptionName ? `
                        <div class="event-detail-row">
                            <span class="detail-label">Subscription:</span>
                            <span class="detail-value">${event.subscriptionName}</span>
                        </div>
                    ` : ''}
                    <div class="event-detail-row">
                        <span class="detail-label">Type:</span>
                        <span class="detail-value">${event.messageBodyType || 'N/A'}</span>
                    </div>
                </div>
            </div>
        </div>
    `;
}

function formatTimestamp(date) {
    const now = new Date();
    const diffMs = now - date;
    const diffSec = Math.floor(diffMs / 1000);
    const diffMin = Math.floor(diffSec / 60);
    const diffHour = Math.floor(diffMin / 60);

    if (diffSec < 60) {
        return `${diffSec}s ago`;
    } else if (diffMin < 60) {
        return `${diffMin}m ago`;
    } else if (diffHour < 24) {
        return `${diffHour}h ago`;
    } else {
        return date.toLocaleString();
    }
}

function updateEventCount(count) {
    const badge = document.querySelector('#event-count-badge');
    if (badge) {
        badge.textContent = count;
    }
}

export async function cleanup() {
    // Unsubscribe from lifecycle events
    if (unsubscribeLifecycle) {
        unsubscribeLifecycle();
        unsubscribeLifecycle = null;
    }

    try {
        await signalRClient.unsubscribeFromLifecycle();
    } catch (error) {
        console.error('Error unsubscribing from lifecycle:', error);
    }

    // Clear events
    events = [];
    filteredEvents = [];
    isPaused = false;
}

export default { render, cleanup };
