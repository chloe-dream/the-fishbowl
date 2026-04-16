/**
 * The Fishbowl - Core Application Logic
 */

// Utility: Debounce
function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

class AdminAPI {
    constructor() {
        this.userId = null;
        this.vaultKey = sessionStorage.getItem('fishbowl_vault_key');
    }

    async request(path, options = {}) {
        const headers = {
            'Content-Type': 'application/json',
            ...options.headers
        };

        const response = await fetch(path, { ...options, headers });
        
        if (response.status === 401) {
            window.location.href = '/login?ReturnUrl=' + encodeURIComponent(window.location.pathname);
            return;
        }

        if (!response.ok) throw new Error(`API Error: ${response.statusText}`);
        if (response.status === 204) return null;
        return response.json();
    }

    // Notes
    async getNotes() { return this.request('/api/notes'); }
    async getNote(id) { return this.request(`/api/notes/${id}`); }
    async createNote(note) { return this.request('/api/notes', { method: 'POST', body: JSON.stringify(note) }); }
    async updateNote(id, note) { return this.request(`/api/notes/${id}`, { method: 'PUT', body: JSON.stringify(note) }); }
    async deleteNote(id) { return this.request(`/api/notes/${id}`, { method: 'DELETE' }); }
}

const api = new AdminAPI();
let currentNote = null;
let notes = [];

// DOM Elements
const noteList = document.getElementById('note-list');
const emptyState = document.getElementById('editor-empty-state');
const editor = document.getElementById('note-editor');
const titleInput = document.getElementById('note-title');
const contentInput = document.getElementById('note-content');
const deleteBtn = document.getElementById('delete-note');
const newNoteBtn = document.getElementById('new-note-btn');

// Initialize
async function init() {
    checkVaultSecurity();
    await refreshNoteList();
    setupEventListeners();
}

function checkVaultSecurity() {
    if (!api.vaultKey) {
        const warning = document.createElement('div');
        warning.style.cssText = 'background: var(--sunset-red); color: white; padding: 10px; text-align: center; font-weight: 600; cursor: pointer;';
        warning.innerHTML = '<i class="fa-solid fa-triangle-exclamation"></i> Vault Not Initialized. Click to set your Master Password.';
        warning.onclick = () => {
            const pw = prompt("Set your Master Password. IMPORTANT: Write it down and keep it safe!");
            if (pw) {
                sessionStorage.setItem('fishbowl_vault_key', btoa(pw));
                window.location.reload();
            }
        };
        document.body.prepend(warning);
    }
}

function updateSaveStatus(status, color = 'var(--text-muted)') {
    const el = document.getElementById('last-saved');
    if (!el) return;
    el.innerHTML = `<span style="display:inline-block; width:8px; height:8px; border-radius:50%; background:${color}; margin-right:8px;"></span> ${status}`;
}

async function refreshNoteList() {
    try {
        notes = await api.getNotes();
        renderNoteList();
    } catch (e) {
        console.error("Failed to load notes", e);
    }
}

function renderNoteList() {
    noteList.innerHTML = notes.map(note => `
        <div class="note-item ${currentNote?.id === note.id ? 'active' : ''}" data-id="${note.id}">
            <h3>${note.title || 'Untitled Note'}</h3>
            <p>${note.content ? (note.content.substring(0, 60) + (note.content.length > 60 ? '...' : '')) : 'No content'}</p>
        </div>
    `).join('');

    document.querySelectorAll('.note-item').forEach(el => {
        el.addEventListener('click', () => selectNote(el.dataset.id));
    });
}

async function selectNote(id) {
    currentNote = notes.find(n => n.id === id);
    if (!currentNote) return;

    renderNoteList();
    emptyState.classList.add('hidden');
    editor.classList.remove('hidden');

    titleInput.value = currentNote.title;
    contentInput.value = currentNote.content;
    updateSaveStatus(`Loaded ${currentNote.title || 'Note'}`);
}

const autoSave = debounce(async () => {
    if (!currentNote) return;
    
    updateSaveStatus('Saving...', 'var(--sunset-orange)');
    
    const updatedNote = {
        ...currentNote,
        title: titleInput.value,
        content: contentInput.value
    };

    try {
        await api.updateNote(currentNote.id, updatedNote);
        const index = notes.findIndex(n => n.id === currentNote.id);
        if (index !== -1) notes[index] = updatedNote;
        
        renderNoteList();
        updateSaveStatus(`Saved at ${new Date().toLocaleTimeString()}`, 'var(--sunset-gold)');
    } catch (e) {
        console.error("Failed to auto-save", e);
        updateSaveStatus('Save Failed', 'var(--sunset-red)');
    }
}, 1500);

async function createNewNote() {
    const newNote = {
        title: "New Note",
        content: "",
        type: "note",
        tags: []
    };

    try {
        const createdNote = await api.createNote(newNote);
        await refreshNoteList();
        selectNote(createdNote.id);
    } catch (e) {
        alert("Failed to create note");
    }
}

async function deleteCurrentNote() {
    if (!currentNote || !confirm("Are you sure you want to delete this note?")) return;

    try {
        await api.deleteNote(currentNote.id);
        currentNote = null;
        editor.classList.add('hidden');
        emptyState.classList.remove('hidden');
        await refreshNoteList();
    } catch (e) {
        alert("Failed to delete note");
    }
}

function setupEventListeners() {
    deleteBtn.addEventListener('click', deleteCurrentNote);
    newNoteBtn.addEventListener('click', createNewNote);
    titleInput.addEventListener('input', autoSave);
    contentInput.addEventListener('input', autoSave);
}

document.addEventListener('DOMContentLoaded', init);
