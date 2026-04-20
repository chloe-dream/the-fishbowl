/**
 * <fb-slider label="Depth" min="0" max="100" step="1" value="50" suffix="px">
 * <fb-slider label="Quality" min="0" max="2" step="1" value="1" labels="Low,Medium,High">
 *
 * Range slider with live value display. Events:
 *   input  — fired on drag (live); e.detail = string value.
 *   change — fired on release; e.detail = string value.
 */
class FbSlider extends HTMLElement {
    static get observedAttributes() { return ["label", "min", "max", "step", "value", "suffix", "labels"]; }

    constructor() {
        super();
        this.attachShadow({ mode: "open" });
    }

    connectedCallback()        { this.render(); this.attach(); }
    attributeChangedCallback() { if (this.shadowRoot.firstChild) this.render(); }

    render() {
        const label  = this.getAttribute("label")  || "";
        const min    = this.getAttribute("min")    || "0";
        const max    = this.getAttribute("max")    || "100";
        const step   = this.getAttribute("step")   || "1";
        const value  = this.getAttribute("value")  || min;
        const suffix = this.getAttribute("suffix") || "";
        const labels = (this.getAttribute("labels") || "").split(",").map(s => s.trim()).filter(Boolean);

        const displayValue = labels.length ? (labels[parseInt(value, 10)] || value) : `${value}${suffix}`;

        this.shadowRoot.innerHTML = `
            <style>
                :host { display: block; }
                .row { display: flex; justify-content: space-between; font-size: 0.8rem; color: #cbd5e1; margin-bottom: 0.3rem; }
                .val { color: var(--accent, #3b82f6); font-weight: 600; }
                input[type="range"] {
                    width: 100%;
                    -webkit-appearance: none;
                    height: 4px;
                    background: rgba(255,255,255,0.1);
                    border-radius: 2px;
                    outline: none;
                }
                input[type="range"]::-webkit-slider-thumb {
                    -webkit-appearance: none;
                    width: 14px; height: 14px;
                    border-radius: 50%;
                    background: var(--accent, #3b82f6);
                    cursor: pointer;
                }
                input[type="range"]::-moz-range-thumb {
                    width: 14px; height: 14px;
                    border-radius: 50%;
                    background: var(--accent, #3b82f6);
                    cursor: pointer;
                    border: none;
                }
            </style>
            <div class="row">
                <span>${label}</span>
                <span class="val">${displayValue}</span>
            </div>
            <input type="range" min="${min}" max="${max}" step="${step}" value="${value}"/>
        `;
    }

    attach() {
        const input = this.shadowRoot.querySelector("input");
        if (!input) return;
        const fire = (type) => this.dispatchEvent(new CustomEvent(type, {
            detail: input.value,
            bubbles: true,
            composed: true
        }));
        input.addEventListener("input",  () => { this.setAttribute("value", input.value); fire("input");  });
        input.addEventListener("change", () => { this.setAttribute("value", input.value); fire("change"); });
    }
}

customElements.define("fb-slider", FbSlider);
