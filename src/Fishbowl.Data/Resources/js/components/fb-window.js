/**
 * FbWindow - A premium glassmorphic, draggable and resizable window component.
 * Ported from DreamWindow; adapted to Fishbowl tokens.
 * Fixed issues: Event listener loss, drag/resize coordinates, and scroll bleeding.
 */
class FbWindow extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: 'open' });
        this.isDragging = false;
        this.isResizing = false;
        this.startX = 0;
        this.startY = 0;
        this.startLeft = 0;
        this.startTop = 0;
        this.startWidth = 0;
        this.startHeight = 0;
    }

    static get observedAttributes() {
        return ['title', 'width', 'height', 'top', 'left', 'open'];
    }

    connectedCallback() {
        this.initStructure();
        this.setupEventListeners();
        this.applyAttributes();
    }

    attributeChangedCallback(name, oldValue, newValue) {
        if (oldValue !== newValue && this.shadowRoot.innerHTML !== '') {
            this.applyAttributes();
        }
    }

    open() {
        this.setAttribute('open', '');
        this.bringToFront();
        this.dispatchEvent(new CustomEvent('open', { detail: { window: this } }));
    }

    close() {
        this.removeAttribute('open');
        this.dispatchEvent(new CustomEvent('close', { detail: { window: this } }));
    }

    toggle() {
        if (this.hasAttribute('open')) this.close();
        else this.open();
    }

    bringToFront() {
        let maxZ = 10000;
        document.querySelectorAll('fb-window').forEach(w => {
            const z = parseInt(window.getComputedStyle(w).zIndex || 10000);
            if (z > maxZ) maxZ = z;
        });
        this.style.zIndex = maxZ + 1;
    }

    initStructure() {
        this.shadowRoot.innerHTML = `
        <style>
            :host {
                display: none;
                position: fixed;
                z-index: 10000;
                font-family: 'Inter', sans-serif;
                box-sizing: border-box;
            }

            :host([open]) {
                display: block;
            }

            .window-container {
                width: 100%;
                height: 100%;
                background: rgba(30, 41, 59, 0.7);
                backdrop-filter: blur(20px) saturate(180%);
                -webkit-backdrop-filter: blur(20px) saturate(180%);
                border: 1px solid rgba(255, 255, 255, 0.1);
                border-radius: 16px 16px 2px 16px;
                display: flex;
                flex-direction: column;
                overflow: hidden;
                box-shadow: 0 20px 50px rgba(0, 0, 0, 0.5), inset 0 0 0 1px rgba(255, 255, 255, 0.05);
            }

            .title-bar {
                padding: 12px 16px;
                background: rgba(255, 255, 255, 0.05);
                border-bottom: 1px solid rgba(255, 255, 255, 0.1);
                display: flex;
                align-items: center;
                justify-content: space-between;
                cursor: grab;
                user-select: none;
            }

            .title-bar:active {
                cursor: grabbing;
            }

            .title {
                font-family: 'Outfit', sans-serif;
                font-weight: 700;
                font-size: 0.8rem;
                color: #f8fafc;
                text-transform: uppercase;
                letter-spacing: 0.08em;
                pointer-events: none;
            }

            .controls {
                display: flex;
                gap: 8px;
            }

            .close-btn {
                width: 15px;
                height: 15px;
                border-radius: 50%;
                background: var(--danger, #ef4444);
                cursor: pointer;
                border: none;
                transition: transform 0.2s, background 0.2s;
                position: relative;
            }

            .close-btn:hover {
                transform: scale(1.1);
                background: #ff5f57;
            }

            .content {
                flex: 1;
                overflow: auto;
                padding: 20px;
                color: #cbd5e1;
                font-size: 0.9rem;
                line-height: 1.6;
            }

            /* Custom Scrollbar */
            .content::-webkit-scrollbar {
                width: 6px;
            }
            .content::-webkit-scrollbar-track {
                background: transparent;
            }
            .content::-webkit-scrollbar-thumb {
                background: rgba(255, 255, 255, 0.15);
                border-radius: 10px;
            }

            .resize-handle {
                position: absolute;
                bottom: 0;
                right: 0;
                width: 20px;
                height: 20px;
                cursor: nwse-resize;
                z-index: 10;
            }
            .resize-handle::after {
                content: '';
                position: absolute;
                bottom: 4px;
                right: 4px;
                width: 8px;
                height: 8px;
                border-right: 2px solid rgba(255, 255, 255, 0.3);
                border-bottom: 2px solid rgba(255, 255, 255, 0.3);
            }
        </style>
        <div class="window-container">
            <div class="title-bar">
                <span class="title" id="window-title-text"></span>
                <div class="controls">
                    <button class="close-btn" id="window-close-btn"></button>
                </div>
            </div>
            <div class="content" id="window-content">
                <slot></slot>
            </div>
            <div class="resize-handle"></div>
        </div>
        `;
    }

    applyAttributes() {
        const titleText = this.shadowRoot.getElementById('window-title-text');
        if (titleText) titleText.textContent = this.getAttribute('title') || 'Window';

        if (this.style.width === '') this.style.width = this.getAttribute('width') || '400px';
        if (this.style.height === '') this.style.height = this.getAttribute('height') || '300px';
        if (this.style.top === '') this.style.top = this.getAttribute('top') || '100px';
        if (this.style.left === '') this.style.left = this.getAttribute('left') || '100px';
    }

    setupEventListeners() {
        const titleBar = this.shadowRoot.querySelector('.title-bar');
        const closeBtn = this.shadowRoot.getElementById('window-close-btn');
        const resizeHandle = this.shadowRoot.querySelector('.resize-handle');
        const contentArea = this.shadowRoot.getElementById('window-content');

        closeBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.close();
        });

        // SCROLL ISOLATION (Lightweight version)
        this.addEventListener('wheel', (e) => {
            // Only stop propagation to prevent background zoom/flight
            // but don't preventDefault unless we are sure it's not needed.
            e.stopPropagation();
        }, { passive: true });

        // DRAG LOGIC
        titleBar.addEventListener('mousedown', (e) => {
            if (e.target === closeBtn) return;
            this.isDragging = true;
            this.startX = e.clientX;
            this.startY = e.clientY;

            const rect = this.getBoundingClientRect();
            this.startLeft = rect.left;
            this.startTop = rect.top;

            document.addEventListener('mousemove', this.onMouseMove);
            document.addEventListener('mouseup', this.onMouseUp);
            e.preventDefault();
        });

        // RESIZE LOGIC
        resizeHandle.addEventListener('mousedown', (e) => {
            this.isResizing = true;
            this.startX = e.clientX;
            this.startY = e.clientY;
            this.startWidth = this.offsetWidth;
            this.startHeight = this.offsetHeight;

            document.addEventListener('mousemove', this.onMouseMove);
            document.addEventListener('mouseup', this.onMouseUp);
            e.preventDefault();
        });

        this.onMouseMove = (e) => {
            if (this.isDragging) {
                const dx = e.clientX - this.startX;
                const dy = e.clientY - this.startY;
                this.style.left = `${this.startLeft + dx}px`;
                this.style.top = `${this.startTop + dy}px`;
            }
            if (this.isResizing) {
                const dx = e.clientX - this.startX;
                const dy = e.clientY - this.startY;
                this.style.width = `${Math.max(200, this.startWidth + dx)}px`;
                this.style.height = `${Math.max(150, this.startHeight + dy)}px`;
            }
        };

        this.onMouseUp = () => {
            this.isDragging = false;
            this.isResizing = false;
            document.removeEventListener('mousemove', this.onMouseMove);
            document.removeEventListener('mouseup', this.onMouseUp);
        };
    }
}

customElements.define('fb-window', FbWindow);
