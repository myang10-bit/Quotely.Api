const apiBaseEl = document.getElementById("apiBase");
const emailEl = document.getElementById("email");
const pwdEl = document.getElementById("password");
const tagsEl = document.getElementById("defaultTags");
const loginBtn = document.getElementById("loginBtn");
const registerBtn = document.getElementById("registerBtn");
const saveBtn = document.getElementById("saveSettingsBtn");
const statusEl = document.getElementById("status");

init();

async function init() {
    const { apiBase = "http://localhost:5265", defaultTags = "inbox", token = "", email = "" } =
        await chrome.storage.sync.get(["apiBase", "defaultTags", "token", "email"]);
    apiBaseEl.value = apiBase;
    tagsEl.value = defaultTags;
    emailEl.value = email;
    statusEl.textContent = token ? "Logged in ✅" : "Not logged in";
}

saveBtn.addEventListener("click", async () => {
    await chrome.storage.sync.set({
        apiBase: apiBaseEl.value.trim(),
        defaultTags: tagsEl.value.trim()
    });
    setStatus("Settings saved ✅");
});

loginBtn.addEventListener("click", async () => {
    await auth("login");
});

registerBtn.addEventListener("click", async () => {
    await auth("register");
});

async function auth(kind) {
    const apiBase = apiBaseEl.value.trim();
    const email = emailEl.value.trim();
    const password = pwdEl.value;

    if (!apiBase || !email || !password) {
        setStatus("Please fill API, email, and password.", true);
        return;
    }

    const url = `${apiBase}/api/auth/${kind}`;
    try {
        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email, password })
        });

        if (!res.ok) {
            const msg = await safeText(res);
            setStatus(`${capitalize(kind)} failed (${res.status}). ${msg}`, true);
            return;
        }

        const data = await res.json();
        const token = data.token || data.Token || "";
        if (!token) {
            setStatus("No token in response.", true);
            return;
        }

        await chrome.storage.sync.set({ apiBase, token, email });
        setStatus(`${capitalize(kind)} success. Logged in ✅`);
    } catch (e) {
        setStatus("Network error. Check API URL.", true);
    }
}

function setStatus(msg, isError = false) {
    statusEl.textContent = msg;
    statusEl.style.color = isError ? "#b00020" : "#0a7a2f";
}

async function safeText(res) { try { return await res.text(); } catch { return ""; } }
function capitalize(s) { return s.charAt(0).toUpperCase() + s.slice(1); }
