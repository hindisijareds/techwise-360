(function initializeTechWiseTheme() {
  const STORAGE_KEY = "techwise360.theme";
  const THEME_TOGGLE_ENABLED = false;
  const root = document.documentElement;
  const systemTheme = window.matchMedia?.("(prefers-color-scheme: dark)");

  function readStoredTheme() {
    try {
      const stored = window.localStorage.getItem(STORAGE_KEY);
      return stored === "dark" || stored === "light" ? stored : "";
    } catch (_error) {
      return "";
    }
  }

  function preferredTheme() {
    if (!THEME_TOGGLE_ENABLED) return "light";
    return readStoredTheme() || (systemTheme?.matches ? "dark" : "light");
  }

  function syncToggle(button, theme) {
    const dark = theme === "dark";
    button.setAttribute("aria-pressed", String(dark));
    button.setAttribute("aria-label", dark ? "Switch to light mode" : "Switch to dark mode");
    button.title = dark ? "Switch to light mode" : "Switch to dark mode";
  }

  function applyTheme(theme, { persist = false, animate = false } = {}) {
    const nextTheme = theme === "dark" ? "dark" : "light";
    if (animate) root.classList.add("theme-transitioning");
    root.dataset.theme = nextTheme;
    root.style.colorScheme = nextTheme;
    document.querySelectorAll("[data-theme-toggle]").forEach((button) => syncToggle(button, nextTheme));
    if (persist) {
      try {
        window.localStorage.setItem(STORAGE_KEY, nextTheme);
      } catch (_error) {
        // The selected theme still applies for this page when storage is unavailable.
      }
    }
    if (animate) window.setTimeout(() => root.classList.remove("theme-transitioning"), 280);
    window.dispatchEvent(new CustomEvent("techwise:themechange", { detail: { theme: nextTheme } }));
  }

  function toggleMarkup() {
    return `
      <span class="theme-toggle-track" aria-hidden="true">
        <i data-lucide="sun"></i>
        <i data-lucide="moon"></i>
        <span class="theme-toggle-thumb"></span>
      </span>
    `;
  }

  function ensureAuthenticationToggle() {
    if (!THEME_TOGGLE_ENABLED) return;
    if (["teacher", "student"].includes(document.body?.dataset.page)) return;
    if (document.querySelector("[data-theme-toggle]")) return;
    const button = document.createElement("button");
    button.type = "button";
    button.className = "theme-toggle auth-theme-toggle";
    button.dataset.themeToggle = "";
    button.innerHTML = toggleMarkup();
    document.body.appendChild(button);
  }

  applyTheme(preferredTheme());

  document.addEventListener("DOMContentLoaded", () => {
    ensureAuthenticationToggle();
    applyTheme(root.dataset.theme || preferredTheme());
    document.querySelectorAll("[data-theme-toggle]").forEach((button) => {
      button.addEventListener("click", () => {
        const nextTheme = root.dataset.theme === "dark" ? "light" : "dark";
        applyTheme(nextTheme, { persist: true, animate: true });
      });
    });
    if (window.lucide) window.lucide.createIcons();
  });

  systemTheme?.addEventListener?.("change", (event) => {
    if (!THEME_TOGGLE_ENABLED) return;
    if (!readStoredTheme()) applyTheme(event.matches ? "dark" : "light", { animate: true });
  });

  window.TechWiseTheme = { apply: applyTheme, current: () => root.dataset.theme || "light" };
})();
