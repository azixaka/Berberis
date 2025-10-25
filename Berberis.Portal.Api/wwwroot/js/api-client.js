// API Client - Simple fetch wrapper for Portal API

const API_BASE = '/api';

class ApiClient {
    constructor() {
        this.baseUrl = API_BASE;
    }

    async request(endpoint, options = {}) {
        const url = `${this.baseUrl}${endpoint}`;
        const config = {
            headers: {
                'Content-Type': 'application/json',
                ...options.headers
            },
            ...options
        };

        try {
            const response = await fetch(url, config);

            if (!response.ok) {
                const error = await response.json().catch(() => ({ error: 'Request failed' }));
                throw new Error(error.error || `HTTP ${response.status}: ${response.statusText}`);
            }

            // Handle empty responses (204 No Content)
            if (response.status === 204) {
                return null;
            }

            const contentType = response.headers.get('content-type');
            if (contentType && contentType.includes('application/json')) {
                return await response.json();
            }

            return await response.text();
        } catch (error) {
            console.error(`API Error [${endpoint}]:`, error);
            throw error;
        }
    }

    // Overview
    async getOverview() {
        return this.request('/overview');
    }

    // Channels
    async getChannels(params = {}) {
        const query = new URLSearchParams(params).toString();
        return this.request(`/channels${query ? '?' + query : ''}`);
    }

    async getChannel(channelName) {
        return this.request(`/channels/${encodeURIComponent(channelName)}`);
    }

    async getChannelSubscriptions(channelName) {
        return this.request(`/channels/${encodeURIComponent(channelName)}/subscriptions`);
    }

    async getChannelState(channelName) {
        return this.request(`/channels/${encodeURIComponent(channelName)}/state`);
    }

    async deleteChannelStateKey(channelName, key) {
        return this.request(`/channels/${encodeURIComponent(channelName)}/state/${encodeURIComponent(key)}`, {
            method: 'DELETE'
        });
    }

    async resetChannel(channelName) {
        return this.request(`/channels/${encodeURIComponent(channelName)}/reset`, {
            method: 'POST'
        });
    }

    // Subscriptions
    async getSubscriptions(params = {}) {
        const query = new URLSearchParams(params).toString();
        return this.request(`/subscriptions${query ? '?' + query : ''}`);
    }

    async getSubscription(id) {
        return this.request(`/subscriptions/${id}`);
    }

    async suspendSubscription(id) {
        return this.request(`/subscriptions/${id}/suspend`, {
            method: 'POST'
        });
    }

    async resumeSubscription(id) {
        return this.request(`/subscriptions/${id}/resume`, {
            method: 'POST'
        });
    }

    async detachSubscription(id) {
        return this.request(`/subscriptions/${id}/detach`, {
            method: 'POST'
        });
    }

    // Metrics
    async getMetrics() {
        return this.request('/metrics');
    }

    async exportMetrics(format = 'json') {
        return this.request(`/metrics/export?format=${format}`);
    }

    // Configuration (to be implemented)
    async getConfiguration() {
        return this.request('/configuration');
    }

    // Errors (to be implemented)
    async getErrors(params = {}) {
        const query = new URLSearchParams(params).toString();
        return this.request(`/errors${query ? '?' + query : ''}`);
    }

    async clearErrors() {
        return this.request('/errors', {
            method: 'DELETE'
        });
    }
}

// Export singleton instance
export const api = new ApiClient();
