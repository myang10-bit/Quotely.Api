// Create the right-click context menu
chrome.runtime.onInstalled.addListener(() => {
    chrome.contextMenus.create({
        id: "save-quote",
        title: "Save to Quotely",
        contexts: ["selection"]
    });
});

// When clicked, send the selected text to your API
chrome.contextMenus.onClicked.addListener(async (info, tab) => {
    if (info.menuItemId !== "save-quote") return;
    const selectedText = info.selectionText?.trim();
    if (!selectedText) return;

    const pageUrl = info.pageUrl || tab?.url || "";
    const pageTitle = tab?.title || "";

    // Load settings
    const { apiBase = "http://localhost:5265", token = "", defaultTags = "inbox" } =
        await chrome.storage.sync.get(["apiBase", "token", "defaultTags"]);

    if (!token) {
        notify("Quotely: please log in from the popup first.");
        return;
    }

    try {
        const res = await fetch(`${apiBase}/api/quotes`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Authorization": `Bearer ${token}`
            },
            body: JSON.stringify({
                text: selectedText,
                sourceTitle: pageTitle || null,
                sourceAuthor: null,
                sourceUrl: pageUrl || null,
                note: null,
                tags: defaultTags
                    ? defaultTags.split(",").map(t => t.trim()).filter(Boolean)
                    : ["inbox"]
            })
        });

        if (!res.ok) {
            const msg = await safeText(res);
            notify(`Quotely: failed (${res.status}). ${msg}`);
            return;
        }

        notify("Quotely: quote saved ✅");
    } catch (e) {
        notify("Quotely: network error. Check API URL and CORS.");
    }
});

async function safeText(res) {
    try { return await res.text(); } catch { return ""; }
}

function notify(message) {
    // Lightweight toast via Chrome notification
    chrome.notifications?.create?.(undefined, {
        type: "basic",
        iconUrl: "icons/128.png",
        title: "Quotely",
        message
    });
}
