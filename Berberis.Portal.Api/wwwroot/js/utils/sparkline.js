// Simple SVG Sparkline Component
// Minimal, lightweight, no dependencies

export function createSparkline(dataPoints, options = {}) {
    const {
        width = 100,
        height = 30,
        strokeWidth = 1.5,
        strokeColor = '#60a5fa',
        fillColor = 'rgba(96, 165, 250, 0.1)',
        showDots = false,
        dotRadius = 2,
        dotColor = '#3b82f6'
    } = options;

    // Handle empty or invalid data
    if (!dataPoints || dataPoints.length === 0) {
        return `<svg width="${width}" height="${height}" class="sparkline"><text x="${width/2}" y="${height/2}" text-anchor="middle" font-size="10" fill="#999">No data</text></svg>`;
    }

    // Filter out null/undefined values
    const validPoints = dataPoints.filter(p => p !== null && p !== undefined && !isNaN(p));
    if (validPoints.length === 0) {
        return `<svg width="${width}" height="${height}" class="sparkline"><line x1="0" y1="${height/2}" x2="${width}" y2="${height/2}" stroke="#444" stroke-width="1" stroke-dasharray="2,2"/></svg>`;
    }

    // Calculate min/max for scaling
    const min = Math.min(...validPoints);
    const max = Math.max(...validPoints);
    const range = max - min;

    // If all values are the same, draw a horizontal line
    if (range === 0) {
        return `<svg width="${width}" height="${height}" class="sparkline">
            <line x1="0" y1="${height/2}" x2="${width}" y2="${height/2}" stroke="${strokeColor}" stroke-width="${strokeWidth}"/>
        </svg>`;
    }

    // Scale function: maps data value to Y coordinate (inverted because SVG Y goes down)
    const scaleY = (value) => {
        const normalized = (value - min) / range;
        return height - (normalized * height * 0.8) - (height * 0.1); // Leave 10% padding top/bottom
    };

    // Scale function: maps index to X coordinate
    const scaleX = (index) => {
        return (index / (validPoints.length - 1)) * width;
    };

    // Build SVG path
    let pathData = '';
    validPoints.forEach((value, i) => {
        const x = scaleX(i);
        const y = scaleY(value);
        if (i === 0) {
            pathData += `M ${x},${y}`;
        } else {
            pathData += ` L ${x},${y}`;
        }
    });

    // Build filled area path
    const firstX = scaleX(0);
    const lastX = scaleX(validPoints.length - 1);
    const areaPath = `${pathData} L ${lastX},${height} L ${firstX},${height} Z`;

    // Build dots SVG
    let dotsHtml = '';
    if (showDots) {
        dotsHtml = validPoints.map((value, i) => {
            const x = scaleX(i);
            const y = scaleY(value);
            return `<circle cx="${x}" cy="${y}" r="${dotRadius}" fill="${dotColor}"/>`;
        }).join('');
    }

    return `
        <svg width="${width}" height="${height}" class="sparkline" viewBox="0 0 ${width} ${height}">
            <path d="${areaPath}" fill="${fillColor}" opacity="0.3"/>
            <path d="${pathData}" fill="none" stroke="${strokeColor}" stroke-width="${strokeWidth}" stroke-linecap="round" stroke-linejoin="round"/>
            ${dotsHtml}
        </svg>
    `.trim();
}

// History tracker - keeps last N data points for sparklines
export class MetricsHistory {
    constructor(maxPoints = 20) {
        this.maxPoints = maxPoints;
        this.history = new Map();
    }

    add(key, value) {
        if (!this.history.has(key)) {
            this.history.set(key, []);
        }

        const points = this.history.get(key);
        points.push(value);

        // Keep only last N points
        if (points.length > this.maxPoints) {
            points.shift();
        }
    }

    get(key) {
        return this.history.get(key) || [];
    }

    clear(key) {
        if (key) {
            this.history.delete(key);
        } else {
            this.history.clear();
        }
    }
}
