// SignalR Client - Manages real-time connection to EventsHub

class SignalRClient {
    constructor() {
        this.connection = null;
        this.isConnected = false;
        this.eventHandlers = {
            lifecycle: [],
            trace: [],
            metrics: []
        };
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 10;
    }

    async connect() {
        if (this.connection && this.isConnected) {
            console.log('SignalR already connected');
            return;
        }

        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/events')
                .withAutomaticReconnect({
                    nextRetryDelayInMilliseconds: retryContext => {
                        if (retryContext.previousRetryCount < 5) {
                            return 2000; // 2 seconds for first 5 attempts
                        } else {
                            return 5000; // 5 seconds after that
                        }
                    }
                })
                .configureLogging(signalR.LogLevel.Information)
                .build();

            this.setupEventHandlers();
            this.setupConnectionHandlers();

            await this.connection.start();
            this.isConnected = true;
            this.reconnectAttempts = 0;
            this.updateConnectionStatus('connected');
            console.log('SignalR connected successfully');

        } catch (error) {
            console.error('SignalR connection error:', error);
            this.isConnected = false;
            this.updateConnectionStatus('disconnected');
            this.scheduleReconnect();
        }
    }

    setupEventHandlers() {
        // Lifecycle events
        this.connection.on('OnLifecycleEvent', (event) => {
            this.eventHandlers.lifecycle.forEach(handler => handler(event));
        });

        // Message traces
        this.connection.on('OnMessageTrace', (message) => {
            this.eventHandlers.trace.forEach(handler => handler(message));
        });

        // Metrics updates
        this.connection.on('OnMetricsUpdate', (metrics) => {
            this.eventHandlers.metrics.forEach(handler => handler(metrics));
        });
    }

    setupConnectionHandlers() {
        this.connection.onreconnecting(() => {
            this.isConnected = false;
            this.updateConnectionStatus('reconnecting');
            console.log('SignalR reconnecting...');
        });

        this.connection.onreconnected(() => {
            this.isConnected = true;
            this.updateConnectionStatus('connected');
            console.log('SignalR reconnected');
        });

        this.connection.onclose(() => {
            this.isConnected = false;
            this.updateConnectionStatus('disconnected');
            console.log('SignalR connection closed');
            this.scheduleReconnect();
        });
    }

    scheduleReconnect() {
        if (this.reconnectAttempts < this.maxReconnectAttempts) {
            this.reconnectAttempts++;
            const delay = Math.min(1000 * Math.pow(2, this.reconnectAttempts), 30000);
            console.log(`Scheduling reconnect attempt ${this.reconnectAttempts} in ${delay}ms`);
            setTimeout(() => this.connect(), delay);
        } else {
            console.error('Max reconnect attempts reached');
        }
    }

    updateConnectionStatus(status) {
        const statusEl = document.getElementById('connection-status');
        if (!statusEl) return;

        statusEl.className = `connection-status ${status}`;
        const statusText = statusEl.querySelector('.status-text');
        if (statusText) {
            statusText.textContent = status === 'connected' ? 'Connected' :
                                     status === 'reconnecting' ? 'Reconnecting...' :
                                     'Disconnected';
        }
    }

    // Subscription management
    async subscribeToLifecycle() {
        if (!this.isConnected) {
            throw new Error('Not connected to SignalR hub');
        }
        await this.connection.invoke('SubscribeToLifecycle');
    }

    async unsubscribeFromLifecycle() {
        if (!this.isConnected) return;
        await this.connection.invoke('UnsubscribeFromLifecycle');
    }

    async subscribeToTraces(samplingRate = 0.01) {
        if (!this.isConnected) {
            throw new Error('Not connected to SignalR hub');
        }
        await this.connection.invoke('SubscribeToTraces', samplingRate);
    }

    async unsubscribeFromTraces() {
        if (!this.isConnected) return;
        await this.connection.invoke('UnsubscribeFromTraces');
    }

    async subscribeToMetrics(intervalMs = 5000) {
        if (!this.isConnected) {
            throw new Error('Not connected to SignalR hub');
        }
        await this.connection.invoke('SubscribeToMetrics', intervalMs);
    }

    async unsubscribeFromMetrics() {
        if (!this.isConnected) return;
        await this.connection.invoke('UnsubscribeFromMetrics');
    }

    // Event handler registration
    onLifecycleEvent(handler) {
        this.eventHandlers.lifecycle.push(handler);
        return () => {
            const index = this.eventHandlers.lifecycle.indexOf(handler);
            if (index > -1) this.eventHandlers.lifecycle.splice(index, 1);
        };
    }

    onMessageTrace(handler) {
        this.eventHandlers.trace.push(handler);
        return () => {
            const index = this.eventHandlers.trace.indexOf(handler);
            if (index > -1) this.eventHandlers.trace.splice(index, 1);
        };
    }

    onMetricsUpdate(handler) {
        this.eventHandlers.metrics.push(handler);
        return () => {
            const index = this.eventHandlers.metrics.indexOf(handler);
            if (index > -1) this.eventHandlers.metrics.splice(index, 1);
        };
    }

    async disconnect() {
        if (this.connection) {
            await this.connection.stop();
            this.isConnected = false;
            this.updateConnectionStatus('disconnected');
        }
    }
}

// Export singleton instance
export const signalRClient = new SignalRClient();
