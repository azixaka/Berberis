// Main Application - Router and Screen Loader

import { signalRClient } from './signalr-client.js';

class App {
    constructor() {
        this.currentScreen = null;
        this.contentContainer = document.getElementById('app-content');
        this.screens = {
            'overview': () => import('./screens/overview.js'),
            'channels': () => import('./screens/channels-list.js'),
            'channel-detail': () => import('./screens/channel-detail.js'),
            'subscriptions': () => import('./screens/subscriptions-list.js'),
            'subscription-detail': () => import('./screens/subscription-detail.js'),
            'analytics': () => import('./screens/analytics.js'),
            'bottlenecks': () => import('./screens/bottlenecks.js'),
            'lifecycle': () => import('./screens/lifecycle.js'),
            'traces': () => import('./screens/traces.js'),
            'errors': () => import('./screens/errors.js'),
            'pattern-tester': () => import('./screens/pattern-tester.js'),
            'configuration': () => import('./screens/configuration.js')
        };
    }

    async init() {
        console.log('Initializing Berberis Portal...');

        // Connect to SignalR
        await signalRClient.connect();

        // Setup routing
        this.setupRouting();

        // Handle initial route or default to overview
        this.handleRoute();
    }

    setupRouting() {
        // Listen for hash changes
        window.addEventListener('hashchange', () => this.handleRoute());

        // Update active nav items on navigation
        document.querySelectorAll('.nav-item').forEach(item => {
            item.addEventListener('click', (e) => {
                this.updateActiveNav(item.dataset.route);
            });
        });
    }

    async handleRoute() {
        const hash = window.location.hash.slice(1) || '/overview';
        const [path, ...params] = hash.split('/').filter(p => p);

        console.log(`Navigating to: ${path}`, params);

        // Update active navigation
        this.updateActiveNav(path);

        // Load screen
        await this.loadScreen(path, params);
    }

    updateActiveNav(route) {
        document.querySelectorAll('.nav-item').forEach(item => {
            if (item.dataset.route === route) {
                item.classList.add('active');
            } else {
                item.classList.remove('active');
            }
        });
    }

    async loadScreen(screenName, params = []) {
        try {
            // Show loading state
            this.showLoading();

            // Cleanup previous screen
            if (this.currentScreen && typeof this.currentScreen.cleanup === 'function') {
                await this.currentScreen.cleanup();
            }

            // Load screen module
            const screenLoader = this.screens[screenName];
            if (!screenLoader) {
                this.showError(`Screen not found: ${screenName}`);
                return;
            }

            const screenModule = await screenLoader();
            this.currentScreen = screenModule.default || screenModule;

            // Render screen
            if (typeof this.currentScreen.render === 'function') {
                await this.currentScreen.render(this.contentContainer, params);
            } else {
                this.showError(`Screen ${screenName} has no render function`);
            }

        } catch (error) {
            console.error('Error loading screen:', error);
            this.showError(error.message);
        }
    }

    showLoading() {
        this.contentContainer.innerHTML = `
            <div class="loading">
                <div>Loading...</div>
            </div>
        `;
    }

    showError(message) {
        this.contentContainer.innerHTML = `
            <div class="error-message">
                <strong>Error:</strong> ${message}
            </div>
            <div class="empty-state">
                <div class="empty-state-icon">⚠️</div>
                <div class="empty-state-title">Something went wrong</div>
                <div class="empty-state-description">
                    Please try navigating to a different page or refreshing the browser.
                </div>
            </div>
        `;
    }
}

// Initialize app when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        const app = new App();
        app.init();
    });
} else {
    const app = new App();
    app.init();
}
