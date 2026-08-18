const SESSION_KEY = "techwise360.session";
const STUDENTS_PER_PAGE = 8;
const LESSONS_PER_PAGE = 5;
const ASSESSMENTS_PER_PAGE = 6;
const PROFILE_PAGE_SIZE = 5;
const AWARDS_PER_PAGE = 6;
const STUDENT_LEARN_ITEMS_PER_PAGE = 6;
const STUDENT_ASSESSMENTS_PER_PAGE = 5;
const NOTIFICATION_POLL_MS = 45000;
const REPORT_TEMPLATE_NAME = "TechWise 360 Performance Report";
const EVIDENCE_SUPPORT_DAYS = 7;
const MAX_LESSON_FILE_BYTES = 104857600;
const MAX_STUDENT_AVATAR_BYTES = 2097152;
const TEACHER_SIDEBAR_STORAGE_KEY = "techwise360.teacher.sidebar-collapsed";
const TEACHER_VIEW_META = {
  overview: { parent: "Overview", current: "Dashboard", title: "Teacher Dashboard" },
  students: { parent: "Management", current: "Students", title: "Students" },
  "student-profile": { parent: "Students", current: "Student Profile", title: "Student Profile" },
  quarters: { parent: "Curriculum", current: "Terms", title: "Terms" },
  content: { parent: "Curriculum", current: "Lessons", title: "Lessons & Modules" },
  assessments: { parent: "Curriculum", current: "Assessments", title: "Assessments" },
  reports: { parent: "Analytics", current: "Reports", title: "Reports" },
  leaderboard: { parent: "Analytics", current: "VR Leaderboard", title: "VR Leaderboard" },
  evaluation: { parent: "Analytics", current: "Evaluation", title: "Evaluation" },
  achievements: { parent: "Recognition", current: "Achievements", title: "Achievements" },
  settings: { parent: "Account", current: "Settings", title: "Settings" }
};
const PACKAGE_MODULE_TITLES = new Set(["PC Assembly and Disassembly"]);
const LESSON_SECTION_TYPES = [
  "paragraph",
  "key_points",
  "example",
  "quick_tip",
  "resource",
  "steps",
  "safety_note",
  "vocabulary",
  "summary",
  "reflection"
];
const LESSON_QUESTION_TYPES = ["multiple_choice", "true_false", "short_answer", "ordering", "identification"];
const LESSON_FILE_TYPES = new Set([
  "application/pdf",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  "application/vnd.openxmlformats-officedocument.presentationml.presentation",
  "video/mp4"
]);
const STUDENT_AVATAR_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);
const STUDENT_SECTION_ADVISERS = {
  "Grade 9": {
    "Ylang Ylang": "Carla Mar Locquiao",
    "Dama De Noche": "Lara Santos",
    Sampaguita: "Joel Jacob"
  },
  "Grade 10": {
    Rosal: "Salvador Reasonda Jr.",
    Lavender: "Noella Krista Valdez",
    Tulip: "Richie Unlayao"
  }
};
const GRADE_LEVELS = Object.keys(STUDENT_SECTION_ADVISERS);
const DEFAULT_GRADE_SECTION_FILTER = { grade: "", section: "" };
const REWARD_TEMPLATES = {
  badge: [
    "Assembly Procedure Record",
    "Hardware Identification Record",
    "Safety Procedure Record",
    "Cable Identification Record",
    "ESD Safety Record",
    "Practice Completion Record",
    "Assessment Completion Record"
  ],
  certificate: [
    "PC Assembly Completion",
    "PC Safety Check Completion",
    "Ports and Cables Completion",
    "Hardware Identification Completion",
    "Module 5 Completion"
  ]
};
const BADGE_ICON_OPTIONS = [
  "award",
  "badge-check",
  "shield-check",
  "check-circle-2",
  "clipboard-check",
  "file-badge",
  "wrench",
  "cpu",
  "book-open",
  "settings",
  "network",
  "monitor"
];
const CERTIFICATE_TEMPLATES = [
  {
    id: "achievement",
    name: "Certificate of Achievement",
    image: "assets/certificates/certificate-achievement-template.png",
    description: "Formal school achievement certificate with printable layout."
  }
];
const DEFAULT_CERTIFICATE_DESCRIPTION = "for outstanding performance, dedication, and exemplary achievement in";

function gradeOptionsHtml(selected = "", includeAll = true) {
  const value = GRADE_LEVELS.includes(selected) ? selected : "";
  return `${includeAll ? `<option value="" ${value ? "" : "selected"}>All Grades</option>` : ""}${GRADE_LEVELS.map((grade) =>
    `<option value="${escapeAttribute(grade)}" ${value === grade ? "selected" : ""}>${escapeHtml(grade)}</option>`
  ).join("")}`;
}

function sectionOptionsForGrade(grade = "") {
  return GRADE_LEVELS.includes(grade) ? Object.keys(STUDENT_SECTION_ADVISERS[grade] || {}) : [];
}

function sectionOptionsHtml(grade = "", selected = "", includeAll = true) {
  const sections = sectionOptionsForGrade(grade);
  const value = sections.includes(selected) ? selected : "";
  return `${includeAll ? `<option value="" ${value ? "" : "selected"}>All Sections</option>` : ""}${sections.map((section) =>
    `<option value="${escapeAttribute(section)}" ${value === section ? "selected" : ""}>${escapeHtml(section)}</option>`
  ).join("")}`;
}

function normalizeGradeSectionFilter(filters = {}) {
  const grade = GRADE_LEVELS.includes(filters.grade) ? filters.grade : "";
  const sections = sectionOptionsForGrade(grade);
  return {
    grade,
    section: grade && sections.includes(filters.section) ? filters.section : ""
  };
}

function filterStudentsByGradeSection(students, filters = {}) {
  const normalized = normalizeGradeSectionFilter(filters);
  return (students || []).filter((student) =>
    (!normalized.grade || student.grade_level === normalized.grade) &&
    (!normalized.section || student.section === normalized.section)
  );
}

const STUDENT_LEARN_ASSETS = [
  { match: ["assembly", "hardware", "computer", "pc"], src: "assets/pc-case.png", icon: "cpu" },
  { match: ["fan", "cooling"], src: "assets/fan.png", icon: "fan" },
  { match: ["video", "graphics", "gpu"], src: "assets/gpu.png", icon: "monitor" },
  { match: ["spreadsheet", "excel"], src: "assets/excel.png", icon: "table-2" },
  { match: ["word", "document", "processing"], src: "assets/word.png", icon: "file-text" },
  { match: ["presentation", "powerpoint"], src: "assets/powerpoint.png", icon: "presentation" },
  { match: ["vr"], src: "assets/vr-headset.png", icon: "badge-check" }
];

const page = document.body.dataset.page;
let teacherState = {
  students: [],
  quarters: [],
  activeQuarter: null,
  modules: [],
  lessons: [],
  assessments: [],
  recentSubmissions: [],
  topicScores: [],
  lessonFiles: [],
  progress: [],
  badges: [],
  certificates: [],
  notifications: [],
  unreadNotifications: 0,
  notificationPanelOpen: false,
  notificationsLoading: false,
  teacherProfile: null,
  settings: defaultTeacherSettings(),
  studentPage: 1,
  lessonPage: 1,
  assessmentPage: 1,
  selectedAssessmentId: "",
  assessmentFilters: {
    class: "",
    type: "",
    status: "",
    search: ""
  },
  evidenceFilters: {
    classKey: ""
  },
  evaluation: defaultTeacherEvaluationData(),
  evaluationGrade: "",
  achievementTab: "overview",
  expandedAwardStudentId: "",
  awardPage: 1,
  awardFilters: { ...DEFAULT_GRADE_SECTION_FILTER },
  leaderboard: { rows: [], competitions: [], summary: {} },
  leaderboardFilters: { competition_id: "", simulation: "" },
  competitions: [],
  badgeDefinitions: [],
  badgeAwards: [],
  badgeManager: { filter: "" },
  completion: { students: [], modules: [], lessons: [], progress: [] },
  completionFilters: { grade: "", section: "", status: "" },
  completionPage: 1,
  reports: {},
  reportFilters: defaultReportFilters(),
  calendarMonth: new Date().toISOString().slice(0, 7),
  selectedLessonFile: null,
  lessonDraftSections: [],
  lessonDraftQuestions: [],
  compactLessons: false,
  previewLayout: null,
  selectedGrade: "",
  selectedSection: "",
  selectedStudentId: "",
  studentProfile: {
    loading: false,
    error: "",
    data: null,
    quarterId: "",
    pages: defaultStudentProfilePages()
  }
};
let teacherNotificationTimer = null;
let studentNotificationTimer = null;
let teacherDefaultsApplied = false;
let studentState = {
  profile: null,
  activeQuarter: null,
  modules: [],
  lessons: [],
  progress: [],
  attempts: [],
  badges: [],
  certificates: [],
  notifications: [],
  unreadNotifications: 0,
  notificationPanelOpen: false,
  notificationsLoading: false,
  progressFilters: defaultStudentProgressFilters(),
  lessonFilter: "all",
  learnPage: 1,
  assessmentPage: 1,
  achievementTab: "badges",
  leaderboard: { rows: [], competitions: [], summary: {} },
  completionSummary: { modules: [], lessons: [], progress: [] },
  evaluation: defaultStudentEvaluationData(),
  lessonPlayer: null,
  avatarUrl: "",
  selectedAvatarFile: null
};

function getDashboardPaginationPages(currentPage, pageCount, visibleCount = 4) {
  const total = Math.max(1, Number(pageCount) || 1);
  const current = Math.min(Math.max(1, Number(currentPage) || 1), total);
  const windowSize = Math.min(total, visibleCount + 1);
  const start = Math.max(1, Math.min(current - Math.floor(windowSize / 2), total - windowSize + 1));
  return Array.from({ length: windowSize }, (_, index) => start + index)
    .filter((pageNumber) => pageNumber !== current)
    .slice(0, visibleCount);
}

function renderDashboardPagination({
  currentPage,
  pageCount,
  pageNumberAttribute,
  label = "Pagination",
  className = "",
  extraAttributes = ""
}) {
  const total = Math.max(1, Number(pageCount) || 1);
  if (total <= 1) return "";
  const current = Math.min(Math.max(1, Number(currentPage) || 1), total);
  const pages = getDashboardPaginationPages(current, total);
  const attributes = (pageNumber) => `${extraAttributes} ${pageNumberAttribute}="${pageNumber}"`;
  return `
    <nav class="student-pagination dashboard-pagination ${escapeAttribute(className)}" aria-label="${escapeAttribute(label)}">
      <button class="pagination-edge" type="button" ${attributes(1)} ${current <= 1 ? "disabled" : ""} aria-label="First page"><i data-lucide="chevrons-left"></i></button>
      <span class="pagination-current" aria-current="page">Page <strong>${current}</strong><span aria-hidden="true"> / </span><span class="sr-only">of </span>${total}</span>
      ${pages.map((pageNumber) => `<button class="pagination-number" type="button" ${attributes(pageNumber)} aria-label="Go to page ${pageNumber}">${pageNumber}</button>`).join("")}
      <button class="pagination-edge" type="button" ${attributes(Math.min(current + 1, total))} ${current >= total ? "disabled" : ""} aria-label="Next page"><i data-lucide="chevron-right"></i></button>
      <button class="pagination-edge" type="button" ${attributes(total)} ${current >= total ? "disabled" : ""} aria-label="Last page"><i data-lucide="chevrons-right"></i></button>
    </nav>
  `;
}

function defaultStudentProfilePages() {
  return {
    lessons: 1,
    assessments: 1,
    achievements: 1,
    activity: 1
  };
}

function ensureFeedbackRoot() {
  let root = document.getElementById("feedbackRoot");
  if (!root) {
    root = document.createElement("div");
    root.id = "feedbackRoot";
    document.body.appendChild(root);
  }
  return root;
}

function ensureToastContainer() {
  let container = document.getElementById("toastContainer");
  if (!container) {
    container = document.createElement("div");
    container.id = "toastContainer";
    container.className = "toast-container";
    container.setAttribute("aria-live", "polite");
    container.setAttribute("aria-atomic", "false");
    document.body.appendChild(container);
  }
  return container;
}

function showToast({ title, message = "", type = "info", duration } = {}) {
  const container = ensureToastContainer();
  const toast = document.createElement("article");
  const displayDuration = duration ?? (type === "error" ? 6500 : 4200);
  const icon = {
    success: "check-circle-2",
    error: "alert-circle",
    warning: "triangle-alert",
    info: "info"
  }[type] || "info";
  toast.className = `app-toast ${type}`;
  toast.setAttribute("role", type === "error" ? "alert" : "status");
  toast.setAttribute("aria-live", type === "error" ? "assertive" : "polite");
  toast.innerHTML = `
    <i data-lucide="${icon}"></i>
    <div>
      <strong>${escapeHtml(title || "Notification")}</strong>
      ${message ? `<span>${escapeHtml(message)}</span>` : ""}
    </div>
    <button type="button" aria-label="Dismiss notification"><i data-lucide="x"></i></button>
  `;
  const dismiss = () => {
    toast.classList.add("leaving");
    setTimeout(() => toast.remove(), 180);
  };
  toast.querySelector("button")?.addEventListener("click", dismiss);
  container.prepend(toast);
  Array.from(container.children).slice(3).forEach((node) => node.remove());
  if (window.lucide) window.lucide.createIcons();
  if (displayDuration) setTimeout(dismiss, displayDuration);
  return toast;
}

function setButtonLoading(button, isLoading, label = "Loading...") {
  if (!button) return;
  if (isLoading) {
    button.dataset.originalHtml = button.innerHTML;
    button.disabled = true;
    button.setAttribute("aria-busy", "true");
    button.classList.add("is-loading");
    button.innerHTML = `<span class="button-spinner" aria-hidden="true"></span><span>${escapeHtml(label)}</span>`;
    return;
  }
  button.disabled = false;
  button.removeAttribute("aria-busy");
  button.classList.remove("is-loading");
  if (button.dataset.originalHtml) {
    button.innerHTML = button.dataset.originalHtml;
    delete button.dataset.originalHtml;
  }
}

function getFocusableElements(container) {
  return Array.from(container.querySelectorAll("button, [href], input, select, textarea, [tabindex]:not([tabindex='-1'])"))
    .filter((element) => !element.disabled && !element.hidden && element.offsetParent !== null);
}

function createFeedbackModal({
  title,
  message,
  tone = "primary",
  icon = "help-circle",
  content = "",
  modalClass = "",
  confirmText = "Confirm",
  cancelText = "Cancel",
  processingText = "Working...",
  errorTitle = "Action failed",
  errorMessage = "",
  hideCancel = false,
  onConfirm,
  validate
} = {}) {
  const root = ensureFeedbackRoot();
  const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
  const overlay = document.createElement("div");
  overlay.className = "feedback-modal-overlay";
  overlay.innerHTML = `
    <section class="feedback-modal ${tone} ${escapeAttribute(modalClass)}" role="dialog" aria-modal="true" aria-labelledby="feedbackModalTitle" aria-describedby="feedbackModalBody">
      <div class="feedback-modal-main">
        <div class="feedback-modal-icon"><i data-lucide="${escapeAttribute(icon)}"></i></div>
        <div class="feedback-modal-content">
          <h2 id="feedbackModalTitle">${escapeHtml(title || "Confirm action")}</h2>
          ${message ? `<p id="feedbackModalBody">${escapeHtml(message)}</p>` : `<p id="feedbackModalBody" class="sr-only">Review this action before continuing.</p>`}
          ${content}
          <p class="feedback-modal-error" data-modal-error hidden></p>
        </div>
      </div>
      <div class="feedback-modal-actions">
        ${hideCancel ? "" : `<button class="small-button" type="button" data-modal-cancel>${escapeHtml(cancelText)}</button>`}
        <button class="primary-button compact ${tone === "danger" ? "danger" : ""}" type="button" data-modal-confirm>${escapeHtml(confirmText)}</button>
      </div>
    </section>
  `;
  root.appendChild(overlay);
  const dialog = overlay.querySelector(".feedback-modal");
  const confirmButton = overlay.querySelector("[data-modal-confirm]");
  const cancelButton = overlay.querySelector("[data-modal-cancel]");
  const errorElement = overlay.querySelector("[data-modal-error]");
  let processing = false;
  let resolved = false;

  const setError = (text) => {
    if (!errorElement) return;
    errorElement.textContent = text || "";
    errorElement.hidden = !text;
  };

  const close = (result) => {
    if (processing || resolved) return;
    resolved = true;
    document.removeEventListener("keydown", handleKeydown);
    overlay.classList.add("closing");
    setTimeout(() => {
      overlay.remove();
      previousFocus?.focus?.();
    }, 160);
    resolver(result);
  };

  const setProcessing = (active) => {
    processing = active;
    overlay.classList.toggle("processing", active);
    if (cancelButton) cancelButton.disabled = active;
    confirmButton.disabled = active;
    confirmButton.innerHTML = active
      ? `<span class="feedback-modal-spinner" aria-hidden="true"></span>${escapeHtml(processingText)}`
      : escapeHtml(confirmText);
  };

  const handleKeydown = (event) => {
    if (event.key === "Escape") {
      event.preventDefault();
      if (!processing) close(false);
      return;
    }
    if (event.key !== "Tab") return;
    const focusable = getFocusableElements(dialog);
    if (!focusable.length) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  let resolver;
  const promise = new Promise((resolve) => {
    resolver = resolve;
  });

  overlay.addEventListener("click", (event) => {
    const suggestionButton = event.target.closest("[data-suggestion-value]");
    if (suggestionButton && overlay.contains(suggestionButton)) {
      const input = overlay.querySelector("[data-modal-input]");
      if (input) {
        input.value = suggestionButton.dataset.suggestionValue || "";
        input.focus();
      }
      return;
    }
    if (event.target === overlay && !processing) close(false);
  });
  cancelButton?.addEventListener("click", () => close(false));
  confirmButton.addEventListener("click", async () => {
    setError("");
    const validationMessage = validate?.(overlay);
    if (validationMessage) {
      setError(validationMessage);
      return;
    }
    if (!onConfirm) {
      close(true);
      return;
    }
    try {
      setProcessing(true);
      const result = await onConfirm(overlay);
      processing = false;
      close(result ?? true);
    } catch (error) {
      setProcessing(false);
      setError(error.message || "Something went wrong. Please try again.");
      showToast({ title: errorTitle, message: errorMessage || error.message || "Please try again.", type: "error", duration: null });
    }
  });
  document.addEventListener("keydown", handleKeydown);
  requestAnimationFrame(() => {
    getFocusableElements(dialog)[0]?.focus();
  });
  if (window.lucide) window.lucide.createIcons();
  return promise;
}

function showConfirmModal(options) {
  return createFeedbackModal(options);
}

function showInputModal({ label, placeholder = "", initialValue = "", required = true, suggestions = [], ...options } = {}) {
  const listId = `feedbackInputSuggestions-${Date.now()}-${Math.random().toString(16).slice(2)}`;
  return createFeedbackModal({
    ...options,
    content: `
      <label class="feedback-modal-field">
        <span>${escapeHtml(label || "Value")}</span>
        <input type="text" data-modal-input value="${escapeAttribute(initialValue)}" placeholder="${escapeAttribute(placeholder)}" ${suggestions.length ? `list="${escapeAttribute(listId)}"` : ""}>
        ${suggestions.length ? `<datalist id="${escapeAttribute(listId)}">${suggestions.map((item) => `<option value="${escapeAttribute(item)}"></option>`).join("")}</datalist>` : ""}
      </label>
      ${suggestions.length ? `<div class="feedback-suggestion-list" aria-label="Suggested ${escapeAttribute(label || "values")}">${suggestions.slice(0, 6).map((item) => `<button type="button" data-suggestion-value="${escapeAttribute(item)}">${escapeHtml(item)}</button>`).join("")}</div>` : ""}
    `,
    validate: (overlay) => {
      const value = overlay.querySelector("[data-modal-input]")?.value.trim() || "";
      if (required && !value) return `${label || "This field"} is required.`;
      return "";
    },
    onConfirm: async (overlay) => {
      const value = overlay.querySelector("[data-modal-input]")?.value.trim() || "";
      if (options.onConfirm) return options.onConfirm(value, overlay);
      return value;
    }
  });
}

if (window.lucide) {
  window.lucide.createIcons();
}

document.querySelectorAll("[data-toggle-password]").forEach((button) => {
  button.addEventListener("click", () => {
    const input = document.getElementById(button.dataset.togglePassword);
    if (!input) return;
    const visible = input.type === "text";
    input.type = visible ? "password" : "text";
    button.setAttribute("aria-label", visible ? "Show password" : "Hide password");
  });
});

document.getElementById("logoutButton")?.addEventListener("click", confirmLogout);

if (page === "login") setupLogin();
if (page === "register") setupRegistration();
if (page === "forgot-password") setupForgotPassword();
if (page === "reset-password") setupResetPassword();
if (page === "teacher" || page === "student") setupMobileNavigation();
if (page === "teacher") setupTeacherDashboard();
if (page === "student") setupStudentDashboard();

async function confirmLogout() {
  const confirmed = await showConfirmModal({
    title: "Log out of TechWise 360?",
    message: "You will return to the login page. Any unsaved work should be saved first.",
    tone: "primary",
    icon: "log-out",
    confirmText: "Log Out",
    cancelText: "Stay Here"
  });
  if (!confirmed) return;
  clearSession();
  window.location.href = "index.html";
}

function setupLogin() {
  const form = document.getElementById("loginForm");
  const message = document.getElementById("loginMessage");

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    setMessage(message, "Signing in...");

    const payload = Object.fromEntries(new FormData(form).entries());

    try {
      const result = await apiPost("/api/login", {
        identifier: payload.identifier,
        password: payload.password
      });

      setSession(result.session, result.profile);
      window.location.href = result.profile.role === "teacher" ? "teacher-dashboard.html" : "student-dashboard.html";
    } catch (error) {
      setMessage(message, error.message, "error");
    }
  });
}

function setupForgotPassword() {
  const form = document.getElementById("forgotPasswordForm");
  const message = document.getElementById("forgotPasswordMessage");
  form?.addEventListener("submit", async (event) => {
    event.preventDefault();
    setMessage(message, "Sending reset link...");
    try {
      const payload = Object.fromEntries(new FormData(form).entries());
      await apiPost("/api/auth/request-password-reset", payload);
      setMessage(message, "Reset link sent. Please check your email inbox.", "success");
      form.reset();
    } catch (error) {
      setMessage(message, error.message, "error");
    }
  });
}

function setupResetPassword() {
  const form = document.getElementById("resetPasswordForm");
  const message = document.getElementById("resetPasswordMessage");
  const resetToken = getResetAccessToken();
  if (!resetToken) {
    setMessage(message, "Reset token is missing or expired. Request a new reset link.", "error");
  }
  form?.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!resetToken) return;
    const payload = Object.fromEntries(new FormData(form).entries());
    if (payload.password !== payload.confirm_password) {
      setMessage(message, "Passwords do not match.", "error");
      return;
    }
    setMessage(message, "Updating password...");
    try {
      await apiPostWithToken("/api/auth/update-password", payload, resetToken);
      setMessage(message, "Password updated. You can now log in.", "success");
      form.reset();
      window.setTimeout(() => {
        window.location.href = "index.html";
      }, 1200);
    } catch (error) {
      setMessage(message, error.message, "error");
    }
  });
}

function getResetAccessToken() {
  const hashParams = new URLSearchParams(window.location.hash.replace(/^#/, ""));
  const queryParams = new URLSearchParams(window.location.search);
  return hashParams.get("access_token") || queryParams.get("access_token") || "";
}

function setupRegistration() {
  const form = document.getElementById("registerForm");
  const message = document.getElementById("registerMessage");
  const firstName = form.elements.first_name;
  const lastName = form.elements.last_name;
  const fullName = form.elements.full_name;
  const phoneNumber = form.elements.phone_number;
  const gradeSelect = form.elements.grade_level;
  const sectionSelect = form.elements.section;
  const adviserInput = form.elements.adviser;
  let fullNameEdited = false;

  const updateAdviser = () => {
    const adviser = STUDENT_SECTION_ADVISERS[gradeSelect.value]?.[sectionSelect.value] || "";
    adviserInput.value = adviser;
    adviserInput.placeholder = adviser ? "Adviser assigned automatically" : "Select grade level and section first";
  };

  const updateSectionOptions = () => {
    const grade = gradeSelect.value;
    const sections = Object.keys(STUDENT_SECTION_ADVISERS[grade] || {});
    sectionSelect.innerHTML = "";

    const placeholder = document.createElement("option");
    placeholder.value = "";
    placeholder.textContent = grade ? "Select section" : "Select grade level first";
    sectionSelect.appendChild(placeholder);

    sections.forEach((section) => {
      const option = document.createElement("option");
      option.value = section;
      option.textContent = section;
      sectionSelect.appendChild(option);
    });

    sectionSelect.disabled = sections.length === 0;
    updateAdviser();
  };

  const showAdviserMessage = () => {
    if (!gradeSelect.value || !sectionSelect.value) {
      setMessage(message, "Select grade level and section first to fill the adviser name.", "error");
      return;
    }
    setMessage(message, "Adviser is automatically assigned from the selected grade level and section.");
  };

  phoneNumber?.addEventListener("input", () => {
    phoneNumber.value = phoneNumber.value.replace(/\D/g, "").slice(0, 11);
  });

  gradeSelect.addEventListener("change", updateSectionOptions);
  sectionSelect.addEventListener("change", updateAdviser);
  adviserInput.addEventListener("focus", showAdviserMessage);
  adviserInput.addEventListener("mousedown", showAdviserMessage);
  updateSectionOptions();

  fullName.addEventListener("input", () => {
    fullNameEdited = true;
  });

  [firstName, lastName].forEach((input) => {
    input.addEventListener("input", () => {
      if (fullNameEdited) return;
      fullName.value = `${firstName.value} ${lastName.value}`.trim();
    });
  });

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    setMessage(message, "Creating your account...");

    const payload = Object.fromEntries(new FormData(form).entries());
    payload.adviser = STUDENT_SECTION_ADVISERS[payload.grade_level]?.[payload.section] || payload.adviser || "";
    adviserInput.value = payload.adviser;

    const validationError = validateRegistrationPayload(payload);
    if (validationError) {
      setMessage(message, validationError, "error");
      return;
    }

    delete payload.confirm_password;
    delete payload.terms;

    try {
      await apiPost("/api/register-student", payload);
      form.reset();
      fullNameEdited = false;
      updateSectionOptions();
      setMessage(message, "Account created. Please wait for your teacher to approve it before logging in.", "success");
    } catch (error) {
      setMessage(message, error.message, "error");
    }
  });
}

async function setupTeacherDashboard() {
  const message = document.getElementById("teacherMessage");

  try {
    const me = await requireSession();
    if (me.profile.role !== "teacher") {
      window.location.href = "student-dashboard.html";
      return;
    }
    const teacherDisplayName = me.profile.full_name || me.profile.username || "ICT Teacher";
    document.getElementById("teacherName").textContent = teacherDisplayName;
    document.getElementById("teacherWelcomeName").textContent = teacherDisplayName.split(/\s+/)[0] || teacherDisplayName;
    setupMobileNavigation();
    setupTeacherSidebarCollapse();
    setupTeacherNavigation();
    setupTeacherForms();
    setupTeacherNotifications();
    await loadTeacherData();
    startTeacherNotificationPolling();
  } catch (error) {
    setMessage(message, error.message, "error");
    redirectToLoginSoon();
  }
}

async function setupStudentDashboard() {
  const message = document.getElementById("studentMessage");

  try {
    const me = await requireSession();
    if (me.profile.role === "teacher") {
      window.location.href = "teacher-dashboard.html";
      return;
    }
    if (me.profile.status !== "approved") {
      clearSession();
      window.location.href = "index.html";
      return;
    }
    setupMobileNavigation();
    setupStudentNavigation();
    setupStudentNotifications();
    document.querySelector('.student-section[data-view="home"]')?.addEventListener("click", handleStudentHomeAction);
    document.getElementById("studentLessons")?.addEventListener("click", handleStudentLessonAction);
    document.getElementById("studentAssessmentsDashboard")?.addEventListener("click", handleStudentAssessmentAction);
    document.getElementById("studentProgressDashboard")?.addEventListener("click", handleStudentLessonAction);
    document.getElementById("studentAchievementsDashboard")?.addEventListener("click", handleStudentAchievementAction);
    document.getElementById("studentEvaluationSurvey")?.addEventListener("click", handleStudentAssessmentAction);
    document.getElementById("studentEvaluationSurvey")?.addEventListener("submit", submitStudentEvaluationSurvey);
    document.getElementById("studentLessonPlayer")?.addEventListener("click", handleLessonPlayerAction);
    document.getElementById("studentProfileForm")?.addEventListener("submit", submitStudentProfileForm);
    document.getElementById("studentAvatarInput")?.addEventListener("change", handleStudentAvatarSelection);
    document.getElementById("uploadStudentAvatar")?.addEventListener("click", uploadStudentAvatar);
    document.getElementById("removeStudentAvatar")?.addEventListener("click", removeStudentAvatar);
    await loadStudentDashboard();
    await loadStudentNotifications({ silent: true });
    startStudentNotificationPolling();
  } catch (error) {
    setMessage(message, error.message || "Please log in first.", "error");
    redirectToLoginSoon(900);
  }
}

function setupTeacherNavigation() {
  document.querySelectorAll("[data-teacher-view]").forEach((button) => {
    button.addEventListener("click", () => showTeacherView(button.dataset.teacherView, button));
  });
  document.querySelectorAll("[data-jump-view]").forEach((button) => {
    button.addEventListener("click", () => showTeacherView(button.dataset.jumpView));
  });
  document.querySelector("[data-teacher-view].active")?.setAttribute("aria-current", "page");
  updateTeacherBreadcrumb(document.querySelector(".teacher-section.active")?.dataset.view || "overview");
  setupTeacherNavSearch();
}

function setupTeacherSidebarCollapse() {
  const toggle = document.getElementById("teacherSidebarToggle");
  const sidebar = document.getElementById("teacherSidebar");
  const search = document.querySelector(".sidebar-search");
  const searchInput = document.getElementById("teacherNavSearch");
  if (!toggle || !sidebar || toggle.dataset.ready === "true") return;
  toggle.dataset.ready = "true";

  sidebar.querySelectorAll("[data-teacher-view]").forEach((item) => {
    const label = item.querySelector("span")?.textContent.trim() || "Teacher page";
    item.setAttribute("aria-label", label);
    item.setAttribute("title", label);
  });

  let collapsed = false;
  try {
    collapsed = window.localStorage.getItem(TEACHER_SIDEBAR_STORAGE_KEY) === "true";
  } catch (error) {
    collapsed = false;
  }
  setTeacherSidebarCollapsed(collapsed, { persist: false });

  toggle.addEventListener("click", () => {
    setTeacherSidebarCollapsed(!document.body.classList.contains("teacher-sidebar-collapsed"));
  });

  search?.addEventListener("click", () => {
    if (!document.body.classList.contains("teacher-sidebar-collapsed")) return;
    setTeacherSidebarCollapsed(false);
    window.requestAnimationFrame(() => searchInput?.focus());
  });
}

function setTeacherSidebarCollapsed(collapsed, { persist = true } = {}) {
  const toggle = document.getElementById("teacherSidebarToggle");
  const sidebar = document.getElementById("teacherSidebar");
  const sidebarScrollTop = sidebar?.scrollTop || 0;
  document.body.classList.toggle("teacher-sidebar-collapsed", Boolean(collapsed));
  if (sidebar) {
    sidebar.scrollTop = sidebarScrollTop;
    window.requestAnimationFrame(() => {
      sidebar.scrollTop = sidebarScrollTop;
    });
  }
  if (toggle) {
    toggle.setAttribute("aria-expanded", collapsed ? "false" : "true");
    toggle.setAttribute("aria-label", collapsed ? "Expand navigation" : "Collapse navigation");
    toggle.setAttribute("title", collapsed ? "Expand navigation" : "Collapse navigation");
    if (!toggle.querySelector(".sidebar-toggle-glyph")) {
      toggle.innerHTML = `<span class="sidebar-toggle-glyph" aria-hidden="true"></span>`;
    }
  }
  if (persist) {
    try {
      window.localStorage.setItem(TEACHER_SIDEBAR_STORAGE_KEY, collapsed ? "true" : "false");
    } catch (error) {
      // The sidebar still works when browser storage is unavailable.
    }
  }
}

function setupTeacherNavSearch() {
  const input = document.getElementById("teacherNavSearch");
  if (!input || input.dataset.ready === "true") return;
  input.dataset.ready = "true";
  const items = [...document.querySelectorAll("#teacherSidebar [data-teacher-view]")];
  const filterNavigation = () => {
    const query = input.value.trim().toLowerCase();
    items.forEach((item) => {
      item.hidden = Boolean(query) && !item.textContent.toLowerCase().includes(query);
    });
  };
  input.addEventListener("input", filterNavigation);
  input.addEventListener("keydown", (event) => {
    if (event.key !== "Escape") return;
    input.value = "";
    filterNavigation();
    input.blur();
  });
  document.addEventListener("keydown", (event) => {
    if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== "k") return;
    event.preventDefault();
    setTeacherSidebarCollapsed(false);
    input.focus();
    input.select();
  });
}

function setupMobileNavigation() {
  const toggle = document.querySelector(".mobile-nav-toggle");
  const sidebar = document.querySelector(".app-sidebar");
  const backdrop = document.querySelector(".mobile-nav-backdrop");
  if (!toggle || !sidebar || toggle.dataset.mobileNavReady === "true") return;
  toggle.dataset.mobileNavReady = "true";

  toggle.addEventListener("click", (event) => {
    event.stopPropagation();
    setMobileNavigationOpen(!document.body.classList.contains("mobile-nav-open"));
  });
  backdrop?.addEventListener("click", () => setMobileNavigationOpen(false));
  sidebar.addEventListener("click", (event) => event.stopPropagation());
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") setMobileNavigationOpen(false);
  });
  window.addEventListener("resize", () => {
    if (window.innerWidth > 840) setMobileNavigationOpen(false);
  });
  setMobileNavigationOpen(false);
}

function setMobileNavigationOpen(isOpen) {
  const toggle = document.querySelector(".mobile-nav-toggle");
  const sidebar = document.querySelector(".app-sidebar");
  const open = Boolean(isOpen) && window.innerWidth <= 840;
  const isMobile = window.innerWidth <= 840;
  document.body.classList.toggle("mobile-nav-open", open);
  if (toggle) {
    toggle.setAttribute("aria-expanded", open ? "true" : "false");
    toggle.setAttribute("aria-label", open ? "Close navigation" : "Open navigation");
    const iconName = open ? "x" : "menu";
    if (toggle.dataset.mobileNavIcon !== iconName) {
      toggle.dataset.mobileNavIcon = iconName;
      toggle.innerHTML = `<i data-lucide="${iconName}"></i>`;
      if (window.lucide) window.lucide.createIcons();
    }
  }
  if (sidebar) {
    sidebar.setAttribute("aria-hidden", isMobile && !open ? "true" : "false");
    if (isMobile && !open) sidebar.setAttribute("inert", "");
    else sidebar.removeAttribute("inert");
  }
}

function setupTeacherNotifications() {
  const button = document.getElementById("teacherNotificationButton");
  const panel = document.getElementById("teacherNotificationPanel");
  button?.addEventListener("click", (event) => {
    event.stopPropagation();
    teacherState.notificationPanelOpen = !teacherState.notificationPanelOpen;
    renderTeacherNotifications();
  });
  panel?.addEventListener("click", (event) => {
    event.stopPropagation();
  });
  document.getElementById("teacherNotificationList")?.addEventListener("click", handleNotificationClick);
  document.getElementById("markAllNotificationsRead")?.addEventListener("click", markAllTeacherNotificationsRead);
  document.addEventListener("click", () => {
    if (!teacherState.notificationPanelOpen) return;
    teacherState.notificationPanelOpen = false;
    renderTeacherNotifications();
  });
  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape" || !teacherState.notificationPanelOpen) return;
    teacherState.notificationPanelOpen = false;
    renderTeacherNotifications();
  });
}

function startTeacherNotificationPolling() {
  if (teacherNotificationTimer) window.clearInterval(teacherNotificationTimer);
  teacherNotificationTimer = window.setInterval(() => {
    loadTeacherNotifications({ silent: true });
  }, NOTIFICATION_POLL_MS);
}

function showTeacherView(view, activeButton = null) {
  document.querySelectorAll(".teacher-section").forEach((section) => {
    section.classList.toggle("active", section.dataset.view === view);
  });
  document.querySelectorAll("[data-teacher-view]").forEach((button) => {
    const navView = view === "student-profile" ? "students" : view;
    const isActive = activeButton ? button === activeButton : button.dataset.teacherView === navView;
    button.classList.toggle("active", isActive);
    if (isActive) button.setAttribute("aria-current", "page");
    else button.removeAttribute("aria-current");
  });
  updateTeacherBreadcrumb(view);
  setMobileNavigationOpen(false);
  resetDashboardScroll(".teacher-section.active");
}

function updateTeacherBreadcrumb(view) {
  const meta = TEACHER_VIEW_META[view] || TEACHER_VIEW_META.overview;
  setText("teacherBreadcrumbParent", meta.parent);
  setText("teacherBreadcrumbCurrent", meta.current);
  document.title = `TechWise 360 | ${meta.title}`;
}

function setupStudentNavigation() {
  document.querySelectorAll("[data-student-view]").forEach((button) => {
    button.addEventListener("click", () => showStudentView(button.dataset.studentView, button));
  });
  document.querySelectorAll("[data-jump-student]").forEach((button) => {
    button.addEventListener("click", () => showStudentView(button.dataset.jumpStudent));
  });
}

function setupStudentNotifications() {
  const button = document.getElementById("studentNotificationButton");
  const panel = document.getElementById("studentNotificationPanel");
  button?.addEventListener("click", (event) => {
    event.stopPropagation();
    studentState.notificationPanelOpen = !studentState.notificationPanelOpen;
    renderStudentNotifications();
  });
  panel?.addEventListener("click", (event) => {
    event.stopPropagation();
  });
  document.getElementById("studentNotificationList")?.addEventListener("click", handleStudentNotificationClick);
  document.getElementById("markAllStudentNotificationsRead")?.addEventListener("click", markAllStudentNotificationsRead);
  document.addEventListener("click", () => {
    if (!studentState.notificationPanelOpen) return;
    studentState.notificationPanelOpen = false;
    renderStudentNotifications();
  });
  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape" || !studentState.notificationPanelOpen) return;
    studentState.notificationPanelOpen = false;
    renderStudentNotifications();
  });
}

function startStudentNotificationPolling() {
  if (studentNotificationTimer) window.clearInterval(studentNotificationTimer);
  studentNotificationTimer = window.setInterval(() => {
    loadStudentNotifications({ silent: true });
  }, NOTIFICATION_POLL_MS);
}

function showStudentView(view, activeButton = null) {
  document.querySelectorAll(".student-section").forEach((section) => {
    section.classList.toggle("active", section.dataset.view === view);
  });
  let firstMatchActivated = false;
  document.querySelectorAll("[data-student-view]").forEach((button) => {
    const shouldActivate = activeButton
      ? button === activeButton
      : button.dataset.studentView === view && !firstMatchActivated;
    if (!activeButton && shouldActivate) firstMatchActivated = true;
    button.classList.toggle("active", shouldActivate);
  });
  setMobileNavigationOpen(false);
  resetDashboardScroll(".student-section.active");
}

function resetDashboardScroll(activeSectionSelector) {
  const scrollTop = () => {
    window.scrollTo({ top: 0, left: 0, behavior: "auto" });
    document.scrollingElement?.scrollTo({ top: 0, left: 0, behavior: "auto" });
    document.querySelector(".app-main")?.scrollTo({ top: 0, left: 0, behavior: "auto" });
    document.querySelector(activeSectionSelector)?.scrollTo({ top: 0, left: 0, behavior: "auto" });
  };
  scrollTop();
  window.requestAnimationFrame(scrollTop);
}

function setupTeacherForms() {
  document.getElementById("refreshPending")?.addEventListener("click", loadTeacherData);
  document.getElementById("studentSearch")?.addEventListener("input", () => {
    teacherState.studentPage = 1;
    renderTeacherStudents();
  });
  document.getElementById("studentGradeFilter")?.addEventListener("change", (event) => {
    teacherState.selectedGrade = event.target.value;
    teacherState.selectedSection = "";
    teacherState.studentPage = 1;
    renderTeacherStudents();
  });
  document.getElementById("studentSectionFilter")?.addEventListener("change", (event) => {
    teacherState.selectedSection = event.target.value;
    teacherState.studentPage = 1;
    renderTeacherStudents();
  });
  document.getElementById("studentStatusFilter")?.addEventListener("change", () => {
    teacherState.studentPage = 1;
    renderTeacherStudents();
  });
  document.getElementById("studentSupportFilter")?.addEventListener("change", () => {
    teacherState.studentPage = 1;
    renderTeacherStudents();
  });
  document.getElementById("exportStudents")?.addEventListener("click", exportStudentsCsv);
  document.getElementById("viewAllAttention")?.addEventListener("click", () => {
    document.getElementById("studentStatusFilter").value = "support";
    teacherState.studentPage = 1;
    renderTeacherStudents();
  });
  document.getElementById("contentSearch")?.addEventListener("input", () => {
    teacherState.lessonPage = 1;
    renderLessonLibrary();
  });
  document.getElementById("contentStatusFilter")?.addEventListener("change", () => {
    teacherState.lessonPage = 1;
    renderLessonLibrary();
  });
  document.getElementById("lessonCategoryFilter")?.addEventListener("change", () => {
    teacherState.lessonPage = 1;
    renderLessonLibrary();
  });
  document.getElementById("lessonGradeFilter")?.addEventListener("change", () => {
    teacherState.lessonPage = 1;
    renderLessonLibrary();
  });
  document.getElementById("lessonViewToggle")?.addEventListener("click", () => {
    teacherState.compactLessons = !teacherState.compactLessons;
    renderLessonLibrary();
  });
  document.getElementById("assessmentSearch")?.addEventListener("input", (event) => {
    teacherState.assessmentFilters.search = event.target.value;
    teacherState.assessmentPage = 1;
    renderAssessmentsDashboard();
  });
  document.getElementById("assessmentClassFilter")?.addEventListener("change", (event) => {
    teacherState.assessmentFilters.class = event.target.value;
    teacherState.assessmentPage = 1;
    renderAssessmentsDashboard();
  });
  document.getElementById("assessmentTypeFilter")?.addEventListener("change", (event) => {
    teacherState.assessmentFilters.type = event.target.value;
    teacherState.assessmentPage = 1;
    renderAssessmentsDashboard();
  });
  document.getElementById("assessmentStatusFilter")?.addEventListener("change", (event) => {
    teacherState.assessmentFilters.status = event.target.value;
    teacherState.assessmentPage = 1;
    renderAssessmentsDashboard();
  });
  document.getElementById("assessmentPagination")?.addEventListener("click", handleAssessmentPagination);
  document.getElementById("assessmentsTable")?.addEventListener("click", handleAssessmentAction);
  document.getElementById("assessmentRecentSubmissions")?.addEventListener("click", handleAssessmentAction);
  document.getElementById("viewAllAssessmentSubmissions")?.addEventListener("click", () => {
    teacherState.assessmentFilters.status = "";
    teacherState.assessmentFilters.search = "";
    teacherState.assessmentPage = 1;
    syncAssessmentFilterControls();
    renderAssessmentsDashboard();
  });
  document.getElementById("reportStartDate")?.addEventListener("change", (event) => {
    teacherState.reportFilters.startDate = event.target.value;
    renderReportsDashboard();
  });
  document.getElementById("reportEndDate")?.addEventListener("change", (event) => {
    teacherState.reportFilters.endDate = event.target.value;
    renderReportsDashboard();
  });
  document.getElementById("reportGradeFilter")?.addEventListener("change", (event) => {
    teacherState.reportFilters.grade = event.target.value;
    teacherState.reportFilters.section = "";
    teacherState.completionPage = 1;
    renderReportsDashboard();
  });
  document.getElementById("reportSectionFilter")?.addEventListener("change", (event) => {
    teacherState.reportFilters.section = event.target.value;
    teacherState.completionPage = 1;
    renderReportsDashboard();
  });
  document.getElementById("reportDownloads")?.addEventListener("click", handleReportDownload);
  document.getElementById("reportSupportInsights")?.addEventListener("click", handleReportSupportAction);
  document.getElementById("teacherLeaderboardDashboard")?.addEventListener("change", handleTeacherLeaderboardChange);
  document.getElementById("teacherLeaderboardDashboard")?.addEventListener("click", handleTeacherLeaderboardAction);
  document.getElementById("teacherCompetitionsDashboard")?.addEventListener("click", handleTeacherCompetitionAction);
  document.addEventListener("change", handleCompetitionModalChange);
  document.getElementById("teacherCompletionDashboard")?.addEventListener("change", handleTeacherCompletionChange);
  document.getElementById("teacherCompletionDashboard")?.addEventListener("click", handleTeacherCompletionChange);
  document.getElementById("evidenceClassFilter")?.addEventListener("change", (event) => {
    teacherState.evidenceFilters.classKey = event.target.value;
    renderCapstoneEvidenceDashboard();
  });
  document.querySelector(".capstone-evidence-panel")?.addEventListener("click", handleEvidenceDownload);
  document.getElementById("evaluationGradeTabs")?.addEventListener("click", handleEvaluationGradeTab);
  document.getElementById("evaluationDashboard")?.addEventListener("click", handleEvaluationDashboardClick);
  document.getElementById("evaluationDashboard")?.addEventListener("submit", handleEvaluationSubmit);
  document.querySelector(".evaluation-actions")?.addEventListener("click", handleEvaluationExport);
  document.querySelector(".achievement-management-header")?.addEventListener("click", handleTeacherAchievementsAction);
  document.getElementById("teacherAchievementsDashboard")?.addEventListener("click", handleTeacherAchievementsAction);
  document.getElementById("teacherAchievementsDashboard")?.addEventListener("change", handleTeacherAchievementsFilterChange);
  document.getElementById("teacherProfileSettingsForm")?.addEventListener("submit", submitTeacherProfileSettings);
  document.getElementById("teacherNotificationSettingsForm")?.addEventListener("submit", submitTeacherNotificationSettings);
  document.getElementById("teacherDashboardSettingsForm")?.addEventListener("submit", submitTeacherDashboardSettings);
  document.getElementById("teacherReportSettingsForm")?.addEventListener("submit", submitTeacherReportSettings);
  document.getElementById("teacherPasswordSettingsForm")?.addEventListener("submit", submitTeacherPasswordSettings);
  document.getElementById("openLessonDrawer")?.addEventListener("click", () => openLessonDrawer());
  document.getElementById("openLessonAuthorPage")?.addEventListener("click", () => openLessonDrawer());
  document.getElementById("lessonFileInput")?.addEventListener("change", handleLessonFileSelection);
  document.getElementById("lessonDropZone")?.addEventListener("dragover", handleLessonDragOver);
  document.getElementById("lessonDropZone")?.addEventListener("dragleave", handleLessonDragLeave);
  document.getElementById("lessonDropZone")?.addEventListener("drop", handleLessonFileDrop);
  document.getElementById("lessonPagination")?.addEventListener("click", handleLessonPagination);
  document.getElementById("viewAllUploads")?.addEventListener("click", () => {
    document.getElementById("contentStatusFilter").value = "";
    teacherState.lessonPage = 1;
    renderLessonLibrary();
  });
  document.getElementById("viewCalendar")?.addEventListener("click", openCalendarDrawer);
  document.getElementById("nextCalendarMonth")?.addEventListener("click", () => {
    teacherState.calendarMonth = shiftMonth(teacherState.calendarMonth, 1);
    renderContentCalendar();
  });

  document.getElementById("pendingStudents")?.addEventListener("click", handleStudentAction);
  document.getElementById("studentsTable")?.addEventListener("click", handleStudentAction);
  setupStudentTableKeyboard();
  document.getElementById("studentPagination")?.addEventListener("click", handleStudentPagination);
  document.querySelector(".student-achievement-coverage-card")?.addEventListener("click", handleStudentAchievementCoverageAction);
  document.getElementById("studentFullProfile")?.addEventListener("click", handleStudentFullProfileAction);
  document.getElementById("studentFullProfile")?.addEventListener("change", handleStudentFullProfileChange);
  document.getElementById("quartersList")?.addEventListener("click", handleQuarterAction);
  document.getElementById("lessonsTable")?.addEventListener("click", handleContentAction);

  const quarterForm = document.getElementById("quarterForm");
  quarterForm?.addEventListener("submit", submitQuarterForm);
  document.getElementById("cancelQuarterEdit")?.addEventListener("click", resetQuarterForm);
  quarterForm?.elements.name?.addEventListener("change", () => {
    const termTitles = { T1: "1st Term", T2: "2nd Term", T3: "3rd Term" };
    const titleInput = quarterForm.elements.title;
    if (!quarterForm.elements.id.value && titleInput && termTitles[quarterForm.elements.name.value]) {
      titleInput.value = termTitles[quarterForm.elements.name.value];
    }
  });
  document.getElementById("studentDrawerForm")?.addEventListener("submit", submitStudentDrawerForm);
  document.getElementById("studentDrawerForm")?.addEventListener("click", handleStudentDrawerAction);
  document.getElementById("studentDrawerForm")?.addEventListener("change", handleStudentDrawerChange);
  document.querySelectorAll("[data-close-student-drawer]").forEach((button) => {
    button.addEventListener("click", closeStudentDrawer);
  });
  document.getElementById("lessonForm")?.addEventListener("submit", submitLessonForm);
  document.getElementById("lessonForm")?.addEventListener("click", handleLessonAuthorAction);
  document.querySelectorAll("[data-lesson-tab]").forEach((button) => {
    button.addEventListener("click", () => showLessonAuthorTab(button.dataset.lessonTab));
  });
  document.querySelector("[data-reset-lesson]")?.addEventListener("click", resetLessonForm);
  document.querySelectorAll("[data-close-lesson-drawer]").forEach((button) => {
    button.addEventListener("click", closeLessonDrawer);
  });
  document.querySelectorAll("[data-close-preview]").forEach((button) => {
    button.addEventListener("click", closeLessonPreview);
  });
  document.getElementById("teacherLessonPreview")?.addEventListener("click", handleLessonPlayerAction);
  document.querySelectorAll("[data-close-calendar]").forEach((button) => {
    button.addEventListener("click", closeCalendarDrawer);
  });
  document.querySelectorAll("[data-close-assessment-results]").forEach((button) => {
    button.addEventListener("click", closeAssessmentResultsDrawer);
  });
}

async function loadTeacherData() {
  const message = document.getElementById("teacherMessage");
  setMessage(message, "Loading dashboard data...");

  try {
    const [dashboard, quarters, files, assessments, notifications, settingsResult, evaluation, leaderboard, competitions, badgeManagement, completion] = await Promise.all([
      apiGet("/api/teacher/students"),
      apiGet("/api/teacher/quarters"),
      apiGet("/api/teacher/lesson-files").catch(() => ({ files: [] })),
      apiGet("/api/teacher/assessments").catch(() => ({ assessments: [], recent_submissions: [], topic_scores: [] })),
      apiGet("/api/teacher/notifications").catch(() => ({ notifications: [], unread_count: 0 })),
      apiGet("/api/teacher/settings").catch(() => ({ profile: null, settings: defaultTeacherSettings() })),
      apiGet("/api/teacher/evaluation").catch(() => defaultTeacherEvaluationData()),
      apiGet("/api/teacher/leaderboard").catch(() => ({ rows: [], competitions: [], summary: {} })),
      apiGet("/api/teacher/vr-competitions").catch(() => ({ competitions: [] })),
      apiGet("/api/teacher/badges").catch(() => ({ badges: [], awards: [], students: [] })),
      apiGet("/api/teacher/completion-monitoring").catch(() => ({ students: [], modules: [], lessons: [], progress: [] }))
    ]);
    const settings = normalizeTeacherSettings(settingsResult.settings);
    const applyDefaults = !teacherDefaultsApplied;

    teacherState = {
      students: dashboard.students || [],
      activeQuarter: dashboard.active_quarter || null,
      modules: dashboard.modules || [],
      lessons: dashboard.lessons || [],
      assessments: assessments.assessments || [],
      recentSubmissions: assessments.recent_submissions || [],
      topicScores: assessments.topic_scores || [],
      lessonFiles: files.files || [],
      progress: dashboard.progress || [],
      badges: dashboard.badges || [],
      certificates: dashboard.certificates || [],
      notifications: notifications.notifications || [],
      unreadNotifications: Number(notifications.unread_count || 0),
      notificationPanelOpen: teacherState.notificationPanelOpen || false,
      notificationsLoading: false,
      teacherProfile: settingsResult.profile || teacherState.teacherProfile || null,
      settings,
      quarters: quarters.quarters || [],
      studentPage: teacherState.studentPage || 1,
      lessonPage: teacherState.lessonPage || 1,
      assessmentPage: teacherState.assessmentPage || 1,
      selectedAssessmentId: teacherState.selectedAssessmentId || "",
      assessmentFilters: teacherState.assessmentFilters || { class: "", type: "", status: "", search: "" },
      evidenceFilters: teacherState.evidenceFilters || { classKey: "" },
      evaluation: normalizeTeacherEvaluationData(evaluation),
      evaluationGrade: teacherState.evaluationGrade || "",
      achievementTab: normalizeTeacherAchievementTab(teacherState.achievementTab || "overview"),
      expandedAwardStudentId: teacherState.expandedAwardStudentId || "",
      awardPage: teacherState.awardPage || 1,
      awardFilters: normalizeGradeSectionFilter(teacherState.awardFilters || {}),
      leaderboard: {
        rows: leaderboard.rows || [],
        competitions: leaderboard.competitions || [],
        summary: leaderboard.summary || {}
      },
      leaderboardFilters: teacherState.leaderboardFilters || { competition_id: "", simulation: "" },
      competitions: competitions.competitions || leaderboard.competitions || [],
      badgeDefinitions: badgeManagement.badges || [],
      badgeAwards: badgeManagement.awards || [],
      badgeManager: teacherState.badgeManager || { filter: "" },
      completion: {
        students: completion.students || dashboard.students || [],
        modules: completion.modules || dashboard.modules || [],
        lessons: completion.lessons || dashboard.lessons || [],
        progress: completion.progress || dashboard.progress || []
      },
      completionFilters: teacherState.completionFilters || { grade: "", section: "", status: "" },
      completionPage: teacherState.completionPage || 1,
      reports: teacherState.reports || {},
      reportFilters: applyDefaults ? reportFiltersFromSettings(settings) : normalizeReportFilters(teacherState.reportFilters),
      calendarMonth: teacherState.calendarMonth || new Date().toISOString().slice(0, 7),
      selectedLessonFile: teacherState.selectedLessonFile || null,
      lessonDraftSections: teacherState.lessonDraftSections || [],
      lessonDraftQuestions: teacherState.lessonDraftQuestions || [],
      compactLessons: applyDefaults ? Boolean(settings.compact_lessons) : Boolean(teacherState.compactLessons),
      previewLayout: teacherState.previewLayout || null,
      selectedGrade: teacherState.selectedGrade || "",
      selectedSection: teacherState.selectedSection || "",
      selectedStudentId: teacherState.selectedStudentId || ""
    };

    renderTeacherDashboard();
    if (applyDefaults) {
      teacherDefaultsApplied = true;
      showTeacherView(settings.default_landing_view || "overview");
    }
    setMessage(message, "", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

async function loadTeacherNotifications({ silent = true } = {}) {
  const message = document.getElementById("teacherMessage");
  try {
    teacherState.notificationsLoading = !silent;
    if (!silent) renderTeacherNotifications();
    const data = await apiGet("/api/teacher/notifications");
    teacherState.notifications = data.notifications || [];
    teacherState.unreadNotifications = Number(data.unread_count || 0);
    teacherState.notificationsLoading = false;
    renderTeacherNotifications();
  } catch (error) {
    teacherState.notificationsLoading = false;
    renderTeacherNotifications();
    if (!silent) setMessage(message, error.message, "error");
  }
}

function renderTeacherDashboard() {
  renderTeacherNotifications();
  renderTeacherMetrics();
  renderPendingStudents();
  renderCapstoneEvidenceDashboard();
  renderTeacherStudents();
  renderQuarters();
  renderContentSelects();
  renderLessonMetrics();
  renderLessonLibrary();
  renderRecentUploads();
  renderContentCalendar();
  renderAssessmentsDashboard();
  renderReportsDashboard();
  renderTeacherLeaderboardDashboard();
  renderTeacherCompetitionsDashboard();
  renderTeacherCompletionDashboard();
  renderEvaluationDashboard();
  renderTeacherAchievementsDashboard();
  renderSettingsDashboard();
  if (window.lucide) window.lucide.createIcons();
}

function renderTeacherLeaderboardDashboard() {
  const container = document.getElementById("teacherLeaderboardDashboard");
  if (!container) return;
  const data = teacherState.leaderboard || { rows: [], summary: {} };
  const rows = data.rows || [];
  const summary = data.summary || {};
  const competitions = teacherState.competitions?.length ? teacherState.competitions : data.competitions || [];
  const filters = teacherState.leaderboardFilters || { competition_id: "", simulation: "" };
  container.innerHTML = `
    <div class="dashboard-page-header teacher-leaderboard-header">
      <div>
        <span class="home-eyebrow"><i data-lucide="bar-chart-3"></i> VR Performance</span>
        <h1>VR Leaderboard</h1>
        <p>Review assembly and disassembly results by score, completion time, and recorded mistakes.</p>
      </div>
      <button class="primary-button compact-action" type="button" data-open-competition-modal><i data-lucide="plus"></i><span>Create VR Activity</span></button>
    </div>
    <div class="metric-grid leaderboard-metrics teacher-leaderboard-metrics">
      ${renderLeaderboardMetricCard("Highest Score", `${formatProgressNumber(summary.top_score || 0)}%`, "Best recorded attempt", "bar-chart-3", "green")}
      ${renderLeaderboardMetricCard("Shortest Time", formatSeconds(summary.fastest_time || 0), "From recorded attempts", "timer", "blue")}
      ${renderLeaderboardMetricCard("Active VR Activities", summary.active_competitions || 0, "Currently open", "clipboard-check", "purple")}
      ${renderLeaderboardMetricCard("Students Recorded", summary.participants_ranked || 0, "Students with attempts", "users", "orange")}
    </div>
    <section class="panel-card leaderboard-filter-panel">
      <div class="leaderboard-toolbar">
        <label><span>VR Activity</span><select data-leaderboard-filter="competition_id">
          <option value="">All activities</option>
          ${competitions.map((competition) => `<option value="${escapeAttribute(competition.id)}" ${filters.competition_id === competition.id ? "selected" : ""}>${escapeHtml(competition.title)}</option>`).join("")}
        </select></label>
        <label><span>Simulation</span><select data-leaderboard-filter="simulation">
          <option value="">All simulations</option><option value="assembly" ${filters.simulation === "assembly" ? "selected" : ""}>Assembly</option><option value="disassembly" ${filters.simulation === "disassembly" ? "selected" : ""}>Disassembly</option>
        </select></label>
        <button class="small-button" type="button" data-refresh-leaderboard><i data-lucide="refresh-cw"></i><span>Refresh</span></button>
      </div>
      <div class="leaderboard-table-wrap">
        <table class="leaderboard-table">
          <thead><tr><th>Rank</th><th>Student</th><th>Simulation</th><th>Score</th><th>Time</th><th>Mistakes</th><th>Attempts</th><th>Last Attempt</th><th>Status</th></tr></thead>
          <tbody>
            ${rows.length ? rows.map(renderTeacherLeaderboardRow).join("") : `<tr><td colspan="9" class="empty-table-cell leaderboard-empty-cell"><strong>No VR attempts recorded yet.</strong><span>The VR leaderboard is ready. Student assembly or disassembly attempts will appear here after they are submitted.</span></td></tr>`}
          </tbody>
        </table>
      </div>
    </section>
  `;
}

function renderLeaderboardMetricCard(label, value, detail, icon, color) {
  return `
    <article class="stat-card leaderboard-metric-card ${escapeAttribute(color)}">
      <span class="stat-icon ${escapeAttribute(color)}"><i data-lucide="${escapeAttribute(icon)}"></i></span>
      <div><p>${escapeHtml(label)}</p><strong>${escapeHtml(value)}</strong><small>${escapeHtml(detail)}</small></div>
    </article>
  `;
}

function renderTeacherLeaderboardRow(row) {
  const student = row.student || {};
  const attempt = row.attempt || {};
  const rank = Number(row.rank || 0);
  const score = Math.max(0, Math.min(100, Number(attempt.score_percent || 0)));
  const status = String(attempt.status || "completed").toLowerCase();
  const statusClass = status === "disqualified" ? "rejected" : ["completed", "qualified"].includes(status) ? "published" : "draft";
  const studentName = student.full_name || student.username || "Student";
  const simulation = titleCase(attempt.simulation_type || "VR");
  return `
    <tr>
      <td><span class="rank-badge rank-${Math.min(rank, 3)}">${rank <= 3 && rank > 0 ? `<i data-lucide="medal"></i>` : ""}<b>${escapeHtml(rank || "-")}</b></span></td>
      <td>
        <div class="leaderboard-student-cell">
          <span class="avatar-initials">${escapeHtml(getInitials(studentName))}</span>
          <div><strong>${escapeHtml(studentName)}</strong><small>${escapeHtml([student.grade_level, student.section].filter(Boolean).join(" - ") || "No class")}</small></div>
        </div>
      </td>
      <td><span class="leaderboard-simulation">${escapeHtml(simulation)}${row.competition?.title ? `<small>${escapeHtml(row.competition.title)}</small>` : ""}</span></td>
      <td>
        <div class="leaderboard-score-cell">
          <strong>${formatProgressNumber(score)}%</strong>
          <div class="leaderboard-score-bar" aria-hidden="true"><span style="width:${score}%"></span></div>
        </div>
      </td>
      <td>${formatSeconds(attempt.duration_seconds || 0)}</td>
      <td>${Number(attempt.mistakes || 0)}</td>
      <td>${Number(row.attempts_count || 1)}</td>
      <td>${escapeHtml(formatDate(attempt.completed_at))}</td>
      <td><span class="status-pill ${statusClass}">${escapeHtml(titleCase(status || "completed"))}</span></td>
    </tr>
  `;
}

function renderTeacherCompetitionsDashboard() {
  const container = document.getElementById("teacherCompetitionsDashboard");
  if (!container) return;
  const competitions = teacherState.competitions || [];
  const activeCount = competitions.filter((competition) => competition.status === "active").length;
  container.innerHTML = `
    <section class="panel-card competition-management-panel">
      <div class="panel-title-row">
        <div>
          <span class="home-eyebrow"><i data-lucide="clipboard-check"></i> VR Activity Setup</span>
          <h2>VR Activities</h2>
          <p>Manage VR activity windows for assembly and disassembly attempts.</p>
        </div>
        <span class="evidence-chip">${activeCount} active</span>
      </div>
      <div class="competition-grid">
        ${competitions.length ? competitions.map(renderCompetitionCard).join("") : `<section class="empty-state-card compact-empty"><h2>No VR activities yet</h2><p>Use Create VR Activity above before students submit VR scores.</p></section>`}
      </div>
    </section>
  `;
}

function renderCompetitionCard(competition) {
  return `
    <article class="panel-card competition-card">
      <div class="panel-title-row">
        <div>
          <h2>${escapeHtml(competition.title)}</h2>
          <p>${escapeHtml(competition.description || "VR score activity")}</p>
        </div>
        <span class="status-pill ${competition.status === "active" ? "published" : "draft"}">${escapeHtml(titleCase(competition.status))}</span>
      </div>
      <dl class="compact-definition-list">
        <div><dt>Simulation</dt><dd>${escapeHtml(titleCase(competition.simulation_type))}</dd></div>
        <div><dt>Class</dt><dd>${escapeHtml([competition.grade_level, competition.section].filter(Boolean).join(" - ") || "All students")}</dd></div>
        <div><dt>Attempts</dt><dd>${competition.attempts_allowed || "Unlimited"}</dd></div>
        <div><dt>Window</dt><dd>${escapeHtml(formatCompetitionWindow(competition))}</dd></div>
      </dl>
      <div class="card-action-row">
        <button class="small-button" type="button" data-edit-competition="${escapeAttribute(competition.id)}"><i data-lucide="pencil"></i><span>Edit</span></button>
        ${competition.status === "active"
          ? `<button class="small-button warning-action" type="button" data-competition-status="${escapeAttribute(competition.id)}" data-status="closed"><i data-lucide="pause-circle"></i><span>Close</span></button>`
          : `<button class="small-button" type="button" data-competition-status="${escapeAttribute(competition.id)}" data-status="active"><i data-lucide="play-circle"></i><span>Activate</span></button>`}
      </div>
    </article>
  `;
}

function renderBadgeDefinitionCard(badge) {
  const awardCount = getTeacherBadgeAwards().filter((award) => award.badge_id === badge.id || award.badge_key === badge.badge_key || award.title === badge.title).length;
  return `
    <article class="panel-card badge-definition-card badge-color-${escapeAttribute(badge.color || "blue")}">
      <div class="panel-title-row">
        <span class="badge-definition-icon"><i data-lucide="${escapeAttribute(badge.icon || "award")}"></i></span>
        <span class="status-pill ${badge.status === "active" ? "published" : "draft"}">${escapeHtml(titleCase(badge.status))}</span>
      </div>
      <h2>${escapeHtml(formatAwardDisplayTitle(badge.title, "Badge"))}</h2>
      <p>${escapeHtml(badge.description || "No description yet.")}</p>
      <div class="badge-definition-meta"><span>${escapeHtml(formatBadgeCategoryLabel(badge.category))}</span><span>${awardCount} awarded</span></div>
      <div class="card-action-row">
        <button class="small-button" type="button" data-edit-badge="${escapeAttribute(badge.id)}"><i data-lucide="pencil"></i><span>Edit</span></button>
        <button class="small-button" type="button" data-open-award-badge="${escapeAttribute(badge.id)}"><i data-lucide="send"></i><span>Award</span></button>
      </div>
    </article>
  `;
}

function renderTeacherCompletionDashboard() {
  const container = document.getElementById("teacherCompletionDashboard");
  if (!container) return;
  const data = teacherState.completion || {};
  const students = (data.students || teacherState.students || []).filter((student) => student.role === "student" && student.status === "approved");
  const lessons = data.lessons || teacherState.lessons || [];
  const progress = data.progress || teacherState.progress || [];
  const reportFilters = normalizeReportFilters(teacherState.reportFilters || {});
  const savedFilters = teacherState.completionFilters || { grade: "", section: "", status: "" };
  const grade = reportFilters.grade || "";
  const gradeStudents = students.filter((student) => !grade || student.grade_level === grade);
  const sectionOptions = sectionOptionsForGrade(grade);
  const section = grade && sectionOptions.includes(savedFilters.section) ? savedFilters.section : "";
  const filters = {
    ...savedFilters,
    grade,
    section,
    status: savedFilters.status || ""
  };
  teacherState.completionFilters = filters;
  const classRows = gradeStudents.map((student) => buildCompletionRow(student, lessons, progress))
    .filter((row) => !filters.section || row.student.section === filters.section);
  const rows = classRows
    .filter((row) => !filters.status || row.status === filters.status);
  const pageCount = Math.max(1, Math.ceil(rows.length / STUDENTS_PER_PAGE));
  teacherState.completionPage = Math.min(Math.max(teacherState.completionPage || 1, 1), pageCount);
  const start = (teacherState.completionPage - 1) * STUDENTS_PER_PAGE;
  const visibleRows = rows.slice(start, start + STUDENTS_PER_PAGE);
  const completeCount = classRows.filter((row) => row.status === "complete").length;
  const inProgressCount = classRows.filter((row) => row.status === "in_progress").length;
  const notStartedCount = classRows.filter((row) => row.status === "not_started").length;
  const overdueCount = classRows.filter((row) => row.status === "overdue").length;
  const averageProgress = classRows.length ? Math.round(classRows.reduce((sum, row) => sum + row.percent, 0) / classRows.length) : 0;
  const classLabel = [grade || "All Classes", filters.section].filter(Boolean).join(" - ");
  container.innerHTML = `
    <div class="dashboard-page-header">
      <div>
        <span class="home-eyebrow"><i data-lucide="list-checks"></i> Completion Monitoring</span>
        <h1>Class Completion Monitor</h1>
        <p>Monitor lesson completion, in-progress students, and learners who have not started.</p>
      </div>
      <span class="time-pill">${escapeHtml(classLabel)}</span>
    </div>
    <div class="completion-overview-row">
      <article class="completion-overview-card"><span class="stat-icon blue"><i data-lucide="users"></i></span><div><p>Students</p><strong>${classRows.length}</strong><small>Current class filter</small></div></article>
      <article class="completion-overview-card"><span class="stat-icon green"><i data-lucide="check-circle-2"></i></span><div><p>Completed</p><strong>${completeCount}</strong><small>All published lessons</small></div></article>
      <article class="completion-overview-card"><span class="stat-icon orange"><i data-lucide="clock"></i></span><div><p>Average Progress</p><strong>${averageProgress}%</strong><small>Across filtered students</small></div></article>
    </div>
    <div class="completion-breakdown-row" aria-label="Completion breakdown">
      ${renderCompletionBreakdownItem("Completed", completeCount, classRows.length, "green")}
      ${renderCompletionBreakdownItem("In Progress", inProgressCount, classRows.length, "blue")}
      ${renderCompletionBreakdownItem("Not Started", notStartedCount, classRows.length, "orange")}
      ${renderCompletionBreakdownItem("Overdue", overdueCount, classRows.length, "red")}
    </div>
    <section class="panel-card report-completion-card">
      <div class="leaderboard-toolbar">
        <div class="completion-filter-context">
          <strong>${escapeHtml(classLabel)}</strong>
          <span>Grade is controlled by the Reports class filter.</span>
        </div>
        <label><span>Section</span><select data-completion-filter="section" ${grade ? "" : "disabled"}>${sectionOptionsHtml(grade, filters.section, true)}</select></label>
        <label><span>Status</span><select data-completion-filter="status"><option value="">All statuses</option><option value="complete" ${filters.status === "complete" ? "selected" : ""}>Complete</option><option value="in_progress" ${filters.status === "in_progress" ? "selected" : ""}>In Progress</option><option value="not_started" ${filters.status === "not_started" ? "selected" : ""}>Not Started</option><option value="overdue" ${filters.status === "overdue" ? "selected" : ""}>Overdue</option></select></label>
      </div>
      <div class="completion-list">
        ${visibleRows.length ? visibleRows.map(renderCompletionRow).join("") : `<div class="empty-state-card">No students match the current filters.</div>`}
      </div>
      ${renderCompletionPagination(rows.length, start, visibleRows.length, pageCount)}
    </section>
  `;
}

function renderCompletionPagination(total, start, visible, pageCount) {
  if (pageCount <= 1) {
    return `<div class="achievement-list-footer"><span>${total ? `Showing ${total} student${total === 1 ? "" : "s"}` : "No students to show"}</span></div>`;
  }
  const current = teacherState.completionPage || 1;
  return `
    <div class="achievement-list-footer">
      <span>Showing ${start + 1}-${start + visible} of ${total} students</span>
      ${renderDashboardPagination({ currentPage: current, pageCount, pageNumberAttribute: "data-completion-page-number", label: "Completion pages" })}
    </div>
  `;
}

function renderCompletionBreakdownItem(label, count, total, tone) {
  return `
    <article class="completion-breakdown-item ${escapeAttribute(tone)}">
      <span>${escapeHtml(label)}</span>
      <strong>${count}</strong>
      <small>${percentOf(count, total)}%</small>
    </article>
  `;
}

function buildCompletionRow(student, lessons, progressRows) {
  const relevantLessonIds = getRelevantLessonIds(student.grade_level);
  const visibleLessons = lessons.filter((lesson) =>
    lesson.status === "published"
    && (!student.grade_level || relevantLessonIds.has(lesson.id))
  );
  const visibleLessonIds = new Set(visibleLessons.map((lesson) => lesson.id));
  const relevantProgress = progressRows.filter((item) => item.student_id === student.id && visibleLessonIds.has(item.lesson_id));
  const progressByLesson = new Map(relevantProgress.map((item) => [item.lesson_id, item]));
  const completed = visibleLessons.filter((lesson) => {
    const progress = progressByLesson.get(lesson.id);
    return progress?.status === "completed" || Number(progress?.progress_percent || 0) >= 100;
  }).length;
  const started = relevantProgress.filter((item) => Number(item.progress_percent || 0) > 0 || item.status !== "not_started").length;
  const total = visibleLessons.length;
  const percent = total ? Math.round((completed / total) * 100) : 0;
  const today = startOfLocalDay(toDateInputValue(new Date()));
  const completedLessonIds = new Set(visibleLessons
    .filter((lesson) => {
      const progress = progressByLesson.get(lesson.id);
      return progress?.status === "completed" || Number(progress?.progress_percent || 0) >= 100;
    })
    .map((lesson) => lesson.id));
  const hasOverdue = visibleLessons.some((lesson) =>
    lesson.due_date
    && endOfLocalDay(lesson.due_date) < today
    && !completedLessonIds.has(lesson.id)
  );
  return {
    student,
    completed,
    total,
    percent,
    status: percent === 100 ? "complete" : hasOverdue ? "overdue" : started ? "in_progress" : "not_started"
  };
}

function renderCompletionRow(row) {
  const pillClass = row.status === "complete" ? "completed" : row.status;
  return `
    <article class="completion-row">
      <div>
        <strong>${escapeHtml(row.student.full_name || row.student.username || "Student")}</strong>
        <span>${escapeHtml(row.student.grade_level || "")}${row.student.section ? ` - ${escapeHtml(row.student.section)}` : ""}</span>
      </div>
      <div class="completion-bar"><span style="width:${Math.min(100, row.percent)}%"></span></div>
      <strong>${row.percent}%</strong>
      <span class="status-pill ${escapeAttribute(pillClass)}">${escapeHtml(titleCase(row.status))}</span>
      <small>${row.completed}/${row.total} lessons</small>
    </article>
  `;
}

function completionSections(students) {
  return [...new Set(students.map((student) => student.section).filter(Boolean))].sort();
}

function formatCompetitionWindow(competition) {
  if (!competition.start_at && !competition.end_at) return "Open schedule";
  return `${competition.start_at ? formatDateOnly(competition.start_at) : "Now"} - ${competition.end_at ? formatDateOnly(competition.end_at) : "No end"}`;
}

async function handleTeacherLeaderboardChange(event) {
  const select = event.target.closest("[data-leaderboard-filter]");
  if (!select) return;
  teacherState.leaderboardFilters = {
    ...(teacherState.leaderboardFilters || {}),
    [select.dataset.leaderboardFilter]: select.value
  };
  await loadTeacherLeaderboard();
}

async function handleTeacherLeaderboardAction(event) {
  if (event.target.closest("[data-open-competition-modal]")) {
    openCompetitionModal();
    return;
  }
  if (event.target.closest("[data-refresh-leaderboard]")) {
    await loadTeacherLeaderboard();
  }
}

async function loadTeacherLeaderboard() {
  const params = new URLSearchParams();
  const filters = teacherState.leaderboardFilters || {};
  Object.entries(filters).forEach(([key, value]) => {
    if (value) params.set(key, value);
  });
  try {
    const data = await apiGet(`/api/teacher/leaderboard${params.toString() ? `?${params}` : ""}`);
    teacherState.leaderboard = {
      rows: data.rows || [],
      competitions: data.competitions || [],
      summary: data.summary || {}
    };
    renderTeacherLeaderboardDashboard();
    if (window.lucide) window.lucide.createIcons();
  } catch (error) {
    setMessage(document.getElementById("teacherMessage"), error.message, "error");
  }
}

function handleTeacherCompetitionAction(event) {
  const openButton = event.target.closest("[data-open-competition-modal]");
  if (openButton) {
    openCompetitionModal();
    return;
  }
  const editButton = event.target.closest("[data-edit-competition]");
  if (editButton) {
    const competition = teacherState.competitions.find((item) => item.id === editButton.dataset.editCompetition);
    openCompetitionModal(competition);
    return;
  }
  const statusButton = event.target.closest("[data-competition-status]");
  if (statusButton) {
    updateCompetitionStatus(statusButton.dataset.competitionStatus, statusButton.dataset.status);
  }
}

function openCompetitionModal(competition = null) {
  return createFeedbackModal({
    title: competition ? "Edit VR Activity" : "Create VR Activity",
    message: "Set the VR activity details used for class performance records.",
    icon: "clipboard-check",
    modalClass: "management-modal",
    confirmText: competition ? "Save Changes" : "Create VR Activity",
    content: `
      <form class="modal-form-stack management-form" data-competition-form>
        <label><span>Title</span><input name="title" type="text" maxlength="120" required value="${escapeAttribute(competition?.title || "")}" placeholder="Example: PC Assembly Activity"></label>
        <label><span>Description</span><textarea name="description" rows="3" maxlength="500" placeholder="Optional VR activity details">${escapeHtml(competition?.description || "")}</textarea></label>
        <div class="field-grid simple-grid">
          <label><span>Simulation</span><select name="simulation_type"><option value="assembly" ${competition?.simulation_type === "assembly" ? "selected" : ""}>Assembly</option><option value="disassembly" ${competition?.simulation_type === "disassembly" ? "selected" : ""}>Disassembly</option><option value="both" ${competition?.simulation_type === "both" ? "selected" : ""}>Both</option></select></label>
          <label><span>Status</span><select name="status"><option value="draft" ${competition?.status === "draft" ? "selected" : ""}>Draft</option><option value="active" ${!competition || competition.status === "active" ? "selected" : ""}>Active</option><option value="closed" ${competition?.status === "closed" ? "selected" : ""}>Closed</option><option value="archived" ${competition?.status === "archived" ? "selected" : ""}>Archived</option></select></label>
        </div>
        <div class="field-grid simple-grid">
          <label><span>Grade</span><select name="grade_level" data-competition-grade>${gradeOptionsHtml(competition?.grade_level || "", true)}</select></label>
          <label><span>Section</span><select name="section" data-competition-section ${competition?.grade_level ? "" : "disabled"}>${sectionOptionsHtml(competition?.grade_level || "", competition?.section || "", true)}</select></label>
        </div>
        <div class="field-grid simple-grid">
          <label><span>Start</span><input name="start_at" type="datetime-local" value="${escapeAttribute(toDateTimeLocal(competition?.start_at))}"></label>
          <label><span>End</span><input name="end_at" type="datetime-local" value="${escapeAttribute(toDateTimeLocal(competition?.end_at))}"></label>
        </div>
        <label><span>Attempts Allowed</span><input name="attempts_allowed" type="number" min="1" placeholder="Unlimited" value="${escapeAttribute(competition?.attempts_allowed || "")}"><small>Leave blank for unlimited attempts.</small></label>
      </form>
    `,
    onConfirm: async (overlay) => {
      const form = overlay.querySelector("[data-competition-form]");
      const payload = Object.fromEntries(new FormData(form).entries());
      Object.assign(payload, normalizeGradeSectionFilter(payload));
      if (competition?.id) payload.id = competition.id;
      if (!payload.title.trim()) throw new Error("VR activity title is required.");
      const result = competition?.id
        ? await apiPatch("/api/teacher/vr-competitions", payload)
        : await apiPost("/api/teacher/vr-competitions", payload);
      mergeCompetition(result.competition);
      renderTeacherCompetitionsDashboard();
      await loadTeacherLeaderboard();
      if (window.lucide) window.lucide.createIcons();
      showToast({ title: competition ? "VR activity updated" : "VR activity created", type: "success" });
    }
  });
}

function handleCompetitionModalChange(event) {
  if (!event.target.closest("[data-competition-form]")) return;
  const form = event.target.closest("[data-competition-form]");
  if (event.target.matches("[data-competition-grade]")) {
    syncCompetitionSectionSelect(form, "");
    return;
  }
  if (event.target.matches("[data-competition-section]")) {
    syncCompetitionSectionSelect(form);
  }
}

function syncCompetitionSectionSelect(form, selected = "") {
  if (!form?.elements?.grade_level || !form?.elements?.section) return;
  const grade = form.elements.grade_level.value;
  const normalized = normalizeGradeSectionFilter({ grade, section: selected || form.elements.section.value });
  form.elements.section.innerHTML = sectionOptionsHtml(normalized.grade, normalized.section, true);
  form.elements.section.disabled = !normalized.grade;
  form.elements.section.value = normalized.grade ? normalized.section : "";
}

async function updateCompetitionStatus(id, status) {
  try {
    const result = await apiPatch("/api/teacher/vr-competitions", { id, status });
    mergeCompetition(result.competition);
    renderTeacherCompetitionsDashboard();
    await loadTeacherLeaderboard();
    showToast({ title: "VR activity updated", message: result.competition?.title || "", type: "success" });
  } catch (error) {
    setMessage(document.getElementById("teacherMessage"), error.message, "error");
  }
}

function mergeCompetition(competition) {
  if (!competition) return;
  const index = teacherState.competitions.findIndex((item) => item.id === competition.id);
  if (index >= 0) teacherState.competitions[index] = competition;
  else teacherState.competitions.unshift(competition);
}

function handleTeacherBadgeAction(event) {
  const openButton = event.target.closest("[data-open-badge-modal]");
  if (openButton) {
    openBadgeModal();
    return;
  }
  const editButton = event.target.closest("[data-edit-badge]");
  if (editButton) {
    openBadgeModal(teacherState.badgeDefinitions.find((item) => item.id === editButton.dataset.editBadge));
    return;
  }
  const awardButton = event.target.closest("[data-open-award-badge]");
  if (awardButton) {
    const badgeId = awardButton.dataset.openAwardBadge || "";
    openAwardBadgeModal(badgeId);
  }
}

function renderBadgeIconOptions(selectedIcon = "award") {
  const selected = String(selectedIcon || "award").toLowerCase();
  const options = BADGE_ICON_OPTIONS.includes(selected) ? BADGE_ICON_OPTIONS : [selected, ...BADGE_ICON_OPTIONS];
  return options.map((icon) => `<option value="${escapeAttribute(icon)}" ${selected === icon ? "selected" : ""}>${escapeHtml(titleCase(icon.replace(/-/g, " ")))}</option>`).join("");
}

function formatBadgeCategoryLabel(category = "") {
  const labels = {
    achievement: "Achievement",
    completion: "Completion",
    competition: "VR Activity",
    participation: "Participation",
    skill: "Skill",
    custom: "Custom"
  };
  return labels[category] || titleCase(category);
}

function formatAwardDisplayTitle(title = "", fallback = "Award") {
  const value = String(title || "").trim();
  const labels = {
    "assembly ready": "Assembly Procedure Record",
    "hardware star": "Hardware Identification Record",
    "safety checker": "Safety Procedure Record",
    "cable scout": "Cable Identification Record",
    "esd aware": "ESD Safety Record",
    "quick learner": "Lesson Progress Record",
    "hardware hero": "Hardware Procedure Record",
    "practice finisher": "Practice Completion Record",
    "assessment achiever": "Assessment Completion Record"
  };
  return labels[value.toLowerCase()] || value || fallback;
}

function openBadgeModal(badge = null) {
  createFeedbackModal({
    title: badge ? "Edit Badge" : "Create Badge",
    message: "Define a reusable badge that can be awarded to students.",
    icon: "badge-check",
    modalClass: "management-modal",
    confirmText: badge ? "Save Badge" : "Create Badge",
    content: `
      <form class="modal-form-stack management-form" data-badge-form>
        <label><span>Badge Title</span><input name="title" type="text" maxlength="120" required value="${escapeAttribute(badge?.title || "")}" placeholder="Example: Hardware Procedure Record"></label>
        <label><span>Description</span><textarea name="description" rows="3" maxlength="500" placeholder="Describe why this badge is awarded">${escapeHtml(badge?.description || "")}</textarea></label>
        <div class="field-grid simple-grid">
          <label><span>Category</span><select name="category">${["achievement", "completion", "competition", "participation", "skill", "custom"].map((category) => `<option value="${category}" ${badge?.category === category ? "selected" : ""}>${formatBadgeCategoryLabel(category)}</option>`).join("")}</select></label>
          <label><span>Color</span><select name="color">${["blue", "green", "gold", "purple", "teal", "orange", "red"].map((color) => `<option value="${color}" ${badge?.color === color ? "selected" : ""}>${titleCase(color)}</option>`).join("")}</select></label>
        </div>
        <div class="field-grid simple-grid">
          <label><span>Icon</span><select name="icon">${renderBadgeIconOptions(badge?.icon || "award")}</select></label>
          <label><span>Status</span><select name="status"><option value="active" ${badge?.status !== "archived" ? "selected" : ""}>Active</option><option value="archived" ${badge?.status === "archived" ? "selected" : ""}>Archived</option></select></label>
        </div>
      </form>
    `,
    onConfirm: async (overlay) => {
      const form = overlay.querySelector("[data-badge-form]");
      const payload = Object.fromEntries(new FormData(form).entries());
      if (badge?.id) payload.id = badge.id;
      const result = badge?.id ? await apiPatch("/api/teacher/badges", payload) : await apiPost("/api/teacher/badges", payload);
      mergeBadgeDefinition(result.badge);
      renderTeacherAchievementsDashboard();
      if (window.lucide) window.lucide.createIcons();
      showToast({ title: badge ? "Badge updated" : "Badge created", type: "success" });
    }
  });
}

function openAwardBadgeModal(defaultBadgeId = "") {
  const students = teacherState.students.filter((student) => student.role === "student" && student.status === "approved");
  const badges = teacherState.badgeDefinitions.filter((badge) => badge.status === "active");
  if (!badges.length) {
    teacherState.achievementTab = "tools";
    renderTeacherAchievementsDashboard();
    showToast({ title: "Create a badge first", message: "Active badge designs are required before awarding badges.", type: "warning" });
    return;
  }
  if (!students.length) {
    showToast({ title: "No approved students", message: "Approve a student before awarding badges.", type: "warning" });
    return;
  }
  createFeedbackModal({
    title: "Award Badge",
    message: "Select an approved student and an active badge.",
    icon: "send",
    modalClass: "management-modal",
    confirmText: "Award Badge",
    content: `
      <form class="modal-form-stack management-form" data-award-badge-form>
        <label><span>Student</span><select name="student_id" required><option value="">Select student</option>${students.map((student) => `<option value="${escapeAttribute(student.id)}">${escapeHtml(student.full_name || student.username)}${student.grade_level ? ` - ${escapeHtml(student.grade_level)}` : ""}</option>`).join("")}</select></label>
        <label><span>Badge</span><select name="badge_id" required><option value="">Select badge</option>${badges.map((badge) => `<option value="${escapeAttribute(badge.id)}" ${defaultBadgeId === badge.id ? "selected" : ""}>${escapeHtml(formatAwardDisplayTitle(badge.title, "Badge"))}</option>`).join("")}</select></label>
        <label><span>Reason</span><textarea name="reason" rows="3" maxlength="500" placeholder="Optional note for this award"></textarea></label>
      </form>
    `,
    onConfirm: async (overlay) => {
      const form = overlay.querySelector("[data-award-badge-form]");
      const payload = Object.fromEntries(new FormData(form).entries());
      if (!payload.student_id || !payload.badge_id) throw new Error("Student and badge are required.");
      const result = await apiPost("/api/teacher/award-badge", payload);
      const existingIndex = teacherState.badgeAwards.findIndex((award) => award.id === result.badge.id);
      if (existingIndex >= 0) teacherState.badgeAwards[existingIndex] = result.badge;
      else teacherState.badgeAwards.unshift(result.badge);
      renderTeacherAchievementsDashboard();
      if (window.lucide) window.lucide.createIcons();
      showToast({ title: "Badge awarded", type: "success" });
    }
  });
}

function mergeBadgeDefinition(badge) {
  if (!badge) return;
  const index = teacherState.badgeDefinitions.findIndex((item) => item.id === badge.id);
  if (index >= 0) teacherState.badgeDefinitions[index] = badge;
  else teacherState.badgeDefinitions.unshift(badge);
}

function handleTeacherCompletionChange(event) {
  const pageButton = event.target.closest("[data-completion-page-number]");
  if (pageButton) {
    teacherState.completionPage = Number(pageButton.dataset.completionPageNumber) || 1;
    renderTeacherCompletionDashboard();
    if (window.lucide) window.lucide.createIcons();
    return;
  }
  const navButton = event.target.closest("[data-completion-page-nav]");
  if (navButton?.dataset.completionPageNav === "prev") {
    teacherState.completionPage -= 1;
    renderTeacherCompletionDashboard();
    if (window.lucide) window.lucide.createIcons();
    return;
  }
  if (navButton?.dataset.completionPageNav === "next") {
    teacherState.completionPage += 1;
    renderTeacherCompletionDashboard();
    if (window.lucide) window.lucide.createIcons();
    return;
  }

  const select = event.target.closest("[data-completion-filter]");
  if (!select) return;
  teacherState.completionFilters = {
    ...(teacherState.completionFilters || {}),
    [select.dataset.completionFilter]: select.value
  };
  teacherState.completionPage = 1;
  renderTeacherCompletionDashboard();
  if (window.lucide) window.lucide.createIcons();
}

function toDateTimeLocal(value) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 16);
}

function renderTeacherNotifications() {
  const button = document.getElementById("teacherNotificationButton");
  const badge = document.getElementById("teacherNotificationBadge");
  const panel = document.getElementById("teacherNotificationPanel");
  const summary = document.getElementById("teacherNotificationSummary");
  const list = document.getElementById("teacherNotificationList");
  const markAll = document.getElementById("markAllNotificationsRead");
  if (!button || !badge || !panel || !summary || !list) return;

  const unread = Number(teacherState.unreadNotifications || 0);
  button.setAttribute("aria-expanded", teacherState.notificationPanelOpen ? "true" : "false");
  panel.hidden = !teacherState.notificationPanelOpen;
  badge.hidden = unread <= 0;
  badge.textContent = unread > 99 ? "99+" : String(unread);
  summary.textContent = unread ? `${unread} unread update${unread === 1 ? "" : "s"}` : "No unread updates";
  if (markAll) markAll.disabled = unread <= 0;

  if (teacherState.notificationsLoading) {
    list.innerHTML = `<div class="notification-empty"><span>Loading notifications...</span></div>`;
    if (window.lucide) window.lucide.createIcons();
    return;
  }

  const notifications = teacherState.notifications || [];
  if (!notifications.length) {
    list.innerHTML = `<div class="notification-empty"><span>No notifications yet.</span></div>`;
    return;
  }

  list.innerHTML = notifications.map(renderTeacherNotificationItem).join("");
  if (window.lucide) window.lucide.createIcons();
}

function renderTeacherNotificationItem(notification) {
  const unread = !notification.read_at;
  const meta = notification.metadata || {};
  const icon = iconForNotification(notification.event_type);
  const color = colorForNotification(notification.event_type);
  const student = teacherState.students.find((item) => item.id === notification.student_id);
  const studentLabel = student ? `${student.full_name || student.username}${student.grade_level ? ` - ${student.grade_level}` : ""}` : "";
  return `
    <button class="notification-item ${unread ? "unread" : ""}" type="button" data-notification-id="${escapeHtml(notification.id)}">
      <span class="notification-icon ${escapeHtml(color)}"><i data-lucide="${escapeHtml(icon)}"></i></span>
      <span class="notification-content">
        <strong>${escapeHtml(notification.title || "Notification")}</strong>
        <span>${escapeHtml(notification.body || "")}</span>
        ${studentLabel || meta.module_title ? `<span>${escapeHtml([studentLabel, meta.module_title].filter(Boolean).join(" - "))}</span>` : ""}
        <time>${escapeHtml(relativeTime(notification.created_at))}</time>
      </span>
      ${unread ? `<span class="notification-unread-dot" aria-hidden="true"></span>` : ""}
    </button>
  `;
}

async function handleNotificationClick(event) {
  const button = event.target.closest("[data-notification-id]");
  if (!button) return;
  const notification = teacherState.notifications.find((item) => item.id === button.dataset.notificationId);
  if (!notification) return;
  await markTeacherNotificationRead(notification.id);
  routeTeacherNotification(notification);
}

async function markTeacherNotificationRead(notificationId) {
  try {
    const data = await apiPatch("/api/teacher/notifications", { id: notificationId, action: "read" });
    teacherState.notifications = data.notifications || teacherState.notifications;
    teacherState.unreadNotifications = Number(data.unread_count || 0);
    renderTeacherNotifications();
  } catch (error) {
    setMessage(document.getElementById("teacherMessage"), error.message, "error");
  }
}

async function markAllTeacherNotificationsRead() {
  try {
    const data = await apiPatch("/api/teacher/notifications", { action: "read_all" });
    teacherState.notifications = data.notifications || [];
    teacherState.unreadNotifications = Number(data.unread_count || 0);
    renderTeacherNotifications();
  } catch (error) {
    setMessage(document.getElementById("teacherMessage"), error.message, "error");
  }
}

function routeTeacherNotification(notification) {
  teacherState.notificationPanelOpen = false;
  renderTeacherNotifications();

  if (["practice_submitted", "assessment_submitted"].includes(notification.event_type)) {
    showTeacherView("assessments");
    if (notification.entity_id && teacherState.assessments.some((item) => item.id === notification.entity_id)) {
      openAssessmentResultsDrawer(notification.entity_id);
    }
    return;
  }

  showTeacherView("students");
  const student = teacherState.students.find((item) => item.id === notification.student_id);
  if (notification.event_type === "student_registered") {
    const statusFilter = document.getElementById("studentStatusFilter");
    if (statusFilter) statusFilter.value = "pending";
    if (student?.grade_level) teacherState.selectedGrade = student.grade_level;
    teacherState.selectedSection = student?.section || "";
    teacherState.studentPage = 1;
    renderTeacherStudents();
  }
  if (student) openStudentDrawer(student.id);
}

function iconForNotification(eventType) {
  if (eventType === "student_registered") return "user-plus";
  if (eventType === "lesson_completed") return "check-circle-2";
  if (eventType === "practice_submitted" || eventType === "assessment_submitted") return "clipboard-check";
  if (eventType === "certificate_awarded") return "award";
  if (eventType === "badge_awarded") return "shield-check";
  return "bell";
}

function colorForNotification(eventType) {
  if (eventType === "lesson_completed") return "green";
  if (eventType === "practice_submitted" || eventType === "assessment_submitted") return "purple";
  if (eventType === "badge_awarded" || eventType === "certificate_awarded") return "orange";
  return "blue";
}

function getAwardDate(award) {
  return award.awarded_at || award.created_at || award.issue_date || award.updated_at || "";
}

function dedupeAwardRecords(records = []) {
  const seen = new Set();
  return records.filter((record) => {
    const key = record.id || [
      record.student_id || "",
      record.certificate_key || record.badge_key || record.badge_id || record.title || "",
      getAwardDate(record)
    ].join("|");
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function getTeacherBadgeAwards() {
  return dedupeAwardRecords([
    ...(teacherState.badgeAwards || []),
    ...(teacherState.badges || [])
  ]);
}

function normalizeTeacherAchievementTab(tab = "overview") {
  const value = String(tab || "overview");
  if (value === "templates" || value === "badges") return "tools";
  return ["overview", "tools", "students", "history"].includes(value) ? value : "overview";
}

function renderTeacherAchievementsDashboard() {
  const container = document.getElementById("teacherAchievementsDashboard");
  if (!container) return;

  const students = teacherState.students.filter((student) => student.status === "approved");
  const certificates = dedupeAwardRecords(teacherState.certificates || []);
  const badges = getTeacherBadgeAwards();
  const badgeDefinitions = teacherState.badgeDefinitions || [];
  const awardedStudentIds = new Set([...certificates, ...badges].map((item) => item.student_id).filter(Boolean));
  const participation = students.length ? Math.round((awardedStudentIds.size / students.length) * 100) : 0;
  const tab = normalizeTeacherAchievementTab(teacherState.achievementTab || "overview");
  teacherState.achievementTab = tab;
  const recentAwards = [
    ...certificates.map((item) => ({ ...item, award_type: "certificate" })),
    ...badges.map((item) => ({ ...item, award_type: "badge" }))
  ].sort((a, b) => new Date(getAwardDate(b) || 0) - new Date(getAwardDate(a) || 0));

  container.innerHTML = `
    <div class="achievement-summary-grid">
      <article class="stat-card"><span class="stat-icon blue"><i data-lucide="file-badge"></i></span><div><p>Certificates</p><strong>${certificates.length}</strong><small>Issued awards</small></div></article>
      <article class="stat-card"><span class="stat-icon green"><i data-lucide="shield-check"></i></span><div><p>Badges</p><strong>${badges.length}</strong><small>Awarded badges</small></div></article>
      <article class="stat-card"><span class="stat-icon purple"><i data-lucide="layout-template"></i></span><div><p>Templates</p><strong>${CERTIFICATE_TEMPLATES.length}</strong><small>Certificate layout</small></div></article>
      <article class="stat-card"><span class="stat-icon orange"><i data-lucide="users"></i></span><div><p>Participation</p><strong>${participation}%</strong><small>Students with awards</small></div></article>
    </div>
    <div class="achievement-tab-bar" role="tablist" aria-label="Achievement tools">
      ${[
        ["overview", "Overview", "layout-dashboard"],
        ["tools", "Award Tools", "badge-check"],
        ["students", "Student Awards", "users"],
        ["history", "Issuance History", "history"]
      ].map(([key, label, icon]) => `
        <button type="button" class="${tab === key ? "active" : ""}" data-achievement-teacher-tab="${key}">
          <i data-lucide="${icon}"></i><span>${label}</span>
        </button>
      `).join("")}
    </div>
    ${renderTeacherAchievementTab(tab, { students, certificates, badges, badgeDefinitions, recentAwards })}
  `;
  if (window.lucide) window.lucide.createIcons();
}

function renderTeacherAchievementTab(tab, data) {
  if (tab === "tools") return renderAwardToolsTab(data.certificates, data.badgeDefinitions, data.badges);
  if (tab === "students") return renderStudentAwardsTab(data.students, data.certificates, data.badges);
  if (tab === "history") return renderAwardHistoryTab(data.recentAwards);
  return renderAchievementOverviewTab(data);
}

function renderAchievementOverviewTab({ students, certificates, badges, recentAwards }) {
  const latest = recentAwards.slice(0, 5);
  const filters = normalizeGradeSectionFilter(teacherState.awardFilters || {});
  teacherState.awardFilters = filters;
  const awardStudents = filterStudentsByGradeSection(students, filters);
  const awardStudentIds = new Set(awardStudents.map((student) => student.id));
  const visibleCertificates = certificates.filter((item) => awardStudentIds.has(item.student_id));
  const visibleBadges = badges.filter((item) => awardStudentIds.has(item.student_id));
  const awardSummaries = buildStudentAwardSummaries(awardStudents, visibleCertificates, visibleBadges);
  const visibleAwardSummaries = awardSummaries.slice(0, 4);
  const studentsWithCertificates = new Set(visibleCertificates.map((item) => item.student_id).filter(Boolean)).size;
  const studentsWithBadges = new Set(visibleBadges.map((item) => item.student_id).filter(Boolean)).size;
  const studentsWithoutAwards = awardSummaries.filter((summary) => !summary.awards.length).length;
  return `
    <div class="achievement-workspace-grid teacher-achievement-main-grid">
      <section class="panel-card achievement-template-card">
        <div class="panel-title-row">
          <div>
            <h2>Certificate Template</h2>
            <p>Use the uploaded school template for printable student certificates.</p>
          </div>
          <button class="primary-button compact" type="button" data-generate-certificate><i data-lucide="plus"></i><span>Generate</span></button>
        </div>
        ${renderCertificatePreview(buildCertificatePreviewData({ title: "Certificate of Achievement" }), { compact: true })}
      </section>
      <section class="panel-card achievement-awards-card">
        <div class="panel-title-row">
          <h2>Recent Awards</h2>
          <button class="link-button" type="button" data-achievement-teacher-tab="history">View All</button>
        </div>
        ${latest.length ? `
          <div class="achievement-history-list">${latest.map((award) => renderAwardHistoryRow(award)).join("")}</div>
        ` : `
          <div class="empty-progress-state compact-empty"><i data-lucide="award"></i><h2>No awards yet</h2><p>Generate certificates or award badges from a student profile.</p></div>
        `}
      </section>
    </div>
    <section class="panel-card student-awards-table-card overview-student-awards-card">
      <div class="panel-title-row achievement-snapshot-header">
        <div><h2>Student Awards Snapshot</h2><p>Approved students and their current certificates and badges.</p></div>
        <div class="page-action-row">
          <button class="small-button" type="button" data-generate-certificate><i data-lucide="file-badge"></i><span>Issue Certificate</span></button>
          <button class="small-button" type="button" data-jump-view="students"><i data-lucide="users"></i><span>Open Students</span></button>
          <button class="link-button" type="button" data-achievement-teacher-tab="students">View All</button>
        </div>
      </div>
      <div class="award-mini-stats">
        <article><strong>${studentsWithCertificates}</strong><span>With certificates</span></article>
        <article><strong>${studentsWithBadges}</strong><span>With badges</span></article>
        <article><strong>${studentsWithoutAwards}</strong><span>No awards yet</span></article>
      </div>
      ${renderAwardClassFilters(filters, "snapshot")}
      <div class="student-awards-list compact">
        ${renderStudentAwardRows(visibleAwardSummaries, { compact: true, emptyTitle: "No approved students", emptyText: "Approved students will appear here." })}
      </div>
    </section>
  `;
}

function renderAwardToolsTab(certificates, badgeDefinitions, badges) {
  return `
    <div class="achievement-workspace-grid award-tools-grid">
      ${renderCertificateTemplatesTab(certificates)}
      ${renderBadgeDesignsTab(badgeDefinitions, badges)}
    </div>
  `;
}

function renderCertificateTemplatesTab(certificates) {
  return `
    <section class="panel-card certificate-template-library">
      <div class="panel-title-row">
        <div><h2>Certificate Templates</h2><p>Templates use real certificate records and print with the uploaded design.</p></div>
        <span class="evidence-chip">${certificates.length} issued</span>
      </div>
      <div class="certificate-template-grid">
        ${CERTIFICATE_TEMPLATES.map((template) => `
          <article class="certificate-template-option">
            ${renderCertificatePreview(buildCertificatePreviewData({ template_id: template.id, title: template.name }), { compact: true })}
            <div>
              <strong>${escapeHtml(template.name)}</strong>
              <p>${escapeHtml(template.description)}</p>
              <button class="primary-button compact" type="button" data-generate-certificate data-template-id="${escapeAttribute(template.id)}"><i data-lucide="plus"></i><span>Generate Certificate</span></button>
            </div>
          </article>
        `).join("")}
      </div>
    </section>
  `;
}

function renderBadgeDesignsTab(badgeDefinitions, badges) {
  const definitions = badgeDefinitions || [];
  const awardedDesigns = buildAwardedBadgeDesigns(definitions, badges);
  const designCards = definitions.length
    ? definitions.map(renderBadgeDefinitionCard).join("")
    : awardedDesigns.map(renderAwardedBadgeDesignCard).join("");
  return `
    <section class="panel-card badge-design-library">
      <div class="panel-title-row">
        <div><h2>Badge Designs</h2><p>Create reusable badge designs, then award them to approved students.</p></div>
        <div class="page-action-row">
          <button class="small-button" type="button" data-open-award-badge><i data-lucide="send"></i><span>Award Badge</span></button>
          <button class="primary-button compact-action" type="button" data-open-badge-modal><i data-lucide="plus"></i><span>Create Badge</span></button>
        </div>
      </div>
      <div class="badge-design-grid">
        ${designCards || `<div class="empty-progress-state compact-empty"><i data-lucide="badge-check"></i><h2>No badge designs yet</h2><p>Create your first reusable badge from the action above, then award it to students from this tab.</p></div>`}
      </div>
    </section>
  `;
}

function buildAwardedBadgeDesigns(definitions, badges) {
  const definitionKeys = new Set((definitions || []).flatMap((badge) => [badge.id, badge.badge_key, badge.title].filter(Boolean).map((value) => String(value).toLowerCase())));
  const designs = new Map();
  (badges || []).forEach((badge) => {
    const key = String(badge.badge_id || badge.badge_key || badge.title || "").toLowerCase();
    if (!key || definitionKeys.has(key)) return;
    const existing = designs.get(key) || {
      title: formatAwardDisplayTitle(badge.title, "Badge"),
      category: badge.category || "achievement",
      color: badge.color || "blue",
      icon: badge.icon || "award",
      count: 0
    };
    existing.count += 1;
    designs.set(key, existing);
  });
  return [...designs.values()];
}

function renderAwardedBadgeDesignCard(badge) {
  return `
    <article class="badge-definition-card badge-color-${escapeAttribute(badge.color || "blue")} legacy-badge-design">
      <div class="panel-title-row">
        <span class="badge-definition-icon"><i data-lucide="${escapeAttribute(badge.icon || "award")}"></i></span>
        <span class="status-pill published">Awarded</span>
      </div>
      <h2>${escapeHtml(formatAwardDisplayTitle(badge.title, "Badge"))}</h2>
      <p>Badge title found in awarded student records.</p>
      <div class="badge-definition-meta"><span>${escapeHtml(formatBadgeCategoryLabel(badge.category || "achievement"))}</span><span>${badge.count} awarded</span></div>
    </article>
  `;
}

function renderStudentAwardsTab(students, certificates, badges) {
  const filters = normalizeGradeSectionFilter(teacherState.awardFilters || {});
  teacherState.awardFilters = filters;
  const awardSummaries = buildStudentAwardSummaries(filterStudentsByGradeSection(students, filters), certificates, badges);
  const pageCount = Math.max(1, Math.ceil(awardSummaries.length / AWARDS_PER_PAGE));
  teacherState.awardPage = Math.min(Math.max(teacherState.awardPage || 1, 1), pageCount);
  const start = (teacherState.awardPage - 1) * AWARDS_PER_PAGE;
  const visibleSummaries = awardSummaries.slice(start, start + AWARDS_PER_PAGE);
  return `
    <section class="panel-card student-awards-table-card">
      <div class="panel-title-row">
        <div><h2>Student Awards</h2><p>Approved students and their current certificate/badge totals.</p></div>
        <button class="primary-button compact" type="button" data-generate-certificate><i data-lucide="file-badge"></i><span>Generate</span></button>
      </div>
      ${renderAwardClassFilters(filters, "students")}
      <div class="student-awards-list">
        ${renderStudentAwardRows(visibleSummaries, { emptyTitle: "No approved students", emptyText: "Approved students will appear here." })}
      </div>
      ${renderAwardPagination(awardSummaries.length, start, visibleSummaries.length, pageCount)}
    </section>
  `;
}

function renderAwardClassFilters(filters, scope) {
  const normalized = normalizeGradeSectionFilter(filters || {});
  return `
    <div class="award-filter-row" data-award-filter-scope="${escapeAttribute(scope)}">
      <label><span>Grade</span><select data-award-filter="grade">${gradeOptionsHtml(normalized.grade, true)}</select></label>
      <label><span>Section</span><select data-award-filter="section" ${normalized.grade ? "" : "disabled"}>${sectionOptionsHtml(normalized.grade, normalized.section, true)}</select></label>
    </div>
  `;
}

function buildStudentAwardSummaries(students, certificates, badges) {
  return (students || []).map((student) => {
    const studentCertificates = certificates
      .filter((item) => item.student_id === student.id)
      .map((item) => ({ ...item, award_type: "certificate" }));
    const studentBadges = badges
      .filter((item) => item.student_id === student.id)
      .map((item) => ({ ...item, award_type: "badge" }));
    const awards = [...studentCertificates, ...studentBadges]
      .sort((a, b) => new Date(getAwardDate(b) || 0) - new Date(getAwardDate(a) || 0));
    return {
      student,
      certificates: studentCertificates,
      badges: studentBadges,
      awards,
      latestAward: awards[0] || null
    };
  }).sort((a, b) => {
    const dateDiff = new Date(getAwardDate(b.latestAward || {}) || 0) - new Date(getAwardDate(a.latestAward || {}) || 0);
    if (dateDiff) return dateDiff;
    const countDiff = b.awards.length - a.awards.length;
    if (countDiff) return countDiff;
    return String(a.student.full_name || a.student.username || "").localeCompare(String(b.student.full_name || b.student.username || ""));
  });
}

function renderStudentAwardRows(summaries, { compact = false, emptyTitle = "No students", emptyText = "Students will appear here." } = {}) {
  if (!summaries.length) {
    return `<div class="empty-progress-state compact-empty"><i data-lucide="users"></i><h2>${escapeHtml(emptyTitle)}</h2><p>${escapeHtml(emptyText)}</p></div>`;
  }
  return summaries.map((summary) => renderStudentAwardRow(summary, { compact })).join("");
}

function renderStudentAwardRow(summary, { compact = false } = {}) {
  const { student, certificates, badges, awards } = summary;
  const isExpanded = teacherState.expandedAwardStudentId === student.id;
  const recentChips = awards.slice(0, 3).map(renderAwardChip).join("");
  return `
    <article class="student-award-row ${compact ? "compact" : ""} ${isExpanded ? "expanded" : ""}">
      <div class="student-award-main">
        <span class="avatar-initials">${escapeHtml(getInitials(student.full_name || student.username))}</span>
        <div class="student-award-identity">
          <strong>${escapeHtml(student.full_name || student.username || "Student")}</strong>
          <small>${escapeHtml([student.grade_level, student.section].filter(Boolean).join(" - ") || "No class")}</small>
        </div>
        <div class="student-award-counts">
          <span><b>${certificates.length}</b> certificates</span>
          <span><b>${badges.length}</b> badges</span>
        </div>
        <div class="student-award-chips">${recentChips || `<span class="award-chip empty">No awards yet</span>`}</div>
        <div class="student-award-actions">
          <button type="button" data-toggle-award-student="${escapeAttribute(student.id)}" aria-expanded="${isExpanded ? "true" : "false"}"><i data-lucide="${isExpanded ? "chevron-up" : "list"}"></i><span>${isExpanded ? "Hide" : compact ? "View" : "View Awards"}</span></button>
          <button type="button" data-generate-certificate data-student-id="${escapeAttribute(student.id)}"><i data-lucide="file-badge"></i><span>${compact ? "Issue" : "Certificate"}</span></button>
          <button type="button" data-open-student-awards-profile="${escapeAttribute(student.id)}"><i data-lucide="user-round"></i><span>Profile</span></button>
        </div>
      </div>
      ${isExpanded ? renderStudentAwardDetails(summary, { compact }) : ""}
    </article>
  `;
}

function renderAwardPagination(total, start, visible, pageCount) {
  if (pageCount <= 1) {
    return `<div class="achievement-list-footer"><span>${total ? `Showing ${total} student${total === 1 ? "" : "s"}` : "No students to show"}</span></div>`;
  }
  const current = teacherState.awardPage || 1;
  return `
    <div class="achievement-list-footer">
      <span>Showing ${start + 1}-${start + visible} of ${total} students</span>
      ${renderDashboardPagination({ currentPage: current, pageCount, pageNumberAttribute: "data-award-page-number", label: "Student award pages", className: "achievement-pagination" })}
    </div>
  `;
}

function renderAwardChip(award) {
  const isCertificate = award.award_type === "certificate" || Boolean(award.certificate_key);
  return `<span class="award-chip ${isCertificate ? "certificate" : "badge"}"><i data-lucide="${isCertificate ? "file-badge" : "shield-check"}"></i>${escapeHtml(formatAwardDisplayTitle(award.title, isCertificate ? "Certificate" : "Badge"))}</span>`;
}

function renderStudentAwardDetails(summary, { compact = false } = {}) {
  if (!summary.awards.length) {
    return `<div class="student-award-details ${compact ? "compact" : ""}"><p class="empty-text">No certificates or badges awarded yet.</p></div>`;
  }
  const awards = compact ? summary.awards.slice(0, 3) : summary.awards;
  const remaining = summary.awards.length - awards.length;
  return `
    <div class="student-award-details ${compact ? "compact" : ""}">
      ${awards.map(renderStudentAwardDetailRow).join("")}
      ${remaining > 0 ? `<p class="student-award-more">+${remaining} more award${remaining === 1 ? "" : "s"}. Open Student Awards to view all.</p>` : ""}
    </div>
  `;
}

function renderStudentAwardDetailRow(award) {
  const isCertificate = award.award_type === "certificate" || Boolean(award.certificate_key);
  const icon = isCertificate ? "file-badge" : award.icon || "shield-check";
  return `
    <div class="student-award-detail-row">
      <span class="student-award-detail-icon ${isCertificate ? "certificate" : "badge"} ${escapeAttribute(award.color || "blue")}"><i data-lucide="${escapeAttribute(icon)}"></i></span>
      <div>
        <strong>${escapeHtml(formatAwardDisplayTitle(award.title, isCertificate ? "Certificate" : "Badge"))}</strong>
        <small>${escapeHtml(titleCase(isCertificate ? "certificate" : "badge"))} - ${escapeHtml(formatDateOnly(getAwardDate(award)))}</small>
      </div>
      ${isCertificate ? `
        <div class="history-row-actions">
          <button type="button" data-view-certificate="${escapeAttribute(award.id)}"><i data-lucide="eye"></i><span>View</span></button>
          <button type="button" data-print-certificate="${escapeAttribute(award.id)}"><i data-lucide="printer"></i><span>Print</span></button>
        </div>
      ` : ""}
    </div>
  `;
}

function renderAwardHistoryTab(recentAwards) {
  return `
    <section class="panel-card">
      <div class="panel-title-row">
        <div><h2>Issuance History</h2><p>Certificate and badge awards sorted by most recent.</p></div>
        <span class="evidence-chip">${recentAwards.length} records</span>
      </div>
      ${recentAwards.length ? `
        <div class="achievement-history-list">${recentAwards.map((award) => renderAwardHistoryRow(award, true)).join("")}</div>
      ` : `
        <div class="empty-progress-state compact-empty"><i data-lucide="history"></i><h2>No history yet</h2><p>Awarded badges and certificates will appear here.</p></div>
      `}
    </section>
  `;
}

function renderAwardHistoryRow(award, showActions = false) {
  const student = teacherState.students.find((item) => item.id === award.student_id);
  const isCertificate = award.award_type === "certificate" || Boolean(award.certificate_key);
  return `
    <article class="achievement-history-row">
      <span class="history-award-icon ${isCertificate ? "certificate" : "badge"}"><i data-lucide="${isCertificate ? "file-badge" : "shield-check"}"></i></span>
      <div>
        <strong>${escapeHtml(formatAwardDisplayTitle(award.title, "Award"))}</strong>
        <small>${escapeHtml(student?.full_name || student?.username || "Student")} - ${escapeHtml(formatDateOnly(getAwardDate(award)))}</small>
      </div>
      <span>${isCertificate ? "Certificate" : "Badge"}</span>
      ${showActions && isCertificate ? `
        <div class="history-row-actions">
          <button type="button" data-view-certificate="${escapeAttribute(award.id)}"><i data-lucide="eye"></i><span>View</span></button>
          <button type="button" data-print-certificate="${escapeAttribute(award.id)}"><i data-lucide="printer"></i><span>Print</span></button>
        </div>
      ` : ""}
    </article>
  `;
}

async function handleTeacherAchievementsAction(event) {
  const tabButton = event.target.closest("[data-achievement-teacher-tab]");
  if (tabButton) {
    const nextTab = normalizeTeacherAchievementTab(tabButton.dataset.achievementTeacherTab || "overview");
    if (nextTab === "students" && teacherState.achievementTab !== "students") teacherState.awardPage = 1;
    teacherState.achievementTab = nextTab;
    renderTeacherAchievementsDashboard();
    return;
  }
  const awardPageButton = event.target.closest("[data-award-page-number]");
  if (awardPageButton) {
    teacherState.awardPage = Number(awardPageButton.dataset.awardPageNumber) || 1;
    teacherState.expandedAwardStudentId = "";
    renderTeacherAchievementsDashboard();
    return;
  }
  const awardNavButton = event.target.closest("[data-award-page-nav]");
  if (awardNavButton) {
    teacherState.awardPage += awardNavButton.dataset.awardPageNav === "prev" ? -1 : 1;
    teacherState.expandedAwardStudentId = "";
    renderTeacherAchievementsDashboard();
    return;
  }
  const toggleAwardButton = event.target.closest("[data-toggle-award-student]");
  if (toggleAwardButton) {
    const studentId = toggleAwardButton.dataset.toggleAwardStudent || "";
    teacherState.expandedAwardStudentId = teacherState.expandedAwardStudentId === studentId ? "" : studentId;
    renderTeacherAchievementsDashboard();
    return;
  }
  const profileButton = event.target.closest("[data-open-student-awards-profile]");
  if (profileButton?.dataset.openStudentAwardsProfile) {
    await openStudentFullProfile(profileButton.dataset.openStudentAwardsProfile);
    return;
  }
  const jumpButton = event.target.closest("[data-jump-view]");
  if (jumpButton) {
    showTeacherView(jumpButton.dataset.jumpView);
    return;
  }
  if (
    event.target.closest("[data-open-badge-modal]") ||
    event.target.closest("[data-edit-badge]") ||
    event.target.closest("[data-open-award-badge]")
  ) {
    handleTeacherBadgeAction(event);
    return;
  }
  const generateButton = event.target.closest("[data-generate-certificate]");
  if (generateButton) {
    openGenerateCertificateModal(generateButton.dataset.studentId || "", generateButton.dataset.templateId || "achievement");
    return;
  }
  const viewButton = event.target.closest("[data-view-certificate]");
  if (viewButton) {
    openCertificateViewer(viewButton.dataset.viewCertificate);
    return;
  }
  const printButton = event.target.closest("[data-print-certificate]");
  if (printButton) {
    openCertificateViewer(printButton.dataset.printCertificate, { printAfterOpen: true });
  }
}

function handleTeacherAchievementsFilterChange(event) {
  const select = event.target.closest("[data-award-filter]");
  if (!select) return;
  const nextFilters = {
    ...(teacherState.awardFilters || {}),
    [select.dataset.awardFilter]: select.value
  };
  if (select.dataset.awardFilter === "grade") nextFilters.section = "";
  teacherState.awardFilters = normalizeGradeSectionFilter(nextFilters);
  teacherState.awardPage = 1;
  teacherState.expandedAwardStudentId = "";
  renderTeacherAchievementsDashboard();
}

function openGenerateCertificateModal(initialStudentId = "", templateId = "achievement") {
  const approvedStudents = teacherState.students.filter((student) => student.status === "approved");
  const defaultDate = toDateInputValue(new Date());
  const teacherName = teacherState.teacherProfile?.full_name || getSession()?.profile?.full_name || "";
  const studentOptions = approvedStudents.map((student) => `
    <option value="${escapeAttribute(student.id)}" ${student.id === initialStudentId ? "selected" : ""}>${escapeHtml(student.full_name || student.username || "Student")} ${student.grade_level ? `- ${escapeHtml(student.grade_level)}` : ""}</option>
  `).join("");
  const content = `
    <form class="certificate-generator-form" data-certificate-generator>
      <div class="certificate-generator-layout">
        <div class="certificate-generator-fields">
          <div class="field-grid simple-grid">
            <label>Student<select name="student_id" required><option value="">Select student</option>${studentOptions}</select></label>
            <label>Template<select name="template_id" required>${CERTIFICATE_TEMPLATES.map((template) => `<option value="${escapeAttribute(template.id)}" ${template.id === templateId ? "selected" : ""}>${escapeHtml(template.name)}</option>`).join("")}</select></label>
          </div>
          <label>Certificate Title<input name="title" type="text" value="Certificate of Achievement" required maxlength="120"></label>
          <label>Achievement Name<input name="achievement_name" type="text" placeholder="Example: PC Assembly Completion" required maxlength="140"></label>
          <label>Achievement Description<textarea name="achievement_description" rows="2" required>${escapeHtml(DEFAULT_CERTIFICATE_DESCRIPTION)}</textarea></label>
          <div class="field-grid simple-grid">
            <label>Issue Date<input name="issue_date" type="date" value="${escapeAttribute(defaultDate)}" required></label>
            <label>Principal Name<input name="principal_name" type="text" placeholder="School Principal" required maxlength="120"></label>
          </div>
          <div class="field-grid simple-grid">
            <label>ICT/TLE Teacher<input name="teacher_name" type="text" value="${escapeAttribute(teacherName)}" required maxlength="120"></label>
            <label>Certificate Number<input name="certificate_number" type="text" value="${escapeAttribute(buildCertificateNumber())}" required maxlength="80"></label>
          </div>
          <label>Notes<input name="notes" type="text" placeholder="Optional internal note" maxlength="160"></label>
        </div>
        <aside class="certificate-generator-preview-panel">
          <div class="certificate-generator-preview" data-certificate-preview></div>
          <button class="small-button certificate-print-button" type="button" data-preview-print><i data-lucide="printer"></i><span>Print Preview</span></button>
        </aside>
      </div>
    </form>
  `;
  const modalPromise = createFeedbackModal({
    title: "Generate Certificate",
    message: "Fill in the award details, preview the certificate, then issue it to the student.",
    icon: "file-badge",
    content,
    modalClass: "certificate-modal certificate-generator-modal",
    confirmText: "Issue Certificate",
    cancelText: "Cancel",
    processingText: "Issuing...",
    validate: validateCertificateModal,
    onConfirm: async (overlay) => {
      const form = overlay.querySelector("[data-certificate-generator]");
      const payload = collectCertificateFormPayload(form);
      const selectedStudent = teacherState.students.find((student) => student.id === payload.student_id);
      payload.recipient_name = selectedStudent?.full_name || selectedStudent?.username || payload.recipient_name;
      payload.formatted_issue_date = formatCertificateIssueDate(payload.issue_date);
      payload.reward_type = "certificate";
      payload.action = "add";
      payload.reward_key = normalizeClientRewardKey(payload.certificate_number || `${payload.title}-${Date.now()}`);
      payload.certificate_data = {
        template_id: payload.template_id,
        title: payload.title,
        recipient_name: payload.recipient_name,
        achievement_name: payload.achievement_name
      };
      await apiPost("/api/teacher/student-reward", payload);
      await loadTeacherData();
      if (teacherState.selectedStudentId === payload.student_id) {
        await openStudentFullProfile(payload.student_id, teacherState.studentProfile?.quarterId || "");
      }
      showToast({
        title: "Certificate issued",
        message: `${payload.title} was added to ${payload.recipient_name || "the student"}.`,
        type: "success"
      });
      return true;
    }
  });
  setupCertificateGeneratorOverlay(getLatestFeedbackOverlay());
  return modalPromise;
}

function setupCertificateGeneratorOverlay(overlay) {
  const form = overlay?.querySelector("[data-certificate-generator]");
  if (!form) return;
  const getPreviewData = () => {
    const payload = collectCertificateFormPayload(form);
    const student = teacherState.students.find((item) => item.id === payload.student_id);
    return buildCertificatePreviewData({
      ...payload,
      recipient_name: student?.full_name || student?.username || "",
      formatted_issue_date: formatCertificateIssueDate(payload.issue_date)
    });
  };
  const updatePreview = () => {
    const preview = getPreviewData();
    const previewTarget = form.querySelector("[data-certificate-preview]");
    if (previewTarget) previewTarget.innerHTML = renderCertificatePreview(preview, { expandable: true });
    if (window.lucide) window.lucide.createIcons();
  };
  const openExpandedPreview = (event) => {
    const trigger = event.target.closest("[data-certificate-expand]");
    if (!trigger || !form.contains(trigger)) return;
    if (event.type === "keydown" && !["Enter", " "].includes(event.key)) return;
    event.preventDefault();
    openCertificateFullscreen(getPreviewData());
  };
  form.addEventListener("input", updatePreview);
  form.addEventListener("change", updatePreview);
  form.addEventListener("click", openExpandedPreview);
  form.addEventListener("keydown", openExpandedPreview);
  form.querySelector("[data-preview-print]")?.addEventListener("click", () => printCertificate());
  updatePreview();
}

function validateCertificateModal(overlay) {
  const form = overlay.querySelector("[data-certificate-generator]");
  if (!form) return "Certificate form is missing.";
  const payload = collectCertificateFormPayload(form);
  const required = [
    ["student_id", "Choose a student."],
    ["title", "Enter the certificate title."],
    ["achievement_name", "Enter the achievement name."],
    ["achievement_description", "Enter the achievement description."],
    ["issue_date", "Choose the issue date."],
    ["principal_name", "Enter the principal name."],
    ["teacher_name", "Enter the teacher name."],
    ["certificate_number", "Enter the certificate number."]
  ];
  const missing = required.find(([key]) => !payload[key]);
  return missing ? missing[1] : "";
}

function collectCertificateFormPayload(form) {
  const data = formToObject(form);
  return {
    student_id: String(data.student_id || "").trim(),
    template_id: String(data.template_id || "achievement").trim(),
    title: String(data.title || "").trim(),
    achievement_name: String(data.achievement_name || "").trim(),
    achievement_description: String(data.achievement_description || "").trim(),
    issue_date: String(data.issue_date || "").trim(),
    principal_name: String(data.principal_name || "").trim(),
    teacher_name: String(data.teacher_name || "").trim(),
    certificate_number: String(data.certificate_number || "").trim(),
    notes: String(data.notes || "").trim()
  };
}

function openCertificateViewer(certificateId, { printAfterOpen = false } = {}) {
  const certificate = findCertificateById(certificateId);
  if (!certificate) {
    showToast({ title: "Certificate not found", message: "The selected certificate could not be loaded.", type: "error" });
    return;
  }
  const student = teacherState.students.find((item) => item.id === certificate.student_id)
    || (studentState.profile?.id === certificate.student_id ? studentState.profile : null);
  const previewData = buildCertificatePreviewData(certificate, student);
  const content = `
    <div class="certificate-viewer">
      ${renderCertificatePreview(previewData, { expandable: true })}
      <button class="small-button certificate-print-button" type="button" data-certificate-print-current><i data-lucide="printer"></i><span>Print Certificate</span></button>
    </div>
  `;
  createFeedbackModal({
    title: certificate.title || "Certificate",
    message: "Preview the certificate before printing.",
    icon: "file-badge",
    content,
    modalClass: "certificate-modal certificate-viewer-modal",
    confirmText: "Close",
    hideCancel: true
  });
  const overlay = getLatestFeedbackOverlay();
  const openExpandedPreview = (event) => {
    const trigger = event.target.closest("[data-certificate-expand]");
    if (!trigger || !overlay?.contains(trigger)) return;
    if (event.type === "keydown" && !["Enter", " "].includes(event.key)) return;
    event.preventDefault();
    openCertificateFullscreen(previewData);
  };
  overlay?.addEventListener("click", openExpandedPreview);
  overlay?.addEventListener("keydown", openExpandedPreview);
  overlay?.querySelector("[data-certificate-print-current]")?.addEventListener("click", printCertificate);
  if (printAfterOpen) window.setTimeout(printCertificate, 300);
}

function renderCertificatePreview(data, { compact = false, expandable = false } = {}) {
  const template = CERTIFICATE_TEMPLATES.find((item) => item.id === data.template_id) || CERTIFICATE_TEMPLATES[0];
  const expandAttributes = expandable
    ? ` role="button" tabindex="0" aria-label="Open certificate full size" data-certificate-expand`
    : "";
  return `
    <div class="certificate-preview ${compact ? "compact" : ""}">
      <div class="certificate-preview-stage certificate-print-target ${expandable ? "is-expandable" : ""}"${expandAttributes}>
        <img src="${escapeAttribute(template.image)}" alt="${escapeAttribute(template.name)}">
        <span class="certificate-overlay certificate-recipient">${escapeHtml(data.recipient_name || "Student Name")}</span>
        <span class="certificate-overlay certificate-achievement">${escapeHtml(data.achievement_name ? `(${data.achievement_name})` : "(the achievement)")}</span>
        <span class="certificate-overlay certificate-date">${escapeHtml(data.formatted_issue_date || formatCertificateIssueDate(data.issue_date || toDateInputValue(new Date())))}</span>
        <span class="certificate-overlay certificate-principal">${escapeHtml(data.principal_name || "Principal Name")}</span>
        <span class="certificate-overlay certificate-teacher">${escapeHtml(data.teacher_name || "Teacher Name")}</span>
        <span class="certificate-overlay certificate-number">${escapeHtml(data.certificate_number || "")}</span>
      </div>
    </div>
  `;
}

function buildCertificatePreviewData(certificate = {}, student = null) {
  return {
    template_id: certificate.template_id || "achievement",
    title: certificate.title || "Certificate of Achievement",
    recipient_name: certificate.recipient_name || student?.full_name || student?.username || "",
    achievement_name: certificate.achievement_name || certificate.title || "",
    achievement_description: certificate.achievement_description || DEFAULT_CERTIFICATE_DESCRIPTION,
    issue_date: certificate.issue_date || String(certificate.awarded_at || "").slice(0, 10) || toDateInputValue(new Date()),
    formatted_issue_date: certificate.formatted_issue_date || formatCertificateIssueDate(certificate.issue_date || certificate.awarded_at || new Date()),
    principal_name: certificate.principal_name || "",
    teacher_name: certificate.teacher_name || "",
    certificate_number: certificate.certificate_number || certificate.certificate_key || ""
  };
}

function openCertificateFullscreen(data) {
  const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
  const overlay = document.createElement("div");
  overlay.className = "certificate-lightbox-overlay";
  overlay.innerHTML = `
    <section class="certificate-lightbox-card" role="dialog" aria-modal="true" aria-label="Full size certificate preview">
      <button class="certificate-lightbox-close" type="button" data-certificate-lightbox-close aria-label="Close certificate preview"><i data-lucide="x"></i></button>
      ${renderCertificatePreview(data)}
    </section>
  `;

  const close = () => {
    document.removeEventListener("keydown", handleKeydown, true);
    overlay.classList.add("closing");
    setTimeout(() => {
      overlay.remove();
      previousFocus?.focus?.();
    }, 140);
  };
  const handleKeydown = (event) => {
    if (event.key === "Escape") {
      event.preventDefault();
      event.stopImmediatePropagation();
      close();
    }
  };

  overlay.addEventListener("click", (event) => {
    if (event.target === overlay || event.target.closest("[data-certificate-lightbox-close]")) close();
  });
  document.addEventListener("keydown", handleKeydown, true);
  document.body.appendChild(overlay);
  overlay.querySelector("[data-certificate-lightbox-close]")?.focus();
  if (window.lucide) window.lucide.createIcons();
}

function findCertificateById(certificateId) {
  return [...(teacherState.certificates || []), ...(studentState.certificates || [])]
    .find((certificate) => certificate.id === certificateId);
}

function buildCertificateNumber() {
  const stamp = new Date().toISOString().slice(0, 10).replace(/-/g, "");
  const random = Math.random().toString(36).slice(2, 7).toUpperCase();
  return `TW360-${stamp}-${random}`;
}

function normalizeClientRewardKey(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 80) || `certificate-${Date.now()}`;
}

function formatCertificateIssueDate(value) {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const day = date.getDate();
  const month = new Intl.DateTimeFormat(undefined, { month: "long" }).format(date);
  const year = date.getFullYear();
  return `Given this ${ordinalDay(day)} day of ${month} ${year}`;
}

function ordinalDay(day) {
  const value = Number(day);
  const mod100 = value % 100;
  if (mod100 >= 11 && mod100 <= 13) return `${value}th`;
  if (value % 10 === 1) return `${value}st`;
  if (value % 10 === 2) return `${value}nd`;
  if (value % 10 === 3) return `${value}rd`;
  return `${value}th`;
}

function getLatestFeedbackOverlay() {
  const overlays = document.querySelectorAll(".feedback-modal-overlay");
  return overlays[overlays.length - 1] || null;
}

function printCertificate() {
  document.body.classList.add("printing-certificate");
  const cleanup = () => {
    document.body.classList.remove("printing-certificate");
    window.removeEventListener("afterprint", cleanup);
  };
  window.addEventListener("afterprint", cleanup);
  window.print();
  window.setTimeout(cleanup, 1200);
}

function renderSettingsDashboard() {
  const profile = teacherState.teacherProfile || getSession()?.profile || {};
  const settings = normalizeTeacherSettings(teacherState.settings);
  const profileForm = document.getElementById("teacherProfileSettingsForm");
  const notificationForm = document.getElementById("teacherNotificationSettingsForm");
  const dashboardForm = document.getElementById("teacherDashboardSettingsForm");
  const reportForm = document.getElementById("teacherReportSettingsForm");

  if (profileForm) {
    profileForm.elements.username.value = profile.username || "";
    profileForm.elements.email.value = profile.email || "";
    profileForm.elements.role.value = titleCase(profile.role || "teacher");
    profileForm.elements.status.value = titleCase(profile.status || "approved");
    profileForm.elements.full_name.value = profile.full_name || "";
    profileForm.elements.first_name.value = profile.first_name || "";
    profileForm.elements.last_name.value = profile.last_name || "";
    profileForm.elements.phone_number.value = profile.phone_number || profile.cp_number || "";
  }

  if (notificationForm) {
    const preferences = settings.notification_preferences || {};
    notificationForm.elements.student_accounts.checked = preferences.student_accounts !== false;
    notificationForm.elements.lesson_completions.checked = preferences.lesson_completions !== false;
    notificationForm.elements.practice_submissions.checked = preferences.practice_submissions !== false;
    notificationForm.elements.achievement_awards.checked = preferences.achievement_awards !== false;
  }

  if (dashboardForm) {
    dashboardForm.elements.default_grade.value = settings.default_grade || "";
    dashboardForm.elements.default_landing_view.value = settings.default_landing_view || "overview";
    dashboardForm.elements.compact_lessons.checked = Boolean(settings.compact_lessons);
  }

  if (reportForm) {
    reportForm.elements.report_default_range.value = settings.report_default_range || "week";
    reportForm.elements.report_export_format.value = settings.report_export_format || "pdf";
    reportForm.elements.report_default_grade.value = settings.report_default_grade || "";
  }
}

async function submitTeacherProfileSettings(event) {
  event.preventDefault();
  const message = document.getElementById("teacherMessage");
  const form = event.currentTarget;
  const payload = formToObject(form);
  try {
    setMessage(message, "Saving teacher profile...");
    const data = await apiPatch("/api/teacher/settings", {
      profile: {
        full_name: payload.full_name,
        first_name: payload.first_name,
        last_name: payload.last_name,
        phone_number: payload.phone_number
      }
    });
    applyTeacherSettingsResponse(data);
    setMessage(message, "Teacher profile saved.", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

async function submitTeacherNotificationSettings(event) {
  event.preventDefault();
  const message = document.getElementById("teacherMessage");
  const form = event.currentTarget;
  try {
    setMessage(message, "Saving notification preferences...");
    const data = await apiPatch("/api/teacher/settings", {
      settings: {
        notification_preferences: {
          student_accounts: form.elements.student_accounts.checked,
          lesson_completions: form.elements.lesson_completions.checked,
          practice_submissions: form.elements.practice_submissions.checked,
          achievement_awards: form.elements.achievement_awards.checked
        }
      }
    });
    applyTeacherSettingsResponse(data);
    await loadTeacherNotifications({ silent: true });
    setMessage(message, "Notification preferences saved.", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

async function submitTeacherDashboardSettings(event) {
  event.preventDefault();
  const message = document.getElementById("teacherMessage");
  const form = event.currentTarget;
  try {
    setMessage(message, "Saving dashboard defaults...");
    const data = await apiPatch("/api/teacher/settings", {
      settings: {
        default_grade: form.elements.default_grade.value,
        default_landing_view: form.elements.default_landing_view.value,
        compact_lessons: form.elements.compact_lessons.checked
      }
    });
    applyTeacherSettingsResponse(data);
    teacherState.selectedGrade = teacherState.settings.default_grade || "";
    teacherState.selectedSection = "";
    teacherState.compactLessons = Boolean(teacherState.settings.compact_lessons);
    renderTeacherStudents();
    renderLessonLibrary();
    setMessage(message, "Dashboard defaults saved.", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

async function submitTeacherReportSettings(event) {
  event.preventDefault();
  const message = document.getElementById("teacherMessage");
  const form = event.currentTarget;
  try {
    setMessage(message, "Saving report defaults...");
    const data = await apiPatch("/api/teacher/settings", {
      settings: {
        report_default_range: form.elements.report_default_range.value,
        report_export_format: form.elements.report_export_format.value,
        report_default_grade: form.elements.report_default_grade.value
      }
    });
    applyTeacherSettingsResponse(data);
    teacherState.reportFilters = reportFiltersFromSettings(teacherState.settings);
    renderReportsDashboard();
    setMessage(message, "Report defaults saved.", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

async function submitTeacherPasswordSettings(event) {
  event.preventDefault();
  const message = document.getElementById("teacherMessage");
  const form = event.currentTarget;
  const payload = formToObject(form);
  if (payload.new_password !== payload.confirm_password) {
    setMessage(message, "New password and confirmation must match.", "error");
    return;
  }
  if (!isStrongPassword(payload.new_password || "")) {
    setMessage(message, "Password must be at least 8 characters and include uppercase, lowercase, number, and symbol.", "error");
    return;
  }
  try {
    setMessage(message, "Updating password...");
    await apiPatch("/api/teacher/settings", { action: "password", ...payload });
    form.reset();
    setMessage(message, "Password updated.", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

function applyTeacherSettingsResponse(data) {
  if (data.profile) {
    teacherState.teacherProfile = data.profile;
    const teacherDisplayName = data.profile.full_name || data.profile.username || "ICT Teacher";
    document.getElementById("teacherName").textContent = teacherDisplayName;
    document.getElementById("teacherWelcomeName").textContent = teacherDisplayName.split(/\s+/)[0] || teacherDisplayName;
    const session = getSession();
    if (session) setSession(session, data.profile);
  }
  if (data.settings) {
    teacherState.settings = normalizeTeacherSettings(data.settings);
  }
  renderSettingsDashboard();
}

function renderTeacherMetrics() {
  const approvedStudents = teacherState.students.filter((student) => student.status === "approved");
  const summaries = approvedStudents.map(getTeacherStudentSummary);
  const publishedLessons = teacherState.lessons.filter((lesson) => lesson.status === "published" && isLessonInActiveQuarter(lesson));
  const topPerformer = summaries
    .filter((summary) => summary.student.status === "approved")
    .sort((a, b) => b.average - a.average)[0];

  document.getElementById("metricStudents").textContent = String(teacherState.students.length);
  document.getElementById("metricOnTrack").textContent = String(summaries.filter((summary) => summary.progressStatus === "On Track").length);
  document.getElementById("metricSupport").textContent = String(summaries.filter((summary) => summary.progressStatus === "Needs Support").length);
  document.getElementById("metricLessons").textContent = String(publishedLessons.length);
  document.getElementById("metricQuarter").textContent = teacherState.activeQuarter ? teacherState.activeQuarter.title : "No active term";
  renderTeacherMetricSparklines({ approvedStudents, summaries, publishedLessons });

  setText("studentMetricTotal", teacherState.students.length);
  setText("studentMetricClasses", new Set(teacherState.students.map((student) => `${student.grade_level || "-"}-${student.section || "-"}`)).size);
  setText("studentMetricTrack", summaries.filter((summary) => summary.progressStatus === "On Track").length);
  setText("studentMetricSupport", summaries.filter((summary) => summary.progressStatus === "Needs Support").length);
  setText("studentMetricTrackPct", `${percentOf(summaries.filter((summary) => summary.progressStatus === "On Track").length, approvedStudents.length)}%`);
  setText("studentMetricSupportPct", `${percentOf(summaries.filter((summary) => summary.progressStatus === "Needs Support").length, approvedStudents.length)}%`);
  setText("studentMetricTop", topPerformer?.student.full_name || "-");
  setText("studentMetricTopScore", `${topPerformer?.average || 0}%`);

  const activeQuarterCard = document.getElementById("activeQuarterCard");
  if (!teacherState.activeQuarter) {
    activeQuarterCard.innerHTML = `<p class="empty-text">No active term selected. Set one in Term Management.</p>`;
    return;
  }

  const activeModules = teacherState.modules.filter((module) => module.quarter_id === teacherState.activeQuarter.id);
  activeQuarterCard.innerHTML = `
    <strong>${escapeHtml(teacherState.activeQuarter.title)}</strong>
    <span>${escapeHtml(teacherState.activeQuarter.school_year)}</span>
    <p>${activeModules.length} modules and ${publishedLessons.length} published lessons are attached.</p>
  `;
}

function renderTeacherMetricSparklines({ approvedStudents, summaries, publishedLessons }) {
  const allCohorts = sortedCohortLabels(teacherState.students);
  const summaryByStudentId = new Map(summaries.map((summary) => [summary.student.id, summary]));
  const studentSeries = countRecordsByCohort(teacherState.students, allCohorts, (student) => student);
  const onTrackSeries = countRecordsByCohort(
    approvedStudents.filter((student) => summaryByStudentId.get(student.id)?.progressStatus === "On Track"),
    allCohorts,
    (student) => student
  );
  const supportSeries = countRecordsByCohort(
    approvedStudents.filter((student) => summaryByStudentId.get(student.id)?.progressStatus === "Needs Support"),
    allCohorts,
    (student) => student
  );
  const moduleById = new Map(teacherState.modules.map((module) => [module.id, module]));
  const lessonModules = [...new Set(publishedLessons.map((lesson) => lesson.module_id).filter(Boolean))]
    .map((moduleId) => moduleById.get(moduleId))
    .filter(Boolean)
    .sort((a, b) => Number(a.sort_order || 0) - Number(b.sort_order || 0) || String(a.title || "").localeCompare(String(b.title || "")));
  const lessonLabels = lessonModules.map((module) => module.title || "Module");
  const lessonSeries = lessonModules.map((module) => publishedLessons.filter((lesson) => lesson.module_id === module.id).length);

  renderMetricSparkline("students", studentSeries, allCohorts, "Total students by class");
  renderMetricSparkline("on-track", onTrackSeries, allCohorts, "On-track students by class");
  renderMetricSparkline("support", supportSeries, allCohorts, "Students needing support by class");
  renderMetricSparkline("lessons", lessonSeries, lessonLabels, "Published lessons by module");
}

function teacherCohortLabel(student) {
  return [student?.grade_level, student?.section].filter(Boolean).join(" - ") || "Unassigned";
}

function sortedCohortLabels(students) {
  return [...new Set((students || []).map(teacherCohortLabel))].sort((a, b) => a.localeCompare(b));
}

function countRecordsByCohort(records, labels, getStudent) {
  const counts = new Map((labels || []).map((label) => [label, 0]));
  (records || []).forEach((record) => {
    const label = teacherCohortLabel(getStudent(record));
    counts.set(label, (counts.get(label) || 0) + 1);
  });
  return (labels || []).map((label) => counts.get(label) || 0);
}

function renderMetricSparkline(key, values, labels, description, unit = "") {
  const svg = document.querySelector(`[data-metric-sparkline="${key}"]`);
  if (!svg) return;
  const sourceValues = (values || []).map((value) => Number.isFinite(Number(value)) ? Math.max(0, Number(value)) : 0);
  const sourceLabels = (labels || []).map((label) => String(label || "Data"));
  const plottedValues = sourceValues.length > 1 ? sourceValues : [sourceValues[0] || 0, sourceValues[0] || 0];
  const top = 6;
  const bottom = 40;
  const left = 2;
  const right = 98;
  const minimum = Math.min(...plottedValues);
  const maximum = Math.max(...plottedValues);
  const range = maximum - minimum;
  const points = plottedValues.map((value, index) => {
    const x = left + ((right - left) * index / Math.max(1, plottedValues.length - 1));
    const y = range ? bottom - ((value - minimum) / range) * (bottom - top) : (value ? 23 : bottom);
    return { x: Number(x.toFixed(2)), y: Number(y.toFixed(2)) };
  });
  const pointString = points.map((point) => `${point.x},${point.y}`).join(" ");
  const areaPath = `M ${points.map((point) => `${point.x} ${point.y}`).join(" L ")} L ${right} 46 L ${left} 46 Z`;
  const details = sourceValues.length
    ? sourceValues.map((value, index) => `${sourceLabels[index] || `Item ${index + 1}`}: ${formatProgressNumber(value)}${unit}`).join("; ")
    : "No records yet";

  svg.querySelector(".sparkline-area")?.setAttribute("d", areaPath);
  svg.querySelector("polyline")?.setAttribute("points", pointString);
  const accessibleText = `${description}. ${details}`;
  svg.setAttribute("aria-label", accessibleText);
  const title = svg.querySelector("title");
  if (title) title.textContent = accessibleText;
  svg.classList.remove("is-ready");
  svg.getBoundingClientRect();
  svg.classList.add("is-ready");
}

function renderPendingStudents() {
  const container = document.getElementById("pendingStudents");
  const pending = teacherState.students.filter((student) => student.status === "pending");

  if (!pending.length) {
    container.innerHTML = `<p class="empty-text">No pending student accounts right now.</p>`;
    return;
  }

  container.innerHTML = pending.map((student) => `
    <article class="mini-row">
      <div class="mini-avatar">${escapeHtml(getInitials(student.full_name || student.username))}</div>
      <div>
        <strong>${escapeHtml(student.full_name || "Unnamed Student")}</strong>
        <span>${escapeHtml(student.grade_level || "-")} ${escapeHtml(student.section || "")}</span>
        <small>${escapeHtml(student.email || "")}</small>
      </div>
      <div class="row-actions">
        <button class="approve-button mini" type="button" data-student-action="approve" data-id="${escapeHtml(student.id)}"><i data-lucide="check"></i></button>
        <button class="reject-button mini" type="button" data-student-action="reject" data-id="${escapeHtml(student.id)}"><i data-lucide="x"></i></button>
      </div>
    </article>
  `).join("");
}

function renderCapstoneEvidenceDashboard() {
  syncEvidenceClassFilter();
  const evidence = buildCapstoneEvidenceDataset();

  setText("evidenceMetricStudents", evidence.students.length);
  setText("evidenceMetricCompletion", `${evidence.metrics.completionRate}%`);
  setText("evidenceMetricAverage", evidence.metrics.averageScore === null ? "-" : `${evidence.metrics.averageScore}%`);
  setText("evidenceMetricSubmissions", `${evidence.metrics.submissionRate}%`);
  setText("evidenceMetricCertificates", evidence.metrics.certificates);
  setText("evidenceMetricSupport", evidence.interventions.length);
  setText("evidenceActiveQuarterLabel", evidence.activeQuarterLabel);
  renderEvidenceMetricSparklines(evidence);

  renderEvidenceMasteryList(evidence);
  renderEvidencePrePost(evidence);
  renderEvidenceInterventions(evidence);
  renderEvidenceReadiness(evidence);
  if (window.lucide) window.lucide.createIcons();
}

function renderEvidenceMetricSparklines(evidence) {
  const cohortLabels = sortedCohortLabels(evidence.students);
  const studentById = new Map(evidence.students.map((student) => [student.id, student]));
  const studentSeries = countRecordsByCohort(evidence.students, cohortLabels, (student) => student);
  const certificateSeries = countRecordsByCohort(evidence.certificates, cohortLabels, (certificate) => studentById.get(certificate.student_id));
  const supportSeries = countRecordsByCohort(evidence.interventions, cohortLabels, (intervention) => intervention.student);
  const modules = [...evidence.modules].sort((a, b) => Number(a.sort_order || 0) - Number(b.sort_order || 0) || String(a.title || "").localeCompare(String(b.title || "")));
  const moduleLabels = modules.map((module) => module.title || "Module");
  const completionSeries = modules.map((module) => {
    const moduleLessons = evidence.lessons.filter((lesson) => lesson.module_id === module.id);
    const lessonIds = new Set(moduleLessons.map((lesson) => lesson.id));
    const eligibleStudents = evidence.students.filter((student) => student.grade_level === module.grade_level);
    const completed = evidence.progress.filter((item) => lessonIds.has(item.lesson_id) && (item.status === "completed" || item.progress_percent === 100)).length;
    return percentOf(completed, eligibleStudents.length * moduleLessons.length);
  });
  const scoreSeries = modules.map((module) => {
    const scores = evidence.latestAttempts
      .filter((attempt) => attempt.module_id === module.id)
      .map((attempt) => Number(attempt.score_percent))
      .filter(Number.isFinite);
    return scores.length ? Math.round(scores.reduce((sum, score) => sum + score, 0) / scores.length) : 0;
  });
  const assessments = [...evidence.assessments].sort((a, b) => String(a.title || "").localeCompare(String(b.title || "")));
  const assessmentLabels = assessments.map((assessment) => assessment.title || "Assessment");
  const submissionSeries = assessments.map((assessment) => {
    const eligibleStudents = evidence.students.filter((student) => student.grade_level === assessment.grade_level).length;
    const submittedStudents = new Set(evidence.latestAttempts
      .filter((attempt) => attempt.lesson_id === assessment.lesson_id)
      .map((attempt) => attempt.student_id)).size;
    return percentOf(submittedStudents, eligibleStudents);
  });

  renderMetricSparkline("evidence-students", studentSeries, cohortLabels, "Approved students by class");
  renderMetricSparkline("evidence-completion", completionSeries, moduleLabels, "Completion rate by module", "%");
  renderMetricSparkline("evidence-score", scoreSeries, moduleLabels, "Average score by module", "%");
  renderMetricSparkline("evidence-submissions", submissionSeries, assessmentLabels, "Submission rate by assessment", "%");
  renderMetricSparkline("evidence-certificates", certificateSeries, cohortLabels, "Certificates by class");
  renderMetricSparkline("evidence-support", supportSeries, cohortLabels, "Interventions by class");
}

function syncEvidenceClassFilter() {
  const select = document.getElementById("evidenceClassFilter");
  if (!select) return;
  const options = evidenceClassOptions();
  const current = teacherState.evidenceFilters?.classKey || "";
  select.innerHTML = options.map((option) =>
    `<option value="${escapeAttribute(option.value)}">${escapeHtml(option.label)}</option>`
  ).join("");
  select.value = options.some((option) => option.value === current) ? current : "";
  teacherState.evidenceFilters = { classKey: select.value };
}

function evidenceClassOptions() {
  const approved = teacherState.students.filter((student) => student.status === "approved");
  const grades = [...new Set(approved.map((student) => student.grade_level).filter(Boolean))].sort();
  const classKeys = new Map();
  approved.forEach((student) => {
    if (!student.grade_level || !student.section) return;
    const value = `class:${student.grade_level}||${student.section}`;
    classKeys.set(value, `${student.grade_level} - ${student.section}`);
  });
  return [
    { value: "", label: "All Classes" },
    ...grades.map((grade) => ({ value: `grade:${grade}`, label: grade })),
    ...[...classKeys.entries()].sort((a, b) => a[1].localeCompare(b[1])).map(([value, label]) => ({ value, label }))
  ];
}

function buildCapstoneEvidenceDataset() {
  const filters = teacherState.evidenceFilters || { classKey: "" };
  const studentPool = teacherState.students.filter((student) =>
    student.status === "approved" && matchesEvidenceClass(student, filters.classKey)
  );
  const studentById = new Map(studentPool.map((student) => [student.id, student]));
  const studentIds = new Set(studentPool.map((student) => student.id));
  const selectedGrades = new Set(studentPool.map((student) => student.grade_level).filter(Boolean));
  const moduleById = new Map(teacherState.modules.map((module) => [module.id, module]));
  const activeModules = teacherState.modules.filter((module) =>
    module.status === "published"
    && selectedGrades.has(module.grade_level)
    && (!teacherState.activeQuarter || module.quarter_id === teacherState.activeQuarter.id)
  );
  const activeModuleIds = new Set(activeModules.map((module) => module.id));
  const lessons = teacherState.lessons.filter((lesson) =>
    lesson.status === "published"
    && activeModuleIds.has(lesson.module_id)
    && (!teacherState.activeQuarter || lesson.quarter_id === teacherState.activeQuarter.id)
  );
  const lessonById = new Map(lessons.map((lesson) => [lesson.id, lesson]));
  const progress = teacherState.progress.filter((item) => {
    const student = studentById.get(item.student_id);
    const lesson = lessonById.get(item.lesson_id);
    const module = lesson ? moduleById.get(lesson.module_id) : null;
    return Boolean(student && lesson && module?.grade_level === student.grade_level);
  });
  const assessments = getEvidenceAssessmentRows(lessons, moduleById);
  const allAttempts = assessments.flatMap((assessment) => {
    const attempts = Array.isArray(assessment.all_attempts) ? assessment.all_attempts : assessment.attempts || [];
    return attempts
      .filter((attempt) => {
        const student = studentById.get(attempt.student_id);
        return student && student.grade_level === assessment.grade_level;
      })
      .map((attempt) => ({
        ...attempt,
        assessment_title: assessment.title,
        module_id: assessment.module_id,
        module_title: assessment.module_title,
        module_category: assessment.module_category,
        lesson_type: assessment.lesson_type,
        grade_level: assessment.grade_level,
        due_date: assessment.due_date
      }));
  });
  const latestAttempts = getLatestAttemptsByStudentLesson(allAttempts);
  const scoreValues = latestAttempts.map((attempt) => Number(attempt.score_percent)).filter(Number.isFinite);
  const assignedLessonSlots = studentPool.reduce((sum, student) =>
    sum + lessons.filter((lesson) => moduleById.get(lesson.module_id)?.grade_level === student.grade_level).length
  , 0);
  const completedSlots = progress.filter((item) => item.status === "completed" || item.progress_percent === 100).length;
  const assignedAssessmentSlots = studentPool.reduce((sum, student) =>
    sum + assessments.filter((assessment) => assessment.grade_level === student.grade_level).length
  , 0);
  const submittedSlots = new Set(latestAttempts.map((attempt) => `${attempt.student_id}-${attempt.lesson_id}`)).size;
  const certificates = teacherState.certificates.filter((certificate) => studentIds.has(certificate.student_id));
  const badges = teacherState.badges.filter((badge) => studentIds.has(badge.student_id));
  const topicMastery = buildEvidenceTopicMastery({
    modules: activeModules,
    lessons,
    progress,
    latestAttempts,
    studentPool,
    moduleById
  });
  const interventions = buildEvidenceInterventions({
    students: studentPool,
    lessons,
    assessments,
    progress,
    latestAttempts,
    moduleById
  });
  const prePost = buildEvidencePrePost(assessments, latestAttempts, selectedGrades);
  const readiness = buildEvidenceReadiness({ progress, latestAttempts });

  return {
    filters,
    filterLabel: evidenceFilterLabel(filters.classKey),
    activeQuarterLabel: teacherState.activeQuarter ? `${teacherState.activeQuarter.title} only` : "No active term",
    students: studentPool,
    modules: activeModules,
    lessons,
    assessments,
    progress,
    latestAttempts,
    certificates,
    badges,
    topicMastery,
    interventions,
    prePost,
    readiness,
    metrics: {
      completionRate: percentOf(completedSlots, assignedLessonSlots),
      averageScore: scoreValues.length ? Math.round(scoreValues.reduce((sum, value) => sum + value, 0) / scoreValues.length) : null,
      submissionRate: percentOf(submittedSlots, assignedAssessmentSlots),
      certificates: certificates.length
    }
  };
}

function matchesEvidenceClass(student, classKey = "") {
  if (!classKey) return true;
  if (classKey.startsWith("grade:")) return student.grade_level === classKey.slice(6);
  if (classKey.startsWith("class:")) {
    const [grade, section] = classKey.slice(6).split("||");
    return student.grade_level === grade && (student.section || "") === (section || "");
  }
  return true;
}

function getEvidenceAssessmentRows(lessons, moduleById) {
  const publishedAssessmentIds = new Set(lessons
    .filter((lesson) => ["practice", "assessment"].includes(lesson.lesson_type))
    .map((lesson) => lesson.id));
  return teacherState.assessments
    .filter((assessment) => assessment.status === "published" && publishedAssessmentIds.has(assessment.lesson_id || assessment.id))
    .map((assessment) => {
      const module = moduleById.get(assessment.module_id);
      return {
        ...assessment,
        lesson_id: assessment.lesson_id || assessment.id,
        module_title: assessment.module_title || module?.title || "Module",
        module_category: assessment.module_category || module?.category || "",
        grade_level: assessment.grade_level || module?.grade_level || ""
      };
    });
}

function getLatestAttemptsByStudentLesson(attempts) {
  const latest = new Map();
  attempts.forEach((attempt) => {
    const key = `${attempt.student_id}-${attempt.lesson_id}`;
    const current = latest.get(key);
    if (!current || new Date(attempt.submitted_at || 0) > new Date(current.submitted_at || 0)) {
      latest.set(key, attempt);
    }
  });
  return [...latest.values()].sort((a, b) => new Date(b.submitted_at || 0) - new Date(a.submitted_at || 0));
}

function buildEvidenceTopicMastery({ modules, lessons, progress, latestAttempts, studentPool, moduleById }) {
  return modules.map((module) => {
    const moduleLessons = lessons.filter((lesson) => lesson.module_id === module.id);
    const moduleLessonIds = new Set(moduleLessons.map((lesson) => lesson.id));
    const moduleStudents = studentPool.filter((student) => student.grade_level === module.grade_level);
    const scores = latestAttempts
      .filter((attempt) => attempt.module_id === module.id)
      .map((attempt) => Number(attempt.score_percent))
      .filter(Number.isFinite);
    const assignedSlots = moduleStudents.length * moduleLessons.length;
    const completedSlots = progress.filter((item) =>
      moduleLessonIds.has(item.lesson_id) && (item.status === "completed" || item.progress_percent === 100)
    ).length;
    const completionPercent = percentOf(completedSlots, assignedSlots);
    const mastery = scores.length
      ? Math.round(scores.reduce((sum, value) => sum + value, 0) / scores.length)
      : completionPercent;
    return {
      module_id: module.id,
      title: module.title,
      category: module.category,
      grade_level: module.grade_level,
      mastery,
      source: scores.length ? "Submitted scores" : "Lesson completion",
      evidence_count: scores.length || completedSlots,
      lesson_count: moduleLessons.length,
      icon: iconForTopic(moduleById.get(module.id) || module)
    };
  }).filter((topic) => topic.lesson_count > 0)
    .sort((a, b) => a.mastery - b.mastery || a.title.localeCompare(b.title));
}

function buildEvidenceInterventions({ students, lessons, assessments, progress, latestAttempts, moduleById }) {
  const today = startOfLocalDay(toDateInputValue(new Date()));
  const inactiveCutoff = new Date(today);
  inactiveCutoff.setDate(inactiveCutoff.getDate() - EVIDENCE_SUPPORT_DAYS);
  return students.map((student) => {
    const assignedLessons = lessons.filter((lesson) => moduleById.get(lesson.module_id)?.grade_level === student.grade_level);
    const assignedLessonIds = new Set(assignedLessons.map((lesson) => lesson.id));
    const assignedAssessments = assessments.filter((assessment) => assessment.grade_level === student.grade_level);
    const assignedAssessmentIds = new Set(assignedAssessments.map((assessment) => assessment.lesson_id));
    const studentProgress = progress.filter((item) => item.student_id === student.id && assignedLessonIds.has(item.lesson_id));
    const studentAttempts = latestAttempts
      .filter((attempt) => attempt.student_id === student.id && assignedAssessmentIds.has(attempt.lesson_id))
      .sort((a, b) => new Date(b.submitted_at || 0) - new Date(a.submitted_at || 0));
    const completed = studentProgress.filter((item) => item.status === "completed" || item.progress_percent === 100).length;
    const completionRate = percentOf(completed, assignedLessons.length);
    const latestScore = studentAttempts.length && Number.isFinite(Number(studentAttempts[0].score_percent))
      ? Number(studentAttempts[0].score_percent)
      : null;
    const submittedAssessmentIds = new Set(studentAttempts.map((attempt) => attempt.lesson_id));
    const overdue = assignedAssessments.filter((assessment) =>
      assessment.due_date
      && startOfLocalDay(assessment.due_date) < today
      && !submittedAssessmentIds.has(assessment.lesson_id)
    );
    const activityDates = [
      ...studentProgress.flatMap((item) => [item.updated_at, item.completed_at, item.started_at]),
      ...studentAttempts.map((attempt) => attempt.submitted_at)
    ].filter(Boolean).map((value) => new Date(value)).filter((date) => !Number.isNaN(date.getTime()));
    const latestActivity = activityDates.sort((a, b) => b - a)[0] || null;
    const reasons = [];
    if (assignedLessons.length && completionRate < 50) reasons.push(`Completion ${completionRate}%`);
    if (latestScore !== null && latestScore < 75) reasons.push(`Latest score ${latestScore}%`);
    if (overdue.length) reasons.push(`${overdue.length} overdue check${overdue.length === 1 ? "" : "s"}`);
    if ((assignedLessons.length || assignedAssessments.length) && (!latestActivity || latestActivity < inactiveCutoff)) {
      reasons.push("No recent activity");
    }
    return {
      student,
      reasons,
      completionRate,
      latestScore,
      overdueCount: overdue.length,
      lastActive: latestActivity ? latestActivity.toISOString() : ""
    };
  }).filter((item) => item.reasons.length)
    .sort((a, b) => b.reasons.length - a.reasons.length || a.completionRate - b.completionRate || (a.latestScore ?? 101) - (b.latestScore ?? 101));
}

function buildEvidencePrePost(assessments, latestAttempts, selectedGrades = new Set()) {
  const mapped = getMappedEvaluationLessonIds(selectedGrades);
  let preIds = mapped.preIds;
  let postIds = mapped.postIds;
  let usesConfiguredMapping = mapped.hasMapping;

  if (!usesConfiguredMapping) {
    preIds = new Set(assessments
      .filter((assessment) => /pre[\s-]?test/i.test(assessment.title || ""))
      .map((assessment) => assessment.lesson_id));
    postIds = new Set(assessments
      .filter((assessment) => /post[\s-]?test|final assessment/i.test(assessment.title || ""))
      .map((assessment) => assessment.lesson_id));
    usesConfiguredMapping = Boolean(preIds.size || postIds.size);
  }

  const preScores = latestAttempts
    .filter((attempt) => preIds.has(attempt.lesson_id))
    .map((attempt) => Number(attempt.score_percent))
    .filter(Number.isFinite);
  const postScores = latestAttempts
    .filter((attempt) => postIds.has(attempt.lesson_id))
    .map((attempt) => Number(attempt.score_percent))
    .filter(Number.isFinite);
  if (!preIds.size || !postIds.size) {
    return {
      ready: false,
      setupMissing: true,
      preCount: preScores.length,
      postCount: postScores.length,
      message: "Choose a pre-assessment and post-assessment to start tracking class improvement."
    };
  }

  if (!preScores.length || !postScores.length) {
    return {
      ready: false,
      setupMissing: false,
      preCount: preScores.length,
      postCount: postScores.length,
      message: "Students have not submitted both checks yet."
    };
  }
  const preAverage = Math.round(preScores.reduce((sum, value) => sum + value, 0) / preScores.length);
  const postAverage = Math.round(postScores.reduce((sum, value) => sum + value, 0) / postScores.length);
  return {
    ready: true,
    preAverage,
    postAverage,
    growth: postAverage - preAverage,
    preCount: preScores.length,
    postCount: postScores.length
  };
}

function getMappedEvaluationLessonIds(selectedGrades = new Set()) {
  const evaluation = normalizeTeacherEvaluationData(teacherState.evaluation);
  const preIds = new Set();
  const postIds = new Set();
  evaluation.summaries.forEach((summary) => {
    if (selectedGrades.size && !selectedGrades.has(summary.grade_level)) return;
    const preId = summary.cycle?.pretest_lesson_id || summary.pretest_lesson?.id || "";
    const postId = summary.cycle?.posttest_lesson_id || summary.posttest_lesson?.id || "";
    if (preId) preIds.add(preId);
    if (postId) postIds.add(postId);
  });
  return {
    preIds,
    postIds,
    hasMapping: Boolean(preIds.size || postIds.size)
  };
}

function buildEvidenceReadiness({ progress, latestAttempts }) {
  return [
    {
      label: "Lesson Progress",
      status: progress.length ? "ready" : "pending",
      detail: progress.length ? `${progress.length} progress record${progress.length === 1 ? "" : "s"} available` : "Students have not started lessons yet"
    },
    {
      label: "Submitted Checks",
      status: latestAttempts.length ? "ready" : "pending",
      detail: latestAttempts.length ? `${latestAttempts.length} submitted check${latestAttempts.length === 1 ? "" : "s"}` : "Students have not submitted checks yet"
    }
  ];
}

function renderEvidenceMasteryList(evidence) {
  const container = document.getElementById("evidenceMasteryList");
  if (!container) return;
  if (!evidence.topicMastery.length) {
    container.innerHTML = `<p class="empty-text">No active-term modules are available for the selected class.</p>`;
    return;
  }
  const averageMastery = Math.round(evidence.topicMastery.reduce((sum, topic) => sum + topic.mastery, 0) / evidence.topicMastery.length);
  const masteryTone = averageMastery >= 75 ? "good" : "warn";
  const topicRows = evidence.topicMastery.slice(0, 6).map((topic) => `
    <article>
      <span class="evidence-topic-icon"><i data-lucide="${escapeAttribute(topic.icon)}"></i></span>
      <div>
        <strong>${escapeHtml(topic.title)}</strong>
        <small>${escapeHtml(topic.category || topic.grade_level)} - ${escapeHtml(topic.source)}</small>
        <div class="wide-progress"><span style="width:${topic.mastery}%"></span></div>
      </div>
      <b class="${topic.mastery < 75 ? "warn-text" : "good-text"}">${topic.mastery}%</b>
    </article>
  `).join("");
  container.innerHTML = `
    <div class="evidence-mastery-overview">
      <div class="mastery-ring ${masteryTone}" style="--mastery:${averageMastery}">
        <strong>${averageMastery}%</strong>
        <span>Avg</span>
      </div>
      <div>
        <strong>Class mastery snapshot</strong>
        <span>${evidence.topicMastery.length} active module${evidence.topicMastery.length === 1 ? "" : "s"} measured from real progress and submitted checks.</span>
      </div>
    </div>
    ${topicRows}
  `;
}

function renderEvidencePrePost(evidence) {
  const container = document.getElementById("evidencePrePost");
  if (!container) return;
  const growth = evidence.prePost;
  if (!growth.ready) {
    container.innerHTML = `
      <div class="evidence-empty">
        <i data-lucide="trending-up"></i>
        <strong>No test results yet</strong>
        <span>${escapeHtml(growth.message)}</span>
        <small>Pre-test submissions: ${growth.preCount} - Post/final submissions: ${growth.postCount}</small>
        ${growth.setupMissing ? `<button class="small-button" type="button" data-jump-view="evaluation"><i data-lucide="settings-2"></i><span>Open Evaluation</span></button>` : ""}
      </div>
    `;
    return;
  }
  const deltaClass = growth.growth >= 0 ? "good-text" : "warn-text";
  const preY = Math.max(12, Math.min(108, 120 - growth.preAverage));
  const postY = Math.max(12, Math.min(108, 120 - growth.postAverage));
  container.innerHTML = `
    <div class="evidence-line-chart">
      <svg viewBox="0 0 220 120" role="img" aria-label="Pre-test average ${growth.preAverage} percent and post-test average ${growth.postAverage} percent">
        <title>Pre-test ${growth.preAverage}% to post-test ${growth.postAverage}%</title>
        <path class="chart-grid-line" d="M12 96H208M12 60H208M12 24H208"></path>
        <path class="chart-area" d="M28 ${preY} L192 ${postY} L192 108 L28 108 Z"></path>
        <path class="chart-line" pathLength="1" d="M28 ${preY} L192 ${postY}"></path>
        <circle class="chart-point pre" cx="28" cy="${preY}" r="4"></circle>
        <circle class="chart-point post" cx="192" cy="${postY}" r="4"></circle>
      </svg>
    </div>
    <div class="evidence-growth-score">
      <div><span>Pre-test</span><strong>${growth.preAverage}%</strong></div>
      <i data-lucide="arrow-right"></i>
      <div><span>Post/final</span><strong>${growth.postAverage}%</strong></div>
    </div>
    <div class="evidence-growth-bar">
      <span style="width:${Math.max(0, Math.min(100, growth.preAverage))}%"></span>
      <span class="post" style="width:${Math.max(0, Math.min(100, growth.postAverage))}%"></span>
    </div>
    <p class="${deltaClass}">${growth.growth >= 0 ? "+" : ""}${growth.growth}% growth from ${growth.preCount + growth.postCount} submissions</p>
  `;
}

function renderEvidenceInterventions(evidence) {
  const container = document.getElementById("evidenceInterventionList");
  if (!container) return;
  if (!evidence.interventions.length) {
    container.innerHTML = `<p class="empty-text">No intervention signals for the selected class right now.</p>`;
    return;
  }
  container.innerHTML = evidence.interventions.slice(0, 6).map((item) => `
    <article>
      <div class="mini-avatar">${escapeHtml(getInitials(item.student.full_name || item.student.username))}</div>
      <div>
        <strong>${escapeHtml(item.student.full_name || item.student.username || "Student")}</strong>
        <span>${escapeHtml(item.student.grade_level || "-")} ${escapeHtml(item.student.section || "")}</span>
        <small>${escapeHtml(item.reasons.join(" - "))}</small>
      </div>
      <b>${item.latestScore === null ? `${item.completionRate}%` : `${item.latestScore}%`}</b>
    </article>
  `).join("");
}

function renderEvidenceReadiness(evidence) {
  const container = document.getElementById("evidenceReadiness");
  if (!container) return;
  container.innerHTML = evidence.readiness.map((item) => `
    <article class="${item.status}">
      <span><i data-lucide="${item.status === "ready" ? "check-circle-2" : "clock-3"}"></i></span>
      <div><strong>${escapeHtml(item.label)} ${item.status === "ready" ? "Ready" : "Pending"}</strong><small>${escapeHtml(item.detail)}</small></div>
    </article>
  `).join("");
}

function handleEvidenceDownload(event) {
  const jumpButton = event.target.closest("[data-jump-view]");
  if (jumpButton) {
    showTeacherView(jumpButton.dataset.jumpView);
    return;
  }
  const button = event.target.closest("[data-evidence-download]");
  if (!button) return;
  const format = button.dataset.evidenceDownload;
  const evidence = buildCapstoneEvidenceDataset();
  const stamp = new Date().toISOString().slice(0, 10);
  const filename = `techwise-class-results-${stamp}`;
  if (format === "pdf") {
    downloadBlob(`${filename}.pdf`, buildEvidencePdf(evidence), "application/pdf");
    return;
  }
  downloadTextFile(`${filename}.csv`, rowsToCsv(buildEvidenceCsvRows(evidence)), "text/csv;charset=utf-8");
}

function buildEvidencePdf(evidence) {
  const teacherName = document.getElementById("teacherName")?.textContent || "ICT Teacher";
  const generatedAt = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(new Date());
  return buildSimplePdf([
    "TechWise 360",
    "Class Results Dashboard",
    `Teacher: ${teacherName}`,
    `Generated: ${generatedAt}`,
    `Filter: ${evidence.filterLabel}`,
    `Term: ${evidence.activeQuarterLabel}`,
    "",
    ...buildEvidencePdfLines(evidence),
    "",
    "Generated by TechWise 360"
  ]);
}

function buildEvidencePdfLines(evidence) {
  const prePostLine = evidence.prePost.ready
    ? `Pre/Post Test Progress: ${evidence.prePost.preAverage}% to ${evidence.prePost.postAverage}% (${evidence.prePost.growth >= 0 ? "+" : ""}${evidence.prePost.growth}%)`
    : `Pre/Post Test Progress: Waiting (${evidence.prePost.preCount} pre-test, ${evidence.prePost.postCount} post/final submissions)`;
  return [
    "Class Snapshot",
    `Approved Students: ${evidence.students.length}`,
    `Completion Rate: ${evidence.metrics.completionRate}%`,
    `Average Score: ${evidence.metrics.averageScore === null ? "-" : `${evidence.metrics.averageScore}%`}`,
    `Submission Rate: ${evidence.metrics.submissionRate}%`,
    `Certificates: ${evidence.metrics.certificates}`,
    `Needs Support: ${evidence.interventions.length}`,
    "",
    "Class Mastery",
    ...(evidence.topicMastery.length ? evidence.topicMastery.slice(0, 8).map((topic) =>
      `${topic.title}: ${topic.mastery}% (${topic.source})`
    ) : ["No active-term module records for this filter."]),
    "",
    prePostLine,
    "",
    "Intervention Queue",
    ...(evidence.interventions.length ? evidence.interventions.slice(0, 8).map((item) =>
      `${item.student.full_name || item.student.username || "Student"}: ${item.reasons.join("; ")}`
    ) : ["No intervention signals for this filter."]),
    "",
    "Setup Checklist",
    ...evidence.readiness.map((item) => `${item.label}: ${titleCase(item.status)} - ${item.detail}`)
  ];
}

function buildEvidenceCsvRows(evidence) {
  return [
    ["TechWise 360 Class Results Dashboard"],
    ["Filter", evidence.filterLabel],
    ["Term", evidence.activeQuarterLabel],
    [],
    ["Metric", "Value"],
    ["Approved Students", evidence.students.length],
    ["Completion Rate", `${evidence.metrics.completionRate}%`],
    ["Average Score", evidence.metrics.averageScore === null ? "-" : `${evidence.metrics.averageScore}%`],
    ["Submission Rate", `${evidence.metrics.submissionRate}%`],
    ["Certificates", evidence.metrics.certificates],
    ["Needs Support", evidence.interventions.length],
    [],
    ["Class Mastery", "Mastery", "Source", "Record Count"],
    ...evidence.topicMastery.map((topic) => [topic.title, `${topic.mastery}%`, topic.source, topic.evidence_count]),
    [],
    ["Intervention Student", "Grade", "Section", "Completion", "Latest Score", "Reasons", "Last Active"],
    ...evidence.interventions.map((item) => [
      item.student.full_name || item.student.username || "Student",
      item.student.grade_level || "",
      item.student.section || "",
      `${item.completionRate}%`,
      item.latestScore === null ? "-" : `${item.latestScore}%`,
      item.reasons.join("; "),
      item.lastActive ? formatDateOnly(item.lastActive) : "No activity"
    ]),
    [],
    ["Setup Checklist", "Status", "Detail"],
    ...evidence.readiness.map((item) => [item.label, titleCase(item.status), item.detail])
  ];
}

function evidenceFilterLabel(classKey = "") {
  const option = evidenceClassOptions().find((item) => item.value === classKey);
  return option?.label || "All Classes";
}

function renderTeacherStudents() {
  const table = document.getElementById("studentsTable");
  const attention = document.getElementById("attentionList");
  const search = (document.getElementById("studentSearch")?.value || "").toLowerCase();
  const status = document.getElementById("studentStatusFilter")?.value || "";
  const support = document.getElementById("studentSupportFilter")?.value || "";
  const normalizedClassFilter = normalizeGradeSectionFilter({
    grade: teacherState.selectedGrade,
    section: teacherState.selectedSection
  });
  teacherState.selectedGrade = normalizedClassFilter.grade;
  teacherState.selectedSection = normalizedClassFilter.section;
  syncGradeSectionSelect("studentGradeFilter", "studentSectionFilter", normalizedClassFilter);

  const allRows = teacherState.students
    .map((student) => getTeacherStudentSummary(student))
    .filter((summary) => {
      const student = summary.student;
      const haystack = `${student.full_name} ${student.email} ${student.username}`.toLowerCase();
      if (search && !haystack.includes(search)) return false;
      if (normalizedClassFilter.grade && student.grade_level !== normalizedClassFilter.grade) return false;
      if (normalizedClassFilter.section && student.section !== normalizedClassFilter.section) return false;
      if (status === "support") return summary.progressStatus === "Needs Support";
      if (status && status !== "support" && student.status !== status) return false;
      if (support && summary.performanceBand !== support) return false;
      return true;
    });

  const pageCount = Math.max(1, Math.ceil(allRows.length / STUDENTS_PER_PAGE));
  teacherState.studentPage = Math.min(Math.max(teacherState.studentPage || 1, 1), pageCount);
  const start = (teacherState.studentPage - 1) * STUDENTS_PER_PAGE;
  const rows = allRows.slice(start, start + STUDENTS_PER_PAGE);

  if (!rows.length) {
    table.innerHTML = `<tr><td colspan="8" class="empty-cell">No students match the current filters.</td></tr>`;
  } else {
    table.innerHTML = rows.map((summary) => {
      const student = summary.student;
      return `
      <tr class="student-data-row" data-student-row data-id="${escapeHtml(student.id)}" tabindex="0" aria-label="Open ${escapeHtml(student.full_name || student.username)} details">
        <td><div class="student-name-cell"><i data-lucide="user-round"></i><strong>${escapeHtml(student.full_name || student.username)}</strong></div></td>
        <td><span class="grade-chip">${escapeHtml(student.grade_level || "-")}</span></td>
        <td><strong>${summary.completed}/${summary.total}</strong><div class="mini-progress"><span style="width:${summary.progressPercent}%"></span></div></td>
        <td class="${summary.average < 75 ? "warn-text" : "good-text"}">${summary.average}%</td>
        <td>${renderRewardIcons(summary.badges, "badge")}</td>
        <td>${renderCertificateCount(summary.certificates)}</td>
        <td>${escapeHtml(summary.lastActive)}</td>
        <td><span class="status-pill ${summary.statusClass}">${escapeHtml(student.status === "approved" ? summary.progressStatus : titleCase(student.status))}</span></td>
      </tr>
    `;
    }).join("");
  }

  const needsSupport = teacherState.students
    .map((student) => getTeacherStudentSummary(student))
    .filter((summary) => summary.student.status === "approved" && summary.progressStatus === "Needs Support")
    .sort((a, b) => a.average - b.average)
    .slice(0, 4);

  attention.innerHTML = needsSupport.length ? needsSupport.map((summary) => `
    <article class="attention-row">
      <i data-lucide="user-round"></i>
      <div><strong>${escapeHtml(summary.student.full_name || summary.student.username)}</strong><span>${escapeHtml(summary.student.grade_level || "-")} &nbsp; ${summary.completed}/${summary.total} lessons &nbsp; ${escapeHtml(summary.lastActive)}</span></div>
      <b>${summary.average}%</b>
    </article>
  `).join("") : `<p class="empty-text">No students need attention right now.</p>`;

  renderStudentAchievementCoverage(allRows);
  renderProgressDistribution();
  renderStudentPagination(allRows.length, start, rows.length, pageCount);
  if (window.lucide) window.lucide.createIcons();
}

function syncGradeSectionSelect(gradeId, sectionId, filters) {
  const normalized = normalizeGradeSectionFilter(filters);
  const gradeSelect = document.getElementById(gradeId);
  const sectionSelect = document.getElementById(sectionId);
  if (gradeSelect) {
    gradeSelect.innerHTML = gradeOptionsHtml(normalized.grade, true);
    gradeSelect.value = normalized.grade;
  }
  if (sectionSelect) {
    sectionSelect.innerHTML = sectionOptionsHtml(normalized.grade, normalized.section, true);
    sectionSelect.value = normalized.section;
    sectionSelect.disabled = !normalized.grade;
  }
}

function renderStudentAchievementCoverage(rows) {
  const container = document.getElementById("studentAchievementCoverage");
  if (!container) return;
  if (!rows.length) {
    container.innerHTML = `
      <p class="empty-text">No matching students for the current filters.</p>
      <div class="coverage-action-row">
        <button type="button" data-student-coverage-achievements><i data-lucide="award"></i><span>Open Achievements</span></button>
      </div>
    `;
    return;
  }
  const studentIds = new Set(rows.map((row) => row.student.id));
  const certificates = dedupeAwardRecords(teacherState.certificates || []).filter((award) => studentIds.has(award.student_id));
  const badges = getTeacherBadgeAwards().filter((award) => studentIds.has(award.student_id));
  const awardedStudentIds = new Set([...certificates, ...badges].map((award) => award.student_id).filter(Boolean));
  const latestAward = [
    ...certificates.map((award) => ({ ...award, award_type: "certificate" })),
    ...badges.map((award) => ({ ...award, award_type: "badge" }))
  ].sort((a, b) => new Date(getAwardDate(b) || 0) - new Date(getAwardDate(a) || 0))[0] || null;
  const latestStudent = latestAward ? rows.find((row) => row.student.id === latestAward.student_id)?.student : null;
  const profileStudent = latestStudent || rows[0]?.student;
  const noAwardCount = rows.filter((row) => !awardedStudentIds.has(row.student.id)).length;
  const certificateStudentCount = new Set(certificates.map((award) => award.student_id).filter(Boolean)).size;
  const badgeStudentCount = new Set(badges.map((award) => award.student_id).filter(Boolean)).size;
  const isCertificate = latestAward?.award_type === "certificate" || Boolean(latestAward?.certificate_key);

  container.innerHTML = `
    <div class="coverage-stat-grid">
      <article><strong>${certificateStudentCount}</strong><span>With certificates</span></article>
      <article><strong>${badgeStudentCount}</strong><span>With badges</span></article>
      <article><strong>${noAwardCount}</strong><span>No awards yet</span></article>
    </div>
    <div class="coverage-latest-award">
      <span class="${latestAward ? (isCertificate ? "certificate" : "badge") : "empty"}"><i data-lucide="${latestAward ? (isCertificate ? "file-badge" : "shield-check") : "award"}"></i></span>
      <div>
        <strong>${escapeHtml(latestAward?.title || "No awards yet")}</strong>
        <small>${latestAward ? `${escapeHtml(latestStudent?.full_name || latestStudent?.username || "Student")} - ${escapeHtml(formatDateOnly(getAwardDate(latestAward)))}` : "Award activity will appear here."}</small>
      </div>
    </div>
    <div class="coverage-action-row">
      <button type="button" data-student-coverage-achievements><i data-lucide="award"></i><span>Achievements</span></button>
      ${profileStudent ? `<button type="button" data-student-coverage-profile="${escapeAttribute(profileStudent.id)}"><i data-lucide="user-round"></i><span>Profile</span></button>` : ""}
    </div>
  `;
}

function renderRewardIcons(rewards) {
  if (!rewards.length) return `<span class="reward-empty">-</span>`;
  return `<span class="reward-icons">${rewards.slice(0, 3).map((reward) =>
    `<i class="reward-icon ${escapeHtml(reward.color || "blue")}" data-lucide="shield-check" title="${escapeHtml(formatAwardDisplayTitle(reward.title, "Badge"))}"></i>`
  ).join("")}${rewards.length > 3 ? `<small>+${rewards.length - 3}</small>` : ""}</span>`;
}

function renderCertificateCount(certificates) {
  const count = certificates.length;
  return `<span class="certificate-count">${count}<i data-lucide="${count ? "award" : "badge"}"></i></span>`;
}

function renderProgressDistribution() {
  const summaries = teacherState.students
    .filter((student) => student.status === "approved")
    .map((student) => getTeacherStudentSummary(student));
  const buckets = {
    excellent: summaries.filter((summary) => summary.average >= 90).length,
    good: summaries.filter((summary) => summary.average >= 75 && summary.average < 90).length,
    fair: summaries.filter((summary) => summary.average >= 60 && summary.average < 75).length,
    needs: summaries.filter((summary) => summary.average < 60).length
  };
  const total = summaries.length;
  const excellentDeg = percentOf(buckets.excellent, total) * 3.6;
  const goodDeg = excellentDeg + percentOf(buckets.good, total) * 3.6;
  const fairDeg = goodDeg + percentOf(buckets.fair, total) * 3.6;
  const donut = document.getElementById("progressDistributionDonut");
  const legend = document.getElementById("distributionLegend");

  setText("distributionTotal", total);
  if (donut) {
    donut.classList.toggle("empty", total === 0);
    donut.style.setProperty("--excellent", `${excellentDeg}deg`);
    donut.style.setProperty("--good", `${goodDeg}deg`);
    donut.style.setProperty("--fair", `${fairDeg}deg`);
  }
  if (legend) {
    legend.innerHTML = [
      ["excellent", "Excellent (90-100%)", buckets.excellent],
      ["good", "Good (75-89%)", buckets.good],
      ["fair", "Fair (60-74%)", buckets.fair],
      ["needs", "Needs Improvement (<60%)", buckets.needs]
    ].map(([key, label, count]) => `
      <div><span class="legend-dot ${key}"></span><p>${label}</p><strong>${count} (${percentOf(count, total)}%)</strong></div>
    `).join("");
  }
}

function renderStudentPagination(total, start, visible, pageCount) {
  const summary = document.getElementById("studentPageSummary");
  const pagination = document.getElementById("studentPagination");
  if (summary) {
    const from = total ? start + 1 : 0;
    const to = start + visible;
    summary.textContent = `Showing ${from} to ${to} of ${total} students`;
  }
  if (!pagination) return;

  pagination.innerHTML = renderDashboardPagination({
    currentPage: teacherState.studentPage,
    pageCount,
    pageNumberAttribute: "data-page-number",
    label: "Student list pages"
  });
}

function renderQuarters() {
  const list = document.getElementById("quartersList");
  if (!teacherState.quarters.length) {
    list.innerHTML = `<p class="empty-text">No terms yet. Create 1st Term to begin.</p>`;
    return;
  }

  list.innerHTML = teacherState.quarters.map((quarter) => {
    const moduleCount = teacherState.modules.filter((module) => module.quarter_id === quarter.id).length;
    const lessonCount = teacherState.lessons.filter((lesson) => lesson.quarter_id === quarter.id).length;
    const isArchived = quarter.status === "archived";
    return `
      <article class="quarter-card ${quarter.is_active ? "active" : ""} ${isArchived ? "archived" : ""}">
        <div class="quarter-card-header">
          <span>${escapeHtml(getTermLabel(quarter))}</span>
          <small class="status-pill ${isArchived ? "archived" : quarter.is_active ? "published" : "draft"}">${escapeHtml(isArchived ? "Archived" : quarter.is_active ? "Active" : "Inactive")}</small>
        </div>
        <strong>${escapeHtml(quarter.title)}</strong>
        <p>${escapeHtml(quarter.school_year)} &middot; ${moduleCount} modules &middot; ${lessonCount} lessons</p>
        <div class="quarter-actions">
          <button class="small-button" type="button" data-quarter-active="${escapeAttribute(quarter.id)}" ${quarter.is_active || isArchived ? "disabled" : ""}>
            <i data-lucide="check-circle-2"></i><span>${quarter.is_active ? "Active" : "Set Active"}</span>
          </button>
          <button class="small-button" type="button" data-quarter-edit="${escapeAttribute(quarter.id)}">
            <i data-lucide="pencil"></i><span>Edit</span>
          </button>
          <button class="small-button" type="button" data-quarter-archive="${escapeAttribute(quarter.id)}" data-status="${isArchived ? "active" : "archived"}">
            <i data-lucide="${isArchived ? "archive-restore" : "archive"}"></i><span>${isArchived ? "Restore" : "Archive"}</span>
          </button>
          <button class="small-button danger" type="button" data-quarter-delete="${escapeAttribute(quarter.id)}">
            <i data-lucide="trash-2"></i><span>Delete</span>
          </button>
        </div>
      </article>
    `;
  }).join("");
}

function getTermLabel(term) {
  const labels = { T1: "1st Term", T2: "2nd Term", T3: "3rd Term" };
  return labels[term?.name] || term?.title || "Term";
}

function renderContentSelects() {
  const moduleOptions = teacherState.modules.map((module) =>
    `<option value="${escapeHtml(module.id)}">${escapeHtml(module.title)} - ${escapeHtml(module.grade_level)}</option>`
  ).join("");
  const categories = [...new Set(teacherState.modules.map((module) => module.category).filter(Boolean))].sort();
  const categoryOptions = `<option value="">All Categories</option>${categories.map((category) =>
    `<option value="${escapeHtml(category)}">${escapeHtml(category)}</option>`
  ).join("")}`;

  const lessonModuleSelect = document.getElementById("lessonModuleSelect");
  const categorySelect = document.getElementById("lessonCategoryFilter");
  if (lessonModuleSelect) lessonModuleSelect.innerHTML = moduleOptions || `<option value="">Create a module first</option>`;
  if (categorySelect) categorySelect.innerHTML = categoryOptions;
}

function renderLessonMetrics() {
  const total = teacherState.lessons.length;
  const publishedLessons = teacherState.lessons.filter((lesson) => lesson.status === "published").length;
  const draftLessons = teacherState.lessons.filter((lesson) => lesson.status === "draft").length;
  const publishedModules = teacherState.modules.filter((module) => module.status === "published").length;
  const practice = teacherState.lessons.filter((lesson) => lesson.lesson_type === "practice").length;
  const categories = new Set(teacherState.modules.map((module) => module.category).filter(Boolean));

  setText("lessonMetricTotal", total);
  setText("lessonMetricCategories", categories.size);
  setText("lessonMetricPublished", publishedModules);
  setText("lessonMetricPublishedPct", `${percentOf(publishedLessons, total)}%`);
  setText("lessonMetricDraft", draftLessons);
  setText("lessonMetricPractice", practice);
}

function renderLessonLibrary() {
  const table = document.getElementById("lessonsTable");
  if (!table) return;
  const search = (document.getElementById("contentSearch")?.value || "").toLowerCase();
  const status = document.getElementById("contentStatusFilter")?.value || "";
  const category = document.getElementById("lessonCategoryFilter")?.value || "";
  const grade = document.getElementById("lessonGradeFilter")?.value || "";
  const modulesById = new Map(teacherState.modules.map((module) => [module.id, module]));

  const rows = buildLessonLibraryRows().filter((row) => {
    const module = row.module;
    const haystack = row.type === "package"
      ? `${module?.title || ""} ${module?.description || ""} ${module?.category || ""} ${row.lessons.map((lesson) => `${lesson.title} ${lesson.description || ""}`).join(" ")}`.toLowerCase()
      : `${row.lesson.title} ${row.lesson.description || ""} ${module?.title || ""} ${module?.category || ""}`.toLowerCase();
    if (search && !haystack.includes(search)) return false;
    if (category && module?.category !== category) return false;
    if (grade && module?.grade_level !== grade) return false;
    if (status && row.status !== status) return false;
    return true;
  });

  const pageCount = Math.max(1, Math.ceil(rows.length / LESSONS_PER_PAGE));
  teacherState.lessonPage = Math.min(Math.max(teacherState.lessonPage || 1, 1), pageCount);
  const start = (teacherState.lessonPage - 1) * LESSONS_PER_PAGE;
  const visibleRows = rows.slice(start, start + LESSONS_PER_PAGE);
  table.classList.toggle("compact", teacherState.compactLessons);

  if (!rows.length) {
    table.innerHTML = `<p class="empty-text">No lessons match the current filters.</p>`;
    renderLessonPagination(0, 0, 0, 1);
    return;
  }

  table.innerHTML = visibleRows.map((row) => {
    if (row.type === "package") {
      const firstLesson = row.lessons[0];
      const lessonCount = row.lessons.filter((lesson) => lesson.lesson_type !== "practice").length;
      const practiceCount = row.lessons.filter((lesson) => lesson.lesson_type === "practice").length;
      return `
        <article class="lesson-row package-row">
          <div class="lesson-title-cell">
            <span class="lesson-type-icon ${escapeHtml(moduleCategoryClass(row.module?.category))}"><i data-lucide="layers-3"></i></span>
            <div>
              <strong>${escapeHtml(row.module?.title || "Lesson Package")}</strong>
              <small>${escapeHtml(row.lessons.length)} parts - ${lessonCount} lessons - ${practiceCount} practices</small>
            </div>
          </div>
          <div><span class="category-pill ${escapeHtml(moduleCategoryClass(row.module?.category))}">${escapeHtml(row.module?.category || "General")}</span></div>
          <div>${escapeHtml(row.module?.grade_level || "-")}</div>
          <div class="duration-cell"><i data-lucide="clock"></i>${row.duration} min</div>
          <div><span class="status-pill ${row.status}">${escapeHtml(titleCase(row.status))}</span></div>
          <div class="lesson-actions">
            <button class="small-button" type="button" data-preview-lesson="${escapeHtml(firstLesson?.id || "")}"><i data-lucide="eye"></i><span>Preview</span></button>
            <button class="small-button" type="button" data-edit-lesson="${escapeHtml(firstLesson?.id || "")}"><i data-lucide="pencil"></i><span>Edit First Part</span></button>
          </div>
        </article>
      `;
    }
    const lesson = row.lesson;
    const module = row.module;
    const nextStatus = lesson.status === "published" ? "draft" : "published";
    return `
      <article class="lesson-row">
        <div class="lesson-title-cell">
          <span class="lesson-type-icon ${escapeHtml(moduleCategoryClass(module?.category))}"><i data-lucide="${escapeHtml(iconForLesson(lesson, module))}"></i></span>
          <div><strong>${escapeHtml(lesson.title)}</strong><small>${escapeHtml(lesson.description || lesson.lesson_type)}</small></div>
        </div>
        <div><span class="category-pill ${escapeHtml(moduleCategoryClass(module?.category))}">${escapeHtml(module?.category || "General")}</span></div>
        <div>${escapeHtml(module?.grade_level || "-")}</div>
        <div class="duration-cell"><i data-lucide="clock"></i>${Number(lesson.duration_minutes || 0)} min</div>
        <div><span class="status-pill ${lesson.status}">${escapeHtml(titleCase(lesson.status))}</span></div>
        <div class="lesson-actions">
          <button class="small-button" type="button" data-edit-lesson="${escapeHtml(lesson.id)}"><i data-lucide="pencil"></i><span>Edit</span></button>
          <button class="small-button" type="button" data-preview-lesson="${escapeHtml(lesson.id)}"><i data-lucide="eye"></i><span>Preview</span></button>
          <button class="small-button ${nextStatus === "published" ? "good-action" : ""}" type="button" data-toggle-lesson="${escapeHtml(lesson.id)}" data-status="${nextStatus}"><i data-lucide="${nextStatus === "published" ? "upload" : "file-pen"}"></i><span>${nextStatus === "published" ? "Publish" : "Draft"}</span></button>
          <button class="icon-button" type="button" data-archive-lesson="${escapeHtml(lesson.id)}" aria-label="Archive lesson"><i data-lucide="more-vertical"></i></button>
        </div>
      </article>
    `;
  }).join("");
  renderLessonPagination(rows.length, start, visibleRows.length, pageCount);
  if (window.lucide) window.lucide.createIcons();
}

function renderAssessmentsDashboard() {
  renderAssessmentFilterOptions();
  syncAssessmentFilterControls();
  renderAssessmentMetrics();
  renderAssessmentTable();
  renderAssessmentTopicScores();
  renderAssessmentRecentSubmissions();
  if (window.lucide) window.lucide.createIcons();
}

function renderAssessmentFilterOptions() {
  const select = document.getElementById("assessmentClassFilter");
  if (!select) return;
  const current = teacherState.assessmentFilters.class || "";
  const classes = [...new Set(teacherState.assessments.map((item) => item.grade_level).filter(Boolean))].sort();
  select.innerHTML = `<option value="">All Classes</option>${classes.map((item) =>
    `<option value="${escapeAttribute(item)}">${escapeHtml(item)}</option>`
  ).join("")}`;
  select.value = classes.includes(current) ? current : "";
  teacherState.assessmentFilters.class = select.value;
}

function syncAssessmentFilterControls() {
  const filters = teacherState.assessmentFilters;
  const classFilter = document.getElementById("assessmentClassFilter");
  const typeFilter = document.getElementById("assessmentTypeFilter");
  const statusFilter = document.getElementById("assessmentStatusFilter");
  const search = document.getElementById("assessmentSearch");
  if (classFilter) classFilter.value = filters.class || "";
  if (typeFilter) typeFilter.value = filters.type || "";
  if (statusFilter) statusFilter.value = filters.status || "";
  if (search) search.value = filters.search || "";
}

function getFilteredAssessments() {
  const filters = teacherState.assessmentFilters;
  const search = String(filters.search || "").trim().toLowerCase();
  return teacherState.assessments.filter((assessment) => {
    const displayStatus = assessmentDisplayStatus(assessment);
    const haystack = `${assessment.title} ${assessment.description || ""} ${assessment.module_title || ""} ${assessment.module_category || ""} ${assessment.grade_level || ""}`.toLowerCase();
    if (filters.class && assessment.grade_level !== filters.class) return false;
    if (filters.type && assessment.lesson_type !== filters.type) return false;
    if (filters.status && displayStatus !== filters.status) return false;
    if (search && !haystack.includes(search)) return false;
    return true;
  });
}

function renderAssessmentMetrics() {
  const assessments = teacherState.assessments;
  const published = assessments.filter((item) => item.status === "published").length;
  const draft = assessments.filter((item) => item.status === "draft").length;
  const assignedSlots = assessments.reduce((sum, item) => sum + Number(item.assigned_count || 0), 0);
  const submittedSlots = assessments.reduce((sum, item) => sum + Number(item.submitted_count || 0), 0);
  setText("assessmentMetricTotal", assessments.length);
  setText("assessmentMetricPublished", published);
  setText("assessmentMetricPublishedPct", `${percentOf(published, assessments.length)}%`);
  setText("assessmentMetricDraft", draft);
  setText("assessmentMetricSubmissionRate", `${percentOf(submittedSlots, assignedSlots)}%`);
}

function renderAssessmentTable() {
  const tbody = document.getElementById("assessmentsTable");
  if (!tbody) return;
  const rows = getFilteredAssessments();
  const pageCount = Math.max(1, Math.ceil(rows.length / ASSESSMENTS_PER_PAGE));
  teacherState.assessmentPage = Math.min(Math.max(teacherState.assessmentPage || 1, 1), pageCount);
  const start = (teacherState.assessmentPage - 1) * ASSESSMENTS_PER_PAGE;
  const visibleRows = rows.slice(start, start + ASSESSMENTS_PER_PAGE);

  if (!rows.length) {
    tbody.innerHTML = `
      <tr><td colspan="8">
        <div class="assessment-empty-state">
          <i data-lucide="clipboard-list"></i>
          <strong>No assessments found</strong>
          <span>Create or publish practice questions in the existing lesson editor, then they will appear here.</span>
          <button class="small-button" type="button" data-jump-view="content"><i data-lucide="book-open"></i><span>Open Lessons</span></button>
        </div>
      </td></tr>
    `;
    renderAssessmentPagination(0, 0, 0, 1);
    return;
  }

  tbody.innerHTML = visibleRows.map((assessment) => {
    const status = assessmentDisplayStatus(assessment);
    const score = Number.isInteger(assessment.average_score) ? `${assessment.average_score}%` : "-";
    const action = assessmentAction(assessment);
    return `
      <tr>
        <td>
          <div class="assessment-title-cell">
            <span class="assessment-row-icon ${assessment.lesson_type === "assessment" ? "purple" : "green"}"><i data-lucide="${assessment.lesson_type === "assessment" ? "clipboard-check" : "flag"}"></i></span>
            <div><strong>${escapeHtml(assessment.title)}</strong><small>${escapeHtml(assessment.grade_level || "Class")} - ${escapeHtml(assessment.module_category || "General")}</small></div>
          </div>
        </td>
        <td>${escapeHtml(assessment.module_title || "-")}</td>
        <td><span class="assessment-type-pill ${escapeHtml(assessment.lesson_type)}">${escapeHtml(titleCase(assessment.lesson_type))}</span></td>
        <td>${assessment.due_date ? escapeHtml(formatDate(assessment.due_date)) : "-"}</td>
        <td>${Number(assessment.submitted_count || 0)}/${Number(assessment.assigned_count || 0)}</td>
        <td><strong class="${score === "-" ? "muted-score" : scoreClass(assessment.average_score)}">${score}</strong></td>
        <td><span class="status-pill ${escapeHtml(status)}">${escapeHtml(titleCase(status))}</span></td>
        <td>
          <div class="assessment-actions">
            <button class="small-button" type="button" ${action.kind === "review" ? `data-review-assessment="${escapeHtml(assessment.id)}"` : `data-edit-assessment="${escapeHtml(assessment.id)}"`}>
              <i data-lucide="${escapeHtml(action.icon)}"></i><span>${escapeHtml(action.label)}</span>
            </button>
            <button class="icon-button" type="button" data-preview-assessment="${escapeHtml(assessment.id)}" aria-label="Preview assessment"><i data-lucide="eye"></i></button>
          </div>
        </td>
      </tr>
    `;
  }).join("");
  renderAssessmentPagination(rows.length, start, visibleRows.length, pageCount);
}

function renderAssessmentPagination(total, start, visible, pageCount) {
  const summary = document.getElementById("assessmentPageSummary");
  const pagination = document.getElementById("assessmentPagination");
  if (summary) {
    const from = total ? start + 1 : 0;
    const to = start + visible;
    summary.textContent = `Showing ${from} to ${to} of ${total} assessments`;
  }
  if (!pagination) return;
  pagination.innerHTML = renderDashboardPagination({
    currentPage: teacherState.assessmentPage,
    pageCount,
    pageNumberAttribute: "data-assessment-page-number",
    label: "Assessment pages"
  });
}

function renderAssessmentTopicScores() {
  const chart = document.getElementById("assessmentTopicScores");
  if (!chart) return;
  const topics = teacherState.topicScores.slice(0, 6);
  if (!topics.length) {
    chart.innerHTML = `<p class="empty-text">No scored submissions yet.</p>`;
    return;
  }
  chart.innerHTML = topics.map((topic) => {
    const value = Number.isInteger(topic.average_score) ? topic.average_score : 0;
    return `
      <article>
        <div class="assessment-bar-track"><span style="height:${value}%"></span></div>
        <strong>${value}%</strong>
        <small>${escapeHtml(shortTopicLabel(topic.module_title))}</small>
      </article>
    `;
  }).join("");
}

function renderAssessmentRecentSubmissions() {
  const list = document.getElementById("assessmentRecentSubmissions");
  if (!list) return;
  const submissions = teacherState.recentSubmissions || [];
  if (!submissions.length) {
    list.innerHTML = `<p class="empty-text">No student submissions yet.</p>`;
    return;
  }
  list.innerHTML = submissions.map((submission) => `
    <button type="button" data-review-assessment="${escapeHtml(submission.lesson_id)}">
      <span><i data-lucide="user-round"></i></span>
      <div><strong>${escapeHtml(submission.student_name || "Student")}</strong><small>${escapeHtml(submission.assessment_title || "Assessment")} - ${escapeHtml(relativeTime(submission.submitted_at))}</small></div>
      <b class="${scoreClass(submission.score_percent)}">${Number(submission.score_percent || 0)}%</b>
    </button>
  `).join("");
}

function renderReportsDashboard() {
  renderReportGradeOptions();
  syncReportFilterControls();
  const report = buildReportDataset();
  teacherState.reports = report;
  syncCompletionFiltersWithReport(report.filters);
  renderReportMetrics(report);
  renderTeacherCompletionDashboard();
  renderReportSupportInsights(report);
  renderReportWeakAreaAnalysis(report);
  renderReportWeeklyActivity(report);
  renderReportTopicMastery(report);
  renderReportCompletionDistribution(report);
  syncReportDownloadPreference();
  if (window.lucide) window.lucide.createIcons();
}

function syncCompletionFiltersWithReport(filters) {
  teacherState.completionFilters = {
    ...(teacherState.completionFilters || {}),
    grade: filters?.grade || "",
    section: filters?.section || ""
  };
}

function syncReportDownloadPreference() {
  const preferred = normalizeTeacherSettings(teacherState.settings).report_export_format || "pdf";
  document.querySelectorAll("[data-report-format]").forEach((button) => {
    button.classList.toggle("preferred", button.dataset.reportFormat === preferred);
  });
}

function renderReportGradeOptions() {
  const select = document.getElementById("reportGradeFilter");
  if (!select) return;
  const current = normalizeGradeSectionFilter(teacherState.reportFilters || {});
  select.innerHTML = gradeOptionsHtml(current.grade, true);
  select.value = current.grade;
  teacherState.reportFilters.grade = current.grade;
  teacherState.reportFilters.section = current.section;
}

function syncReportFilterControls() {
  const filters = normalizeReportFilters(teacherState.reportFilters);
  teacherState.reportFilters = filters;
  const start = document.getElementById("reportStartDate");
  const end = document.getElementById("reportEndDate");
  const grade = document.getElementById("reportGradeFilter");
  const section = document.getElementById("reportSectionFilter");
  if (start) start.value = filters.startDate;
  if (end) end.value = filters.endDate;
  if (grade) grade.value = filters.grade || "";
  if (section) {
    section.innerHTML = sectionOptionsHtml(filters.grade, filters.section, true);
    section.value = filters.section || "";
    section.disabled = !filters.grade;
  }
}

function renderReportMetrics(report) {
  setText("reportMetricAverage", report.metrics.averageScore === null ? "-" : `${report.metrics.averageScore}%`);
  setText("reportMetricCompletion", `${report.metrics.completionRate}%`);
  setText("reportMetricSupport", report.metrics.supportCount);
  setText("reportMetricNotStarted", report.metrics.notStartedCount);
  setText("reportMetricCertificates", report.metrics.certificatesIssued);
  setText("reportMetricSubmissions", `${report.metrics.submissionRate}%`);
  setText("reportMetricRange", report.rangeLabel);
}

function renderReportSupportInsights(report) {
  const container = document.getElementById("reportSupportInsights");
  const count = document.getElementById("reportSupportCount");
  if (!container) return;
  const items = report.supportInsights || [];
  if (count) count.textContent = `${items.length} student${items.length === 1 ? "" : "s"}`;
  if (!items.length) {
    container.innerHTML = `
      <div class="report-support-empty">
        <i data-lucide="circle-check-big"></i>
        <strong>No urgent support signals</strong>
        <span>Students in this report range are currently on track based on available progress and scores.</span>
      </div>
    `;
    return;
  }
  container.innerHTML = `
    <div class="report-support-table" role="table" aria-label="Students needing support">
      <div class="report-support-head" role="row">
        <span>Student</span>
        <span>Risk Score</span>
        <span>Key Reasons</span>
        <span>Diagnosis</span>
        <span>Recommended Action</span>
        <span>Risk Level</span>
      </div>
      ${items.slice(0, 8).map((item) => `
        <article class="report-support-row" role="row">
          <button class="report-support-student" type="button" data-report-student-profile="${escapeAttribute(item.student.id)}">
            <span class="report-support-avatar">${escapeHtml(getInitials(item.student.full_name || item.student.username))}</span>
            <span class="report-support-main">
              <strong>${escapeHtml(item.student.full_name || item.student.username || "Student")}</strong>
              <small>${escapeHtml([item.student.grade_level, item.student.section].filter(Boolean).join(" - ") || "No class assigned")}</small>
            </span>
          </button>
          <span class="risk-score-ring ${escapeAttribute(item.riskLevel.key)}" style="--risk:${item.riskScore}%"><b>${item.riskScore}%</b></span>
          <span class="support-reason-stack">
            ${item.reasons.slice(0, 3).map((reason) => `<em class="${escapeAttribute(reason.tone)}">${escapeHtml(reason.label)}</em>`).join("")}
          </span>
          <span class="support-diagnosis">${escapeHtml(item.diagnosis)}</span>
          <button class="support-action-button" type="button" data-report-student-profile="${escapeAttribute(item.student.id)}">
            <i data-lucide="${escapeAttribute(item.primaryAction.icon)}"></i>
            <span>${escapeHtml(item.primaryAction.label)}</span>
          </button>
          <span class="risk-level-pill ${escapeAttribute(item.riskLevel.key)}">${escapeHtml(item.riskLevel.label)}</span>
        </article>
      `).join("")}
    </div>
    <p class="report-risk-note"><i data-lucide="info"></i><span>Risk scores are calculated from assessments, completion, submission history, and recent activity.</span></p>
  `;
}

function renderReportWeeklyActivity(report) {
  const chart = document.getElementById("reportWeeklyActivity");
  if (!chart) return;
  if (!report.dailyScores.some((day) => day.submissions > 0)) {
    chart.innerHTML = `
      <div class="report-empty-state">
        <i data-lucide="bar-chart-3"></i>
        <strong>No scored submissions in this range</strong>
        <span>Student practice or assessment submissions will appear here.</span>
      </div>
    `;
    return;
  }
  const width = 760;
  const height = 250;
  const chartPaddingX = 72;
  const plotWidth = width - chartPaddingX * 2;
  const points = report.dailyScores.map((day, index) => {
    const x = report.dailyScores.length === 1 ? width / 2 : chartPaddingX + (index / (report.dailyScores.length - 1)) * plotWidth;
    const y = height - ((day.average || 0) / 100) * (height - 24) - 12;
    return { ...day, x, y };
  });
  const area = `M ${points[0].x} ${height} L ${points.map((point) => `${point.x} ${point.y}`).join(" L ")} L ${points[points.length - 1].x} ${height} Z`;
  const line = points.map((point) => `${point.x},${point.y}`).join(" ");
  chart.innerHTML = `
    <svg viewBox="0 0 ${width} ${height + 52}" role="img" aria-label="Weekly average scores">
      <g class="report-grid-lines">
        ${[0, 25, 50, 75, 100].map((value) => {
          const y = height - (value / 100) * (height - 24) - 12;
          return `<line x1="${chartPaddingX}" y1="${y}" x2="${width - chartPaddingX}" y2="${y}"></line><text x="${chartPaddingX}" y="${y - 6}">${value}</text>`;
        }).join("")}
      </g>
      <path class="report-area" d="${area}"></path>
      <polyline class="report-line" points="${line}"></polyline>
      ${points.map((point) => `
        <g class="report-point">
          <circle cx="${point.x}" cy="${point.y}" r="8"></circle>
          <text x="${point.x}" y="${point.y - 14}">${point.average || 0}%</text>
          <text class="report-axis-label" x="${point.x}" y="${height + 34}">${escapeHtml(point.shortLabel)}</text>
        </g>
      `).join("")}
    </svg>
  `;
}

function renderReportTopicMastery(report) {
  const chart = document.getElementById("reportTopicMastery");
  if (!chart) return;
  if (!report.topicScores.length) {
    chart.innerHTML = `<p class="empty-text">No topic scores in this range.</p>`;
    return;
  }
  chart.innerHTML = report.topicScores.slice(0, 5).map((topic) => `
    <article>
      <div class="report-topic-bar"><span style="height:${Number(topic.average_score || 0)}%"></span></div>
      <strong>${Number(topic.average_score || 0)}%</strong>
      <small>${escapeHtml(shortTopicLabel(topic.module_title))}</small>
    </article>
  `).join("");
}

function renderReportCompletionDistribution(report) {
  const donut = document.getElementById("reportCompletionDonut");
  const legend = document.getElementById("reportCompletionLegend");
  if (!donut || !legend) return;
  const items = report.completionDistribution;
  const total = report.students.length;
  let cursor = 0;
  const colors = {
    completed: "#09a957",
    inProgress: "#1f7bf2",
    notStarted: "#ffb020",
    overdue: "#7b42f5"
  };
  const slices = items.map((item) => {
    const start = cursor;
    cursor += total ? (item.count / total) * 100 : 0;
    return `${colors[item.key]} ${start}% ${cursor}%`;
  }).join(", ");
  donut.style.background = total ? `conic-gradient(${slices})` : "#edf2fa";
  donut.innerHTML = `<strong>${total}</strong><span>Students</span>`;
  legend.innerHTML = items.map((item) => `
    <span><b style="background:${colors[item.key]}"></b>${escapeHtml(item.label)} <strong>${percentOf(item.count, total)}%</strong><small>${item.count}</small></span>
  `).join("");
}

function buildReportDataset(filtersOverride = null) {
  const filters = normalizeReportFilters(filtersOverride || teacherState.reportFilters);
  const startDate = startOfLocalDay(filters.startDate);
  const endDate = endOfLocalDay(filters.endDate);
  const students = filterStudentsByGradeSection(
    teacherState.students.filter((student) => student.status === "approved"),
    filters
  );
  const studentIds = new Set(students.map((student) => student.id));
  const assessments = teacherState.assessments.filter((assessment) =>
    !filters.grade || assessment.grade_level === filters.grade
  );
  const attempts = assessments.flatMap((assessment) => {
    const rows = Array.isArray(assessment.all_attempts) ? assessment.all_attempts : assessment.attempts || [];
    return rows
      .filter((attempt) => studentIds.has(attempt.student_id))
      .map((attempt) => ({
        ...attempt,
        assessment_title: assessment.title,
        module_id: assessment.module_id,
        module_title: assessment.module_title,
        module_category: assessment.module_category,
        grade_level: assessment.grade_level,
        lesson_type: assessment.lesson_type
      }));
  });
  const rangeAttempts = attempts.filter((attempt) => isDateInRange(attempt.submitted_at, startDate, endDate));
  const scoreValues = rangeAttempts.map((attempt) => Number(attempt.score_percent)).filter(Number.isFinite);
  const uniqueSubmitted = new Set(rangeAttempts.map((attempt) => `${attempt.lesson_id}-${attempt.student_id}`));
  const assignedSlots = assessments.reduce((sum, assessment) => sum + Number(assessment.assigned_count || 0), 0);
  const certificates = teacherState.certificates.filter((certificate) =>
    studentIds.has(certificate.student_id) && isDateInRange(certificate.awarded_at, startDate, endDate)
  );
  const dailyScores = buildDailyScores(startDate, endDate, rangeAttempts);
  const topicScores = buildReportTopicScores(rangeAttempts);
  const studentSummaries = students.map((student) => buildReportStudentSummary(student));
  const completionDistribution = buildCompletionDistribution(studentSummaries);
  const completionCounts = completionDistribution.reduce((counts, item) => {
    counts[item.key] = item.count;
    return counts;
  }, {});
  const analyticsContext = buildTeacherAnalyticsContext({
    students,
    assessments,
    attempts: rangeAttempts,
    progress: teacherState.progress,
    modules: teacherState.modules,
    lessons: teacherState.lessons,
    quarterId: teacherState.activeQuarter?.id || "",
    startDate,
    endDate
  });
  const riskAnalyses = students.map((student) => calculateStudentRiskAnalysis(student, analyticsContext));
  const supportInsights = buildReportSupportInsights(riskAnalyses);

  return {
    filters,
    students,
    assessments,
    attempts: rangeAttempts,
    dailyScores,
    topicScores,
    studentSummaries,
    completionDistribution,
    analyticsContext,
    riskAnalyses,
    weakAreas: buildClassWeakAreas(analyticsContext),
    moduleMasteryBreakdown: buildClassModuleMasteryBreakdown(analyticsContext),
    lowestScoringTopics: buildClassLowestScoringTopics(analyticsContext),
    interventions: buildClassInterventions(analyticsContext, riskAnalyses),
    supportInsights,
    completedCount: completionCounts.completed || 0,
    inProgressCount: completionCounts.inProgress || 0,
    notStartedCount: completionCounts.notStarted || 0,
    overdueCount: completionCounts.overdue || 0,
    supportCount: supportInsights.length,
    rangeLabel: `${formatDateOnly(filters.startDate)} - ${formatDateOnly(filters.endDate)}`,
    metrics: {
      averageScore: scoreValues.length ? Math.round(scoreValues.reduce((sum, value) => sum + value, 0) / scoreValues.length) : null,
      completionRate: percentOf(studentSummaries.reduce((sum, item) => sum + item.completedLessons, 0), studentSummaries.reduce((sum, item) => sum + item.totalLessons, 0)),
      supportCount: supportInsights.length,
      notStartedCount: completionCounts.notStarted || 0,
      certificatesIssued: certificates.length,
      submissionRate: percentOf(uniqueSubmitted.size, assignedSlots)
    }
  };
}

function buildReportSupportInsights(riskAnalyses) {
  return riskAnalyses
    .filter((item) => item.riskScore >= 30 || item.reasons.length > 0)
    .sort((a, b) => b.riskScore - a.riskScore
      || (a.analytics.averageScore ?? 101) - (b.analytics.averageScore ?? 101)
      || String(a.student.full_name || a.student.username || "").localeCompare(String(b.student.full_name || b.student.username || "")));
}

function buildDailyScores(startDate, endDate, attempts) {
  const days = [];
  const cursor = new Date(startDate);
  while (cursor <= endDate) {
    const iso = toDateInputValue(cursor);
    const rows = attempts.filter((attempt) => toDateInputValue(new Date(attempt.submitted_at)) === iso);
    const scores = rows.map((attempt) => Number(attempt.score_percent)).filter(Number.isFinite);
    days.push({
      iso,
      label: formatDateOnly(iso),
      shortLabel: new Intl.DateTimeFormat(undefined, { weekday: "short", month: "short", day: "numeric" }).format(cursor),
      average: scores.length ? Math.round(scores.reduce((sum, value) => sum + value, 0) / scores.length) : 0,
      submissions: rows.length
    });
    cursor.setDate(cursor.getDate() + 1);
  }
  return days;
}

function buildReportTopicScores(attempts) {
  const groups = new Map();
  attempts.forEach((attempt) => {
    const key = attempt.module_id || "unknown";
    if (!groups.has(key)) {
      groups.set(key, {
        module_id: key,
        module_title: attempt.module_title || "Module",
        module_category: attempt.module_category || "",
        scores: []
      });
    }
    const score = Number(attempt.score_percent);
    if (Number.isFinite(score)) groups.get(key).scores.push(score);
  });
  return [...groups.values()]
    .map((group) => ({
      module_id: group.module_id,
      module_title: group.module_title,
      module_category: group.module_category,
      average_score: group.scores.length ? Math.round(group.scores.reduce((sum, value) => sum + value, 0) / group.scores.length) : 0,
      submitted_count: group.scores.length
    }))
    .filter((topic) => topic.submitted_count > 0)
    .sort((a, b) => b.average_score - a.average_score);
}

function buildReportStudentSummary(student) {
  const lessonIds = getRelevantLessonIds(student.grade_level);
  const lessons = teacherState.lessons.filter((lesson) => lessonIds.has(lesson.id));
  const progress = teacherState.progress.filter((item) => item.student_id === student.id && lessonIds.has(item.lesson_id));
  const completedLessons = progress.filter((item) => item.status === "completed" || item.progress_percent === 100).length;
  const scores = progress.map((item) => Number(item.score_percent)).filter(Number.isFinite);
  const today = new Date().toISOString().slice(0, 10);
  const completedLessonIds = new Set(progress
    .filter((item) => item.status === "completed" || item.progress_percent === 100)
    .map((item) => item.lesson_id));
  const hasOverdue = lessons.some((lesson) => lesson.due_date && lesson.due_date < today && !completedLessonIds.has(lesson.id));
  const hasProgress = progress.some((item) => Number(item.progress_percent || 0) > 0 || item.status === "in_progress");
  const badges = teacherState.badges.filter((badge) => badge.student_id === student.id);
  const certificates = teacherState.certificates.filter((certificate) => certificate.student_id === student.id);
  const status = hasOverdue
    ? "overdue"
    : lessonIds.size && completedLessons >= lessonIds.size
      ? "completed"
      : hasProgress ? "inProgress" : "notStarted";
  return {
    student,
    totalLessons: lessonIds.size,
    completedLessons,
    averageScore: scores.length ? Math.round(scores.reduce((sum, value) => sum + value, 0) / scores.length) : 0,
    badges: badges.length,
    certificates: certificates.length,
    status
  };
}

function buildCompletionDistribution(summaries) {
  const labels = [
    ["completed", "Completed"],
    ["inProgress", "In Progress"],
    ["notStarted", "Not Started"],
    ["overdue", "Overdue"]
  ];
  return labels.map(([key, label]) => ({
    key,
    label,
    count: summaries.filter((summary) => summary.status === key).length
  }));
}

function renderReportWeakAreaAnalysis(report) {
  const container = document.getElementById("reportWeakAreaAnalysis");
  if (!container) return;
  container.innerHTML = `
    <article class="weak-analysis-card">
      <div class="weak-card-heading">
        <div><h3>Top Skill Gaps</h3><p>Lowest average mastery</p></div>
        <i data-lucide="target"></i>
      </div>
      <div class="weak-progress-list">
        ${report.weakAreas.length ? report.weakAreas.slice(0, 5).map((item) => renderWeakProgressRow(item.label, item.score, `${item.studentCount} student${item.studentCount === 1 ? "" : "s"}`, "score")).join("") : renderWeakEmpty("No skill gaps detected from current records.")}
      </div>
      ${renderWeakLegend()}
    </article>
    <article class="weak-analysis-card">
      <div class="weak-card-heading">
        <div><h3>Module Mastery Breakdown</h3><p>Average mastery by module</p></div>
        <i data-lucide="layout-dashboard"></i>
      </div>
      <div class="weak-progress-list">
        ${report.moduleMasteryBreakdown.length ? report.moduleMasteryBreakdown.slice(0, 5).map((item) => renderWeakProgressRow(item.title, item.score, item.source, "score")).join("") : renderWeakEmpty("No module mastery data is available yet.")}
      </div>
    </article>
    <article class="weak-analysis-card">
      <div class="weak-card-heading">
        <div><h3>Most Missed Topics</h3><p>Based on assessments</p></div>
        <i data-lucide="list-checks"></i>
      </div>
      <div class="missed-topic-list">
        ${report.lowestScoringTopics.length ? report.lowestScoringTopics.slice(0, 5).map((item) => `
          <div>
            <span>${escapeHtml(item.title)}</span>
            <strong class="${escapeAttribute(weakToneForScore(100 - item.missRate))}">${item.missRate}%</strong>
          </div>
        `).join("") : renderWeakEmpty("No submitted assessment scores in this range.")}
      </div>
    </article>
    <article class="weak-analysis-card">
      <div class="weak-card-heading">
        <div><h3>Recommended Intervention</h3><p>Actions to improve class outcomes</p></div>
        <i data-lucide="clipboard-check"></i>
      </div>
      <div class="intervention-list">
        ${report.interventions.length ? report.interventions.map((item) => `
          <article class="${escapeAttribute(item.tone)}">
            <span><i data-lucide="${escapeAttribute(item.icon)}"></i></span>
            <div><strong>${escapeHtml(item.title)}</strong><small>${escapeHtml(item.detail)}</small></div>
          </article>
        `).join("") : renderWeakEmpty("No intervention recommendation is available for the selected data.")}
      </div>
    </article>
  `;
}

function renderWeakProgressRow(label, value, detail, mode = "score") {
  const percent = Math.min(100, Math.max(0, Number(value || 0)));
  const tone = mode === "risk" ? riskToneForScore(percent) : weakToneForScore(percent);
  return `
    <div class="weak-progress-row ${escapeAttribute(tone)}">
      <div><span>${escapeHtml(label)}</span><b>${percent}%</b></div>
      <div class="weak-progress-track"><i style="width:${percent}%"></i></div>
      <small>${escapeHtml(detail)}</small>
    </div>
  `;
}

function renderWeakLegend() {
  return `
    <div class="weak-legend">
      <span><b class="critical"></b>Critical (0-30%)</span>
      <span><b class="attention"></b>Needs Attention (31-60%)</span>
      <span><b class="improving"></b>Improving (61-100%)</span>
    </div>
  `;
}

function renderWeakEmpty(message) {
  return `<p class="weak-empty">${escapeHtml(message)}</p>`;
}

function buildTeacherAnalyticsContext({
  students = [],
  assessments = [],
  attempts = [],
  progress = [],
  modules = [],
  lessons = [],
  quarterId = "",
  startDate = null,
  endDate = null
} = {}) {
  const studentIds = new Set(students.map((student) => student.id).filter(Boolean));
  const studentGrades = new Set(students.map((student) => student.grade_level).filter(Boolean));
  const visibleModules = modules.filter((module) =>
    module.status === "published"
    && (!studentGrades.size || studentGrades.has(module.grade_level))
    && (!quarterId || module.quarter_id === quarterId)
  );
  const moduleById = new Map(visibleModules.map((module) => [module.id, module]));
  const visibleLessons = lessons.filter((lesson) =>
    lesson.status === "published"
    && moduleById.has(lesson.module_id)
    && (!quarterId || !lesson.quarter_id || lesson.quarter_id === quarterId)
  );
  const lessonById = new Map(visibleLessons.map((lesson) => [lesson.id, lesson]));
  const visibleLessonIds = new Set(visibleLessons.map((lesson) => lesson.id));
  const visibleProgress = progress.filter((item) =>
    studentIds.has(item.student_id)
    && visibleLessonIds.has(item.lesson_id)
  );
  const visibleAttempts = attempts
    .filter((attempt) => studentIds.has(attempt.student_id) && visibleLessonIds.has(attempt.lesson_id))
    .map((attempt) => {
      const lesson = lessonById.get(attempt.lesson_id);
      const module = lesson ? moduleById.get(lesson.module_id) : null;
      return {
        ...attempt,
        module_id: attempt.module_id || module?.id || "",
        module_title: attempt.module_title || module?.title || "",
        module_category: attempt.module_category || module?.category || ""
      };
    });
  const progressByStudentLesson = new Map();
  visibleProgress.forEach((item) => {
    progressByStudentLesson.set(`${item.student_id}:${item.lesson_id}`, item);
  });
  const latestAttemptByStudentLesson = new Map();
  [...visibleAttempts]
    .sort((a, b) => new Date(b.submitted_at || 0) - new Date(a.submitted_at || 0))
    .forEach((attempt) => {
      const key = `${attempt.student_id}:${attempt.lesson_id}`;
      if (!latestAttemptByStudentLesson.has(key)) latestAttemptByStudentLesson.set(key, attempt);
    });
  return {
    students,
    assessments,
    attempts: visibleAttempts,
    progress: visibleProgress,
    modules: visibleModules,
    lessons: visibleLessons,
    moduleById,
    lessonById,
    progressByStudentLesson,
    latestAttemptByStudentLesson,
    skillDefinitions: getStudentSkillDefinitions(),
    studentAnalytics: new Map(),
    startDate,
    endDate
  };
}

function getStudentAnalytics(student, context) {
  if (context.studentAnalytics.has(student.id)) return context.studentAnalytics.get(student.id);
  const studentLessons = context.lessons.filter((lesson) => context.moduleById.get(lesson.module_id)?.grade_level === student.grade_level);
  const studentLessonIds = new Set(studentLessons.map((lesson) => lesson.id));
  const progressRows = context.progress.filter((item) => item.student_id === student.id && studentLessonIds.has(item.lesson_id));
  const attempts = context.attempts.filter((attempt) => attempt.student_id === student.id && studentLessonIds.has(attempt.lesson_id));
  const progressByLesson = new Map(progressRows.map((item) => [item.lesson_id, item]));
  const latestAttemptByLesson = new Map();
  attempts
    .sort((a, b) => new Date(b.submitted_at || 0) - new Date(a.submitted_at || 0))
    .forEach((attempt) => {
      if (!latestAttemptByLesson.has(attempt.lesson_id)) latestAttemptByLesson.set(attempt.lesson_id, attempt);
    });
  const completedLessons = studentLessons.filter((lesson) => {
    const item = progressByLesson.get(lesson.id);
    return item?.status === "completed" || Number(item?.progress_percent || 0) >= 100;
  });
  const practiceLessons = studentLessons.filter((lesson) => ["practice", "assessment"].includes(lesson.lesson_type));
  const latestAttempts = [...latestAttemptByLesson.values()];
  const attemptScores = latestAttempts.map((attempt) => Number(attempt.score_percent)).filter(Number.isFinite);
  const progressScores = progressRows.map((item) => Number(item.score_percent)).filter(Number.isFinite);
  const averageScore = attemptScores.length
    ? averageNumber(attemptScores)
    : progressScores.length ? averageNumber(progressScores) : null;
  const submittedLessonIds = new Set(latestAttempts.map((attempt) => attempt.lesson_id));
  const today = new Date().toISOString().slice(0, 10);
  const overdueLessons = studentLessons.filter((lesson) => {
    const item = progressByLesson.get(lesson.id);
    const complete = item?.status === "completed" || Number(item?.progress_percent || 0) >= 100;
    return lesson.due_date && lesson.due_date < today && !complete;
  });
  const missedAssessments = practiceLessons.filter((lesson) => lesson.due_date && lesson.due_date < today && !submittedLessonIds.has(lesson.id));
  const lastActive = [
    ...progressRows.flatMap((item) => [item.completed_at, item.updated_at, item.started_at]),
    ...attempts.map((attempt) => attempt.submitted_at)
  ].filter(Boolean).sort().pop() || null;
  const skills = buildStudentSkillAnalytics(studentLessons, context.moduleById, progressByLesson, latestAttemptByLesson);
  const analytics = {
    student,
    lessons: studentLessons,
    totalLessons: studentLessons.length,
    completedLessons: completedLessons.length,
    completionRate: percentOf(completedLessons.length, studentLessons.length),
    practiceLessons,
    totalPracticeLessons: practiceLessons.length,
    submittedLessons: submittedLessonIds.size,
    submissionRate: percentOf(submittedLessonIds.size, practiceLessons.length),
    attempts: latestAttempts,
    averageScore,
    overdueLessons,
    missedAssessments,
    lastActive,
    skills,
    weakAreas: skills
      .filter((skill) => Number.isFinite(skill.score))
      .sort((a, b) => a.score - b.score || a.label.localeCompare(b.label))
  };
  context.studentAnalytics.set(student.id, analytics);
  return analytics;
}

function buildStudentSkillAnalytics(lessons, moduleById, progressByLesson, latestAttemptByLesson) {
  return getStudentSkillDefinitions().map((definition) => {
    const relatedLessons = lessons.filter((lesson) => lessonMatchesSkillDefinition(lesson, moduleById.get(lesson.module_id), definition));
    const scores = relatedLessons
      .map((lesson) => Number(latestAttemptByLesson.get(lesson.id)?.score_percent))
      .filter(Number.isFinite);
    const completed = relatedLessons.filter((lesson) => {
      const item = progressByLesson.get(lesson.id);
      return item?.status === "completed" || Number(item?.progress_percent || 0) >= 100;
    }).length;
    const score = scores.length
      ? averageNumber(scores)
      : relatedLessons.length ? percentOf(completed, relatedLessons.length) : null;
    return {
      ...definition,
      score,
      completed,
      relatedCount: relatedLessons.length,
      source: scores.length
        ? `${scores.length} submitted score${scores.length === 1 ? "" : "s"}`
        : relatedLessons.length ? `${completed}/${relatedLessons.length} lessons complete` : "No matching lessons"
    };
  }).filter((skill) => skill.relatedCount > 0);
}

function calculateStudentRiskAnalysis(student, context) {
  const analytics = getStudentAnalytics(student, context);
  if (!analytics.totalLessons && !analytics.totalPracticeLessons) {
    return {
      student,
      analytics,
      riskScore: 0,
      riskLevel: riskLevelForScore(0),
      reasons: [],
      diagnosis: "No published lesson records are available for this student in the selected context.",
      primaryAction: { label: "Open profile", icon: "user-round" },
      recommendedActions: [{ label: "Review student profile", detail: "Confirm class assignment and available lessons.", icon: "user-round", tone: "blue" }],
      rootCauses: [],
      weakAreas: [],
      performanceTrend: buildStudentPerformanceTrend(student, context)
    };
  }

  const weakestArea = analytics.weakAreas[0] || null;
  const scoreRisk = analytics.averageScore === null ? (analytics.totalPracticeLessons ? 45 : 20) : Math.max(0, 75 - analytics.averageScore) * 1.8;
  const completionRisk = analytics.totalLessons ? 100 - analytics.completionRate : 0;
  const submissionRisk = analytics.totalPracticeLessons ? 100 - analytics.submissionRate : 0;
  const overdueRisk = Math.min(100, (analytics.overdueLessons.length * 18) + (analytics.missedAssessments.length * 16));
  const inactiveDays = analytics.lastActive ? Math.floor((Date.now() - new Date(analytics.lastActive).getTime()) / (24 * 60 * 60 * 1000)) : null;
  const activityRisk = inactiveDays === null
    ? 20
    : inactiveDays > EVIDENCE_SUPPORT_DAYS ? Math.min(100, (inactiveDays - EVIDENCE_SUPPORT_DAYS) * 9) : 0;
  const riskScore = Math.min(100, Math.round(
    completionRisk * 0.28
    + scoreRisk * 0.28
    + submissionRisk * 0.22
    + overdueRisk * 0.16
    + activityRisk * 0.06
  ));
  const reasons = [];
  if (analytics.completionRate < 50 && analytics.totalLessons) reasons.push({ label: "Low completion", tone: "orange" });
  if (analytics.averageScore !== null && analytics.averageScore < 60) reasons.push({ label: "Low quiz mastery", tone: "red" });
  else if (analytics.averageScore !== null && analytics.averageScore < 75) reasons.push({ label: "Score below target", tone: "orange" });
  if (analytics.missedAssessments.length) reasons.push({ label: "Missed practical", tone: "orange" });
  if (analytics.overdueLessons.length) reasons.push({ label: "Overdue work", tone: "red" });
  if (weakestArea && weakestArea.score < 60) reasons.push({ label: `Weak in ${weakestArea.label}`, tone: "red" });
  if (inactiveDays === null || inactiveDays > EVIDENCE_SUPPORT_DAYS) reasons.push({ label: "Inconsistent activity", tone: "slate" });
  if (!analytics.attempts.length && analytics.totalPracticeLessons) reasons.push({ label: "No submissions", tone: "orange" });

  const rootCauses = buildStudentRootCauses(analytics, weakestArea, inactiveDays);
  const recommendedActions = buildStudentRecommendedActions(analytics, weakestArea, inactiveDays);
  const primaryAction = recommendedActions[0] || { label: "Review profile", icon: "user-round" };
  return {
    student,
    analytics,
    riskScore,
    riskLevel: riskLevelForScore(riskScore),
    reasons: dedupeByLabel(reasons).slice(0, 5),
    diagnosis: buildStudentDiagnosis(analytics, weakestArea, riskScore),
    primaryAction,
    recommendedActions,
    rootCauses,
    weakAreas: analytics.weakAreas,
    performanceTrend: buildStudentPerformanceTrend(student, context)
  };
}

function buildStudentRootCauses(analytics, weakestArea, inactiveDays) {
  const causes = [];
  if (analytics.completionRate < 60 && analytics.totalLessons) {
    causes.push({ icon: "user-round", label: "Low completion", detail: `Only ${analytics.completionRate}% of lessons completed this term.`, tone: "orange" });
  }
  if (analytics.missedAssessments.length) {
    causes.push({ icon: "file-x-2", label: "Missed assessments", detail: `${analytics.missedAssessments.length} practice or assessment check${analytics.missedAssessments.length === 1 ? "" : "s"} not submitted.`, tone: "red" });
  }
  if (analytics.averageScore !== null && analytics.averageScore < 75) {
    causes.push({ icon: "user-check", label: "Low practical performance", detail: `Latest average is ${analytics.averageScore}%, below the 75% target.`, tone: "blue" });
  }
  if (weakestArea && weakestArea.score < 70) {
    causes.push({ icon: weakestArea.icon, label: `${weakestArea.label} needs support`, detail: `${weakestArea.score}% mastery from ${weakestArea.source}.`, tone: weakestArea.score < 45 ? "red" : "orange" });
  }
  if (inactiveDays === null || inactiveDays > EVIDENCE_SUPPORT_DAYS) {
    causes.push({ icon: "calendar-clock", label: "Inconsistent activity", detail: inactiveDays === null ? "No recent lesson activity recorded." : `${inactiveDays} days since latest recorded activity.`, tone: "slate" });
  }
  return causes.slice(0, 5);
}

function buildStudentRecommendedActions(analytics, weakestArea, inactiveDays) {
  const actions = [];
  if (weakestArea && weakestArea.score < 70) {
    actions.push({ label: "Assign targeted practice", detail: `Focus on ${weakestArea.label.toLowerCase()} drills.`, icon: "search", tone: "blue" });
  }
  if (analytics.missedAssessments.length || analytics.overdueLessons.length) {
    actions.push({ label: "Schedule check-in", detail: "Review missing work and agree on the next submission target.", icon: "user-round", tone: "purple" });
  }
  if (analytics.completionRate < 60 && analytics.totalLessons) {
    actions.push({ label: "Assign remedial lesson", detail: "Start with the oldest incomplete lesson before adding new work.", icon: "book-open", tone: "orange" });
  }
  if (analytics.averageScore !== null && analytics.averageScore < 75) {
    actions.push({ label: "Give remediation worksheet", detail: "Use guided practice before the next quiz or test.", icon: "clipboard-list", tone: "green" });
  }
  if (inactiveDays === null || inactiveDays > EVIDENCE_SUPPORT_DAYS) {
    actions.push({ label: "Send reminder and resources", detail: "Prompt the student to resume current-term work.", icon: "bell", tone: "orange" });
  }
  if (!actions.length) {
    actions.push({ label: "Monitor next assessment", detail: "Continue regular progress checks.", icon: "line-chart", tone: "blue" });
  }
  return dedupeByLabel(actions).slice(0, 4);
}

function buildStudentDiagnosis(analytics, weakestArea, riskScore) {
  if (riskScore < 30) return "Current progress is stable based on available records.";
  const weakLabel = weakestArea ? weakestArea.label.toLowerCase() : "current lesson outcomes";
  if (analytics.missedAssessments.length && analytics.completionRate < 60) {
    return `Incomplete lessons and missed checks are affecting ${weakLabel}.`;
  }
  if (analytics.averageScore !== null && analytics.averageScore < 75) {
    return `Assessment scores show difficulty in ${weakLabel}.`;
  }
  if (analytics.completionRate < 60) {
    return "Lesson progress is behind the current term pacing.";
  }
  return `Needs closer monitoring around ${weakLabel}.`;
}

function buildClassWeakAreas(context) {
  return context.skillDefinitions.map((definition) => {
    const scores = context.students.map((student) => {
      const analytics = getStudentAnalytics(student, context);
      return analytics.skills.find((skill) => skill.id === definition.id)?.score;
    }).filter(Number.isFinite);
    return {
      ...definition,
      score: scores.length ? averageNumber(scores) : null,
      studentCount: scores.length
    };
  })
    .filter((item) => Number.isFinite(item.score))
    .sort((a, b) => a.score - b.score || a.label.localeCompare(b.label));
}

function buildClassModuleMasteryBreakdown(context) {
  return context.modules.map((module) => {
    const moduleLessons = context.lessons.filter((lesson) => lesson.module_id === module.id);
    const lessonIds = new Set(moduleLessons.map((lesson) => lesson.id));
    const moduleAttempts = context.attempts.filter((attempt) => lessonIds.has(attempt.lesson_id));
    const scores = moduleAttempts.map((attempt) => Number(attempt.score_percent)).filter(Number.isFinite);
    const moduleStudentIds = new Set(context.students.filter((student) => student.grade_level === module.grade_level).map((student) => student.id));
    const completedPairs = new Set(context.progress
      .filter((item) => moduleStudentIds.has(item.student_id) && lessonIds.has(item.lesson_id) && (item.status === "completed" || Number(item.progress_percent || 0) >= 100))
      .map((item) => `${item.student_id}:${item.lesson_id}`));
    const possibleCompletions = moduleLessons.length * moduleStudentIds.size;
    const completionScore = percentOf(completedPairs.size, possibleCompletions);
    const score = scores.length ? averageNumber(scores) : completionScore;
    return {
      module,
      title: module.title,
      score,
      source: scores.length ? `${scores.length} submitted score${scores.length === 1 ? "" : "s"}` : `${completedPairs.size}/${possibleCompletions} lesson completions`
    };
  })
    .filter((item) => item.module && item.module.title)
    .sort((a, b) => a.score - b.score || a.title.localeCompare(b.title));
}

function buildClassLowestScoringTopics(context) {
  return context.lessons
    .filter((lesson) => ["practice", "assessment"].includes(lesson.lesson_type))
    .map((lesson) => {
      const scores = context.attempts
        .filter((attempt) => attempt.lesson_id === lesson.id)
        .map((attempt) => Number(attempt.score_percent))
        .filter(Number.isFinite);
      const averageScore = scores.length ? averageNumber(scores) : null;
      return {
        lesson,
        title: lesson.title,
        averageScore,
        missRate: averageScore === null ? null : Math.max(0, 100 - averageScore),
        submittedCount: scores.length
      };
    })
    .filter((item) => Number.isFinite(item.averageScore))
    .sort((a, b) => b.missRate - a.missRate || a.title.localeCompare(b.title));
}

function buildClassInterventions(context, riskAnalyses) {
  const weakAreas = buildClassWeakAreas(context);
  const moduleBreakdown = buildClassModuleMasteryBreakdown(context);
  const highRiskCount = riskAnalyses.filter((item) => item.riskScore >= 55).length;
  const lowCompletionCount = riskAnalyses.filter((item) => item.analytics.totalLessons && item.analytics.completionRate < 60).length;
  const lowSubmissionCount = riskAnalyses.filter((item) => item.analytics.totalPracticeLessons && item.analytics.submissionRate < 60).length;
  const actions = [];
  const weakest = weakAreas[0];
  if (weakest) {
    actions.push({
      title: `Target ${weakest.label}`,
      detail: `Focus remediation on ${weakest.label.toLowerCase()} for ${weakest.studentCount} student${weakest.studentCount === 1 ? "" : "s"}.`,
      icon: "target",
      tone: weakest.score < 45 ? "red" : "orange"
    });
  }
  const module = moduleBreakdown[0];
  if (module && module.score < 70) {
    actions.push({
      title: "Strengthen module fundamentals",
      detail: `Review ${module.title} before the next assessment.`,
      icon: "book-open",
      tone: "orange"
    });
  }
  if (lowSubmissionCount) {
    actions.push({
      title: "Increase practice frequency",
      detail: `${lowSubmissionCount} student${lowSubmissionCount === 1 ? "" : "s"} have low submission coverage.`,
      icon: "users-round",
      tone: "green"
    });
  }
  if (highRiskCount) {
    actions.push({
      title: "Prioritize support check-ins",
      detail: `${highRiskCount} student${highRiskCount === 1 ? "" : "s"} are high or critical risk.`,
      icon: "bell",
      tone: "purple"
    });
  }
  if (!actions.length && context.students.length) {
    actions.push({
      title: "Continue monitoring",
      detail: "Use the next assessment submissions to update class support priorities.",
      icon: "line-chart",
      tone: "blue"
    });
  }
  if (lowCompletionCount && !actions.some((item) => item.title === "Assign completion catch-up")) {
    actions.push({
      title: "Assign completion catch-up",
      detail: `${lowCompletionCount} student${lowCompletionCount === 1 ? "" : "s"} need current-term lesson completion support.`,
      icon: "clipboard-list",
      tone: "blue"
    });
  }
  return actions.slice(0, 4);
}

function buildStudentPerformanceTrend(student, context) {
  const rows = context.attempts
    .filter((attempt) => attempt.student_id === student.id && Number.isFinite(Number(attempt.score_percent)) && attempt.submitted_at)
    .sort((a, b) => new Date(a.submitted_at) - new Date(b.submitted_at));
  const grouped = new Map();
  rows.forEach((attempt) => {
    const date = new Date(attempt.submitted_at);
    const week = new Date(date);
    const day = week.getDay() || 7;
    week.setDate(week.getDate() - day + 1);
    const key = toDateInputValue(week);
    if (!grouped.has(key)) grouped.set(key, []);
    grouped.get(key).push(Number(attempt.score_percent));
  });
  const points = [...grouped.entries()].slice(-6).map(([key, scores]) => ({
    key,
    label: new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric" }).format(startOfLocalDay(key)),
    score: averageNumber(scores)
  }));
  const first = points[0]?.score ?? null;
  const last = points[points.length - 1]?.score ?? null;
  const change = first !== null && last !== null && points.length > 1 ? last - first : null;
  return {
    points,
    change,
    label: change === null ? "No trend yet" : change >= 0 ? "Improving" : "Declining",
    target: 70
  };
}

function riskLevelForScore(score) {
  if (score >= 75) return { key: "critical", label: "Critical" };
  if (score >= 55) return { key: "high", label: "High" };
  if (score >= 35) return { key: "medium", label: "Medium" };
  return { key: "low", label: "Low" };
}

function weakToneForScore(score) {
  const value = Number(score || 0);
  if (value <= 30) return "critical";
  if (value <= 60) return "attention";
  return "improving";
}

function riskToneForScore(score) {
  const value = Number(score || 0);
  if (value >= 75) return "critical";
  if (value >= 55) return "attention";
  return "improving";
}

function averageNumber(values) {
  const numeric = values.map(Number).filter(Number.isFinite);
  return numeric.length ? Math.round(numeric.reduce((sum, value) => sum + value, 0) / numeric.length) : null;
}

function dedupeByLabel(items) {
  const seen = new Set();
  return items.filter((item) => {
    const key = item.label;
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

function handleReportDownload(event) {
  const button = event.target.closest("[data-report-download]");
  if (!button) return;
  const type = button.dataset.reportDownload;
  const format = button.dataset.reportFormat;
  const report = buildReportDataset(reportFiltersForDownload(type));
  const filename = `techwise-${type}-report-${report.filters.startDate}-to-${report.filters.endDate}`;
  if (format === "pdf") {
    downloadBlob(`${filename}.pdf`, buildReportPdf(type, report), "application/pdf");
    return;
  }
  downloadTextFile(`${filename}.csv`, rowsToCsv(buildReportCsvRows(type, report)), "text/csv;charset=utf-8");
}

async function handleReportSupportAction(event) {
  const button = event.target.closest("[data-report-student-profile]");
  if (!button) return;
  await openStudentFullProfile(button.dataset.reportStudentProfile);
}

function renderEvaluationDashboard() {
  const container = document.getElementById("evaluationDashboard");
  if (!container) return;
  const data = normalizeTeacherEvaluationData(teacherState.evaluation);
  const quarter = data.active_quarter || teacherState.activeQuarter;
  const selectedGrade = teacherState.evaluationGrade || "";
  const summaries = selectedGrade
    ? data.summaries.filter((summary) => summary.grade_level === selectedGrade)
    : data.summaries;
  const metricSource = selectedGrade && summaries[0] ? summaries[0] : data.overall;
  const quarterLabel = document.getElementById("evaluationQuarterLabel");
  if (quarterLabel) quarterLabel.textContent = quarter ? `${quarter.title} ${quarter.school_year || ""}`.trim() : "No active term";
  document.querySelectorAll("[data-evaluation-grade]").forEach((button) => {
    button.classList.toggle("active", button.dataset.evaluationGrade === selectedGrade);
  });

  if (!quarter) {
    container.innerHTML = `
      <section class="panel-card evaluation-empty">
        <i data-lucide="calendar-x"></i>
        <h2>No active term</h2>
        <p>Set an active term before choosing the pre-assessment, post-assessment, and feedback survey.</p>
        <button class="primary-button" type="button" data-jump-view="quarters">Open Terms</button>
      </section>
    `;
    if (window.lucide) window.lucide.createIcons();
    return;
  }

  container.innerHTML = `
    <div class="evaluation-metric-grid">
      ${renderEvaluationMetric("users", "Students", metricValue(metricSource.student_count), selectedGrade || "All grades", "blue")}
      ${renderEvaluationMetric("clipboard-list", "Pre-assessment Avg", formatEvaluationScore(metricSource.pretest_average), "Latest student submissions", "purple")}
      ${renderEvaluationMetric("line-chart", "Post-assessment Avg", formatEvaluationScore(metricSource.posttest_average), "Latest student submissions", "green")}
      ${renderEvaluationMetric("trending-up", "Improvement", formatEvaluationGrowth(metricSource.growth, metricSource.growth_percent), "Pre to post change", "orange")}
      ${renderEvaluationMetric("message-square-check", "Feedback Responses", metricValue((metricSource.survey?.student_response_count ?? metricSource.student_response_count ?? 0) + (metricSource.survey?.teacher_response_count ?? metricSource.teacher_response_count ?? 0)), "Students and teacher", "teal")}
      ${renderEvaluationMetric("star", "Feedback Rating", formatEvaluationRating(metricSource.survey?.overall_average ?? metricSource.survey_average), evaluationFeedbackLabel(metricSource.survey?.overall_average ?? metricSource.survey_average), "gold")}
    </div>

    <div class="evaluation-board-grid">
      <div class="evaluation-board-column">
        <section class="panel-card evaluation-setup-panel">
          <div class="panel-title-row">
            <h2>Test & Survey Setup</h2>
            <span class="evidence-chip">Published checks only</span>
          </div>
          <div class="evaluation-setup-list">
            ${summaries.length ? summaries.map(renderEvaluationSetupCard).join("") : `<p class="empty-text">No grade summaries are available yet.</p>`}
          </div>
        </section>

        <section class="panel-card evaluation-survey-panel">
          <div class="panel-title-row">
            <h2>System Feedback</h2>
            <span class="evidence-chip">1-5 Likert</span>
          </div>
          ${renderEvaluationSurveyResults(summaries)}
        </section>
      </div>

      <div class="evaluation-board-column">
        <section class="panel-card evaluation-gain-panel">
          <div class="panel-title-row">
            <h2>Score Improvement</h2>
            <span class="evidence-chip">Pre-assessment to post-assessment</span>
          </div>
          ${renderEvaluationGainPanel(summaries)}
        </section>

        <section class="panel-card evaluation-readiness-panel">
          <div class="panel-title-row">
            <h2>Setup Checklist</h2>
            <span class="evidence-chip">Required steps</span>
          </div>
          ${renderEvaluationReadiness(summaries)}
        </section>
      </div>
    </div>

    <section class="panel-card evaluation-respondents-panel">
      <div class="panel-title-row">
        <h2>Student Test Results</h2>
        <span>${escapeHtml(selectedGrade || "All Grades")}</span>
      </div>
      ${renderEvaluationStudentTable(summaries)}
    </section>
  `;
  if (window.lucide) window.lucide.createIcons();
}

function renderEvaluationMetric(icon, label, value, detail, color) {
  return `
    <article class="evaluation-metric-card">
      <span class="evaluation-metric-icon ${escapeAttribute(color)}"><i data-lucide="${escapeAttribute(icon)}"></i></span>
      <div>
        <p>${escapeHtml(label)}</p>
        <strong>${escapeHtml(value)}</strong>
        <small>${escapeHtml(detail)}</small>
      </div>
    </article>
  `;
}

function renderEvaluationSetupCard(summary) {
  const data = normalizeTeacherEvaluationData(teacherState.evaluation);
  const options = data.assessment_options.filter((option) => option.grade_level === summary.grade_level);
  const cycle = summary.cycle || {};
  return `
    <article class="evaluation-setup-card">
      <form data-evaluation-cycle-form>
        <input type="hidden" name="grade_level" value="${escapeAttribute(summary.grade_level)}">
        <div class="evaluation-setup-heading">
          <div>
            <strong>${escapeHtml(summary.grade_level)}</strong>
            <span>${summary.student_count} approved student${summary.student_count === 1 ? "" : "s"}</span>
          </div>
          <label class="settings-toggle inline-toggle">
            <input name="survey_is_open" type="checkbox" ${cycle.survey_is_open ? "checked" : ""}>
            <span><strong>Feedback Survey Open</strong></span>
          </label>
        </div>
        ${renderEvaluationSetupNote(options, cycle)}
        <div class="field-grid simple-grid">
          <label>Pre-assessment<select name="pretest_lesson_id">${renderEvaluationOptionRows(options, cycle.pretest_lesson_id)}</select></label>
          <label>Post-assessment<select name="posttest_lesson_id">${renderEvaluationOptionRows(options, cycle.posttest_lesson_id)}</select></label>
        </div>
        <button class="primary-button" type="submit"><i data-lucide="save"></i><span>Save ${escapeHtml(summary.grade_level)}</span></button>
      </form>
      ${renderEvaluationMappedLessonActions(cycle)}
      ${cycle.id ? renderTeacherEvaluationSurveyForm(summary) : `<p class="evaluation-note">Save this setup before submitting teacher feedback.</p>`}
    </article>
  `;
}

function renderEvaluationSetupNote(options, cycle = {}) {
  if (!options.length) {
    return `<p class="evaluation-note">Create and publish practice or assessment checks for this grade and active term first. They will appear here after publishing.</p>`;
  }
  if (!cycle.pretest_lesson_id || !cycle.posttest_lesson_id) {
    return `<p class="evaluation-note">Choose the published pre-assessment and post-assessment for this grade, then save the setup.</p>`;
  }
  return `<p class="evaluation-note">Setup is ready. Students can answer these checks from their Assessments page.</p>`;
}

function renderEvaluationMappedLessonActions(cycle = {}) {
  const rows = [
    ["Pre-assessment", cycle.pretest_lesson_id],
    ["Post-assessment", cycle.posttest_lesson_id]
  ].filter(([, lessonId]) => lessonId);

  if (!rows.length) return "";

  return `
    <div class="evaluation-lesson-actions">
      ${rows.map(([label, lessonId]) => `
        <article>
          <strong>${escapeHtml(label)}</strong>
          <div>
            <button class="small-button" type="button" data-evaluation-open-lesson="${escapeAttribute(lessonId)}"><i data-lucide="eye"></i><span>Open Lesson</span></button>
            <button class="small-button" type="button" data-evaluation-edit-lesson="${escapeAttribute(lessonId)}"><i data-lucide="list-checks"></i><span>Edit Questions</span></button>
          </div>
        </article>
      `).join("")}
    </div>
  `;
}

function renderEvaluationOptionRows(options, selectedId = "") {
  return [
    `<option value="" ${selectedId ? "" : "selected"}>Not mapped yet</option>`,
    ...options.map((option) => `
      <option value="${escapeAttribute(option.id)}" ${option.id === selectedId ? "selected" : ""}>
        ${escapeHtml(option.title)} - ${escapeHtml(option.module_title || "Module")}
      </option>
    `)
  ].join("");
}

function renderTeacherEvaluationSurveyForm(summary) {
  const teacherId = teacherState.teacherProfile?.id || getSession()?.profile?.id || "";
  const response = (summary.survey?.responses || []).find((item) =>
    item.respondent_role === "teacher" && (!teacherId || item.respondent_id === teacherId)
  );
  const answers = response?.answers || {};
  return `
    <form class="teacher-evaluation-survey-form" data-evaluation-teacher-survey>
      <input type="hidden" name="cycle_id" value="${escapeAttribute(summary.cycle.id)}">
      <div class="evaluation-survey-form-heading">
        <strong>Teacher Feedback</strong>
        <span>${response ? `Last submitted ${escapeHtml(formatDateOnly(response.submitted_at))}` : "Not submitted yet"}</span>
      </div>
      ${renderEvaluationSurveyInputs(teacherState.evaluation.survey_categories, answers)}
      <label>Comment<textarea name="comment" rows="2" maxlength="1000" placeholder="Optional note about your experience.">${escapeHtml(response?.comment || "")}</textarea></label>
      <button class="small-button" type="submit"><i data-lucide="send"></i><span>${response ? "Update Feedback" : "Submit Feedback"}</span></button>
    </form>
  `;
}

function renderEvaluationSurveyInputs(categories, answers = {}) {
  const source = categories.length ? categories : [];
  if (!source.length) return `<p class="empty-text">Survey items are not available.</p>`;
  return source.map((category) => `
    <fieldset class="evaluation-survey-category">
      <legend>${escapeHtml(category.label)}</legend>
      ${category.items.map((item) => `
        <label>
          <span>${escapeHtml(item.prompt)}</span>
          <select name="answer_${escapeAttribute(item.key)}" required>
            <option value="">Rate</option>
            ${[1, 2, 3, 4, 5].map((value) =>
              `<option value="${value}" ${Number(answers[item.key]) === value ? "selected" : ""}>${value}</option>`
            ).join("")}
          </select>
        </label>
      `).join("")}
    </fieldset>
  `).join("");
}

function renderEvaluationGainPanel(summaries) {
  if (!summaries.length) return `<p class="empty-text">Choose a grade tab to view score improvement.</p>`;
  return `
    <div class="evaluation-gain-list">
      ${summaries.map((summary) => `
        <article class="evaluation-gain-card">
          <div>
            <strong>${escapeHtml(summary.grade_level)}</strong>
            <span>${escapeHtml(summary.pretest_lesson?.title || "Pre-assessment not selected")} -> ${escapeHtml(summary.posttest_lesson?.title || "Post-assessment not selected")}</span>
          </div>
          <div class="evaluation-gain-bars">
            ${renderEvaluationGainBar("Pre", summary.pretest_average)}
            ${renderEvaluationGainBar("Post", summary.posttest_average)}
          </div>
          <b class="${Number(summary.growth || 0) >= 0 ? "good" : "danger"}">${formatEvaluationGrowth(summary.growth, summary.growth_percent)}</b>
          <small>${summary.missing_pretest_count} missing pre-assessment, ${summary.missing_posttest_count} missing post-assessment</small>
        </article>
      `).join("")}
    </div>
  `;
}

function renderEvaluationGainBar(label, value) {
  const percent = isEvaluationNumber(value) ? Number(value) : 0;
  return `
    <span>
      <em>${escapeHtml(label)}</em>
      <b>${isEvaluationNumber(value) ? `${percent}%` : "-"}</b>
      <i><u style="width:${Math.max(0, Math.min(100, percent))}%"></u></i>
    </span>
  `;
}

function renderEvaluationSurveyResults(summaries) {
  const categories = combineEvaluationCategories(summaries);
  const totalResponses = summaries.reduce((sum, summary) => sum + Number(summary.survey?.total_response_count || 0), 0);
  if (!totalResponses) {
    return `<p class="empty-text">Open the feedback survey when you are ready to collect responses.</p>`;
  }
  return `
    <div class="evaluation-survey-summary">
      <strong>${totalResponses}</strong>
      <span>Total response${totalResponses === 1 ? "" : "s"}</span>
    </div>
    <div class="evaluation-category-list">
      ${categories.map((category) => `
        <article>
          <div><strong>${escapeHtml(category.label)}</strong><span>${escapeHtml(category.rating)}</span></div>
          <div class="wide-progress"><span style="width:${category.average ? (category.average / 5) * 100 : 0}%"></span></div>
          <b>${formatEvaluationRating(category.average)}</b>
        </article>
      `).join("")}
    </div>
  `;
}

function renderEvaluationReadiness(summaries) {
  if (!summaries.length) return `<p class="empty-text">No setup details yet.</p>`;
  return `
    <div class="evaluation-readiness-list">
      ${summaries.map((summary) => {
        const items = [
          ["Pre-assessment selected", summary.readiness?.pretest_mapped],
          ["Post-assessment selected", summary.readiness?.posttest_mapped],
          ["Feedback survey open", summary.readiness?.survey_open],
          ["Responses received", summary.readiness?.responses_collected],
          ["Exports ready", summary.readiness?.exports_ready]
        ];
        return `
          <article>
            <strong>${escapeHtml(summary.grade_level)}</strong>
            ${items.map(([label, ready]) => `
              <span class="${ready ? "ready" : "pending"}"><i data-lucide="${ready ? "check-circle-2" : "circle"}"></i>${escapeHtml(label)}</span>
            `).join("")}
          </article>
        `;
      }).join("")}
    </div>
  `;
}

function renderEvaluationStudentTable(summaries) {
  const rows = summaries.flatMap((summary) => (summary.students || []).map((row) => ({ ...row, grade_level: summary.grade_level })));
  if (!rows.length) return `<p class="empty-text">No approved students match this evaluation view.</p>`;
  return `
    <div class="evaluation-table-wrap">
      <table class="evaluation-table">
        <thead><tr><th>Student</th><th>Grade</th><th>Pre-assessment</th><th>Post-assessment</th><th>Change</th><th>Status</th></tr></thead>
        <tbody>
          ${rows.map((row) => {
            const pre = row.pre_attempt?.score_percent;
            const post = row.post_attempt?.score_percent;
            const complete = isEvaluationNumber(pre) && isEvaluationNumber(post);
            return `
              <tr>
                <td><strong>${escapeHtml(row.student.full_name || row.student.username || "Student")}</strong><span>${escapeHtml(row.student.section || "")}</span></td>
                <td>${escapeHtml(row.grade_level)}</td>
                <td>${formatEvaluationScore(pre)}</td>
                <td>${formatEvaluationScore(post)}</td>
                <td class="${Number(row.change || 0) >= 0 ? "good" : "danger"}">${row.change === null ? "-" : `${row.change > 0 ? "+" : ""}${row.change}%`}</td>
                <td><span class="assessment-status-pill ${complete ? "submitted" : "not_started"}">${complete ? "Complete" : "Missing"}</span></td>
              </tr>
            `;
          }).join("")}
        </tbody>
      </table>
    </div>
  `;
}

function handleEvaluationGradeTab(event) {
  const button = event.target.closest("[data-evaluation-grade]");
  if (!button) return;
  teacherState.evaluationGrade = button.dataset.evaluationGrade || "";
  renderEvaluationDashboard();
}

async function handleEvaluationDashboardClick(event) {
  const jumpButton = event.target.closest("[data-jump-view]");
  const openButton = event.target.closest("[data-evaluation-open-lesson]");
  const editButton = event.target.closest("[data-evaluation-edit-lesson]");
  if (jumpButton) {
    showTeacherView(jumpButton.dataset.jumpView);
    return;
  }
  if (openButton) {
    await previewLesson(openButton.dataset.evaluationOpenLesson);
    return;
  }
  if (editButton) {
    await openLessonDrawer(editButton.dataset.evaluationEditLesson);
    showLessonAuthorTab("practice");
  }
}

async function handleEvaluationSubmit(event) {
  const cycleForm = event.target.closest("[data-evaluation-cycle-form]");
  const surveyForm = event.target.closest("[data-evaluation-teacher-survey]");
  if (!cycleForm && !surveyForm) return;
  event.preventDefault();
  if (cycleForm) {
    await submitEvaluationCycleForm(cycleForm);
    return;
  }
  await submitTeacherEvaluationSurvey(surveyForm);
}

async function submitEvaluationCycleForm(form) {
  const message = document.getElementById("teacherMessage");
  const payload = formToObject(form);
  try {
    setMessage(message, "Saving test setup...");
    const data = await apiPatch("/api/teacher/evaluation", {
      action: "save_cycle",
      quarter_id: teacherState.evaluation.active_quarter?.id || teacherState.activeQuarter?.id,
      grade_level: payload.grade_level,
      pretest_lesson_id: payload.pretest_lesson_id || null,
      posttest_lesson_id: payload.posttest_lesson_id || null,
      survey_is_open: payload.survey_is_open === true
    });
    teacherState.evaluation = normalizeTeacherEvaluationData(data);
    renderEvaluationDashboard();
    setMessage(message, "Test setup saved.", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

async function submitTeacherEvaluationSurvey(form) {
  const message = document.getElementById("teacherMessage");
  try {
    setMessage(message, "Submitting teacher feedback...");
    const data = await apiPatch("/api/teacher/evaluation", {
      action: "submit_survey",
      cycle_id: form.elements.cycle_id?.value,
      answers: collectEvaluationAnswers(form),
      comment: form.elements.comment?.value || ""
    });
    teacherState.evaluation = normalizeTeacherEvaluationData(data);
    renderEvaluationDashboard();
    setMessage(message, "Teacher feedback saved.", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

function handleEvaluationExport(event) {
  const button = event.target.closest("[data-evaluation-export]");
  if (!button) return;
  const format = button.dataset.evaluationExport;
  const gradePart = teacherState.evaluationGrade ? teacherState.evaluationGrade.toLowerCase().replace(/\s+/g, "-") : "all-grades";
  const quarter = teacherState.evaluation.active_quarter || teacherState.activeQuarter;
  const quarterPart = quarter ? `${quarter.name || "term"}-${quarter.school_year || ""}` : "evaluation";
  const filename = `techwise-evaluation-${gradePart}-${quarterPart}`.replace(/[^a-z0-9._-]+/gi, "-").replace(/-+/g, "-");
  if (format === "pdf") {
    downloadBlob(`${filename}.pdf`, buildEvaluationPdf(), "application/pdf");
    return;
  }
  downloadTextFile(`${filename}.csv`, rowsToCsv(buildEvaluationCsvRows()), "text/csv;charset=utf-8");
}

function buildEvaluationPdf() {
  const data = normalizeTeacherEvaluationData(teacherState.evaluation);
  const summaries = getSelectedEvaluationSummaries();
  const quarter = data.active_quarter || teacherState.activeQuarter;
  const teacherName = document.getElementById("teacherName")?.textContent || "ICT Teacher";
  const lines = [
    "TechWise 360",
    "Evaluation Report",
    `Teacher: ${teacherName}`,
    `Term: ${quarter ? `${quarter.title} ${quarter.school_year || ""}`.trim() : "No active term"}`,
    `Grade Filter: ${teacherState.evaluationGrade || "All Grades"}`,
    `Generated: ${new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(new Date())}`,
    "",
    ...summaries.flatMap((summary) => [
      `${summary.grade_level}`,
      `Students: ${summary.student_count}`,
      `Pre-assessment: ${summary.pretest_lesson?.title || "Not selected"} | Average: ${formatEvaluationScore(summary.pretest_average)}`,
      `Post-assessment: ${summary.posttest_lesson?.title || "Not selected"} | Average: ${formatEvaluationScore(summary.posttest_average)}`,
      `Improvement: ${formatEvaluationGrowth(summary.growth, summary.growth_percent)}`,
      `Feedback: ${formatEvaluationRating(summary.survey?.overall_average)} (${evaluationFeedbackLabel(summary.survey?.overall_average)})`,
      `Responses: ${summary.survey?.student_response_count || 0} student, ${summary.survey?.teacher_response_count || 0} teacher`,
      ""
    ]),
    "Student Test Results",
    ...summaries.flatMap((summary) => (summary.students || []).map((row) =>
      `${row.student.full_name || row.student.username || "Student"} | ${summary.grade_level} | Pre ${formatEvaluationScore(row.pre_attempt?.score_percent)} | Post ${formatEvaluationScore(row.post_attempt?.score_percent)} | Change ${row.change === null ? "-" : `${row.change}%`}`
    )),
    "",
    "Generated by TechWise 360"
  ];
  return buildSimplePdf(lines);
}

function buildEvaluationCsvRows() {
  const summaries = getSelectedEvaluationSummaries();
  const rows = [
    ["Section", "Grade", "Label", "Value", "Detail"],
    ...summaries.flatMap((summary) => [
      ["Summary", summary.grade_level, "Students", summary.student_count, ""],
      ["Summary", summary.grade_level, "Pre-assessment Average", formatEvaluationScore(summary.pretest_average), summary.pretest_lesson?.title || "Not selected"],
      ["Summary", summary.grade_level, "Post-assessment Average", formatEvaluationScore(summary.posttest_average), summary.posttest_lesson?.title || "Not selected"],
      ["Summary", summary.grade_level, "Improvement", formatEvaluationGrowth(summary.growth, summary.growth_percent), ""],
      ["Feedback", summary.grade_level, "Student Responses", summary.survey?.student_response_count || 0, ""],
      ["Feedback", summary.grade_level, "Teacher Responses", summary.survey?.teacher_response_count || 0, ""],
      ["Feedback", summary.grade_level, "Overall Rating", formatEvaluationRating(summary.survey?.overall_average), evaluationFeedbackLabel(summary.survey?.overall_average)]
    ])
  ];
  summaries.forEach((summary) => {
    (summary.survey?.category_averages || []).forEach((category) => {
      rows.push(["Feedback Category", summary.grade_level, category.label, formatEvaluationRating(category.average), evaluationFeedbackLabel(category.average)]);
    });
    (summary.students || []).forEach((row) => {
      rows.push([
        "Student",
        summary.grade_level,
        row.student.full_name || row.student.username || "Student",
        `Pre: ${formatEvaluationScore(row.pre_attempt?.score_percent)}`,
        `Post: ${formatEvaluationScore(row.post_attempt?.score_percent)} | Change: ${row.change === null ? "-" : `${row.change}%`}`
      ]);
    });
  });
  return rows;
}

function getSelectedEvaluationSummaries() {
  const data = normalizeTeacherEvaluationData(teacherState.evaluation);
  return teacherState.evaluationGrade
    ? data.summaries.filter((summary) => summary.grade_level === teacherState.evaluationGrade)
    : data.summaries;
}

function collectEvaluationAnswers(form) {
  const answers = {};
  form.querySelectorAll("[name^='answer_']").forEach((select) => {
    answers[select.name.replace(/^answer_/, "")] = Number(select.value);
  });
  return answers;
}

function combineEvaluationCategories(summaries) {
  const groups = new Map();
  summaries.forEach((summary) => {
    (summary.survey?.category_averages || []).forEach((category) => {
      if (!isEvaluationNumber(category.average)) return;
      const current = groups.get(category.key) || { key: category.key, label: category.label, values: [] };
      current.values.push(Number(category.average));
      groups.set(category.key, current);
    });
  });
  return [...groups.values()].map((group) => {
    const average = group.values.length ? Math.round((group.values.reduce((sum, value) => sum + value, 0) / group.values.length) * 10) / 10 : null;
    return {
      key: group.key,
      label: group.label,
      average,
      rating: evaluationFeedbackLabel(average)
    };
  });
}

function formatEvaluationScore(value) {
  return isEvaluationNumber(value) ? `${Number(value)}%` : "-";
}

function formatEvaluationRating(value) {
  return isEvaluationNumber(value) ? `${Number(value).toFixed(1)}/5` : "-";
}

function formatEvaluationGrowth(value, percentValue = null) {
  if (!isEvaluationNumber(value)) return "-";
  const prefix = Number(value) > 0 ? "+" : "";
  const percent = isEvaluationNumber(percentValue) ? ` (${prefix}${Number(percentValue)}%)` : "";
  return `${prefix}${Number(value)} pts${percent}`;
}

function metricValue(value) {
  return isEvaluationNumber(value) ? String(value) : "-";
}

function evaluationFeedbackLabel(value) {
  if (!isEvaluationNumber(value)) return "Not collected";
  if (value >= 4.21) return "Very positive";
  if (value >= 3.41) return "Positive";
  if (value >= 2.61) return "Neutral";
  if (value >= 1.81) return "Needs improvement";
  return "Poor";
}

function isEvaluationNumber(value) {
  return value !== null && value !== undefined && value !== "" && Number.isFinite(Number(value));
}

function reportFiltersForDownload(type) {
  const filters = normalizeReportFilters(teacherState.reportFilters);
  if (type !== "monthly") return filters;
  const [year, month] = filters.startDate.split("-").map(Number);
  const start = new Date(year, month - 1, 1);
  const end = new Date(year, month, 0);
  return {
    ...filters,
    startDate: toDateInputValue(start),
    endDate: toDateInputValue(end)
  };
}

function buildReportPdf(type, report) {
  const title = reportTitle(type);
  const teacherName = document.getElementById("teacherName")?.textContent || "ICT Teacher";
  const generatedAt = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(new Date());
  const lines = [
    "TechWise 360",
    title,
    `${REPORT_TEMPLATE_NAME} | ${teacherName}`,
    `Range: ${report.rangeLabel}`,
    `Generated: ${generatedAt}`,
    "",
    `Average Overall Score: ${report.metrics.averageScore === null ? "-" : `${report.metrics.averageScore}%`}`,
    `Completion Rate: ${report.metrics.completionRate}%`,
    `Certificates Issued: ${report.metrics.certificatesIssued}`,
    `Submission Rate: ${report.metrics.submissionRate}%`,
    "",
    ...buildReportPdfLines(type, report),
    "",
    "Generated by TechWise 360"
  ];
  return buildSimplePdf(lines);
}

function buildReportPdfLines(type, report) {
  if (type === "weekly") {
    return [
      "Daily Activity",
      ...report.dailyScores.map((day) => `${day.label}: ${day.average}% average score, ${day.submissions} submissions`),
      "",
      "Recent Submissions",
      ...(report.attempts.length ? report.attempts.map((attempt) =>
        `${studentNameById(attempt.student_id)} | ${attempt.assessment_title} | ${attempt.score_percent}% | ${formatDateOnly(attempt.submitted_at)}`
      ) : ["No scored submissions in this range."])
    ];
  }
  if (type === "monthly") {
    return [
      "Topic Mastery",
      ...(report.topicScores.length ? report.topicScores.map((topic) =>
        `${topic.module_title}: ${topic.average_score}% average from ${topic.submitted_count} submissions`
      ) : ["No topic scores in this range."]),
      "",
      "Assessments",
      ...report.assessments.map((assessment) =>
        `${assessment.title} | ${assessment.module_title} | ${assessment.submitted_count || 0}/${assessment.assigned_count || 0} submitted | ${Number.isInteger(assessment.average_score) ? `${assessment.average_score}%` : "-"} average`
      )
    ];
  }
  return [
    "Student Summary",
    ...report.studentSummaries.map((summary) =>
      `${summary.student.full_name || summary.student.username || "Student"} | ${summary.student.grade_level || ""} ${summary.student.section || ""} | ${summary.completedLessons}/${summary.totalLessons} lessons | ${summary.averageScore}% average | ${summary.badges} badges | ${summary.certificates} certificates | ${reportStatusLabel(summary.status)}`
    )
  ];
}

function buildSimplePdf(lines) {
  const pageWidth = 595;
  const pageHeight = 842;
  const marginX = 48;
  const startY = 790;
  const lineHeight = 16;
  const wrapped = lines.flatMap((line) => wrapPdfLine(line, 92));
  const pages = [];
  for (let index = 0; index < wrapped.length; index += 42) {
    pages.push(wrapped.slice(index, index + 42));
  }
  if (!pages.length) pages.push(["No records found for this report."]);

  const objects = [];
  const pageIds = pages.map((_, index) => 3 + index * 2);
  const contentIds = pages.map((_, index) => 4 + index * 2);
  objects[1] = "<< /Type /Catalog /Pages 2 0 R >>";
  objects[2] = `<< /Type /Pages /Kids [${pageIds.map((id) => `${id} 0 R`).join(" ")}] /Count ${pages.length} >>`;

  pages.forEach((pageLines, index) => {
    const pageId = pageIds[index];
    const contentId = contentIds[index];
    const content = [
      "BT",
      `/F1 11 Tf ${marginX} ${startY} Td`,
      ...pageLines.flatMap((line, lineIndex) => {
        const font = lineIndex === 0 && index === 0 ? "/F2 24 Tf" : lineIndex === 1 && index === 0 ? "/F2 17 Tf" : "/F1 11 Tf";
        const leading = lineIndex === 0 ? 0 : -lineHeight;
        return [`${font} 0 ${leading} Td (${pdfEscape(line)}) Tj`];
      }),
      "ET"
    ].join("\n");
    objects[pageId] = `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${pageWidth} ${pageHeight}] /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> /F2 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >> >> >> /Contents ${contentId} 0 R >>`;
    objects[contentId] = `<< /Length ${content.length} >>\nstream\n${content}\nendstream`;
  });

  let pdf = "%PDF-1.4\n";
  const offsets = [0];
  for (let id = 1; id < objects.length; id += 1) {
    if (!objects[id]) continue;
    offsets[id] = pdf.length;
    pdf += `${id} 0 obj\n${objects[id]}\nendobj\n`;
  }
  const xrefOffset = pdf.length;
  pdf += `xref\n0 ${objects.length}\n0000000000 65535 f \n`;
  for (let id = 1; id < objects.length; id += 1) {
    pdf += `${String(offsets[id] || 0).padStart(10, "0")} 00000 n \n`;
  }
  pdf += `trailer\n<< /Size ${objects.length} /Root 1 0 R >>\nstartxref\n${xrefOffset}\n%%EOF`;
  return new Blob([pdf], { type: "application/pdf" });
}

function wrapPdfLine(value, maxLength) {
  const text = String(value ?? "").replace(/[^\x20-\x7E]/g, " ").trim();
  if (!text) return [""];
  const words = text.split(/\s+/);
  const lines = [];
  let current = "";
  words.forEach((word) => {
    if (`${current} ${word}`.trim().length > maxLength) {
      if (current) lines.push(current);
      current = word;
    } else {
      current = `${current} ${word}`.trim();
    }
  });
  if (current) lines.push(current);
  return lines;
}

function pdfEscape(value) {
  return String(value ?? "").replace(/\\/g, "\\\\").replace(/\(/g, "\\(").replace(/\)/g, "\\)");
}

function buildReportHtml(type, report) {
  const title = reportTitle(type);
  const teacherName = document.getElementById("teacherName")?.textContent || "ICT Teacher";
  const generatedAt = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(new Date());
  const summaryCards = [
    ["Average Overall Score", report.metrics.averageScore === null ? "-" : `${report.metrics.averageScore}%`],
    ["Completion Rate", `${report.metrics.completionRate}%`],
    ["Certificates Issued", report.metrics.certificatesIssued],
    ["Submission Rate", `${report.metrics.submissionRate}%`]
  ];
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>${escapeHtml(title)} | TechWise 360</title>
  <style>
    body { margin: 0; background: #f4f7fc; color: #071225; font-family: Arial, sans-serif; }
    main { width: min(1080px, calc(100% - 48px)); margin: 32px auto; }
    header, section { background: #fff; border: 1px solid #dfe6f2; border-radius: 18px; box-shadow: 0 8px 20px rgba(16,24,40,.08); }
    header { padding: 28px; margin-bottom: 18px; }
    h1, h2, p { margin-top: 0; }
    .brand { font-size: 34px; font-weight: 900; } .brand span { color: #1f7bf2; }
    .meta { color: #667085; font-weight: 700; }
    .cards { display: grid; grid-template-columns: repeat(4, 1fr); gap: 14px; margin: 18px 0; }
    .card { padding: 16px; border: 1px solid #e3eaf4; border-radius: 14px; background: #fbfcff; }
    .card strong { display: block; margin-top: 8px; font-size: 26px; }
    section { padding: 22px; margin-bottom: 18px; }
    table { width: 100%; border-collapse: collapse; } th, td { padding: 10px; border-bottom: 1px solid #e4eaf4; text-align: left; } th { color: #344054; background: #f8fbff; }
    .bars { display: grid; gap: 10px; } .bar { display: grid; grid-template-columns: 170px 1fr 54px; gap: 12px; align-items: center; }
    .track { height: 14px; border-radius: 999px; background: #e8eef8; overflow: hidden; } .track span { display: block; height: 100%; background: #1f7bf2; }
    footer { color: #667085; font-weight: 700; text-align: center; }
    @media print { body { background: #fff; } main { width: auto; margin: 0; } header, section { box-shadow: none; } }
  </style>
</head>
<body>
  <main>
    <header>
      <div class="brand">TechWise <span>360</span></div>
      <h1>${escapeHtml(title)}</h1>
      <p class="meta">${escapeHtml(REPORT_TEMPLATE_NAME)} · ${escapeHtml(teacherName)} · ${escapeHtml(report.rangeLabel)} · Generated ${escapeHtml(generatedAt)}</p>
    </header>
    <div class="cards">${summaryCards.map(([label, value]) => `<div class="card">${escapeHtml(label)}<strong>${escapeHtml(value)}</strong></div>`).join("")}</div>
    ${buildReportHtmlBody(type, report)}
    <footer>Generated by TechWise 360</footer>
  </main>
</body>
</html>`;
}

function buildReportHtmlBody(type, report) {
  if (type === "weekly") {
    return `
      <section><h2>Daily Activity</h2><div class="bars">${report.dailyScores.map((day) => `
        <div class="bar"><span>${escapeHtml(day.label)}</span><div class="track"><span style="width:${day.average}%"></span></div><strong>${day.average}%</strong></div>
      `).join("")}</div></section>
      <section><h2>Recent Submissions</h2>${reportTable(["Student", "Assessment", "Score", "Submitted"], report.attempts.map((attempt) => [
        studentNameById(attempt.student_id), attempt.assessment_title, `${attempt.score_percent}%`, formatDateOnly(attempt.submitted_at)
      ]))}</section>
    `;
  }
  if (type === "monthly") {
    return `
      <section><h2>Topic Mastery</h2><div class="bars">${report.topicScores.map((topic) => `
        <div class="bar"><span>${escapeHtml(topic.module_title)}</span><div class="track"><span style="width:${topic.average_score}%"></span></div><strong>${topic.average_score}%</strong></div>
      `).join("") || "<p>No topic scores in this range.</p>"}</div></section>
      <section><h2>Assessments</h2>${reportTable(["Assessment", "Module", "Submitted", "Average"], report.assessments.map((assessment) => [
        assessment.title, assessment.module_title, `${assessment.submitted_count || 0}/${assessment.assigned_count || 0}`, Number.isInteger(assessment.average_score) ? `${assessment.average_score}%` : "-"
      ]))}</section>
    `;
  }
  return `
    <section><h2>Student Summary</h2>${reportTable(["Student", "Grade", "Section", "Completion", "Average", "Badges", "Certificates", "Status"], report.studentSummaries.map((summary) => [
      summary.student.full_name || summary.student.username || "Student",
      summary.student.grade_level || "",
      summary.student.section || "",
      `${summary.completedLessons}/${summary.totalLessons}`,
      `${summary.averageScore}%`,
      summary.badges,
      summary.certificates,
      reportStatusLabel(summary.status)
    ]))}</section>
  `;
}

function buildReportCsvRows(type, report) {
  if (type === "weekly") {
    return [
      ["Date", "Average Score", "Submissions"],
      ...report.dailyScores.map((day) => [day.label, `${day.average}%`, day.submissions])
    ];
  }
  if (type === "monthly") {
    return [
      ["Module", "Average Score", "Submissions"],
      ...report.topicScores.map((topic) => [topic.module_title, `${topic.average_score}%`, topic.submitted_count])
    ];
  }
  return [
    ["Student Name", "Grade Level", "Section", "Lessons Completed", "Average Score", "Badges", "Certificates", "Status"],
    ...report.studentSummaries.map((summary) => [
      summary.student.full_name || summary.student.username || "Student",
      summary.student.grade_level || "",
      summary.student.section || "",
      `${summary.completedLessons}/${summary.totalLessons}`,
      `${summary.averageScore}%`,
      summary.badges,
      summary.certificates,
      reportStatusLabel(summary.status)
    ])
  ];
}

function reportTable(headers, rows) {
  if (!rows.length) return `<p>No records found for this report.</p>`;
  return `<table><thead><tr>${headers.map((header) => `<th>${escapeHtml(header)}</th>`).join("")}</tr></thead><tbody>${rows.map((row) =>
    `<tr>${row.map((cell) => `<td>${escapeHtml(cell)}</td>`).join("")}</tr>`
  ).join("")}</tbody></table>`;
}

function rowsToCsv(rows) {
  return rows.map((row) => row.map((cell) => `"${String(cell ?? "").replace(/"/g, '""')}"`).join(",")).join("\n");
}

function downloadTextFile(filename, content, type) {
  const blob = new Blob([content], { type });
  downloadBlob(filename, blob);
}

function downloadBlob(filename, blob) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

function reportTitle(type) {
  if (type === "monthly") return "Monthly Report";
  if (type === "class") return "Class Summary";
  return "Weekly Report";
}

function reportStatusLabel(status) {
  if (status === "inProgress") return "In Progress";
  if (status === "notStarted") return "Not Started";
  return titleCase(status);
}

function studentNameById(studentId) {
  const student = teacherState.students.find((item) => item.id === studentId);
  return student?.full_name || student?.username || "Student";
}

function handleAssessmentPagination(event) {
  const pageButton = event.target.closest("[data-assessment-page-number]");
  const navButton = event.target.closest("[data-assessment-page-nav]");
  if (pageButton) {
    teacherState.assessmentPage = Number(pageButton.dataset.assessmentPageNumber);
    renderAssessmentTable();
    if (window.lucide) window.lucide.createIcons();
    return;
  }
  if (navButton?.dataset.assessmentPageNav === "prev") {
    teacherState.assessmentPage -= 1;
    renderAssessmentTable();
  }
  if (navButton?.dataset.assessmentPageNav === "next") {
    teacherState.assessmentPage += 1;
    renderAssessmentTable();
  }
}

async function handleAssessmentAction(event) {
  const reviewButton = event.target.closest("[data-review-assessment]");
  const editButton = event.target.closest("[data-edit-assessment]");
  const previewButton = event.target.closest("[data-preview-assessment]");
  const jumpButton = event.target.closest("[data-jump-view]");

  if (jumpButton) {
    showTeacherView(jumpButton.dataset.jumpView);
    return;
  }

  if (reviewButton) {
    openAssessmentResultsDrawer(reviewButton.dataset.reviewAssessment);
    return;
  }
  if (editButton) {
    await openLessonDrawer(editButton.dataset.editAssessment);
    return;
  }
  if (previewButton) {
    await previewLesson(previewButton.dataset.previewAssessment);
  }
}

function openAssessmentResultsDrawer(assessmentId) {
  const drawer = document.getElementById("assessmentResultsDrawer");
  const content = document.getElementById("assessmentResultsContent");
  const title = document.getElementById("assessmentResultsTitle");
  const assessment = teacherState.assessments.find((item) => item.id === assessmentId);
  if (!drawer || !content || !assessment) return;
  teacherState.selectedAssessmentId = assessmentId;
  if (title) title.textContent = assessment.title || "Review Results";
  const average = Number.isInteger(assessment.average_score) ? `${assessment.average_score}%` : "-";
  content.innerHTML = `
    <section class="assessment-result-summary">
      <div><span>Submitted</span><strong>${Number(assessment.submitted_count || 0)}/${Number(assessment.assigned_count || 0)}</strong></div>
      <div><span>Average</span><strong>${average}</strong></div>
      <div><span>Type</span><strong>${escapeHtml(titleCase(assessment.lesson_type))}</strong></div>
    </section>
    <section class="assessment-result-meta">
      <strong>${escapeHtml(assessment.module_title || "Module")}</strong>
      <span>${escapeHtml(assessment.grade_level || "Class")} ${assessment.due_date ? `- Due ${escapeHtml(formatDate(assessment.due_date))}` : ""}</span>
    </section>
    <div class="assessment-student-results">
      ${(assessment.students || []).length ? assessment.students.map((student) => renderAssessmentStudentResult(student)).join("") : `<p class="empty-text">No assigned approved students found for this assessment grade level.</p>`}
    </div>
  `;
  drawer.classList.add("open");
  drawer.setAttribute("aria-hidden", "false");
  syncDrawerScrollLock();
  if (window.lucide) window.lucide.createIcons();
}

function renderAssessmentStudentResult(student) {
  const attempt = student.latest_attempt;
  if (!attempt) {
    return `
      <article class="assessment-student-result missing">
        <div class="mini-avatar">${escapeHtml(getInitials(student.full_name || student.username))}</div>
        <div><strong>${escapeHtml(student.full_name || student.username || "Student")}</strong><span>Not submitted yet</span></div>
        <b>Pending</b>
      </article>
    `;
  }
  return `
    <details class="assessment-student-result submitted">
      <summary>
        <div class="mini-avatar">${escapeHtml(getInitials(student.full_name || student.username))}</div>
        <div><strong>${escapeHtml(student.full_name || student.username || "Student")}</strong><span>${escapeHtml(relativeTime(attempt.submitted_at))} - ${Number(attempt.correct_count || 0)} correct</span></div>
        <b class="${scoreClass(attempt.score_percent)}">${Number(attempt.score_percent || 0)}%</b>
      </summary>
      <div class="assessment-attempt-detail">
        ${(attempt.feedback || []).map((item, index) => `
          <article class="${item.is_correct ? "correct" : "incorrect"}">
            <strong>${index + 1}. ${escapeHtml(item.prompt || "Question")}</strong>
            <span>Your answer: ${escapeHtml(item.submitted_answer || "-")}</span>
            <span>Correct answer: ${escapeHtml(item.correct_answer || "-")}</span>
            ${item.explanation ? `<p>${escapeHtml(item.explanation)}</p>` : ""}
          </article>
        `).join("")}
      </div>
    </details>
  `;
}

function closeAssessmentResultsDrawer() {
  const drawer = document.getElementById("assessmentResultsDrawer");
  drawer?.classList.remove("open");
  drawer?.setAttribute("aria-hidden", "true");
  syncDrawerScrollLock();
}

function assessmentDisplayStatus(assessment) {
  if (assessment.status === "draft") return "draft";
  const today = new Date().toISOString().slice(0, 10);
  if (assessment.scheduled_date && assessment.scheduled_date > today && assessment.status === "published") return "scheduled";
  return assessment.status || "draft";
}

function assessmentAction(assessment) {
  if (Number(assessment.submitted_count || 0) > 0) {
    return { kind: "review", label: "Review Results", icon: "bar-chart-3" };
  }
  if (assessment.status === "draft") {
    return { kind: "edit", label: "Continue", icon: "file-pen" };
  }
  return { kind: "edit", label: "Edit", icon: "pencil" };
}

function scoreClass(score) {
  const value = Number(score);
  if (!Number.isFinite(value)) return "muted-score";
  if (value >= 85) return "score-good";
  if (value >= 75) return "score-warn";
  return "score-low";
}

function shortTopicLabel(value) {
  const label = String(value || "Topic").replace(/^PC\s+/i, "");
  return label.length > 18 ? `${label.slice(0, 16)}...` : label;
}

function buildLessonLibraryRows() {
  const rows = [];
  const modulesById = new Map(teacherState.modules.map((module) => [module.id, module]));
  const packageModuleIds = new Set();

  teacherState.modules.forEach((module) => {
    if (!isPackageModule(module)) return;
    const lessons = getSortedLessonsForModule(module.id, teacherState.lessons)
      .filter((lesson) => lesson.status !== "archived");
    if (!lessons.length) return;
    packageModuleIds.add(module.id);
    const statuses = new Set(lessons.map((lesson) => lesson.status));
    rows.push({
      type: "package",
      module,
      lessons,
      status: module.status === "published" && statuses.size === 1 && statuses.has("published") ? "published" : statuses.has("draft") ? "draft" : module.status,
      duration: lessons.reduce((sum, lesson) => sum + Number(lesson.duration_minutes || 0), 0)
    });
  });

  teacherState.lessons.forEach((lesson) => {
    if (packageModuleIds.has(lesson.module_id)) return;
    rows.push({
      type: "lesson",
      lesson,
      module: modulesById.get(lesson.module_id),
      status: lesson.status,
      duration: Number(lesson.duration_minutes || 0)
    });
  });

  return rows;
}

function renderLessonPagination(total, start, visible, pageCount) {
  const summary = document.getElementById("lessonPageSummary");
  const pagination = document.getElementById("lessonPagination");
  if (summary) {
    const from = total ? start + 1 : 0;
    const to = start + visible;
    summary.textContent = `Showing ${from} to ${to} of ${total} lessons`;
  }
  if (!pagination) return;
  pagination.innerHTML = renderDashboardPagination({
    currentPage: teacherState.lessonPage,
    pageCount,
    pageNumberAttribute: "data-lesson-page-number",
    label: "Lesson pages"
  });
}

function renderRecentUploads(limit = 5) {
  const list = document.getElementById("recentUploadsList");
  if (!list) return;
  const files = [...teacherState.lessonFiles].sort((a, b) => new Date(b.created_at) - new Date(a.created_at)).slice(0, limit);
  if (!files.length) {
    list.innerHTML = `<p class="empty-text">No uploaded lesson files yet.</p>`;
    return;
  }
  list.innerHTML = files.map((file) => `
    <article class="recent-upload-row">
      <span class="${escapeHtml(fileIconClass(file.mime_type))}"><i data-lucide="${escapeHtml(fileIcon(file.mime_type))}"></i></span>
      <div><strong>${escapeHtml(file.original_filename)}</strong><small>${escapeHtml(fileLabel(file))}</small></div>
      <time>${escapeHtml(relativeTime(file.created_at))}</time>
    </article>
  `).join("");
}

function renderContentCalendar() {
  const grid = document.getElementById("lessonCalendarGrid");
  if (!grid) return;
  const [year, month] = (teacherState.calendarMonth || new Date().toISOString().slice(0, 7)).split("-").map(Number);
  const firstDay = new Date(year, month - 1, 1);
  const lastDay = new Date(year, month, 0);
  const offset = (firstDay.getDay() + 6) % 7;
  const cells = [];
  const monthLabel = new Intl.DateTimeFormat(undefined, { month: "long", year: "numeric" }).format(firstDay);
  setText("calendarMonthLabel", monthLabel);

  for (let index = 0; index < offset; index += 1) {
    const day = new Date(year, month - 1, index - offset + 1);
    cells.push(calendarCell(day, true));
  }
  for (let day = 1; day <= lastDay.getDate(); day += 1) {
    cells.push(calendarCell(new Date(year, month - 1, day), false));
  }
  while (cells.length % 7 !== 0) {
    const day = new Date(year, month - 1, lastDay.getDate() + (cells.length - offset - lastDay.getDate()) + 1);
    cells.push(calendarCell(day, true));
  }
  grid.innerHTML = cells.join("");
}

function calendarCell(date, muted) {
  const iso = date.toISOString().slice(0, 10);
  const scheduled = teacherState.lessons.some((lesson) => lesson.scheduled_date === iso);
  const due = teacherState.lessons.some((lesson) => lesson.due_date === iso);
  const active = iso === new Date().toISOString().slice(0, 10);
  return `<button class="${muted ? "muted" : ""} ${active ? "active" : ""}" type="button" data-calendar-date="${iso}">
    <span>${date.getDate()}</span>
    <i>${scheduled ? `<b class="dot blue"></b>` : ""}${due ? `<b class="dot orange"></b>` : ""}</i>
  </button>`;
}

function openCalendarDrawer() {
  const drawer = document.getElementById("calendarDrawer");
  const list = document.getElementById("calendarEventList");
  if (!drawer || !list) return;
  const events = teacherState.lessons
    .flatMap((lesson) => [
      lesson.scheduled_date ? { date: lesson.scheduled_date, type: "Lesson", lesson } : null,
      lesson.due_date ? { date: lesson.due_date, type: "Deadline", lesson } : null
    ].filter(Boolean))
    .sort((a, b) => a.date.localeCompare(b.date));
  list.innerHTML = events.length ? events.map((event) => `
    <article>
      <span class="${event.type === "Lesson" ? "blue" : "orange"}"></span>
      <div><strong>${escapeHtml(event.lesson.title)}</strong><small>${escapeHtml(event.type)} - ${escapeHtml(event.date)}</small></div>
    </article>
  `).join("") : `<p class="empty-text">No scheduled lessons or deadlines yet.</p>`;
  drawer.classList.add("open");
  drawer.setAttribute("aria-hidden", "false");
  syncDrawerScrollLock();
}

function closeCalendarDrawer() {
  const drawer = document.getElementById("calendarDrawer");
  drawer?.classList.remove("open");
  drawer?.setAttribute("aria-hidden", "true");
  syncDrawerScrollLock();
}

function handleLessonPagination(event) {
  const pageButton = event.target.closest("[data-lesson-page-number]");
  const navButton = event.target.closest("[data-lesson-page-nav]");
  if (pageButton) {
    teacherState.lessonPage = Number(pageButton.dataset.lessonPageNumber);
    renderLessonLibrary();
    if (window.lucide) window.lucide.createIcons();
    return;
  }
  if (navButton?.dataset.lessonPageNav === "prev") {
    teacherState.lessonPage -= 1;
    renderLessonLibrary();
  }
  if (navButton?.dataset.lessonPageNav === "next") {
    teacherState.lessonPage += 1;
    renderLessonLibrary();
  }
}

async function submitQuarterForm(event) {
  event.preventDefault();
  const message = document.getElementById("teacherMessage");
  const form = event.currentTarget;
  const payload = formToObject(form);
  const id = payload.id;
  const existingQuarter = teacherState.quarters.find((item) => item.id === id);
  delete payload.id;
  payload.is_active = Boolean(payload.is_active);
  payload.status = payload.is_active ? "active" : existingQuarter?.status || "active";

  try {
    setMessage(message, id ? "Updating term..." : "Creating term...");
    if (id) {
      await apiPatch("/api/teacher/quarters", { id, ...payload });
    } else {
      await apiPost("/api/teacher/quarters", payload);
    }
    resetQuarterForm();
    await loadTeacherData();
    setMessage(message, id ? "Term updated." : "Term created.", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

function openQuarterEditor(quarterId) {
  const quarter = teacherState.quarters.find((item) => item.id === quarterId);
  const form = document.getElementById("quarterForm");
  if (!quarter || !form) return;

  form.elements.id.value = quarter.id;
  form.elements.name.value = quarter.name || "T1";
  form.elements.title.value = quarter.title || getTermLabel(quarter);
  form.elements.school_year.value = quarter.school_year || "";
  form.elements.start_date.value = quarter.start_date || "";
  form.elements.end_date.value = quarter.end_date || "";
  form.elements.is_active.checked = Boolean(quarter.is_active);
  setText("quarterFormTitle", "Edit Term");
  setText("quarterSubmitButton", "Save Term");
  document.getElementById("cancelQuarterEdit")?.removeAttribute("hidden");
  form.scrollIntoView({ behavior: "smooth", block: "start" });
}

function resetQuarterForm() {
  const form = document.getElementById("quarterForm");
  if (!form) return;
  form.reset();
  form.elements.id.value = "";
  form.elements.name.value = "T1";
  form.elements.title.value = "1st Term";
  form.elements.school_year.value = "2026-2027";
  setText("quarterFormTitle", "Add Term");
  setText("quarterSubmitButton", "Create Term");
  document.getElementById("cancelQuarterEdit")?.setAttribute("hidden", "");
}

async function submitLessonForm(event) {
  event.preventDefault();
  const message = document.getElementById("teacherMessage");
  const form = event.currentTarget;
  const payload = formToObject(form);
  const module = teacherState.modules.find((item) => item.id === payload.module_id);
  payload.quarter_id = module?.quarter_id || null;
  const id = payload.id;
  delete payload.id;
  if (!payload.resource_url) payload.resource_url = "";
  payload.sections = collectLessonSections();
  payload.questions = collectLessonQuestions();

  if (payload.status === "published") {
    const readyToPublish = await reviewLessonPublishReadiness(payload, {
      hasSelectedFile: Boolean(teacherState.selectedLessonFile)
    });
    if (!readyToPublish) return;
  }

  try {
    setMessage(message, id ? "Updating lesson..." : "Creating lesson...");
    if (teacherState.selectedLessonFile) {
      const uploadedFile = await uploadLessonFile(teacherState.selectedLessonFile, id || null);
      payload.file_id = uploadedFile.id;
      if (!payload.resource_url) payload.resource_url = "";
    } else if (payload.file_id) {
      payload.file_id = payload.file_id;
    } else {
      delete payload.file_id;
    }
    if (id) {
      await apiPatch("/api/teacher/lessons", { id, ...payload });
    } else {
      await apiPost("/api/teacher/lessons", payload);
    }
    resetLessonForm();
    closeLessonDrawer();
    await loadTeacherData();
    setMessage(message, "Lesson saved.", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

async function reviewLessonPublishReadiness(payload, { hasSelectedFile = false } = {}) {
  const checklist = getLessonPublishChecklist(payload, { hasSelectedFile });
  if (!checklist.blockers.length && !checklist.warnings.length) return true;

  const listMarkup = `
    <div class="publish-checklist">
      ${checklist.blockers.length ? `
        <strong>Fix before publishing</strong>
        <ul>${checklist.blockers.map((item) => `<li><i data-lucide="x-circle"></i><span>${escapeHtml(item)}</span></li>`).join("")}</ul>
      ` : ""}
      ${checklist.warnings.length ? `
        <strong>Recommended before publishing</strong>
        <ul>${checklist.warnings.map((item) => `<li><i data-lucide="triangle-alert"></i><span>${escapeHtml(item)}</span></li>`).join("")}</ul>
      ` : ""}
    </div>
  `;

  if (checklist.blockers.length) {
    await createFeedbackModal({
      title: "Complete publish checklist",
      message: "This lesson needs a few fixes before students can access it.",
      icon: "list-checks",
      tone: "danger",
      confirmText: "Keep Editing",
      cancelText: "Close",
      content: listMarkup
    });
    return false;
  }

  return showConfirmModal({
    title: "Publish with recommendations?",
    message: "The lesson can be published, but these improvements will make it clearer for students.",
    icon: "list-checks",
    confirmText: "Publish Anyway",
    cancelText: "Keep Editing",
    content: listMarkup
  });
}

function getLessonPublishChecklist(payload, { hasSelectedFile = false } = {}) {
  const title = String(payload.title || "").trim();
  const description = String(payload.description || "").trim();
  const lessonType = String(payload.lesson_type || "lesson");
  const sections = (payload.sections || []).filter((section) =>
    String(section.title || "").trim()
    || String(section.body || "").trim()
    || String(section.media_url || "").trim()
  );
  const questions = (payload.questions || []).filter((question) => String(question.prompt || "").trim());
  const hasResource = Boolean(
    hasSelectedFile
    || payload.file_id
    || String(payload.resource_url || "").trim()
    || sections.some((section) => String(section.media_url || "").trim())
  );
  const blockers = [];
  const warnings = [];

  if (!payload.module_id) blockers.push("Choose the module where this lesson belongs.");
  if (!title) blockers.push("Add a clear lesson title.");
  if (!sections.length && !hasResource) blockers.push("Add at least one content block, media resource, file, or resource URL.");
  if (["practice", "assessment"].includes(lessonType) && !questions.length) {
    blockers.push("Add at least one auto-graded question for this practice or assessment.");
  }
  if (payload.scheduled_date && payload.due_date && payload.due_date < payload.scheduled_date) {
    blockers.push("Set a due date that is the same day or later than the scheduled date.");
  }

  if (!description) warnings.push("Add a short description so students know what this lesson covers.");
  if (!payload.due_date && ["practice", "assessment"].includes(lessonType)) warnings.push("Add a due date for this practice or assessment.");
  if (!payload.duration_minutes || Number(payload.duration_minutes) < 5) warnings.push("Set a realistic duration so analytics and pacing are clearer.");
  if (lessonType === "lesson" && questions.length) warnings.push("This lesson contains practice questions. Consider changing the type to Practice if students should submit answers.");

  return { blockers, warnings };
}

async function handleStudentAction(event) {
  const button = event.target.closest("[data-student-action]");
  if (!button) {
    const row = event.target.closest("[data-student-row]");
    if (row?.dataset.id) {
      openStudentDrawer(row.dataset.id);
    }
    return;
  }
  const action = button.dataset.studentAction;
  const studentId = button.dataset.id;

  if (action === "view") {
    openStudentDrawer(studentId);
    return;
  }

  if (["approved", "inactive", "pending", "rejected"].includes(action)) {
    await updateStudentStatusFromDashboard(studentId, action);
    return;
  }

  await reviewStudent(studentId, action);
}

async function handleStudentAchievementCoverageAction(event) {
  const achievementsButton = event.target.closest("[data-student-coverage-achievements]");
  if (achievementsButton) {
    teacherState.achievementTab = "overview";
    showTeacherView("achievements");
    renderTeacherAchievementsDashboard();
    return;
  }
  const profileButton = event.target.closest("[data-student-coverage-profile]");
  if (profileButton?.dataset.studentCoverageProfile) {
    await openStudentFullProfile(profileButton.dataset.studentCoverageProfile);
  }
}

function handleStudentPagination(event) {
  const pageButton = event.target.closest("[data-page-number]");
  const navButton = event.target.closest("[data-page-nav]");
  if (pageButton) {
    teacherState.studentPage = Number(pageButton.dataset.pageNumber);
    renderTeacherStudents();
    return;
  }
  if (navButton?.dataset.pageNav === "prev") {
    teacherState.studentPage -= 1;
    renderTeacherStudents();
  }
  if (navButton?.dataset.pageNav === "next") {
    teacherState.studentPage += 1;
    renderTeacherStudents();
  }
}

function setupStudentTableKeyboard() {
  document.getElementById("studentsTable")?.addEventListener("keydown", (event) => {
    if (event.key !== "Enter" && event.key !== " ") return;
    const row = event.target.closest("[data-student-row]");
    if (!row?.dataset.id) return;
    event.preventDefault();
    openStudentDrawer(row.dataset.id);
  });
}

async function updateStudentStatusFromDashboard(studentId, status) {
  const message = document.getElementById("teacherMessage");
  const student = teacherState.students.find((item) => item.id === studentId);
  const studentName = student?.full_name || student?.username || "this student";
  if (["inactive", "rejected"].includes(status)) {
    await showConfirmModal({
      title: status === "inactive" ? "Deactivate student account?" : "Reject student account?",
      message: `${titleCase(status)} will limit ${studentName}'s access until a teacher changes the status again.`,
      tone: "danger",
      icon: status === "inactive" ? "user-x" : "circle-x",
      confirmText: status === "inactive" ? "Deactivate" : "Reject",
      processingText: "Updating...",
      onConfirm: async () => {
        await apiPatch("/api/teacher/student", { id: studentId, status });
        await loadTeacherData();
        showToast({ title: "Student status updated", message: `${studentName} is now ${status}.`, type: "success" });
        return true;
      }
    });
    return;
  }
  try {
    setMessage(message, "Updating student status...");
    await apiPatch("/api/teacher/student", { id: studentId, status });
    await loadTeacherData();
    setMessage(message, "Student status updated.", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

function openStudentDrawer(studentId) {
  const drawer = document.getElementById("studentDrawer");
  const form = document.getElementById("studentDrawerForm");
  const student = teacherState.students.find((item) => item.id === studentId);
  if (!drawer || !form || !student) return;

  const summary = getTeacherStudentSummary(student);
  teacherState.selectedStudentId = studentId;
  form.elements.id.value = student.id;
  form.elements.full_name.value = student.full_name || "";
  form.elements.grade_level.value = student.grade_level || "Grade 9";
  syncStudentDrawerSections(form, student.section || "");
  form.elements.adviser.value = student.adviser || form.elements.adviser.value || "";
  form.elements.home_town.value = student.home_town || "";
  form.elements.phone_number.value = student.phone_number || "";
  form.elements.status.value = student.status || "pending";
  setText("studentDrawerTitle", student.full_name || "Manage Student");
  setText("studentDrawerInitials", getInitials(student.full_name || student.username));
  setText("studentDrawerName", student.full_name || student.username);
  setText("studentDrawerMeta", `${student.grade_level || "-"} ${student.section ? `- ${student.section}` : ""}`);
  setText("drawerLessons", `${summary.completed}/${summary.total}`);
  setText("drawerAverage", `${summary.average}%`);
  setText("drawerBadges", summary.badges.length);
  setText("drawerCertificates", summary.certificates.length);
  drawer.classList.add("open");
  drawer.setAttribute("aria-hidden", "false");
  syncDrawerScrollLock();
  if (window.lucide) window.lucide.createIcons();
}

function closeStudentDrawer() {
  const drawer = document.getElementById("studentDrawer");
  if (!drawer) return;
  drawer.classList.remove("open");
  drawer.setAttribute("aria-hidden", "true");
  teacherState.selectedStudentId = "";
  syncDrawerScrollLock();
}

async function submitStudentDrawerForm(event) {
  event.preventDefault();
  const message = document.getElementById("teacherMessage");
  const form = event.currentTarget;
  const payload = formToObject(form);
  delete payload.student_number;
  payload.adviser = STUDENT_SECTION_ADVISERS[payload.grade_level]?.[payload.section] || payload.adviser || "";

  try {
    setMessage(message, "Saving student details...");
    await apiPatch("/api/teacher/student", payload);
    await loadTeacherData();
    closeStudentDrawer();
    setMessage(message, "Student details saved.", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

function handleStudentDrawerChange(event) {
  const form = event.currentTarget;
  if (event.target?.name === "grade_level") {
    syncStudentDrawerSections(form, "");
    return;
  }
  if (event.target?.name === "section") {
    const grade = form.elements.grade_level.value;
    form.elements.adviser.value = STUDENT_SECTION_ADVISERS[grade]?.[form.elements.section.value] || "";
  }
}

function syncStudentDrawerSections(form, selected = "") {
  if (!form?.elements?.grade_level || !form?.elements?.section) return;
  const grade = form.elements.grade_level.value || GRADE_LEVELS[0];
  const sections = sectionOptionsForGrade(grade);
  const section = sections.includes(selected) ? selected : sections[0] || "";
  form.elements.section.innerHTML = sectionOptionsHtml(grade, section, false);
  form.elements.section.value = section;
  if (form.elements.adviser) {
    form.elements.adviser.value = STUDENT_SECTION_ADVISERS[grade]?.[section] || "";
  }
}

async function handleStudentDrawerAction(event) {
  const profileButton = event.target.closest("[data-view-full-profile]");
  if (profileButton) {
    const form = document.getElementById("studentDrawerForm");
    const studentId = form?.elements.id.value;
    if (studentId) {
      closeStudentDrawer();
      await openStudentFullProfile(studentId);
    }
    return;
  }
  const button = event.target.closest("[data-drawer-status]");
  if (!button) return;
  const form = document.getElementById("studentDrawerForm");
  const studentId = form?.elements.id.value;
  if (!studentId) return;
  await updateStudentStatusFromDashboard(studentId, button.dataset.drawerStatus);
  closeStudentDrawer();
}

async function openStudentFullProfile(studentId, quarterId = "") {
  const requestedQuarterId = quarterId || teacherState.studentProfile?.quarterId || "";
  const keepPages = teacherState.selectedStudentId === studentId && teacherState.studentProfile?.quarterId === requestedQuarterId;
  teacherState.selectedStudentId = studentId;
  teacherState.studentProfile = {
    loading: true,
    error: "",
    data: null,
    quarterId: requestedQuarterId,
    pages: keepPages ? teacherState.studentProfile.pages : defaultStudentProfilePages()
  };
  showTeacherView("student-profile");
  renderStudentFullProfile();

  const query = new URLSearchParams({ id: studentId });
  if (quarterId) query.set("quarter_id", quarterId);
  try {
    const data = await apiGet(`/api/teacher/student?${query.toString()}`);
    teacherState.studentProfile = {
      loading: false,
      error: "",
      data,
      quarterId: data.selected_quarter?.id || "",
      pages: keepPages ? teacherState.studentProfile.pages : defaultStudentProfilePages()
    };
  } catch (error) {
    teacherState.studentProfile = {
      loading: false,
      error: error.message,
      data: null,
      quarterId: requestedQuarterId,
      pages: defaultStudentProfilePages()
    };
  }
  renderStudentFullProfile();
}

function renderStudentFullProfile() {
  const container = document.getElementById("studentFullProfile");
  if (!container) return;
  const state = teacherState.studentProfile || {};
  if (state.loading) {
    container.innerHTML = `
      <section class="panel-card student-profile-loading">
        <i data-lucide="loader-2"></i>
        <h2>Loading student profile...</h2>
        <p>Collecting lessons, attempts, awards, and profile records.</p>
      </section>
    `;
    if (window.lucide) window.lucide.createIcons();
    return;
  }
  if (state.error) {
    container.innerHTML = `
      <section class="panel-card student-profile-empty">
        <i data-lucide="alert-circle"></i>
        <h2>Unable to load profile</h2>
        <p>${escapeHtml(state.error)}</p>
        <button class="primary-button" type="button" data-profile-back><i data-lucide="arrow-left"></i><span>Back to Students</span></button>
      </section>
    `;
    if (window.lucide) window.lucide.createIcons();
    return;
  }
  if (!state.data?.student) {
    container.innerHTML = `
      <section class="panel-card student-profile-empty">
        <i data-lucide="user-search"></i>
        <h2>Select a student</h2>
        <p>Open a student from the Students page to view the full profile.</p>
        <button class="primary-button" type="button" data-profile-back><i data-lucide="arrow-left"></i><span>Back to Students</span></button>
      </section>
    `;
    if (window.lucide) window.lucide.createIcons();
    return;
  }

  const data = state.data;
  const student = data.student;
  const summary = buildFullStudentProfileSummary(data);
  container.innerHTML = `
    <div class="student-profile-shell">
      ${renderFullProfileHeader(data, summary)}
      ${renderFullProfileMetrics(summary)}
      <div class="student-profile-priority-grid">
        <div class="student-profile-column-stack">
          ${renderModuleMastery(summary)}
          ${renderPrePostComparison(summary)}
          ${renderProfileAchievements(summary)}
        </div>
        <div class="student-profile-column-stack">
          ${renderSupportSummary(summary)}
          ${renderProfileRecentActivity(summary)}
        </div>
      </div>
      ${renderStudentProfileWeakAnalysis(summary)}
      <div class="student-profile-record-stack">
        ${renderProfileLessonTable(summary)}
        ${renderAssessmentHistory(summary)}
      </div>
      ${renderSchoolAccountInfo(student, data)}
    </div>
  `;
  if (window.lucide) window.lucide.createIcons();
}

function renderFullProfileHeader(data, summary) {
  const student = data.student;
  const initials = getInitials(student.full_name || student.username);
  const quarterOptions = (data.quarters || []).map((quarter) =>
    `<option value="${escapeAttribute(quarter.id)}" ${quarter.id === data.selected_quarter?.id ? "selected" : ""}>${escapeHtml(quarter.title)} ${escapeHtml(quarter.school_year || "")}${quarter.id === data.active_quarter?.id ? " (Active)" : ""}</option>`
  ).join("");
  return `
    <section class="student-profile-hero">
      <div class="student-profile-top-actions">
        <button class="small-button" type="button" data-profile-back><i data-lucide="arrow-left"></i><span>Back to Students</span></button>
        <label class="profile-quarter-select"><span>Term</span><select data-profile-quarter>${quarterOptions}</select></label>
      </div>
      <div class="student-profile-identity">
        <div class="student-profile-avatar">
          ${data.avatar_url ? `<img src="${escapeAttribute(data.avatar_url)}" alt="${escapeAttribute(student.full_name || "Student")} profile picture" onerror="this.hidden=true;this.nextElementSibling.hidden=false;">` : ""}
          <span ${data.avatar_url ? "hidden" : ""}>${escapeHtml(initials)}</span>
        </div>
        <div>
          <p class="eyebrow">Full Student Profile</p>
          <h1>${escapeHtml(student.full_name || student.username)}</h1>
          <div class="student-profile-tags">
            <span class="status-pill ${escapeAttribute(student.status || "approved")}">${escapeHtml(titleCase(student.status || "approved"))}</span>
            <span><i data-lucide="user"></i>@${escapeHtml(student.username || "-")}</span>
            <span><i data-lucide="mail"></i>${escapeHtml(student.email || "-")}</span>
          </div>
        </div>
      </div>
      <div class="student-profile-meta">
        <article><span>Class</span><strong>${escapeHtml(student.grade_level || "-")} ${student.section ? `- ${escapeHtml(student.section)}` : ""}</strong></article>
        <article><span>Adviser</span><strong>${escapeHtml(student.adviser || "-")}</strong></article>
        <article><span>Registered</span><strong>${escapeHtml(formatDateOnly(student.created_at))}</strong></article>
        <article><span>Last Active</span><strong>${escapeHtml(summary.lastActiveLabel)}</strong></article>
      </div>
      <div class="student-profile-actions">
        <button class="primary-button compact" type="button" data-profile-edit="${escapeAttribute(student.id)}"><i data-lucide="pencil"></i><span>Edit Student</span></button>
        <button class="small-button" type="button" data-profile-award="badge"><i data-lucide="shield-check"></i><span>Award Badge</span></button>
        <button class="small-button" type="button" data-profile-award="certificate"><i data-lucide="file-badge"></i><span>Award Certificate</span></button>
      </div>
    </section>
  `;
}

function renderFullProfileMetrics(summary) {
  const metrics = [
    ["Overall Progress", `${summary.progressPercent}%`, "trending-up", "blue"],
    ["Lessons Completed", `${summary.completedLessons}/${summary.totalLessons}`, "book-check", "green"],
    ["Assessments Submitted", String(summary.assessmentAttempts.length), "send", "purple"],
    ["Average Assessment Score", summary.averageScore === null ? "-" : `${summary.averageScore}%`, "bar-chart-3", "orange"],
    ["Badges Awarded", String(summary.badges.length), "shield-check", "blue"],
    ["Certificates Issued", String(summary.certificates.length), "file-badge", "green"]
  ];
  return `<section class="student-profile-metrics">${metrics.map(([label, value, icon, color]) => `
    <article><i data-lucide="${icon}" class="${color}"></i><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></article>
  `).join("")}</section>`;
}

function renderStudentProfileWeakAnalysis(summary) {
  const risk = summary.riskAnalysis;
  const trend = risk.performanceTrend || { points: [], change: null, label: "No trend yet", target: 70 };
  const weakest = risk.weakAreas.slice(0, 5);
  return `
    <section class="student-weak-panel">
      <div class="student-weak-header">
        <div>
          <h2>Weak Area Analysis</h2>
          <p>Identify learning gaps, root causes, and actions to help ${escapeHtml(summary.studentName)} improve.</p>
        </div>
        <span>Last updated: ${escapeHtml(summary.lastActiveLabel)}</span>
      </div>
      <div class="student-weak-grid">
        <div class="student-weak-column">
          <article class="student-weak-card skill-breakdown">
            <div class="weak-card-heading">
              <div><h3>Skill Weakness Breakdown</h3><p>Mastery by related lessons and checks</p></div>
              <i data-lucide="target"></i>
            </div>
            <div class="weak-progress-list">
              ${weakest.length ? weakest.map((item) => renderWeakProgressRow(item.label, item.score, item.source, "score")).join("") : renderWeakEmpty("No weak areas detected from current records.")}
            </div>
            <button class="weak-link-button" type="button" data-profile-page="lessons" data-page-number="1" data-profile-scroll-target="profileLessonReport">View Lesson Progress Report <i data-lucide="external-link"></i></button>
          </article>
          <article class="student-weak-card trend-card">
            <div class="weak-card-heading">
              <div><h3>Recent Performance Trend</h3><p>Latest assessment weeks</p></div>
              <span class="risk-level-pill ${escapeAttribute(risk.riskLevel.key)}">${escapeHtml(risk.riskLevel.label)}</span>
            </div>
            ${renderProfileTrendChart(trend)}
          </article>
        </div>
        <div class="student-weak-column">
          <article class="student-weak-card root-cause-card">
            <div class="weak-card-heading">
              <div><h3>Root Cause Analysis</h3><p>Signals affecting progress</p></div>
              <i data-lucide="list-tree"></i>
            </div>
            <div class="root-cause-list">
              ${risk.rootCauses.length ? risk.rootCauses.map((cause) => `
                <article class="${escapeAttribute(cause.tone)}">
                  <span><i data-lucide="${escapeAttribute(cause.icon)}"></i></span>
                  <div><strong>${escapeHtml(cause.label)}</strong><small>${escapeHtml(cause.detail)}</small></div>
                </article>
              `).join("") : renderWeakEmpty("No root causes identified from available data.")}
            </div>
            ${risk.riskScore >= 35 ? `<p class="root-cause-note"><i data-lucide="alert-triangle"></i>${escapeHtml(risk.diagnosis)}</p>` : ""}
          </article>
          <article class="student-weak-card profile-intervention-card">
            <div class="weak-card-heading">
              <div><h3>Recommended Interventions</h3><p>Teacher actions</p></div>
              <i data-lucide="clipboard-check"></i>
            </div>
            <div class="profile-intervention-list">
              ${risk.recommendedActions.map((action, index) => `
                <article class="${escapeAttribute(action.tone)}">
                  <b>${index + 1}</b>
                  <div><strong>${escapeHtml(action.label)}</strong><small>${escapeHtml(action.detail)}</small></div>
                  <button type="button" data-profile-page="${action.label.toLowerCase().includes("assessment") ? "assessments" : "lessons"}" data-page-number="1" data-profile-scroll-target="${action.label.toLowerCase().includes("assessment") ? "profileAssessmentReport" : "profileLessonReport"}">Review <i data-lucide="chevron-right"></i></button>
                </article>
              `).join("")}
            </div>
          </article>
        </div>
      </div>
    </section>
  `;
}

function renderProfileTrendChart(trend) {
  if (!trend.points.length) {
    return `
      <div class="profile-trend-empty">
        <i data-lucide="line-chart"></i>
        <p>No scored submissions are available for a trend yet.</p>
      </div>
    `;
  }
  const width = 520;
  const height = 196;
  const chartTop = 28;
  const chartHeight = 112;
  const paddingX = 40;
  const plotWidth = width - paddingX * 2;
  const points = trend.points.map((point, index) => {
    const x = trend.points.length === 1 ? width / 2 : paddingX + (index / (trend.points.length - 1)) * plotWidth;
    const y = chartTop + chartHeight - (Number(point.score || 0) / 100) * chartHeight;
    return { ...point, x, y };
  });
  const line = points.map((point) => `${point.x},${point.y}`).join(" ");
  const lastPoint = points[points.length - 1];
  const changeLabel = trend.change === null ? "No comparison yet" : `${trend.change >= 0 ? "+" : ""}${trend.change}% vs first week`;
  return `
    <div class="profile-trend-chart">
      <svg viewBox="0 0 ${width} ${height}" role="img" aria-label="Recent performance trend">
        <g class="profile-trend-grid">
          ${[0, 25, 50, 75, 100].map((value) => {
            const y = chartTop + chartHeight - (value / 100) * chartHeight;
            return `<line x1="${paddingX}" y1="${y}" x2="${width - paddingX}" y2="${y}"></line><text x="8" y="${y + 4}">${value}%</text>`;
          }).join("")}
        </g>
        <polyline class="profile-trend-line" points="${line}"></polyline>
        ${points.map((point) => `
          <circle class="profile-trend-point" cx="${point.x}" cy="${point.y}" r="6"></circle>
          <text class="profile-trend-value" x="${point.x}" y="${point.y - 12}">${point.score}%</text>
          <text class="profile-trend-label" x="${point.x}" y="174">${escapeHtml(point.label)}</text>
        `).join("")}
        <line class="profile-target-line" x1="${paddingX}" y1="${chartTop + chartHeight - (trend.target / 100) * chartHeight}" x2="${width - paddingX}" y2="${chartTop + chartHeight - (trend.target / 100) * chartHeight}"></line>
      </svg>
      <div class="trend-summary-pill ${trend.change !== null && trend.change < 0 ? "down" : "up"}">
        <i data-lucide="${trend.change !== null && trend.change < 0 ? "trending-down" : "trending-up"}"></i>
        <strong>${escapeHtml(trend.label)}</strong>
        <span>${escapeHtml(changeLabel)}</span>
      </div>
      <div class="trend-target-box"><span>Target</span><strong>${trend.target}%</strong><small>End of term goal</small></div>
      ${lastPoint ? `<p class="trend-latest">Latest visible score: <strong>${lastPoint.score}%</strong></p>` : ""}
    </div>
  `;
}

function renderSchoolAccountInfo(student, data) {
  const rows = [
    ["Full Name", student.full_name || "-"],
    ["First Name", student.first_name || "-"],
    ["Last Name", student.last_name || "-"],
    ["Username", student.username ? `@${student.username}` : "-"],
    ["Email", student.email || "-"],
    ["Grade Level", student.grade_level || "-"],
    ["Section", student.section || "-"],
    ["Adviser", student.adviser || "-"],
    ["Phone Number", student.phone_number || "-"],
    ["Home Town", student.home_town || "-"],
    ["Account Status", titleCase(student.status || "approved")],
    ["Registration Date", formatDate(student.created_at)],
    ["Selected Term", data.selected_quarter ? `${data.selected_quarter.title} ${data.selected_quarter.school_year || ""}` : "No active term"]
  ];
  return `<section class="panel-card profile-info-card"><h2>School and Account</h2><dl>${rows.map(([label, value]) =>
    `<div><dt>${escapeHtml(label)}</dt><dd>${escapeHtml(value)}</dd></div>`
  ).join("")}</dl></section>`;
}

function renderModuleMastery(summary) {
  if (!summary.modules.length) {
    return `<section class="panel-card profile-module-mastery"><h2>Mastery Overview</h2><p class="empty-text">No published modules are available for this student in the selected quarter.</p></section>`;
  }
  return `<section class="panel-card profile-module-mastery">
    <h2>Mastery Overview</h2>
    <div class="profile-module-list">${summary.moduleMastery.map((item) => `
      <article>
        <div><strong>${escapeHtml(item.module.title)}</strong><span>${item.completed}/${item.total} lessons completed</span></div>
        <b>${item.percent}%</b>
        <div class="wide-progress"><span style="width:${item.percent}%"></span></div>
        <small>${escapeHtml(item.averageScore === null ? "No scores yet" : `${item.averageScore}% latest average`)} - ${escapeHtml(item.status)}</small>
      </article>
    `).join("")}</div>
  </section>`;
}

function renderPrePostComparison(summary) {
  const pre = summary.pretest;
  const post = summary.posttest;
  const improvement = pre.score !== null && post.score !== null ? post.score - pre.score : null;
  return `<section class="panel-card profile-prepost-card">
    <h2>Pretest and Posttest</h2>
    ${summary.evaluationCycle ? `
      <div class="prepost-grid">
        ${renderPrePostTile("Pretest", pre)}
        ${renderPrePostTile("Posttest", post)}
        <article><span>Improvement</span><strong>${improvement === null ? "Pending" : `${improvement >= 0 ? "+" : ""}${improvement} pts`}</strong><small>Percentage-point change</small></article>
      </div>
    ` : `<p class="empty-text">No mapped evaluation cycle is available for this student in the selected term.</p>`}
  </section>`;
}

function renderPrePostTile(label, item) {
  return `<article><span>${escapeHtml(label)}</span><strong>${item.score === null ? "Pending" : `${item.score}%`}</strong><small>${item.attempt ? `Submitted ${formatDate(item.attempt.submitted_at)}` : "Not submitted yet"}</small></article>`;
}

function renderProfileLessonTable(summary) {
  const page = getStudentProfilePage("lessons", summary.lessonRows.length);
  const start = (page - 1) * PROFILE_PAGE_SIZE;
  const visibleRows = summary.lessonRows.slice(start, start + PROFILE_PAGE_SIZE);
  const rows = visibleRows.length ? visibleRows.map((row) => `
    <tr>
      <td>${escapeHtml(row.lesson.title)}</td>
      <td>${escapeHtml(row.module?.title || "-")}</td>
      <td>${escapeHtml(titleCase(row.lesson.lesson_type || "lesson"))}</td>
      <td>${row.percent}%</td>
      <td><span class="status-pill ${escapeAttribute(row.statusClass)}">${escapeHtml(row.statusLabel)}</span></td>
      <td>${row.latestScore === null ? "-" : `${row.latestScore}%`}</td>
      <td>${escapeHtml(formatDate(row.progress?.started_at))}</td>
      <td>${escapeHtml(formatDate(row.progress?.completed_at))}</td>
      <td>${escapeHtml(formatDate(row.progress?.updated_at))}</td>
    </tr>
  `).join("") : `<tr><td colspan="9" class="empty-cell">No selected-term lessons are available for this student.</td></tr>`;
  return `<section class="panel-card profile-table-card" id="profileLessonReport">
    <div class="profile-card-heading"><h2>Lesson Progress</h2><span>${summary.lessonRows.length} record${summary.lessonRows.length === 1 ? "" : "s"}</span></div>
    <div class="profile-table-wrap"><table><thead><tr><th>Lesson</th><th>Module</th><th>Type</th><th>Progress</th><th>Status</th><th>Latest Score</th><th>Started</th><th>Completed</th><th>Updated</th></tr></thead><tbody>${rows}</tbody></table></div>
    ${renderProfilePagination("lessons", summary.lessonRows.length)}
  </section>`;
}

function renderAssessmentHistory(summary) {
  const page = getStudentProfilePage("assessments", summary.assessmentAttempts.length);
  const start = (page - 1) * PROFILE_PAGE_SIZE;
  const visibleAttempts = summary.assessmentAttempts.slice(start, start + PROFILE_PAGE_SIZE);
  const rows = visibleAttempts.length ? visibleAttempts.map((attempt) => {
    const lesson = summary.lessonById.get(attempt.lesson_id);
    return `<tr><td>${escapeHtml(lesson?.title || "Assessment")}</td><td>${escapeHtml(titleCase(lesson?.lesson_type || "practice"))}</td><td>${Number(attempt.score_percent || 0)}%</td><td>${Number(attempt.correct_count || 0)}</td><td>${Number(attempt.total_points || 0)}</td><td>${formatSeconds(attempt.time_spent_seconds)}</td><td>${escapeHtml(formatDate(attempt.submitted_at))}</td></tr>`;
  }).join("") : `<tr><td colspan="7" class="empty-cell">No practice or assessment submissions yet.</td></tr>`;
  return `<section class="panel-card profile-table-card" id="profileAssessmentReport">
    <div class="profile-card-heading"><h2>Assessment History</h2><span>${summary.assessmentAttempts.length} record${summary.assessmentAttempts.length === 1 ? "" : "s"}</span></div>
    <div class="profile-table-wrap"><table><thead><tr><th>Assessment</th><th>Type</th><th>Score</th><th>Correct</th><th>Total Points</th><th>Time Spent</th><th>Submitted</th></tr></thead><tbody>${rows}</tbody></table></div>
    ${renderProfilePagination("assessments", summary.assessmentAttempts.length)}
  </section>`;
}

function renderProfileAchievements(summary) {
  const page = getStudentProfilePage("achievements", summary.achievementRows.length);
  const start = (page - 1) * PROFILE_PAGE_SIZE;
  const visibleRows = summary.achievementRows.slice(start, start + PROFILE_PAGE_SIZE);
  const rows = visibleRows.length ? visibleRows.map((item) => `
    <li>
      <i data-lucide="${item.type === "badge" ? "shield-check" : "file-badge"}" class="${escapeAttribute(item.color || "blue")}"></i>
      <span><strong>${escapeHtml(item.title)}</strong><em>${escapeHtml(titleCase(item.type))}</em></span>
      <small>${escapeHtml(formatDateOnly(item.awarded_at))}</small>
    </li>
  `).join("") : `<li class="empty-text">No badges or certificates awarded yet.</li>`;
  return `<section class="panel-card profile-achievements-card">
    <div class="profile-card-heading"><h2>Achievements</h2><span>${summary.achievementRows.length} award${summary.achievementRows.length === 1 ? "" : "s"}</span></div>
    <ul>${rows}</ul>
    ${renderProfilePagination("achievements", summary.achievementRows.length)}
  </section>`;
}

function renderProfileRecentActivity(summary) {
  const page = getStudentProfilePage("activity", summary.activities.length);
  const start = (page - 1) * PROFILE_PAGE_SIZE;
  const visibleRows = summary.activities.slice(start, start + PROFILE_PAGE_SIZE);
  const rows = visibleRows.length ? visibleRows.map((activity) => `
    <article><i data-lucide="${escapeAttribute(activity.icon)}"></i><div><strong>${escapeHtml(activity.label)}</strong><span>${escapeHtml(activity.detail)}</span></div><time>${escapeHtml(formatDate(activity.date))}</time></article>
  `).join("") : `<p class="empty-text">No recent activity for this selected term yet.</p>`;
  return `<section class="panel-card profile-activity-card">
    <div class="profile-card-heading"><h2>Recent Activity</h2><span>${summary.activities.length} event${summary.activities.length === 1 ? "" : "s"}</span></div>
    ${rows}
    ${renderProfilePagination("activity", summary.activities.length)}
  </section>`;
}

function getStudentProfilePage(key, total) {
  const pages = teacherState.studentProfile?.pages || defaultStudentProfilePages();
  const pageCount = Math.max(1, Math.ceil(total / PROFILE_PAGE_SIZE));
  const current = Math.min(Math.max(Number(pages[key] || 1), 1), pageCount);
  pages[key] = current;
  teacherState.studentProfile.pages = pages;
  return current;
}

function renderProfilePagination(key, total) {
  const pageCount = Math.max(1, Math.ceil(total / PROFILE_PAGE_SIZE));
  if (pageCount <= 1) return "";
  const current = getStudentProfilePage(key, total);
  const start = (current - 1) * PROFILE_PAGE_SIZE + 1;
  const end = Math.min(total, current * PROFILE_PAGE_SIZE);
  return `<div class="profile-pagination-row">
    <span>Showing ${start}-${end} of ${total}</span>
    ${renderDashboardPagination({ currentPage: current, pageCount, pageNumberAttribute: "data-page-number", extraAttributes: `data-profile-page="${escapeAttribute(key)}"`, label: `${titleCase(key)} profile pages`, className: "profile-pagination" })}
  </div>`;
}

function renderSupportSummary(summary) {
  const signals = summary.supportSignals;
  const risk = summary.riskAnalysis;
  return `<section class="panel-card profile-support-card">
    <div class="profile-card-heading">
      <h2>Support Summary</h2>
      <span class="risk-level-pill ${escapeAttribute(risk.riskLevel.key)}">${escapeHtml(risk.riskLevel.label)}</span>
    </div>
    <strong class="${risk.riskScore >= 35 ? "warn-text" : "good-text"}">${risk.riskScore}% risk score</strong>
    <p class="profile-support-diagnosis">${escapeHtml(risk.diagnosis)}</p>
    ${risk.reasons.length ? `<div class="profile-support-chips">${risk.reasons.map((reason) => `<span class="${escapeAttribute(reason.tone)}">${escapeHtml(reason.label)}</span>`).join("")}</div>` : ""}
    <dl>
      <div><dt>Weakest Module</dt><dd>${escapeHtml(signals.weakestModule)}</dd></div>
      <div><dt>Missing Work</dt><dd>${escapeHtml(signals.missingWork)}</dd></div>
      <div><dt>Suggested Next Action</dt><dd>${escapeHtml(risk.primaryAction.detail || signals.nextAction)}</dd></div>
    </dl>
  </section>`;
}

function buildFullStudentProfileSummary(data) {
  const modules = data.modules || [];
  const lessons = data.lessons || [];
  const progress = data.progress || [];
  const attempts = [...(data.attempts || [])].sort((a, b) => new Date(b.submitted_at || 0) - new Date(a.submitted_at || 0));
  const badges = data.badges || [];
  const certificates = data.certificates || [];
  const achievementRows = [
    ...badges.map((badge) => ({ ...badge, type: "badge", awarded_at: badge.awarded_at, color: badge.color || "blue" })),
    ...certificates.map((certificate) => ({ ...certificate, type: "certificate", awarded_at: certificate.awarded_at, color: "gold" }))
  ].sort((a, b) => new Date(b.awarded_at || 0) - new Date(a.awarded_at || 0));
  const lessonById = new Map(lessons.map((lesson) => [lesson.id, lesson]));
  const moduleById = new Map(modules.map((module) => [module.id, module]));
  const progressByLesson = new Map(progress.map((item) => [item.lesson_id, item]));
  const latestAttemptByLesson = new Map();
  attempts.forEach((attempt) => {
    if (!latestAttemptByLesson.has(attempt.lesson_id)) latestAttemptByLesson.set(attempt.lesson_id, attempt);
  });
  const completedLessons = lessons.filter((lesson) => {
    const item = progressByLesson.get(lesson.id);
    return item?.status === "completed" || Number(item?.progress_percent || 0) >= 100;
  }).length;
  const practiceLessonIds = new Set(lessons.filter((lesson) => ["practice", "assessment"].includes(lesson.lesson_type)).map((lesson) => lesson.id));
  const assessmentAttempts = attempts.filter((attempt) => practiceLessonIds.has(attempt.lesson_id));
  const scored = assessmentAttempts.map((attempt) => Number(attempt.score_percent)).filter(Number.isFinite);
  const lessonRows = lessons.map((lesson) => {
    const item = progressByLesson.get(lesson.id) || null;
    const attempt = latestAttemptByLesson.get(lesson.id) || null;
    const percent = Math.min(100, Math.max(0, Number(item?.progress_percent || 0)));
    const completed = item?.status === "completed" || percent >= 100;
    return {
      lesson,
      module: moduleById.get(lesson.module_id),
      progress: item,
      attempt,
      percent,
      latestScore: attempt ? Number(attempt.score_percent || 0) : Number.isFinite(Number(item?.score_percent)) ? Number(item.score_percent) : null,
      statusLabel: completed ? "Completed" : percent ? "In Progress" : "Not Started",
      statusClass: completed ? "published" : percent ? "draft" : "pending"
    };
  });
  const moduleMastery = modules.map((module) => {
    const moduleLessons = lessons.filter((lesson) => lesson.module_id === module.id);
    const completed = moduleLessons.filter((lesson) => {
      const item = progressByLesson.get(lesson.id);
      return item?.status === "completed" || Number(item?.progress_percent || 0) >= 100;
    }).length;
    const scores = moduleLessons.map((lesson) => latestAttemptByLesson.get(lesson.id)?.score_percent).map(Number).filter(Number.isFinite);
    const percent = percentOf(completed, moduleLessons.length);
    const averageScore = scores.length ? Math.round(scores.reduce((sum, score) => sum + score, 0) / scores.length) : null;
    return {
      module,
      total: moduleLessons.length,
      completed,
      percent,
      averageScore,
      status: percent === 0 ? "Not Started" : percent < 60 ? "Developing" : percent < 80 ? "Proficient" : "Mastered"
    };
  });
  const evaluationCycle = data.evaluation_cycle || null;
  const pretest = buildProfileTestStatus(evaluationCycle?.pretest_lesson_id, latestAttemptByLesson);
  const posttest = buildProfileTestStatus(evaluationCycle?.posttest_lesson_id, latestAttemptByLesson);
  const activities = buildProfileActivities({ progress, attempts, badges, certificates, lessonById });
  const lastActive = activities[0]?.date || progress.map((item) => item.updated_at).filter(Boolean).sort().pop() || null;
  const supportSignals = buildProfileSupportSignals({
    averageScore: scored.length ? Math.round(scored.reduce((sum, score) => sum + score, 0) / scored.length) : null,
    incompleteCount: lessons.length - completedLessons,
    pretest,
    posttest,
    lastActive,
    moduleMastery
  });
  const analyticsContext = buildTeacherAnalyticsContext({
    students: [data.student],
    assessments: lessons
      .filter((lesson) => ["practice", "assessment"].includes(lesson.lesson_type))
      .map((lesson) => ({
        ...lesson,
        lesson_id: lesson.id,
        grade_level: data.student.grade_level,
        module_id: lesson.module_id,
        module_title: moduleById.get(lesson.module_id)?.title || "",
        module_category: moduleById.get(lesson.module_id)?.category || ""
      })),
    attempts,
    progress,
    modules,
    lessons,
    quarterId: data.selected_quarter?.id || ""
  });
  const riskAnalysis = calculateStudentRiskAnalysis(data.student, analyticsContext);
  return {
    studentName: data.student.full_name || data.student.username || "this student",
    modules,
    lessons,
    progress,
    attempts,
    badges,
    certificates,
    achievementRows,
    lessonById,
    moduleById,
    completedLessons,
    totalLessons: lessons.length,
    progressPercent: percentOf(completedLessons, lessons.length),
    assessmentAttempts,
    averageScore: scored.length ? Math.round(scored.reduce((sum, score) => sum + score, 0) / scored.length) : null,
    lessonRows,
    moduleMastery,
    evaluationCycle,
    pretest,
    posttest,
    activities,
    supportSignals,
    riskAnalysis,
    lastActiveLabel: lastActive ? formatDate(lastActive) : "No activity"
  };
}

function buildProfileTestStatus(lessonId, latestAttemptByLesson) {
  const attempt = lessonId ? latestAttemptByLesson.get(lessonId) || null : null;
  return {
    lessonId: lessonId || "",
    attempt,
    score: attempt ? Number(attempt.score_percent || 0) : null
  };
}

function buildProfileActivities({ progress, attempts, badges, certificates, lessonById }) {
  const rows = [];
  progress.forEach((item) => {
    const lesson = lessonById.get(item.lesson_id);
    if (item.started_at) rows.push({ date: item.started_at, icon: "play-circle", label: "Lesson started", detail: lesson?.title || "Lesson" });
    if (item.completed_at) rows.push({ date: item.completed_at, icon: "check-circle-2", label: "Lesson completed", detail: lesson?.title || "Lesson" });
  });
  attempts.forEach((attempt) => {
    const lesson = lessonById.get(attempt.lesson_id);
    rows.push({
      date: attempt.submitted_at,
      icon: lesson?.lesson_type === "assessment" ? "clipboard-check" : "send",
      label: lesson?.lesson_type === "assessment" ? "Assessment submitted" : "Practice submitted",
      detail: `${lesson?.title || "Assessment"} - ${Number(attempt.score_percent || 0)}%`
    });
  });
  badges.forEach((badge) => rows.push({ date: badge.awarded_at, icon: "shield-check", label: "Badge awarded", detail: formatAwardDisplayTitle(badge.title, "Badge") }));
  certificates.forEach((certificate) => rows.push({ date: certificate.awarded_at, icon: "file-badge", label: "Certificate awarded", detail: certificate.title }));
  return rows.filter((item) => item.date).sort((a, b) => new Date(b.date) - new Date(a.date));
}

function buildProfileSupportSignals({ averageScore, incompleteCount, pretest, posttest, lastActive, moduleMastery }) {
  const weakest = [...moduleMastery].sort((a, b) => (a.averageScore ?? a.percent) - (b.averageScore ?? b.percent))[0] || null;
  const inactiveCutoff = Date.now() - EVIDENCE_SUPPORT_DAYS * 24 * 60 * 60 * 1000;
  const missingTests = [pretest.score === null ? "pretest" : "", posttest.score === null ? "posttest" : ""].filter(Boolean);
  const needsSupport = (averageScore !== null && averageScore < 60) || incompleteCount >= 2 || missingTests.length > 0 || !lastActive || new Date(lastActive).getTime() < inactiveCutoff;
  const nextAction = averageScore !== null && averageScore < 60
    ? "Review recent assessment feedback and assign focused practice."
    : incompleteCount >= 2
      ? "Ask the student to continue the oldest incomplete lesson."
      : missingTests.length
        ? `Complete missing ${missingTests.join(" and ")}.`
        : "Continue regular monitoring.";
  return {
    status: needsSupport ? "Needs Support" : "On Track",
    weakestModule: weakest ? weakest.module.title : "No module data yet",
    missingWork: incompleteCount ? `${incompleteCount} incomplete lesson${incompleteCount === 1 ? "" : "s"}${missingTests.length ? `, missing ${missingTests.join(" and ")}` : ""}` : (missingTests.length ? `Missing ${missingTests.join(" and ")}` : "No missing work detected"),
    nextAction
  };
}

async function handleStudentFullProfileAction(event) {
  const pageButton = event.target.closest("[data-profile-page]");
  if (pageButton) {
    const key = pageButton.dataset.profilePage;
    const pageNumber = Number(pageButton.dataset.pageNumber || 1);
    const scrollTargetId = pageButton.dataset.profileScrollTarget;
    if (key && teacherState.studentProfile?.pages) {
      teacherState.studentProfile.pages[key] = pageNumber;
      renderStudentFullProfile();
      if (scrollTargetId) {
        requestAnimationFrame(() => {
          document.getElementById(scrollTargetId)?.scrollIntoView({ behavior: "smooth", block: "start" });
        });
      }
    }
    return;
  }
  const backButton = event.target.closest("[data-profile-back]");
  if (backButton) {
    showTeacherView("students");
    return;
  }
  const editButton = event.target.closest("[data-profile-edit]");
  if (editButton) {
    showTeacherView("students");
    openStudentDrawer(editButton.dataset.profileEdit);
    return;
  }
  const awardButton = event.target.closest("[data-profile-award]");
  if (!awardButton) return;
  await awardStudentFromProfile(awardButton.dataset.profileAward);
}

async function handleStudentFullProfileChange(event) {
  const select = event.target.closest("[data-profile-quarter]");
  if (!select || !teacherState.selectedStudentId) return;
  teacherState.studentProfile.pages = defaultStudentProfilePages();
  await openStudentFullProfile(teacherState.selectedStudentId, select.value);
}

async function awardStudentFromProfile(type) {
  const data = teacherState.studentProfile?.data;
  const studentId = data?.student?.id;
  if (!studentId) return;
  const label = type === "certificate" ? "certificate" : "badge";
  if (label === "certificate") {
    openGenerateCertificateModal(studentId);
    return;
  }
  await showInputModal({
    title: `Award ${titleCase(label)}`,
    message: `Create a ${label} award for ${data.student.full_name || data.student.username}.`,
    label: `${titleCase(label)} title`,
    placeholder: `Enter ${label} title`,
    suggestions: REWARD_TEMPLATES[label] || [],
    icon: label === "badge" ? "shield-check" : "file-badge",
    confirmText: `Award ${titleCase(label)}`,
    processingText: "Awarding...",
    errorTitle: `Unable to award ${label}`,
    errorMessage: `The ${label} was not added. Please try again.`,
    onConfirm: async (title) => {
      await apiPost("/api/teacher/student-reward", {
        reward_type: label,
        action: "add",
        student_id: studentId,
        title,
        color: label === "badge" ? "blue" : "gold"
      });
      await loadTeacherData();
      await openStudentFullProfile(studentId, teacherState.studentProfile?.quarterId || "");
      showToast({
        title: `${titleCase(label)} awarded`,
        message: `The ${label} was successfully added to ${(data.student.full_name || data.student.username || "the student")}'s profile.`,
        type: "success"
      });
      return true;
    }
  });
}

function formatSeconds(value) {
  const seconds = Number(value || 0);
  if (!seconds) return "-";
  const minutes = Math.floor(seconds / 60);
  const remainder = seconds % 60;
  return minutes ? `${minutes}m ${remainder}s` : `${remainder}s`;
}

function exportStudentsCsv() {
  const summaries = teacherState.students.map((student) => getTeacherStudentSummary(student));
  const rows = [
    ["Student Name", "Email", "Grade Level", "Section", "Lessons Completed", "Average Score", "Badges", "Certificates", "Last Active", "Status"],
    ...summaries.map((summary) => [
      summary.student.full_name || summary.student.username,
      summary.student.email,
      summary.student.grade_level || "",
      summary.student.section || "",
      `${summary.completed}/${summary.total}`,
      `${summary.average}%`,
      summary.badges.length,
      summary.certificates.length,
      summary.lastActive,
      summary.student.status === "approved" ? summary.progressStatus : titleCase(summary.student.status)
    ])
  ];
  const csv = rows.map((row) => row.map((cell) => `"${String(cell ?? "").replace(/"/g, '""')}"`).join(",")).join("\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `techwise-students-${new Date().toISOString().slice(0, 10)}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}

async function handleQuarterAction(event) {
  const editButton = event.target.closest("[data-quarter-edit]");
  const activeButton = event.target.closest("[data-quarter-active]");
  const archiveButton = event.target.closest("[data-quarter-archive]");
  const deleteButton = event.target.closest("[data-quarter-delete]");
  if (editButton) {
    openQuarterEditor(editButton.dataset.quarterEdit);
    return;
  }
  if (!activeButton && !archiveButton && !deleteButton) return;

  const message = document.getElementById("teacherMessage");

  try {
    if (activeButton) {
      setMessage(message, "Setting active term...");
      await apiPatch("/api/teacher/quarters", { id: activeButton.dataset.quarterActive, is_active: true, status: "active" });
      await loadTeacherData();
      setMessage(message, "Active term updated.", "success");
      return;
    }

    if (archiveButton) {
      const status = archiveButton.dataset.status;
      const quarter = teacherState.quarters.find((item) => item.id === archiveButton.dataset.quarterArchive);
      if (status === "archived") {
        await showConfirmModal({
          title: "Archive term?",
          message: `${quarter?.title || "This term"} will be hidden from active term workflows but can be restored later.`,
          tone: "danger",
          icon: "archive",
          confirmText: "Archive Term",
          processingText: "Archiving...",
          onConfirm: async () => {
            await apiPatch("/api/teacher/quarters", { id: archiveButton.dataset.quarterArchive, status });
            await loadTeacherData();
            showToast({ title: "Term archived", message: quarter?.title || "Term archived.", type: "success" });
            return true;
          }
        });
        return;
      }
      setMessage(message, "Restoring term...");
      await apiPatch("/api/teacher/quarters", { id: archiveButton.dataset.quarterArchive, status });
      await loadTeacherData();
      setMessage(message, "Term restored.", "success");
      return;
    }

    const quarter = teacherState.quarters.find((item) => item.id === deleteButton.dataset.quarterDelete);
    const moduleCount = teacherState.modules.filter((module) => module.quarter_id === quarter?.id).length;
    const lessonCount = teacherState.lessons.filter((lesson) => lesson.quarter_id === quarter?.id).length;
    await showConfirmModal({
      title: "Delete term?",
      message: `${quarter?.title || "This term"} will be deleted. ${moduleCount} modules and ${lessonCount} lessons will stay in the system but will no longer be attached to this term.`,
      tone: "danger",
      icon: "trash-2",
      confirmText: "Delete Term",
      processingText: "Deleting...",
      onConfirm: async () => {
        await apiDelete("/api/teacher/quarters", { id: deleteButton.dataset.quarterDelete });
        await loadTeacherData();
        resetQuarterForm();
        showToast({ title: "Term deleted", message: quarter?.title || "Term deleted.", type: "success" });
        return true;
      }
    });
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

async function handleContentAction(event) {
  const editButton = event.target.closest("[data-edit-lesson]");
  const toggleButton = event.target.closest("[data-toggle-lesson]");
  const previewButton = event.target.closest("[data-preview-lesson]");
  const archiveButton = event.target.closest("[data-archive-lesson]");

  if (editButton) {
    openLessonDrawer(editButton.dataset.editLesson);
    return;
  }

  if (previewButton) {
    await previewLesson(previewButton.dataset.previewLesson);
    return;
  }

  if (toggleButton) {
    const message = document.getElementById("teacherMessage");
    try {
      setMessage(message, "Updating lesson status...");
      await apiPatch("/api/teacher/lessons", {
        id: toggleButton.dataset.toggleLesson,
        status: toggleButton.dataset.status
      });
      await loadTeacherData();
      setMessage(message, "Lesson status updated.", "success");
    } catch (error) {
      setMessage(message, error.message, "error");
    }
    return;
  }

  if (archiveButton) {
    const message = document.getElementById("teacherMessage");
    const lesson = teacherState.lessons.find((item) => item.id === archiveButton.dataset.archiveLesson);
    await showConfirmModal({
      title: "Archive lesson?",
      message: `${lesson?.title || "This lesson"} will be removed from active student lesson lists but can still be restored from the teacher library.`,
      tone: "danger",
      icon: "archive",
      confirmText: "Archive Lesson",
      processingText: "Archiving...",
      onConfirm: async () => {
        await apiPatch("/api/teacher/lessons", {
          id: archiveButton.dataset.archiveLesson,
          status: "archived"
        });
        await loadTeacherData();
        showToast({ title: "Lesson archived", message: lesson?.title || "Lesson archived.", type: "success" });
        return true;
      }
    });
  }
}

async function reviewStudent(studentId, action) {
  const endpoint = action === "approve" ? "/api/teacher/approve-student" : "/api/teacher/reject-student";
  const isApprove = action === "approve";
  const student = teacherState.students.find((item) => item.id === studentId);
  const studentName = student?.full_name || student?.username || "this student";
  const classParts = [student?.grade_level, student?.section ? `Section ${student.section}` : ""].filter(Boolean).join(" - ");

  await showConfirmModal({
    title: isApprove ? "Approve student account?" : "Reject student account?",
    message: isApprove
      ? `Approve ${studentName}${classParts ? ` for ${classParts}` : ""}? The student will be able to sign in and access the TechWise 360 learning platform.`
      : `Reject the registration request from ${studentName}? The student will not be able to access the platform.`,
    tone: isApprove ? "primary" : "danger",
    icon: isApprove ? "user-check" : "user-x",
    confirmText: isApprove ? "Approve Account" : "Reject Account",
    processingText: isApprove ? "Approving..." : "Rejecting...",
    errorTitle: isApprove ? "Unable to approve account" : "Unable to reject registration",
    errorMessage: isApprove ? "The account was not changed. Please try again." : "The request was not changed. Please try again.",
    onConfirm: async () => {
      await apiPost(endpoint, { student_id: studentId });
      await loadTeacherData();
      showToast({
        title: isApprove ? "Account approved" : "Registration rejected",
        message: isApprove ? `${studentName} can now access TechWise 360.` : `The request from ${studentName} was rejected.`,
        type: isApprove ? "success" : "warning"
      });
      return true;
    }
  });
}

async function fillLessonForm(lessonId) {
  const lesson = teacherState.lessons.find((item) => item.id === lessonId);
  const form = document.getElementById("lessonForm");
  if (!lesson || !form) return;
  form.elements.id.value = lesson.id;
  form.elements.file_id.value = getLessonFile(lesson.id)?.id || "";
  form.elements.module_id.value = lesson.module_id;
  form.elements.title.value = lesson.title || "";
  form.elements.description.value = lesson.description || "";
  form.elements.lesson_type.value = lesson.lesson_type || "lesson";
  form.elements.duration_minutes.value = lesson.duration_minutes || 30;
  form.elements.status.value = lesson.status || "draft";
  form.elements.sort_order.value = lesson.sort_order || 0;
  form.elements.resource_url.value = lesson.resource_url || "";
  form.elements.scheduled_date.value = lesson.scheduled_date || "";
  form.elements.due_date.value = lesson.due_date || "";
  document.getElementById("lessonFormTitle").textContent = "Edit Lesson";
  setText("lessonAttachedFile", getLessonFile(lesson.id)?.original_filename || "No uploaded file attached");
  try {
    const detail = await apiGet(`/api/teacher/lesson-preview?lesson_id=${encodeURIComponent(lesson.id)}`);
    teacherState.lessonDraftSections = detail.sections || [];
    teacherState.lessonDraftQuestions = detail.questions || [];
    renderLessonAuthorSections();
    renderLessonAuthorQuestions();
  } catch (error) {
    setMessage(document.getElementById("teacherMessage"), error.message, "error");
  }
}

function resetLessonForm() {
  const form = document.getElementById("lessonForm");
  form?.reset();
  if (form) {
    form.elements.id.value = "";
    form.elements.file_id.value = "";
  }
  teacherState.selectedLessonFile = null;
  teacherState.lessonDraftSections = [defaultLessonSection()];
  teacherState.lessonDraftQuestions = [defaultLessonQuestion()];
  setText("selectedLessonFile", "No file selected");
  setText("lessonAttachedFile", "No uploaded file attached");
  document.getElementById("lessonFormTitle").textContent = "Create Lesson";
  showLessonAuthorTab("details");
  renderLessonAuthorSections();
  renderLessonAuthorQuestions();
}

async function openLessonDrawer(lessonId = "") {
  const drawer = document.getElementById("lessonDrawer");
  const form = document.getElementById("lessonForm");
  if (!drawer || !form) return;
  resetLessonForm();
  if (lessonId) {
    await fillLessonForm(lessonId);
  } else if (teacherState.selectedLessonFile) {
    setText("lessonAttachedFile", teacherState.selectedLessonFile.name);
  }
  drawer.classList.add("open");
  drawer.setAttribute("aria-hidden", "false");
  syncDrawerScrollLock();
  if (window.lucide) window.lucide.createIcons();
}

function closeLessonDrawer() {
  const drawer = document.getElementById("lessonDrawer");
  drawer?.classList.remove("open");
  drawer?.setAttribute("aria-hidden", "true");
  syncDrawerScrollLock();
}

function syncDrawerScrollLock() {
  document.body.classList.toggle("drawer-scroll-lock", Boolean(document.querySelector(".student-drawer.open")));
}

function showLessonAuthorTab(tab) {
  document.querySelectorAll("[data-lesson-tab]").forEach((button) => {
    button.classList.toggle("active", button.dataset.lessonTab === tab);
  });
  document.querySelectorAll("[data-lesson-panel]").forEach((panel) => {
    panel.classList.toggle("active", panel.dataset.lessonPanel === tab);
  });
}

async function handleLessonAuthorAction(event) {
  const addSection = event.target.closest("[data-add-section]");
  const addQuestion = event.target.closest("[data-add-question]");
  const removeSection = event.target.closest("[data-remove-section]");
  const removeQuestion = event.target.closest("[data-remove-question]");
  const previewCurrent = event.target.closest("[data-preview-current-lesson]");

  if (previewCurrent) {
    previewCurrentLesson();
    return;
  }
  if (addSection) {
    teacherState.lessonDraftSections = collectLessonSections();
    teacherState.lessonDraftSections.push(defaultLessonSection());
    renderLessonAuthorSections();
  }
  if (addQuestion) {
    teacherState.lessonDraftQuestions = collectLessonQuestions();
    teacherState.lessonDraftQuestions.push(defaultLessonQuestion());
    renderLessonAuthorQuestions();
  }
  if (removeSection) {
    const confirmed = await showConfirmModal({
      title: "Remove content block?",
      message: "This content block will be removed from the lesson draft. Save the lesson to make the change permanent.",
      tone: "danger",
      icon: "trash-2",
      confirmText: "Remove Block"
    });
    if (!confirmed) return;
    teacherState.lessonDraftSections = collectLessonSections().filter((_, index) => index !== Number(removeSection.dataset.removeSection));
    if (!teacherState.lessonDraftSections.length) teacherState.lessonDraftSections.push(defaultLessonSection());
    renderLessonAuthorSections();
  }
  if (removeQuestion) {
    const confirmed = await showConfirmModal({
      title: "Remove practice question?",
      message: "This question will be removed from the lesson draft. Save the lesson to make the change permanent.",
      tone: "danger",
      icon: "trash-2",
      confirmText: "Remove Question"
    });
    if (!confirmed) return;
    teacherState.lessonDraftQuestions = collectLessonQuestions().filter((_, index) => index !== Number(removeQuestion.dataset.removeQuestion));
    if (!teacherState.lessonDraftQuestions.length) teacherState.lessonDraftQuestions.push(defaultLessonQuestion());
    renderLessonAuthorQuestions();
  }
}

function renderLessonAuthorSections() {
  const container = document.getElementById("lessonSectionsEditor");
  if (!container) return;
  const sections = teacherState.lessonDraftSections.length ? teacherState.lessonDraftSections : [defaultLessonSection()];
  container.innerHTML = sections.map((section, index) => `
    <article class="author-item author-section-item" data-index="${index}">
      <div class="author-item-top">
        <strong>Content Block ${index + 1}</strong>
        <button class="icon-button" type="button" data-remove-section="${index}" aria-label="Remove content block"><i data-lucide="trash-2"></i></button>
      </div>
      <div class="field-grid simple-grid">
        <label>Block Type
          <select data-section-field="section_type">
            ${LESSON_SECTION_TYPES.map((type) => `<option value="${type}" ${section.section_type === type ? "selected" : ""}>${titleCase(type.replace("_", " "))}</option>`).join("")}
          </select>
        </label>
        <label>Title<input data-section-field="title" type="text" maxlength="120" value="${escapeAttribute(section.title || "")}"></label>
      </div>
      <label>Content<textarea data-section-field="body" rows="4" placeholder="Write the lesson content, key points, example, or tip.">${escapeHtml(section.body || "")}</textarea></label>
      <label>Media / Resource URL<input data-section-field="media_url" type="url" placeholder="https://" value="${escapeAttribute(section.media_url || "")}"></label>
    </article>
  `).join("");
  if (window.lucide) window.lucide.createIcons();
}

function renderLessonAuthorQuestions() {
  const container = document.getElementById("lessonQuestionsEditor");
  if (!container) return;
  const questions = teacherState.lessonDraftQuestions.length ? teacherState.lessonDraftQuestions : [defaultLessonQuestion()];
  container.innerHTML = questions.map((question, index) => `
    <article class="author-item author-question-item" data-index="${index}">
      <div class="author-item-top">
        <strong>Practice Question ${index + 1}</strong>
        <button class="icon-button" type="button" data-remove-question="${index}" aria-label="Remove practice question"><i data-lucide="trash-2"></i></button>
      </div>
      <div class="field-grid simple-grid">
        <label>Question Type
          <select data-question-field="question_type">
            ${LESSON_QUESTION_TYPES.map((type) => `<option value="${type}" ${question.question_type === type ? "selected" : ""}>${titleCase(type.replace("_", " "))}</option>`).join("")}
          </select>
        </label>
        <label>Points<input data-question-field="points" type="number" min="1" max="100" value="${Number(question.points || 1)}"></label>
      </div>
      <label>Prompt<textarea data-question-field="prompt" rows="3" placeholder="Ask the student what to identify or answer.">${escapeHtml(question.prompt || "")}</textarea></label>
      <label>Choices / Order Items<textarea data-question-field="choices" rows="3" placeholder="One choice or step per line. Leave empty for short answer.">${escapeHtml((question.choices || []).join("\n"))}</textarea></label>
      <label>Correct Answer<input data-question-field="correct_answer" type="text" value="${escapeAttribute(question.correct_answer || "")}" placeholder="Use | for accepted typed answers. Use comma order for ordering questions."></label>
      <div class="field-grid simple-grid">
        <label>Hint<input data-question-field="hint" type="text" value="${escapeAttribute(question.hint || "")}"></label>
        <label>Feedback<input data-question-field="explanation" type="text" value="${escapeAttribute(question.explanation || "")}"></label>
      </div>
    </article>
  `).join("");
  if (window.lucide) window.lucide.createIcons();
}

function collectLessonSections() {
  return Array.from(document.querySelectorAll(".author-section-item")).map((item, index) => ({
    section_type: item.querySelector("[data-section-field='section_type']")?.value || "paragraph",
    title: item.querySelector("[data-section-field='title']")?.value || "",
    body: item.querySelector("[data-section-field='body']")?.value || "",
    media_url: item.querySelector("[data-section-field='media_url']")?.value || "",
    sort_order: index
  })).filter((section) => section.title || section.body || section.media_url);
}

function collectLessonQuestions() {
  return Array.from(document.querySelectorAll(".author-question-item")).map((item, index) => ({
    question_type: item.querySelector("[data-question-field='question_type']")?.value || "multiple_choice",
    prompt: item.querySelector("[data-question-field='prompt']")?.value || "",
    choices: (item.querySelector("[data-question-field='choices']")?.value || "").split(/\r?\n/).map((choice) => choice.trim()).filter(Boolean),
    correct_answer: item.querySelector("[data-question-field='correct_answer']")?.value || "",
    hint: item.querySelector("[data-question-field='hint']")?.value || "",
    explanation: item.querySelector("[data-question-field='explanation']")?.value || "",
    points: Number(item.querySelector("[data-question-field='points']")?.value || 1),
    sort_order: index
  })).filter((question) => question.prompt || question.correct_answer);
}

function buildLessonPreviewFromForm() {
  const form = document.getElementById("lessonForm");
  if (!form) throw new Error("Lesson form is not available.");
  const moduleId = form.elements.module_id.value;
  const module = teacherState.modules.find((item) => item.id === moduleId) || {
    id: moduleId || "preview-module",
    title: "Preview Module",
    category: "TechWise 360",
    grade_level: "Grade 10",
    status: "published"
  };
  const existingId = form.elements.id.value || "";
  const previewId = existingId || "preview-lesson";
  const title = form.elements.title.value.trim() || "Untitled Lesson Preview";
  const description = form.elements.description.value.trim() || "Preview your custom lesson content before saving.";
  const lesson = {
    id: previewId,
    module_id: module.id,
    quarter_id: module.quarter_id || teacherState.activeQuarter?.id || null,
    title,
    description,
    lesson_type: form.elements.lesson_type.value || "lesson",
    status: form.elements.status.value || "draft",
    duration_minutes: Number(form.elements.duration_minutes.value || 30),
    resource_url: form.elements.resource_url.value.trim(),
    scheduled_date: form.elements.scheduled_date.value || null,
    due_date: form.elements.due_date.value || null,
    sort_order: Number(form.elements.sort_order.value || 0),
    created_at: new Date().toISOString(),
    updated_at: new Date().toISOString()
  };
  const savedModuleLessons = teacherState.lessons
    .filter((item) => item.module_id === module.id && item.status !== "archived" && item.id !== previewId)
    .sort((a, b) => (Number(a.sort_order || 0) - Number(b.sort_order || 0)) || String(a.created_at).localeCompare(String(b.created_at)));
  const insertIndex = Math.max(0, savedModuleLessons.findIndex((item) => Number(item.sort_order || 0) > Number(lesson.sort_order || 0)));
  const moduleLessons = [...savedModuleLessons];
  if (insertIndex === -1) moduleLessons.push(lesson);
  else moduleLessons.splice(insertIndex, 0, lesson);
  const attachedFileName = teacherState.selectedLessonFile?.name || getLessonFile(existingId)?.original_filename || "";

  return {
    mode: "teacher_preview",
    module,
    lesson,
    module_lessons: moduleLessons.length ? moduleLessons : [lesson],
    sections: collectLessonSections(),
    questions: collectLessonQuestions().map((question, index) => ({
      ...question,
      id: `preview-question-${index + 1}`,
      lesson_id: previewId
    })),
    files: attachedFileName ? [{
      id: "preview-file",
      lesson_id: previewId,
      original_filename: attachedFileName,
      mime_type: teacherState.selectedLessonFile?.type || "application/octet-stream",
      size_bytes: teacherState.selectedLessonFile?.size || 0
    }] : [],
    progress: null,
    attempts: [],
    preview_file_name: attachedFileName
  };
}

function defaultLessonSection() {
  return {
    section_type: "paragraph",
    title: "Objectives",
    body: "Write what students should understand or be able to do after this lesson.",
    media_url: "",
    sort_order: 0
  };
}

function defaultLessonQuestion() {
  return {
    question_type: "multiple_choice",
    prompt: "",
    choices: ["Option A", "Option B", "Option C"],
    correct_answer: "",
    hint: "",
    explanation: "",
    points: 1,
    sort_order: 0
  };
}

function handleLessonFileSelection(event) {
  const file = event.currentTarget.files?.[0] || null;
  setSelectedLessonFile(file);
}

function handleLessonDragOver(event) {
  event.preventDefault();
  event.currentTarget.classList.add("dragging");
}

function handleLessonDragLeave(event) {
  event.currentTarget.classList.remove("dragging");
}

function handleLessonFileDrop(event) {
  event.preventDefault();
  event.currentTarget.classList.remove("dragging");
  const file = event.dataTransfer?.files?.[0] || null;
  setSelectedLessonFile(file);
}

function setSelectedLessonFile(file) {
  const message = document.getElementById("teacherMessage");
  if (!file) {
    teacherState.selectedLessonFile = null;
    setText("selectedLessonFile", "No file selected");
    return;
  }
  const error = validateLessonFile(file);
  if (error) {
    teacherState.selectedLessonFile = null;
    setText("selectedLessonFile", "No file selected");
    setMessage(message, error, "error");
    return;
  }
  teacherState.selectedLessonFile = file;
  setText("selectedLessonFile", `${file.name} - ${formatBytes(file.size)}`);
  setText("lessonAttachedFile", file.name);
  setMessage(message, "Lesson file selected. Create or save a lesson to upload it.", "info");
}

function validateLessonFile(file) {
  if (!LESSON_FILE_TYPES.has(file.type)) return "Only PDF, DOCX, PPTX, and MP4 files are supported.";
  if (file.size > MAX_LESSON_FILE_BYTES) return "Lesson file must be 100MB or smaller.";
  return "";
}

async function uploadLessonFile(file, lessonId = null) {
  const upload = await apiPost("/api/teacher/lesson-upload-url", {
    filename: file.name,
    mime_type: file.type,
    size_bytes: file.size
  });

  const uploadUrl = upload.token && !upload.upload_url.includes("token=")
    ? `${upload.upload_url}${upload.upload_url.includes("?") ? "&" : "?"}token=${encodeURIComponent(upload.token)}`
    : upload.upload_url;
  const uploadResponse = await fetch(uploadUrl, {
    method: "PUT",
    headers: { "Content-Type": file.type },
    body: file
  });
  if (!uploadResponse.ok) {
    throw new Error("Unable to upload the lesson file to Supabase Storage.");
  }

  const result = await apiPost("/api/teacher/lesson-files", {
    lesson_id: lessonId,
    storage_path: upload.path,
    original_filename: file.name,
    mime_type: file.type,
    size_bytes: file.size
  });
  return result.file;
}

async function previewLesson(lessonId) {
  const drawer = document.getElementById("lessonPreviewDrawer");
  const container = document.getElementById("teacherLessonPreview");
  if (!drawer || !container) return;
  const message = document.getElementById("teacherMessage");
  renderLessonPreviewState("loading", "Loading lesson preview...");
  setMessage(message, "Loading lesson preview...");
  try {
    const detail = await apiGet(`/api/teacher/lesson-preview?lesson_id=${encodeURIComponent(lessonId)}`);
    teacherState.previewLesson = detail;
    openTeacherLessonPreview(detail, resolveLessonPlayerView(detail.lesson, "auto"));
    setMessage(message, "", "success");
  } catch (error) {
    const errorMessage = error.message || "Unable to load lesson preview.";
    renderLessonPreviewState("error", errorMessage);
    setMessage(message, errorMessage, "error");
  }
}

function renderLessonPreviewState(type, message) {
  const drawer = document.getElementById("lessonPreviewDrawer");
  const container = document.getElementById("teacherLessonPreview");
  if (!drawer || !container) return;
  const isError = type === "error";
  container.innerHTML = `
    <div class="lesson-preview-state ${isError ? "error" : "loading"}">
      <span><i data-lucide="${isError ? "alert-circle" : "loader-2"}"></i></span>
      <div>
        <strong>${isError ? "Preview could not open" : "Opening preview"}</strong>
        <p>${escapeHtml(message)}</p>
        ${isError ? `<small>Make sure the latest SQL seed has been run in Supabase and that you are logged in as an approved teacher.</small>` : ""}
      </div>
      ${isError ? `<button class="small-button" type="button" data-player-back><i data-lucide="x"></i><span>Close</span></button>` : ""}
    </div>
  `;
  drawer.classList.add("open");
  drawer.setAttribute("aria-hidden", "false");
  syncDrawerScrollLock();
  if (window.lucide) window.lucide.createIcons();
}

function previewCurrentLesson() {
  const message = document.getElementById("teacherMessage");
  try {
    const detail = buildLessonPreviewFromForm();
    teacherState.previewLesson = detail;
    openTeacherLessonPreview(detail, resolveLessonPlayerView(detail.lesson, "auto"));
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

function openTeacherLessonPreview(detail, view = "lesson") {
  const drawer = document.getElementById("lessonPreviewDrawer");
  const container = document.getElementById("teacherLessonPreview");
  if (!drawer || !container) return;
  teacherState.previewLesson = detail;
  teacherState.previewLayout = teacherState.previewLayout || defaultLessonLayout();
  renderLessonPlayer(container, detail, { preview: true, view, layout: teacherState.previewLayout });
  drawer.classList.add("open");
  drawer.setAttribute("aria-hidden", "false");
  syncDrawerScrollLock();
}

function closeLessonPreview() {
  const drawer = document.getElementById("lessonPreviewDrawer");
  drawer?.classList.remove("open");
  drawer?.setAttribute("aria-hidden", "true");
  syncDrawerScrollLock();
}

async function loadStudentDashboard() {
  const message = document.getElementById("studentMessage");
  setMessage(message, "Loading your dashboard...");

  try {
    const [data, evaluation, leaderboard, completionSummary] = await Promise.all([
      apiGet("/api/student/dashboard"),
      apiGet("/api/student/evaluation").catch(() => defaultStudentEvaluationData()),
      apiGet("/api/student/leaderboard").catch(() => ({ rows: [], competitions: [], summary: {} })),
      apiGet("/api/student/completion-summary").catch(() => ({ modules: [], lessons: [], progress: [] }))
    ]);
    studentState = {
      profile: data.profile,
      activeQuarter: data.active_quarter || null,
      modules: data.modules || [],
      lessons: data.lessons || [],
      progress: data.progress || [],
      attempts: data.attempts || [],
      badges: data.badges || [],
      certificates: data.certificates || [],
      avatarUrl: data.avatar_url || studentState.avatarUrl || "",
      notifications: studentState.notifications || [],
      unreadNotifications: Number(studentState.unreadNotifications || 0),
      notificationPanelOpen: studentState.notificationPanelOpen || false,
      notificationsLoading: false,
      progressFilters: studentState.progressFilters || defaultStudentProgressFilters(),
      lessonFilter: studentState.lessonFilter || "all",
      learnPage: studentState.learnPage || 1,
      assessmentPage: studentState.assessmentPage || 1,
      achievementTab: studentState.achievementTab || "badges",
      leaderboard: {
        rows: leaderboard.rows || [],
        competitions: leaderboard.competitions || [],
        summary: leaderboard.summary || {}
      },
      completionSummary: {
        active_quarter: completionSummary.active_quarter || data.active_quarter || null,
        modules: completionSummary.modules || data.modules || [],
        lessons: completionSummary.lessons || data.lessons || [],
        progress: completionSummary.progress || data.progress || []
      },
      evaluation: normalizeStudentEvaluationData(evaluation),
      lessonPlayer: studentState.lessonPlayer || null,
      selectedAvatarFile: null
    };
    renderStudentDashboard();
    setMessage(message, "", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

async function loadStudentNotifications({ silent = true } = {}) {
  const message = document.getElementById("studentMessage");
  try {
    studentState.notificationsLoading = !silent;
    if (!silent) renderStudentNotifications();
    const data = await apiGet("/api/student/notifications");
    studentState.notifications = data.notifications || [];
    studentState.unreadNotifications = Number(data.unread_count || 0);
    studentState.notificationsLoading = false;
    renderStudentNotifications();
    renderStudentEvidenceTimeline();
    renderStudentProgressDashboard();
  } catch (error) {
    studentState.notificationsLoading = false;
    renderStudentNotifications();
    renderStudentEvidenceTimeline();
    if (!silent) setMessage(message, error.message, "error");
  }
}

function renderStudentDashboard() {
  const profile = studentState.profile;
  const summary = getStudentDashboardSummary();
  const firstName = profile.first_name || profile.full_name || "Student";

  renderStudentNotifications();
  document.getElementById("studentTopName").textContent = profile.full_name || profile.username;
  document.getElementById("studentTopGrade").textContent = profile.grade_level || "Student";
  renderAvatarInto(document.getElementById("studentTopAvatar"), profile, studentState.avatarUrl, "topbar-avatar");
  document.getElementById("studentGreeting").textContent = `Welcome back, ${firstName}!`;
  document.getElementById("studentQuarter").textContent = studentState.activeQuarter
    ? `${studentState.activeQuarter.title} is active. Ready to build your ICT skills today?`
    : "Ready to build your ICT skills today?";
  document.getElementById("studentBadges").textContent = String(summary.badgesEarned);
  document.getElementById("studentCertificates").textContent = String(summary.certificatesEarned);
  document.getElementById("studentRank").textContent = studentState.leaderboard?.summary?.student_rank
    ? `#${studentState.leaderboard.summary.student_rank}`
    : "-";
  document.getElementById("overallProgress").textContent = `${summary.overall}%`;
  document.getElementById("topicsCompleted").textContent = String(summary.completed);
  document.getElementById("hoursLearned").textContent = summary.hours;
  document.getElementById("lessonsCompleted").textContent = String(summary.completed);
  document.getElementById("avgScore").textContent = `${summary.averageScore}%`;

  renderStudentProfile(profile);
  renderQuickAccess();
  renderStudentNextAction();
  renderDailyMission();
  renderRecentActivity();
  renderStudentSkillSnapshot();
  renderStudentDueReminders();
  renderStudentEvidenceTimeline();
  renderStudentEvaluationPrompt();
  renderStudentLessonsPackageView();
  renderStudentAssessmentsDashboard();
  renderStudentProgressDashboard();
  renderStudentLeaderboardDashboard();
  renderStudentAchievementsDashboard();
  renderStudentEvaluationSurvey();

  if (window.lucide) window.lucide.createIcons();
}

function renderStudentEvaluationPrompt() {
  const container = document.getElementById("studentEvaluationPrompt");
  if (!container) return;
  const evaluation = normalizeStudentEvaluationData(studentState.evaluation);
  if (!evaluation.cycle || !evaluation.survey_open) {
    container.hidden = true;
    container.innerHTML = "";
    return;
  }
  const submitted = Boolean(evaluation.response);
  container.hidden = false;
  container.innerHTML = `
    <div class="student-evaluation-prompt-copy">
      <span class="student-evaluation-icon"><i data-lucide="clipboard-check"></i></span>
      <div>
        <strong>${submitted ? "Feedback Survey submitted" : "Feedback Survey is open"}</strong>
        <p>${submitted ? "You can update your feedback while the teacher keeps the survey open." : "Share what worked well and what can be improved."}</p>
      </div>
    </div>
    <button class="primary-button" type="button" data-jump-student="evaluation">
      ${submitted ? "Review Survey" : "Answer Survey"}
      <i data-lucide="arrow-right"></i>
    </button>
  `;
}

function renderStudentEvaluationSurvey() {
  const container = document.getElementById("studentEvaluationSurvey");
  if (!container) return;
  const evaluation = normalizeStudentEvaluationData(studentState.evaluation);
  if (!evaluation.cycle || !evaluation.survey_open) {
    container.innerHTML = `
      <section class="panel-card student-empty-dashboard">
        <i data-lucide="clipboard-check"></i>
        <h2>No feedback survey is open</h2>
        <p>Your teacher will open the survey when it is time to collect student feedback.</p>
        <button class="primary-button" type="button" data-jump-student="home">Back Home</button>
      </section>
    `;
    if (window.lucide) window.lucide.createIcons();
    return;
  }

  const answers = evaluation.response?.answers || {};
  container.innerHTML = `
    <form class="panel-card student-evaluation-form">
      <input type="hidden" name="cycle_id" value="${escapeAttribute(evaluation.cycle.id)}">
      <div class="student-evaluation-form-header">
        <div>
          <span class="evidence-eyebrow"><i data-lucide="star"></i> System Feedback</span>
          <h2>${evaluation.response ? "Update Your Feedback" : "Feedback Survey"}</h2>
          <p>Rate each statement from 1 to 5. Your answers help improve TechWise 360.</p>
        </div>
        <span class="soft-pill">${escapeHtml(evaluation.cycle.grade_level || studentState.profile?.grade_level || "Student")}</span>
      </div>
      ${renderEvaluationSurveyInputs(evaluation.survey_categories, answers)}
      <label>Optional Comment<textarea name="comment" rows="3" maxlength="1000" placeholder="Share anything helpful about your experience.">${escapeHtml(evaluation.response?.comment || "")}</textarea></label>
      <div class="student-evaluation-actions">
        <button class="primary-button" type="submit"><i data-lucide="send"></i><span>${evaluation.response ? "Update Response" : "Submit Survey"}</span></button>
        <button class="small-button" type="button" data-jump-student="home">Back Home</button>
      </div>
      ${evaluation.response ? `<p class="evaluation-note">Submitted ${escapeHtml(formatDate(evaluation.response.submitted_at))}. You may update while the survey remains open.</p>` : ""}
    </form>
  `;
  if (window.lucide) window.lucide.createIcons();
}

async function submitStudentEvaluationSurvey(event) {
  const form = event.target.closest(".student-evaluation-form");
  if (!form) return;
  event.preventDefault();
  const message = document.getElementById("studentMessage");
  try {
    setMessage(message, "Submitting feedback...");
    const data = await apiPost("/api/student/evaluation", {
      cycle_id: form.elements.cycle_id?.value,
      answers: collectEvaluationAnswers(form),
      comment: form.elements.comment?.value || ""
    });
    studentState.evaluation = normalizeStudentEvaluationData(data);
    renderStudentEvaluationPrompt();
    renderStudentEvaluationSurvey();
    setMessage(message, "Feedback saved.", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

function renderStudentNotifications() {
  const button = document.getElementById("studentNotificationButton");
  const badge = document.getElementById("studentNotificationBadge");
  const panel = document.getElementById("studentNotificationPanel");
  const summary = document.getElementById("studentNotificationSummary");
  const list = document.getElementById("studentNotificationList");
  const markAll = document.getElementById("markAllStudentNotificationsRead");
  if (!button || !badge || !panel || !summary || !list) return;

  const unread = Number(studentState.unreadNotifications || 0);
  const reminders = getStudentNotificationReminders();
  button.setAttribute("aria-expanded", studentState.notificationPanelOpen ? "true" : "false");
  panel.hidden = !studentState.notificationPanelOpen;
  badge.hidden = unread <= 0;
  badge.textContent = unread > 99 ? "99+" : String(unread);
  summary.textContent = unread
    ? `${unread} unread update${unread === 1 ? "" : "s"}`
    : reminders.length
      ? `${reminders.length} active reminder${reminders.length === 1 ? "" : "s"}`
      : "No unread updates";
  if (markAll) markAll.disabled = unread <= 0;

  if (studentState.notificationsLoading) {
    list.innerHTML = `<div class="notification-empty"><span>Loading notifications...</span></div>`;
    if (window.lucide) window.lucide.createIcons();
    return;
  }

  const notifications = studentState.notifications || [];
  if (!notifications.length && !reminders.length) {
    list.innerHTML = `<div class="notification-empty"><span>No notifications yet.</span></div>`;
    return;
  }

  const notificationRows = notifications.map(renderStudentNotificationItem).join("");
  const reminderRows = reminders.length
    ? `<div class="notification-section-label">Due reminders</div>${reminders.map(renderStudentReminderItem).join("")}`
    : "";
  list.innerHTML = `${notificationRows}${reminderRows}`;
  if (window.lucide) window.lucide.createIcons();
}

function renderStudentNotificationItem(notification) {
  const unread = !notification.read_at;
  const meta = notification.metadata || {};
  const icon = iconForStudentNotification(notification.event_type);
  const color = colorForStudentNotification(notification.event_type);
  const detail = [meta.module_title, meta.lesson_title || meta.reward_title].filter(Boolean).join(" - ");
  return `
    <button class="notification-item ${unread ? "unread" : ""}" type="button" data-student-notification-id="${escapeAttribute(notification.id)}">
      <span class="notification-icon ${escapeAttribute(color)}"><i data-lucide="${escapeAttribute(icon)}"></i></span>
      <span class="notification-content">
        <strong>${escapeHtml(notification.title || "Notification")}</strong>
        <span>${escapeHtml(notification.body || "")}</span>
        ${detail ? `<span>${escapeHtml(detail)}</span>` : ""}
        <time>${escapeHtml(relativeTime(notification.created_at))}</time>
      </span>
      ${unread ? `<span class="notification-unread-dot" aria-hidden="true"></span>` : ""}
    </button>
  `;
}

function renderStudentReminderItem(reminder) {
  return `
    <button class="notification-item reminder ${escapeAttribute(reminder.severity)}" type="button" data-student-reminder-id="${escapeAttribute(reminder.id)}">
      <span class="notification-icon ${escapeAttribute(reminder.color)}"><i data-lucide="${escapeAttribute(reminder.icon)}"></i></span>
      <span class="notification-content">
        <strong>${escapeHtml(reminder.title)}</strong>
        <span>${escapeHtml(reminder.body)}</span>
        <time>${escapeHtml(reminder.timeLabel)}</time>
      </span>
    </button>
  `;
}

async function handleStudentNotificationClick(event) {
  const notificationButton = event.target.closest("[data-student-notification-id]");
  if (notificationButton) {
    const notification = studentState.notifications.find((item) => item.id === notificationButton.dataset.studentNotificationId);
    if (!notification) return;
    await markStudentNotificationRead(notification.id);
    await routeStudentNotification(notification);
    return;
  }

  const reminderButton = event.target.closest("[data-student-reminder-id]");
  if (!reminderButton) return;
  const reminder = getStudentNotificationReminders().find((item) => item.id === reminderButton.dataset.studentReminderId);
  if (reminder) await routeStudentReminder(reminder);
}

async function markStudentNotificationRead(notificationId) {
  try {
    const data = await apiPatch("/api/student/notifications", { id: notificationId, action: "read" });
    studentState.notifications = data.notifications || studentState.notifications;
    studentState.unreadNotifications = Number(data.unread_count || 0);
    renderStudentNotifications();
  } catch (error) {
    setMessage(document.getElementById("studentMessage"), error.message, "error");
  }
}

async function markAllStudentNotificationsRead() {
  try {
    const data = await apiPatch("/api/student/notifications", { action: "read_all" });
    studentState.notifications = data.notifications || [];
    studentState.unreadNotifications = Number(data.unread_count || 0);
    renderStudentNotifications();
  } catch (error) {
    setMessage(document.getElementById("studentMessage"), error.message, "error");
  }
}

async function routeStudentNotification(notification) {
  studentState.notificationPanelOpen = false;
  renderStudentNotifications();

  if (notification.event_type === "account_approved") {
    showStudentView("home");
    return;
  }

  if (["lesson_published", "lesson_updated"].includes(notification.event_type)) {
    if (notification.entity_id) {
      await openStudentLesson(notification.entity_id, "auto");
      return;
    }
    showStudentView("lessons");
    return;
  }

  if (["assessment_published", "assessment_updated"].includes(notification.event_type)) {
    if (notification.entity_id) {
      await openStudentLesson(notification.entity_id, "auto");
      return;
    }
    showStudentView("assessments");
    return;
  }

  if (notification.event_type === "certificate_awarded") {
    studentState.achievementTab = "certificates";
    renderStudentAchievementsDashboard();
    showStudentView("achievements");
    return;
  }

  if (notification.event_type === "badge_awarded") {
    studentState.achievementTab = "badges";
    renderStudentAchievementsDashboard();
    showStudentView("achievements");
  }
}

async function routeStudentReminder(reminder) {
  studentState.notificationPanelOpen = false;
  renderStudentNotifications();
  if (reminder.lesson_id) {
    await openStudentLesson(reminder.lesson_id, "auto");
    return;
  }
  showStudentView("assessments");
}

function getStudentNotificationReminders() {
  if (!studentState.lessons.length) return [];
  const modulesById = new Map(studentState.modules.map((module) => [module.id, module]));
  const now = new Date();
  const soon = new Date(now);
  soon.setDate(now.getDate() + 3);

  return getStudentAssessmentItems(modulesById)
    .filter((item) => !item.submitted && item.lesson.due_date)
    .map((item) => {
      const due = endOfLocalDay(item.lesson.due_date);
      const overdue = due < now;
      const dueSoon = !overdue && due <= soon;
      if (!overdue && !dueSoon) return null;
      const lessonType = item.lesson.lesson_type === "assessment" ? "assessment" : "practice check";
      return {
        id: `${overdue ? "overdue" : "due"}-${item.lesson.id}`,
        lesson_id: item.lesson.id,
        severity: overdue ? "overdue" : "due-soon",
        icon: overdue ? "alarm-clock" : "calendar-clock",
        color: overdue ? "red" : "orange",
        title: overdue ? "Check overdue" : "Check due soon",
        body: `${item.lesson.title} ${overdue ? "was due" : "is due"} ${formatDateOnly(item.lesson.due_date)}.`,
        timeLabel: `${titleCase(lessonType)} - ${item.module?.title || "Assessment"}`
      };
    })
    .filter(Boolean)
    .sort((a, b) => {
      const priority = { overdue: 0, "due-soon": 1 };
      return priority[a.severity] - priority[b.severity] || a.body.localeCompare(b.body);
    });
}

function iconForStudentNotification(eventType) {
  if (eventType === "account_approved") return "user-check";
  if (eventType === "lesson_published" || eventType === "lesson_updated") return "book-open";
  if (eventType === "assessment_published" || eventType === "assessment_updated") return "clipboard-check";
  if (eventType === "certificate_awarded") return "award";
  if (eventType === "badge_awarded") return "shield-check";
  return "bell";
}

function colorForStudentNotification(eventType) {
  if (eventType === "account_approved") return "green";
  if (eventType === "assessment_published" || eventType === "assessment_updated") return "purple";
  if (eventType === "badge_awarded" || eventType === "certificate_awarded") return "orange";
  return "blue";
}

function renderStudentProfile(profile) {
  renderAvatarInto(document.getElementById("studentProfileAvatar"), profile, studentState.avatarUrl, "profile-editor-avatar");
  document.getElementById("profileFullName").textContent = profile.full_name || "-";
  document.getElementById("profileUsername").textContent = `@${profile.username}`;
  document.getElementById("profileEmail").textContent = profile.email;
  document.getElementById("profileGrade").textContent = `${profile.grade_level || "-"} ${profile.section ? `- ${profile.section}` : ""}`;
  document.getElementById("profileAdviser").textContent = profile.adviser || "-";
  document.getElementById("profileStatus").textContent = titleCase(profile.status || "approved");
  const form = document.getElementById("studentProfileForm");
  if (form) {
    form.elements.phone_number.value = profile.phone_number || "";
    form.elements.home_town.value = profile.home_town || "";
  }
  const removeButton = document.getElementById("removeStudentAvatar");
  if (removeButton) removeButton.disabled = !profile.avatar_path;
  const fileName = document.getElementById("studentAvatarFileName");
  if (fileName) fileName.textContent = studentState.selectedAvatarFile?.name || "No file selected";
  const uploadButton = document.getElementById("uploadStudentAvatar");
  if (uploadButton) uploadButton.disabled = !studentState.selectedAvatarFile;
}

function renderAvatarInto(container, profile, imageUrl = "", className = "") {
  if (!container || !profile) return;
  const initials = getInitials(profile.full_name || profile.username || "Student");
  container.classList.toggle("has-photo", Boolean(imageUrl));
  if (className) container.classList.add(className);
  container.innerHTML = `
    ${imageUrl ? `<img src="${escapeAttribute(imageUrl)}" alt="${escapeAttribute(profile.full_name || "Student")} profile picture" onerror="this.hidden=true;this.nextElementSibling.hidden=false;">` : ""}
    <span ${imageUrl ? "hidden" : ""}>${escapeHtml(initials)}</span>
  `;
}

function handleStudentAvatarSelection(event) {
  const file = event.target.files?.[0] || null;
  const message = document.getElementById("studentMessage");
  studentState.selectedAvatarFile = null;
  if (!file) {
    renderStudentProfile(studentState.profile);
    return;
  }
  const validation = validateStudentAvatarFile(file);
  if (validation) {
    event.target.value = "";
    setMessage(message, validation, "error");
    renderStudentProfile(studentState.profile);
    return;
  }
  studentState.selectedAvatarFile = file;
  studentState.avatarUrl = URL.createObjectURL(file);
  renderStudentProfile(studentState.profile);
  setMessage(message, "Preview ready. Upload to save this profile picture.", "info");
}

function validateStudentAvatarFile(file) {
  if (!STUDENT_AVATAR_TYPES.has(file.type)) return "Profile picture must be a JPEG, PNG, or WebP image.";
  if (file.size > MAX_STUDENT_AVATAR_BYTES) return "Profile picture must be 2 MB or smaller.";
  return "";
}

async function uploadStudentAvatar() {
  const file = studentState.selectedAvatarFile;
  const message = document.getElementById("studentMessage");
  if (!file) return;
  const validation = validateStudentAvatarFile(file);
  if (validation) {
    setMessage(message, validation, "error");
    return;
  }

  try {
    setMessage(message, "Uploading profile picture...");
    const upload = await apiPost("/api/student/avatar-upload-url", {
      filename: file.name,
      mime_type: file.type,
      size_bytes: file.size
    });
    const uploadUrl = upload.token && !upload.upload_url.includes("token=")
      ? `${upload.upload_url}${upload.upload_url.includes("?") ? "&" : "?"}token=${encodeURIComponent(upload.token)}`
      : upload.upload_url;
    const uploadResponse = await fetch(uploadUrl, {
      method: "PUT",
      headers: { "Content-Type": file.type },
      body: file
    });
    if (!uploadResponse.ok) throw new Error("Unable to upload profile picture to Supabase Storage.");
    const refreshed = await apiPatch("/api/student/avatar", { path: upload.path });
    applyUpdatedStudentProfile(refreshed.profile, refreshed.avatar_url || "");
    document.getElementById("studentAvatarInput").value = "";
    studentState.selectedAvatarFile = null;
    renderStudentDashboard();
    setMessage(message, "Profile picture updated.", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

async function removeStudentAvatar() {
  if (!studentState.profile?.avatar_path) return;
  await showConfirmModal({
    title: "Remove profile photo?",
    message: "Your current profile photo will be removed from your account.",
    tone: "danger",
    icon: "trash-2",
    confirmText: "Remove Photo",
    processingText: "Removing...",
    errorTitle: "Unable to remove photo",
    errorMessage: "The profile photo was not changed. Please try again.",
    onConfirm: async () => {
      const data = await apiDelete("/api/student/avatar");
      applyUpdatedStudentProfile(data.profile, "");
      studentState.selectedAvatarFile = null;
      document.getElementById("studentAvatarInput").value = "";
      renderStudentDashboard();
      showToast({ title: "Profile picture removed", message: "Your profile photo was removed.", type: "success" });
      return true;
    }
  });
}

async function submitStudentProfileForm(event) {
  event.preventDefault();
  const message = document.getElementById("studentMessage");
  const payload = formToObject(event.currentTarget);
  try {
    setMessage(message, "Saving profile...");
    const data = await apiPatch("/api/student/profile", payload);
    applyUpdatedStudentProfile(data.profile, data.avatar_url || studentState.avatarUrl || "");
    renderStudentDashboard();
    setMessage(message, "Profile saved.", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

function applyUpdatedStudentProfile(profile, avatarUrl = "") {
  studentState.profile = profile;
  studentState.avatarUrl = avatarUrl;
  const session = getSession();
  if (session) setSession(session, profile);
}

function renderQuickAccess() {
  const container = document.getElementById("quickAccessCards");
  const cards = studentState.modules.slice(0, 4);

  if (!cards.length) {
    container.innerHTML = `<p class="empty-text">No published modules yet for your active term.</p>`;
    return;
  }

  const colors = ["blue-soft", "green-soft", "purple-soft", "gold-soft"];
  container.innerHTML = cards.map((module, index) => `
    <article class="quick-card ${colors[index % colors.length]}">
      <i data-lucide="${index % 2 === 0 ? "monitor" : "file-text"}"></i>
      <strong>${escapeHtml(module.title)}</strong>
      <span>${escapeHtml(module.category)}</span>
      <button type="button" data-jump-student="lessons" aria-label="Open lessons"><i data-lucide="arrow-right-circle"></i></button>
    </article>
  `).join("");
  container.querySelectorAll("[data-jump-student]").forEach((button) => {
    button.addEventListener("click", () => showStudentView(button.dataset.jumpStudent));
  });
}

function renderDailyMission() {
  const container = document.getElementById("dailyMission");
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));
  const nextLesson = getNextUnlockedStudentLesson() || getSortedStudentLessons().find((lesson) => !getStudentLessonGate(lesson.id).locked);

  if (!nextLesson) {
    container.innerHTML = `<p class="empty-text">No learning task is available yet.</p>`;
    return;
  }

  const progress = progressByLesson.get(nextLesson.id);
  const percent = progress?.progress_percent || 0;
  container.innerHTML = `
    <div class="mission-visual"><i data-lucide="network"></i></div>
    <h3>${escapeHtml(nextLesson.title)}</h3>
    <p>${escapeHtml(nextLesson.description || "Complete this lesson to build your ICT skills.")}</p>
    <div class="mission-progress">
      <strong>${percent}% Completed</strong>
      <div class="wide-progress"><span style="width:${percent}%"></span></div>
    </div>
    <button class="primary-button mission-action" type="button" data-open-student-lesson="${escapeAttribute(nextLesson.id)}">${percent ? "Continue" : "Start Lesson"}</button>
  `;
}

function renderStudentNextAction() {
  const container = document.getElementById("studentNextAction");
  const pill = document.getElementById("nextActionKind");
  if (!container) return;

  const action = getStudentNextAction();
  if (!action) {
    if (pill) pill.innerHTML = `<i data-lucide="check-circle-2"></i> Complete`;
    container.innerHTML = `
      <div class="next-action-empty">
        <i data-lucide="party-popper"></i>
        <div>
          <strong>You're caught up for this term.</strong>
          <span>Review your achievements or keep your skills sharp from Learn.</span>
        </div>
      </div>
    `;
    return;
  }

  if (pill) {
    pill.innerHTML = `<i data-lucide="${escapeAttribute(action.pillIcon || "sparkles")}"></i> ${escapeHtml(action.pillLabel || "Ready")}`;
  }

  const buttonAttribute = action.lesson
    ? `data-open-student-lesson="${escapeAttribute(action.lesson.id)}"`
    : `data-jump-student="${escapeAttribute(action.view || "lessons")}"`;
  const percent = Number(action.progressPercent || action.progress?.progress_percent || 0);
  container.innerHTML = `
    <article class="next-action-panel ${escapeAttribute(action.tone || "blue")}">
      <span class="next-action-icon"><i data-lucide="${escapeAttribute(action.icon || "sparkles")}"></i></span>
      <div class="next-action-copy">
        <span>${escapeHtml(action.kicker || "Recommended")}</span>
        <strong>${escapeHtml(action.title)}</strong>
        <p>${escapeHtml(action.body)}</p>
        ${action.lesson ? `
          <div class="wide-progress"><span style="width:${Math.max(0, Math.min(100, percent))}%"></span></div>
          <small>${percent}% complete</small>
        ` : ""}
      </div>
      <button class="primary-button next-action-button" type="button" ${buttonAttribute}>
        ${escapeHtml(action.actionLabel || "Open")}
        <i data-lucide="arrow-right"></i>
      </button>
    </article>
  `;
}

function renderStudentSkillSnapshot() {
  const container = document.getElementById("studentSkillSnapshot");
  if (!container) return;
  const skills = getStudentSkillMap();
  const rows = skills.length
    ? skills.map((skill) => {
      const hasScore = Number.isFinite(skill.score);
      return `
        <article class="student-skill-snapshot-row">
          <i data-lucide="${escapeAttribute(skill.icon)}" class="${escapeAttribute(skill.color)}"></i>
          <div>
            <strong>${escapeHtml(skill.label)}</strong>
            <span>${escapeHtml(skill.source)}</span>
          </div>
          <div class="student-mini-track"><span style="width:${hasScore ? skill.score : 0}%"></span></div>
          <b class="${hasScore ? scoreBandClass(skill.score) : ""}">${hasScore ? `${skill.score}%` : "-"}</b>
        </article>
      `;
    }).join("")
    : `<p class="empty-text">Complete a lesson to build your skill snapshot.</p>`;
  container.innerHTML = `<div class="student-skill-snapshot-list">${rows}</div>`;
}

function renderStudentDueReminders() {
  const container = document.getElementById("studentDueReminders");
  if (!container) return;
  const modulesById = new Map(studentState.modules.map((module) => [module.id, module]));
  const now = new Date();
  const weekAhead = new Date(now);
  weekAhead.setDate(now.getDate() + 7);
  const pending = getStudentAssessmentItems(modulesById)
    .filter((item) => !item.submitted && !item.locked)
    .map((item) => {
      const due = item.lesson.due_date ? endOfLocalDay(item.lesson.due_date) : null;
      const overdue = due ? due < now : false;
      const dueSoon = due ? due <= weekAhead : false;
      return { ...item, due, overdue, dueSoon };
    })
    .filter((item) => item.overdue || item.dueSoon || item.status === "in_progress")
    .slice(0, 4);

  if (!studentState.lessons.some((lesson) => ["practice", "assessment"].includes(lesson.lesson_type))) {
    container.innerHTML = `<p class="empty-text">No assessment checks have been published yet.</p>`;
    return;
  }

  if (!pending.length) {
    container.innerHTML = `
      <div class="student-due-empty">
        <i data-lucide="check-circle-2"></i>
        <span>No urgent checks right now.</span>
      </div>
    `;
    return;
  }

  container.innerHTML = `
    <div class="student-due-list">
      ${pending.map((item) => {
        const label = item.overdue ? "Overdue" : item.dueSoon ? "Due Soon" : "In Progress";
        const tone = item.overdue ? "danger" : item.dueSoon ? "warn" : "good";
        return `
          <article class="student-due-row ${tone}">
            <i data-lucide="${item.overdue ? "alarm-clock" : "calendar-clock"}"></i>
            <div>
              <strong>${escapeHtml(item.lesson.title)}</strong>
              <span>${escapeHtml(item.module?.title || "Assessment")} ${item.lesson.due_date ? `- ${formatDateOnly(item.lesson.due_date)}` : ""}</span>
            </div>
            <button class="small-button" type="button" data-open-student-lesson="${escapeAttribute(item.lesson.id)}">${escapeHtml(label)}</button>
          </article>
        `;
      }).join("")}
    </div>
  `;
}

function renderStudentEvidenceTimeline() {
  const homeContainer = document.getElementById("studentEvidenceTimeline");
  const fullContainer = document.getElementById("studentEvidenceTimelineFull");
  if (!homeContainer && !fullContainer) return;
  const items = getStudentEvidenceItems();
  if (homeContainer) homeContainer.innerHTML = renderStudentEvidenceList(items.slice(0, 5), "compact");
  if (fullContainer) fullContainer.innerHTML = renderStudentEvidenceList(items.slice(0, 12), "full");
  if (window.lucide) window.lucide.createIcons();
}

function renderStudentEvidenceList(items, mode = "compact") {
  if (!items.length) {
    return `<p class="empty-text">Complete a lesson or submit an assessment to build your evidence timeline.</p>`;
  }
  return `
    <div class="student-evidence-list ${escapeAttribute(mode)}">
      ${items.map((item) => {
        const actionAttribute = item.lesson_id
          ? `data-open-student-lesson="${escapeAttribute(item.lesson_id)}"`
          : item.view
            ? `data-jump-student="${escapeAttribute(item.view)}"`
            : "";
        return `
          <button class="student-evidence-row" type="button" ${actionAttribute}>
            <span class="student-evidence-icon ${escapeAttribute(item.color || "blue")}"><i data-lucide="${escapeAttribute(item.icon || "circle")}"></i></span>
            <span>
              <strong>${escapeHtml(item.title)}</strong>
              <small>${escapeHtml(item.detail || "")}</small>
            </span>
            <time>${escapeHtml(relativeTime(item.date))}</time>
          </button>
        `;
      }).join("")}
    </div>
  `;
}

function renderRecentActivity() {
  const container = document.getElementById("recentActivity");
  if (!container) return;
  const lessonsById = new Map(studentState.lessons.map((lesson) => [lesson.id, lesson]));
  const activity = [...studentState.progress]
    .sort((a, b) => new Date(b.updated_at || 0) - new Date(a.updated_at || 0))
    .slice(0, 4);

  if (!activity.length) {
    container.innerHTML = `<p class="empty-text">Start a lesson to see activity here.</p>`;
    return;
  }

  container.innerHTML = activity.map((item) => {
    const lesson = lessonsById.get(item.lesson_id);
    return `
      <article class="activity-row">
        <i data-lucide="${item.status === "completed" ? "check-circle-2" : "circle-dot"}"></i>
        <div><strong>${escapeHtml(titleCase(item.status))}: ${escapeHtml(lesson?.title || "Lesson")}</strong><span>${escapeHtml(formatDate(item.updated_at))}</span></div>
      </article>
    `;
  }).join("");
}

function renderStudentLessonsLegacy() {
  const container = document.getElementById("studentLessons");
  const modulesById = new Map(studentState.modules.map((module) => [module.id, module]));
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));

  if (!studentState.lessons.length) {
    container.innerHTML = `<p class="empty-text">No published lessons yet for your active term.</p>`;
    return;
  }

  container.innerHTML = studentState.lessons.map((lesson) => {
    const module = modulesById.get(lesson.module_id);
    const progress = progressByLesson.get(lesson.id);
    const percent = progress?.progress_percent || 0;
    return `
      <article class="lesson-card">
        <i data-lucide="${lesson.lesson_type === "practice" ? "flag" : "book-open"}"></i>
        <strong>${escapeHtml(lesson.title)}</strong>
        <span>${escapeHtml(module?.title || "Module")} · ${lesson.duration_minutes} min</span>
        <p>${escapeHtml(lesson.description || "Lesson content will be provided by your teacher.")}</p>
        <div class="wide-progress"><span style="width:${percent}%"></span></div>
        <small>${percent}% complete</small>
        <button class="small-button" type="button" data-open-student-lesson="${escapeHtml(lesson.id)}"><i data-lucide="play-circle"></i><span>${percent ? "Continue Lesson" : "Start Lesson"}</span></button>
      </article>
    `;
  }).join("");
}

function renderStudentLessonsPackageView() {
  const container = document.getElementById("studentLessons");
  const modulesById = new Map(studentState.modules.map((module) => [module.id, module]));
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));

  if (!studentState.lessons.length) {
    container.innerHTML = `
      <section class="panel-card student-empty-dashboard">
        <i data-lucide="book-open"></i>
        <h2>No learning modules yet</h2>
        <p>Your teacher's published lessons will appear here once they are available for your grade and active term.</p>
      </section>
    `;
    return;
  }

  const filters = getStudentLearnFilters(modulesById);
  if (!filters.some((filter) => filter.key === studentState.lessonFilter)) {
    studentState.lessonFilter = "all";
  }
  const lessonRows = getStudentLearnRows(modulesById, progressByLesson).filter((row) => {
    if (studentState.lessonFilter === "all") return true;
    return studentLearnCategory(row.module).key === studentState.lessonFilter;
  });
  const pageCount = Math.max(1, Math.ceil(lessonRows.length / STUDENT_LEARN_ITEMS_PER_PAGE));
  studentState.learnPage = Math.min(Math.max(Number(studentState.learnPage) || 1, 1), pageCount);
  const start = (studentState.learnPage - 1) * STUDENT_LEARN_ITEMS_PER_PAGE;
  const visibleRows = lessonRows.slice(start, start + STUDENT_LEARN_ITEMS_PER_PAGE);

  container.innerHTML = `
    <div class="student-learn-layout">
      <div class="student-learn-main">
        <div class="student-learn-filters">
          ${filters.map((filter) => `
            <button class="${filter.key === studentState.lessonFilter ? "active" : ""}" type="button" data-learn-filter="${escapeAttribute(filter.key)}">
              <i data-lucide="${escapeAttribute(filter.icon)}"></i>
              <span>${escapeHtml(filter.label)}</span>
            </button>
          `).join("")}
        </div>
        <div class="student-learn-card-grid">
          ${visibleRows.length
            ? visibleRows.map((row, index) => renderStudentLearnCard(row, start + index)).join("")
            : `<section class="panel-card student-empty-dashboard"><i data-lucide="search"></i><h2>No lessons in this filter</h2><p>Try All Modules to see every published lesson for your active term.</p></section>`}
        </div>
        ${lessonRows.length > STUDENT_LEARN_ITEMS_PER_PAGE ? `<div class="student-dashboard-pagination-row"><span>Showing ${start + 1}-${start + visibleRows.length} of ${lessonRows.length} modules</span>${renderDashboardPagination({ currentPage: studentState.learnPage, pageCount, pageNumberAttribute: "data-student-learn-page", label: "Learning module pages" })}</div>` : ""}
      </div>
      <aside class="student-learn-side">
        ${renderStudentLearningSummary()}
        ${renderStudentLearningGoal()}
      </aside>
    </div>
  `;
}

function renderStudentLearnCard(row, index) {
  const lesson = row.nextLesson || row.lessons[0];
  const module = row.module;
  const percent = row.percent;
  const completed = row.completed;
  const difficulty = getStudentLessonDifficulty(row);
  const asset = getStudentLessonAsset(row, module);
  const action = row.locked ? "Locked" : completed ? "Review" : percent > 0 ? "Continue" : "Start Lesson";
  const category = studentLearnCategory(module);
  const lessonCountLabel = `${row.lessonCount} lesson${row.lessonCount === 1 ? "" : "s"}`;
  const practiceCountLabel = `${row.practiceCount} practice${row.practiceCount === 1 ? "" : "s"}`;

  return `
    <article class="student-learn-card ${completed ? "is-complete" : ""} ${row.locked ? "is-locked" : ""}">
      <div class="student-learn-visual ${asset.src ? "" : "icon-only"}">
        ${asset.src
          ? `<img src="${escapeAttribute(asset.src)}" alt="">`
          : `<i data-lucide="${escapeAttribute(asset.icon)}"></i>`}
        ${completed ? `<span class="student-learn-complete"><i data-lucide="check"></i></span>` : ""}
        ${row.locked ? `<span class="student-learn-lock"><i data-lucide="lock"></i></span>` : ""}
      </div>
      <div class="student-learn-card-body">
        <strong>${index + 1}. ${escapeHtml(row.title)}</strong>
        <p>${escapeHtml(row.locked ? row.lockReason : row.description || `${category.label} lesson prepared by your teacher.`)}</p>
        <div class="student-module-overview">
          <span><i data-lucide="list-checks"></i>${row.lessons.length} total</span>
          <span>${escapeHtml(lessonCountLabel)}</span>
          <span>${escapeHtml(practiceCountLabel)}</span>
        </div>
        ${renderStudentModuleLessonList(row)}
        <div class="student-learn-meta">
          <span class="difficulty-pill ${escapeAttribute(difficulty.key)}"><i data-lucide="bar-chart-3"></i>${escapeHtml(difficulty.label)}</span>
          <small>${percent}%</small>
        </div>
        <div class="wide-progress"><span style="width:${percent}%"></span></div>
        <button class="student-learn-action" type="button" data-open-student-lesson="${escapeAttribute(lesson?.id || "")}" data-locked="${row.locked ? "true" : "false"}" data-lock-reason="${escapeAttribute(row.lockReason)}" ${row.locked ? "disabled" : ""}>
          <i data-lucide="${row.locked ? "lock" : completed ? "book-open" : "arrow-right"}"></i>
          <span>${escapeHtml(action)}</span>
        </button>
      </div>
    </article>
  `;
}

function renderStudentModuleLessonList(row) {
  const items = (row.lessonSummaries || []).slice(0, 5);
  const extraCount = Math.max(0, (row.lessonSummaries || []).length - items.length);
  if (!items.length) return "";

  return `
    <div class="student-module-lesson-list" aria-label="${escapeAttribute(row.title)} lessons">
      ${items.map(({ lesson, percent, completed, locked, lockReason }) => `
        <button class="student-module-lesson-item ${locked ? "is-locked" : ""}" type="button" data-open-student-lesson="${escapeAttribute(lesson.id)}" data-locked="${locked ? "true" : "false"}" data-lock-reason="${escapeAttribute(lockReason || "")}" ${locked ? "disabled" : ""}>
          <i data-lucide="${escapeAttribute(locked ? "lock" : iconForLesson(lesson, row.module))}"></i>
          <span>${escapeHtml(lesson.title)}</span>
          <small>${locked ? "Locked" : completed ? "Done" : `${percent}%`}</small>
        </button>
      `).join("")}
      ${extraCount ? `<span class="student-module-more">+${extraCount} more in this module</span>` : ""}
    </div>
  `;
}

function renderStudentLearningSummary() {
  const summary = getStudentDashboardSummary();
  return `
    <section class="panel-card student-learn-summary">
      <h2>Your Learning Summary</h2>
      <article>
        <i data-lucide="graduation-cap" class="green"></i>
        <div><strong>${summary.completed}</strong><span>Lessons Completed</span></div>
      </article>
      <article>
        <i data-lucide="clock" class="blue"></i>
        <div><strong>${summary.hours}</strong><span>Hours Learned</span></div>
      </article>
      <article>
        <i data-lucide="file-badge" class="teal"></i>
        <div><strong>${summary.certificatesEarned}</strong><span>Certificates</span></div>
      </article>
    </section>
  `;
}

function renderStudentLearningGoal() {
  const range = getStudentProgressDateRange();
  const current = getWeeklyLessonCompletionCount(range);
  const target = Math.max(1, Math.min(5, studentState.lessons.filter((lesson) => !["practice", "assessment"].includes(lesson.lesson_type)).length || studentState.lessons.length));
  const percent = percentOf(Math.min(current, target), target);
  return `
    <section class="panel-card student-weekly-goal">
      <div class="panel-title-row">
        <h2>Weekly Goal</h2>
        <button class="link-button" type="button" data-jump-student="progress">View Progress</button>
      </div>
      <p>Complete ${target} lesson${target === 1 ? "" : "s"} this week</p>
      <div class="student-goal-donut" style="--value:${percent}">
        <span>Lessons</span>
        <strong>${current}/${target}</strong>
      </div>
      <small>${Math.max(0, target - current)} lesson${Math.max(0, target - current) === 1 ? "" : "s"} to go!</small>
    </section>
  `;
}

function buildStudentLessonCards() {
  const rows = [];
  const packageModuleIds = new Set();

  studentState.modules.forEach((module) => {
    if (!isPackageModule(module)) return;
    const lessons = getSortedLessonsForModule(module.id, studentState.lessons)
      .filter((lesson) => lesson.status === "published");
    if (!lessons.length) return;
    packageModuleIds.add(module.id);
    rows.push({
      type: "package",
      module,
      lessons,
      duration: lessons.reduce((sum, lesson) => sum + Number(lesson.duration_minutes || 0), 0)
    });
  });

  studentState.lessons.forEach((lesson) => {
    if (packageModuleIds.has(lesson.module_id)) return;
    rows.push({ type: "lesson", lesson });
  });

  return rows;
}

function getSortedStudentLessons() {
  const modulesById = new Map(studentState.modules.map((module) => [module.id, module]));
  return [...studentState.lessons].sort((a, b) => {
    const moduleA = modulesById.get(a.module_id);
    const moduleB = modulesById.get(b.module_id);
    return (Number(moduleA?.sort_order || 0) - Number(moduleB?.sort_order || 0))
      || String(moduleA?.title || "").localeCompare(String(moduleB?.title || ""))
      || (Number(a.sort_order || 0) - Number(b.sort_order || 0))
      || String(a.title || "").localeCompare(String(b.title || ""));
  });
}

function isStudentLessonComplete(lesson, progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]))) {
  if (!lesson) return false;
  const progress = progressByLesson.get(lesson.id);
  const latestAttempt = ["practice", "assessment"].includes(lesson.lesson_type)
    ? getLatestAttemptsByLesson().find((attempt) => attempt.lesson_id === lesson.id)
    : null;
  return progress?.status === "completed"
    || Number(progress?.progress_percent || 0) >= 100
    || Boolean(latestAttempt);
}

function getStudentLessonGate(lessonId) {
  const lessons = getSortedStudentLessons().filter((lesson) => lesson.status === "published");
  const currentIndex = lessons.findIndex((lesson) => lesson.id === lessonId);
  if (currentIndex <= 0) return { locked: false, reason: "" };
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));
  const current = lessons[currentIndex];
  const currentProgress = progressByLesson.get(lessonId);
  const currentAttempt = getLatestAttemptsByLesson().find((attempt) => attempt.lesson_id === lessonId);
  if (currentAttempt || currentProgress?.status === "in_progress" || Number(currentProgress?.progress_percent || 0) > 0) {
    return { locked: false, reason: "" };
  }
  const blocker = lessons.slice(0, currentIndex).find((lesson) => !isStudentLessonComplete(lesson, progressByLesson));
  if (!blocker) return { locked: false, reason: "" };
  return {
    locked: true,
    reason: blocker.module_id !== current.module_id
      ? "Finish the previous module first."
      : "Finish the previous lesson or practice first.",
    blocker
  };
}

function getNextUnlockedStudentLesson() {
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));
  return getSortedStudentLessons()
    .filter((lesson) => lesson.status === "published")
    .find((lesson) => !getStudentLessonGate(lesson.id).locked && !isStudentLessonComplete(lesson, progressByLesson)) || null;
}

function showLockedLessonMessage(reason = "") {
  showToast({
    title: "Lesson unavailable",
    message: reason || "Finish the required lesson first to open this part.",
    type: "warning"
  });
}

function getStudentLearnRows(modulesById, progressByLesson) {
  let previousModuleComplete = true;
  const attemptsByLesson = new Map(getLatestAttemptsByLesson().map((attempt) => [attempt.lesson_id, attempt]));
  return [...studentState.modules]
    .sort((a, b) => (Number(a.sort_order || 0) - Number(b.sort_order || 0)) || String(a.title || "").localeCompare(String(b.title || "")))
    .map((module) => {
      const lessons = getSortedLessonsForModule(module.id, studentState.lessons)
        .filter((lesson) => lesson.status === "published");
      if (!lessons.length) return null;
      const completedCount = lessons.filter((lesson) => isStudentLessonComplete(lesson, progressByLesson)).length;
      const percent = percentOf(completedCount, lessons.length);
      const nextLesson = lessons.find((lesson) => {
        return !isStudentLessonComplete(lesson, progressByLesson);
      }) || lessons[0];
      const lessonCount = lessons.filter((lesson) => !["practice", "assessment"].includes(lesson.lesson_type)).length;
      const practiceCount = lessons.length - lessonCount;
      const minutes = lessons.reduce((sum, lesson) => sum + Number(lesson.duration_minutes || 0), 0);
      const lessonSummaries = lessons.map((lesson) => {
        const progress = progressByLesson.get(lesson.id);
        const lessonPercent = Math.min(100, Math.max(0, Number(progress?.progress_percent || 0)));
        return {
          lesson,
          percent: lessonPercent,
          completed: isStudentLessonComplete(lesson, progressByLesson)
        };
      });
      let previousLessonComplete = true;
      const gatedLessonSummaries = lessonSummaries.map((summary) => {
        const progress = progressByLesson.get(summary.lesson.id);
        const started = Boolean(attemptsByLesson.get(summary.lesson.id))
          || progress?.status === "in_progress"
          || Number(progress?.progress_percent || 0) > 0
          || summary.completed;
        const locked = !started && (!previousModuleComplete || !previousLessonComplete);
        const lockReason = !previousModuleComplete
          ? "Finish the previous module first."
          : "Finish the previous lesson or practice first.";
        previousLessonComplete = summary.completed;
        return {
          ...summary,
          locked,
          lockReason
        };
      });
      const firstUnlocked = gatedLessonSummaries.find((summary) => !summary.locked && !summary.completed)
        || gatedLessonSummaries.find((summary) => !summary.locked)
        || gatedLessonSummaries[0];
      const moduleLocked = !previousModuleComplete;
      const moduleLockReason = "Finish the previous module first.";
      previousModuleComplete = completedCount === lessons.length;
      return {
        module,
        lessons,
        lessonSummaries: gatedLessonSummaries,
        nextLesson: firstUnlocked?.lesson || nextLesson,
        title: module.title,
        description: module.description || `${lessonCount || lessons.length} lesson${(lessonCount || lessons.length) === 1 ? "" : "s"} for ${module.category || "ICT skills"}.`,
        percent,
        completed: completedCount === lessons.length,
        completedCount,
        locked: moduleLocked,
        lockReason: moduleLocked ? moduleLockReason : "",
        lessonCount,
        practiceCount,
        duration_minutes: minutes,
        lesson_type: practiceCount && !lessonCount ? "practice" : "lesson"
      };
    })
    .filter(Boolean);
}

function getStudentLearnFilters(modulesById) {
  const categories = new Map();
  studentState.modules.forEach((module) => {
    const hasLessons = studentState.lessons.some((lesson) => lesson.module_id === module.id && lesson.status === "published");
    if (!hasLessons) return;
    const category = studentLearnCategory(module);
    if (!categories.has(category.key)) categories.set(category.key, category);
  });
  return [
    { key: "all", label: "All Modules", icon: "layers-3" },
    ...[...categories.values()].sort((a, b) => a.order - b.order || a.label.localeCompare(b.label))
  ];
}

function studentLearnCategory(module = {}) {
  const value = `${module.category || ""} ${module.title || ""}`.toLowerCase();
  if (value.includes("productivity") || value.includes("spreadsheet") || value.includes("word")) {
    return { key: "productivity", label: "Productivity", icon: "file-text", order: 2 };
  }
  if (value.includes("network") || value.includes("internet")) {
    return { key: "networking", label: "Networking", icon: "globe-2", order: 3 };
  }
  if (value.includes("safety") || value.includes("security")) {
    return { key: "safety", label: "Safety", icon: "shield-check", order: 4 };
  }
  return { key: "hardware", label: "Hardware", icon: "monitor", order: 1 };
}

function getStudentLessonDifficulty(lesson) {
  const minutes = Number(lesson.duration_minutes || 0);
  if (lesson.lesson_type === "assessment" || minutes >= 40) return { key: "hard", label: "Hard" };
  if (lesson.lesson_type === "practice" || minutes >= 25) return { key: "medium", label: "Medium" };
  return { key: "easy", label: "Easy" };
}

function getStudentLessonAsset(lesson, module = {}) {
  const value = `${lesson.title || ""} ${lesson.description || ""} ${module.category || ""} ${module.title || ""} ${(lesson.lessons || []).map((item) => `${item.title || ""} ${item.description || ""}`).join(" ")}`.toLowerCase();
  const asset = STUDENT_LEARN_ASSETS.find((item) => item.match.some((term) => value.includes(term)));
  if (asset) return asset;
  return { src: "", icon: iconForLesson(lesson.nextLesson || lesson, module) };
}

function getWeeklyLessonCompletionCount(range) {
  const lessonById = new Map(studentState.lessons.map((lesson) => [lesson.id, lesson]));
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));
  return studentState.progress.filter((item) => {
    const lesson = lessonById.get(item.lesson_id);
    return lesson
      && !["practice", "assessment"].includes(lesson.lesson_type)
      && isStudentLessonComplete(lesson, progressByLesson)
      && isDateInRange(item.completed_at || item.updated_at, range.startDate, range.endDate);
  }).length;
}

async function handleStudentLessonAction(event) {
  const pageButton = event.target.closest("[data-student-learn-page]");
  if (pageButton) {
    studentState.learnPage = Number(pageButton.dataset.studentLearnPage) || 1;
    renderStudentLessonsPackageView();
    if (window.lucide) window.lucide.createIcons();
    return;
  }
  const filterButton = event.target.closest("[data-learn-filter]");
  if (filterButton) {
    studentState.lessonFilter = filterButton.dataset.learnFilter || "all";
    studentState.learnPage = 1;
    renderStudentLessonsPackageView();
    if (window.lucide) window.lucide.createIcons();
    return;
  }
  const jumpButton = event.target.closest("[data-jump-student]");
  if (jumpButton) {
    showStudentView(jumpButton.dataset.jumpStudent);
    return;
  }
  const reviewButton = event.target.closest("[data-review-student-assessment]");
  if (reviewButton) {
    showStudentAssessmentReview(reviewButton.dataset.reviewStudentAssessment);
    return;
  }
  const button = event.target.closest("[data-open-student-lesson]");
  if (!button) return;
  if (button.disabled || button.dataset.locked === "true") {
    showLockedLessonMessage(button.dataset.lockReason);
    return;
  }
  if (!button.dataset.openStudentLesson) return;
  await openStudentLesson(button.dataset.openStudentLesson, button.dataset.openView || "auto");
}

function renderStudentAssessmentsDashboard() {
  const container = document.getElementById("studentAssessmentsDashboard");
  if (!container) return;

  const modulesById = new Map(studentState.modules.map((module) => [module.id, module]));
  const items = getStudentAssessmentItems(modulesById);
  const metrics = getStudentAssessmentMetrics(items);
  const guidance = getStudentAssessmentGuidance(items);

  if (!items.length) {
    container.innerHTML = `
      <section class="panel-card student-empty-dashboard assessment-empty-state">
        <i data-lucide="clipboard-check"></i>
        <h2>No assessments yet</h2>
        <p>Your practice checks and formal assessments will appear here once your teacher publishes them.</p>
        <button class="primary-button" type="button" data-jump-student="lessons">Go to Learn</button>
      </section>
    `;
    if (window.lucide) window.lucide.createIcons();
    return;
  }
  const pageCount = Math.max(1, Math.ceil(items.length / STUDENT_ASSESSMENTS_PER_PAGE));
  studentState.assessmentPage = Math.min(Math.max(Number(studentState.assessmentPage) || 1, 1), pageCount);
  const start = (studentState.assessmentPage - 1) * STUDENT_ASSESSMENTS_PER_PAGE;
  const visibleItems = items.slice(start, start + STUDENT_ASSESSMENTS_PER_PAGE);

  container.innerHTML = `
    <div class="student-assessment-metrics">
      ${renderStudentAssessmentMetric("clock-3", "Pending Checks", metrics.pending, "Need attention", "blue")}
      ${renderStudentAssessmentMetric("check-circle-2", "Completed Checks", metrics.completed, `${metrics.total} total`, "green")}
      ${renderStudentAssessmentMetric("bar-chart-3", "Average Score", metrics.averageLabel, "Latest submissions", "purple")}
      ${renderStudentAssessmentMetric("trophy", "Best Score", metrics.bestLabel, "Personal best", "gold")}
    </div>
    ${renderStudentAssessmentFlowCard(guidance)}
    <div class="student-assessment-layout">
      <section class="panel-card student-assessment-list-card">
        <div class="student-card-title-row">
          <h2>Available Checks</h2>
          <span><i data-lucide="clipboard-list"></i>${items.length} check${items.length === 1 ? "" : "s"}</span>
        </div>
        <div class="student-assessment-list">
          ${visibleItems.map(renderStudentAssessmentCard).join("")}
        </div>
        ${items.length > STUDENT_ASSESSMENTS_PER_PAGE ? `<div class="student-dashboard-pagination-row"><span>Showing ${start + 1}-${start + visibleItems.length} of ${items.length} checks</span>${renderDashboardPagination({ currentPage: studentState.assessmentPage, pageCount, pageNumberAttribute: "data-student-assessment-page", label: "Student assessment pages" })}</div>` : ""}
      </section>
      <aside class="student-assessment-side">
        ${renderStudentAssessmentResults(items)}
        ${renderStudentAssessmentReadiness(items)}
      </aside>
    </div>
  `;
  if (window.lucide) window.lucide.createIcons();
}

function renderStudentAssessmentMetric(icon, label, value, detail, color) {
  return `
    <article class="student-assessment-metric-card">
      <span class="assessment-metric-icon ${escapeAttribute(color)}"><i data-lucide="${escapeAttribute(icon)}"></i></span>
      <div>
        <p>${escapeHtml(label)}</p>
        <strong>${escapeHtml(value)}</strong>
        <small>${escapeHtml(detail)}</small>
      </div>
    </article>
  `;
}

function renderStudentAssessmentCard(item) {
  const { lesson, module, progressPercent, status, statusLabel, latestAttempt } = item;
  const action = item.locked ? "Locked" : status === "submitted" ? "Review" : status === "in_progress" ? "Continue" : "Start";
  const actionIcon = item.locked ? "lock" : status === "submitted" ? "eye" : status === "in_progress" ? "play-circle" : "play";
  const hasScore = Number.isFinite(item.score);
  const score = hasScore ? Number(item.score) : null;
  const scoreLabel = hasScore ? `${score}%` : status === "submitted" ? "Submitted" : `${progressPercent}%`;
  const dateLabel = lesson.due_date
    ? `Due ${formatDateOnly(lesson.due_date)}`
    : lesson.scheduled_date
      ? `Scheduled ${formatDateOnly(lesson.scheduled_date)}`
      : "No due date";
  const submittedLabel = latestAttempt?.submitted_at ? `Submitted ${formatDateOnly(latestAttempt.submitted_at)}` : "";
  const typeLabel = lesson.lesson_type === "assessment" ? "Assessment" : "Practice";

  return `
    <article class="student-assessment-card ${escapeAttribute(status)} ${escapeAttribute(item.evaluationRole || "")} ${item.locked ? "is-locked" : ""}">
      <div class="assessment-card-icon ${escapeAttribute(moduleCategoryClass(module?.category || module?.title || ""))}">
        <i data-lucide="${escapeAttribute(iconForLesson(lesson, module))}"></i>
      </div>
      <div class="assessment-card-copy">
        <div class="assessment-card-topline">
          ${item.stepLabel ? `<span class="assessment-step-pill">${escapeHtml(item.stepLabel)}</span>` : ""}
          <span class="assessment-type-pill">${escapeHtml(typeLabel)}</span>
          <span class="assessment-status-pill ${escapeAttribute(status)}">${escapeHtml(statusLabel)}</span>
        </div>
        <strong>${escapeHtml(lesson.title)}</strong>
        <p>${escapeHtml(item.locked ? item.lockReason : lesson.description || "Complete this check to test your understanding.")}</p>
        <div class="assessment-card-meta">
          <span><i data-lucide="layers-3"></i>${escapeHtml(module?.title || "Module")}</span>
          <span><i data-lucide="calendar-days"></i>${escapeHtml(dateLabel)}</span>
          <span><i data-lucide="timer"></i>${Number(lesson.duration_minutes || 0)} min</span>
          ${submittedLabel ? `<span><i data-lucide="send"></i>${escapeHtml(submittedLabel)}</span>` : ""}
        </div>
        <div class="assessment-progress-row">
          <div class="wide-progress"><span style="width:${status === "submitted" ? 100 : progressPercent}%"></span></div>
          <b class="${hasScore ? scoreBandClass(score) : ""}">${escapeHtml(scoreLabel)}</b>
        </div>
      </div>
      <button class="student-assessment-action" type="button" ${status === "submitted" ? `data-review-student-assessment="${escapeAttribute(lesson.id)}"` : `data-open-student-lesson="${escapeAttribute(lesson.id)}"`} data-locked="${item.locked ? "true" : "false"}" data-lock-reason="${escapeAttribute(item.lockReason || "")}" ${item.locked ? "disabled" : ""}>
        <i data-lucide="${escapeAttribute(actionIcon)}"></i>
        <span>${escapeHtml(action)}</span>
      </button>
    </article>
  `;
}

function renderStudentAssessmentFlowCard(guidance) {
  if (!guidance?.show) return "";
  const action = guidance.action || {};
  const actionButton = action.lessonId
    ? `<button class="student-assessment-flow-action" type="button" data-open-student-lesson="${escapeAttribute(action.lessonId)}"><i data-lucide="${escapeAttribute(action.icon || "play")}"></i><span>${escapeHtml(action.label || "Open")}</span></button>`
    : `<button class="student-assessment-flow-action" type="button" data-jump-student="${escapeAttribute(action.view || "lessons")}"><i data-lucide="${escapeAttribute(action.icon || "book-open")}"></i><span>${escapeHtml(action.label || "Go to Learn")}</span></button>`;

  return `
    <section class="panel-card student-assessment-flow-card ${escapeAttribute(guidance.tone || "blue")}">
      <span class="student-assessment-flow-icon"><i data-lucide="${escapeAttribute(guidance.icon || "clipboard-check")}"></i></span>
      <div>
        <strong>${escapeHtml(guidance.title)}</strong>
        <p>${escapeHtml(guidance.body)}</p>
      </div>
      ${actionButton}
    </section>
  `;
}

function renderStudentAssessmentResults(items) {
  const results = items
    .filter((item) => item.latestAttempt)
    .sort((a, b) => new Date(b.latestAttempt.submitted_at || 0) - new Date(a.latestAttempt.submitted_at || 0))
    .slice(0, 5);
  const rows = results.length
    ? results.map((item) => {
      const hasScore = Number.isFinite(item.score);
      const score = hasScore ? Number(item.score) : null;
      return `
        <article class="student-assessment-result-row">
          <i data-lucide="${escapeAttribute(iconForLesson(item.lesson, item.module))}" class="${escapeAttribute(moduleCategoryClass(item.module?.category || item.module?.title || ""))}"></i>
          <div>
            <strong>${escapeHtml(item.lesson.title)}</strong>
            <span>${escapeHtml(item.module?.title || "Module")} - ${escapeHtml(formatDateOnly(item.latestAttempt.submitted_at))}</span>
          </div>
          <b class="${hasScore ? scoreBandClass(score) : ""}">${hasScore ? `${score}%` : "-"}</b>
          <button class="icon-button" type="button" data-review-student-assessment="${escapeAttribute(item.lesson.id)}" aria-label="Review ${escapeAttribute(item.lesson.title)}"><i data-lucide="eye"></i></button>
        </article>
      `;
    }).join("")
    : `<p class="empty-text">Submit a practice or assessment to see recent results.</p>`;

  return `
    <section class="panel-card student-assessment-results-card">
      <div class="student-card-title-row"><h2>Recent Results</h2><i data-lucide="history"></i></div>
      <div class="student-assessment-results-list">${rows}</div>
    </section>
  `;
}

function renderStudentAssessmentReadiness(items) {
  const groups = new Map();
  items.forEach((item) => {
    if (!Number.isFinite(item.score)) return;
    const key = item.module?.id || item.module?.title || "module";
    const current = groups.get(key) || {
      label: item.module?.category || item.module?.title || "Topic",
      icon: iconForTopic(item.module),
      color: moduleCategoryClass(`${item.module?.category || ""} ${item.module?.title || ""}`),
      scores: []
    };
    current.scores.push(item.score);
    groups.set(key, current);
  });

  const rows = [...groups.values()]
    .map((group) => ({
      ...group,
      score: Math.round(group.scores.reduce((sum, score) => sum + score, 0) / group.scores.length)
    }))
    .sort((a, b) => b.score - a.score || a.label.localeCompare(b.label));

  const body = rows.length
    ? rows.map((row) => `
      <article class="student-assessment-readiness-row">
        <i data-lucide="${escapeAttribute(row.icon)}" class="${escapeAttribute(row.color)}"></i>
        <strong>${escapeHtml(row.label)}</strong>
        <div class="student-mastery-track"><span style="width:${row.score}%"></span></div>
        <b class="${scoreBandClass(row.score)}">${row.score}%</b>
      </article>
    `).join("")
    : `<p class="empty-text">Topic readiness appears after your first submitted check.</p>`;

  return `
    <section class="panel-card student-assessment-readiness-card">
      <div class="student-card-title-row"><h2>Topic Readiness</h2><i data-lucide="activity"></i></div>
      <div class="student-assessment-readiness-list">${body}</div>
    </section>
  `;
}

function getStudentAssessmentItems(modulesById) {
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));
  const latestAttemptsByLesson = new Map(getLatestAttemptsByLesson().map((attempt) => [attempt.lesson_id, attempt]));
  const evaluation = normalizeStudentEvaluationData(studentState.evaluation);
  const pretestLessonId = evaluation.cycle?.pretest_lesson_id || "";
  const posttestLessonId = evaluation.cycle?.posttest_lesson_id || "";
  const pretestSubmitted = pretestLessonId ? Boolean(latestAttemptsByLesson.get(pretestLessonId)) : false;
  const posttestReady = pretestSubmitted && !getNextIncompleteCoreLesson(progressByLesson);
  const now = new Date();

  return studentState.lessons
    .filter((lesson) => ["practice", "assessment"].includes(lesson.lesson_type))
    .map((lesson) => {
      const module = modulesById.get(lesson.module_id) || null;
      const progress = progressByLesson.get(lesson.id) || null;
      const latestAttempt = latestAttemptsByLesson.get(lesson.id) || null;
      const progressPercent = Number(progress?.progress_percent || 0);
      const submitted = Boolean(latestAttempt)
        || progress?.status === "completed"
        || progressPercent >= 100;
      const overdue = !submitted && lesson.due_date && endOfLocalDay(lesson.due_date) < now;
      const inProgress = !submitted && !overdue && (progress?.status === "in_progress" || progressPercent > 0);
      const status = submitted ? "submitted" : overdue ? "overdue" : inProgress ? "in_progress" : "not_started";
      const score = Number(latestAttempt?.score_percent);
      const evaluationRole = lesson.id === pretestLessonId
        ? "pretest"
        : lesson.id === posttestLessonId
          ? "posttest"
          : "";
      const gate = getStudentLessonGate(lesson.id);
      return {
        lesson,
        module,
        progress,
        latestAttempt,
        progressPercent: Math.max(0, Math.min(100, progressPercent)),
        submitted,
        status,
        statusLabel: assessmentStatusLabel(status),
        score: Number.isFinite(score) ? score : null,
        evaluationRole,
        locked: gate.locked,
        lockReason: gate.reason,
        holdUntilLessonsDone: evaluationRole === "posttest" && !submitted && !posttestReady,
        stepLabel: evaluationRole === "pretest"
        ? "Step 1: Pre-assessment"
          : evaluationRole === "posttest"
            ? "Final step: Post-assessment"
            : "",
        guidePriority: studentAssessmentGuidePriority({ evaluationRole, submitted, posttestReady })
      };
    })
    .sort((a, b) => {
      const priority = { overdue: 0, in_progress: 1, not_started: 2, submitted: 3 };
      const statusPriority = (item) => item.holdUntilLessonsDone ? 2.5 : priority[item.status];
      return (a.guidePriority - b.guidePriority)
        || (statusPriority(a) - statusPriority(b))
        || (assessmentSortDate(a.lesson) - assessmentSortDate(b.lesson))
        || (Number(a.module?.sort_order || 0) - Number(b.module?.sort_order || 0))
        || (Number(a.lesson.sort_order || 0) - Number(b.lesson.sort_order || 0))
        || String(a.lesson.title || "").localeCompare(String(b.lesson.title || ""));
    });
}

function studentAssessmentGuidePriority({ evaluationRole, submitted, posttestReady }) {
  if (evaluationRole === "pretest" && !submitted) return -30;
  if (evaluationRole === "posttest" && !submitted && posttestReady) return -20;
  return 0;
}

function getStudentAssessmentGuidance(items) {
  const evaluation = normalizeStudentEvaluationData(studentState.evaluation);
  const cycle = evaluation.cycle;
  if (!cycle?.pretest_lesson_id && !cycle?.posttest_lesson_id) return { show: false };

  const pretest = items.find((item) => item.lesson.id === cycle.pretest_lesson_id);
  const posttest = items.find((item) => item.lesson.id === cycle.posttest_lesson_id);
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));
  const nextLesson = getNextIncompleteCoreLesson(progressByLesson);

  if (pretest && !pretest.submitted) {
    return {
      show: true,
      tone: "blue",
      icon: "clipboard-check",
      title: "Step 1: Take the pre-assessment",
      body: "Answer this before studying the lessons so your teacher can see your starting point.",
      action: { label: pretest.status === "in_progress" ? "Continue" : "Start", icon: "play", lessonId: pretest.lesson.id }
    };
  }

  if (pretest?.submitted && nextLesson) {
    return {
      show: true,
      tone: "green",
      icon: "book-open",
      title: "Next: Continue the lessons",
      body: "Your pre-assessment is done. Work through the lessons and VR activities before taking the post-assessment.",
      action: { label: "Continue Lesson", icon: "arrow-right", lessonId: nextLesson.id }
    };
  }

  if (posttest && !posttest.submitted) {
    return {
      show: true,
      tone: "purple",
      icon: "flag",
      title: "Final step: Take the post-assessment",
      body: "Answer this after the lessons to show what improved. You can still open it from here when your teacher asks.",
      action: { label: posttest.status === "in_progress" ? "Continue" : "Start", icon: "play", lessonId: posttest.lesson.id }
    };
  }

  if (posttest?.submitted) {
    return {
      show: true,
      tone: "green",
      icon: "check-circle-2",
      title: "Pre/Post checks complete",
      body: "Your pre-assessment and post-assessment have both been submitted.",
      action: { label: "Review Post-assessment", icon: "eye", lessonId: posttest.lesson.id }
    };
  }

  return { show: false };
}

function getNextIncompleteCoreLesson(progressByLesson = new Map()) {
  return [...studentState.lessons]
    .filter((lesson) => !["practice", "assessment"].includes(lesson.lesson_type))
    .sort((a, b) =>
      (Number(a.sort_order || 0) - Number(b.sort_order || 0))
      || String(a.scheduled_date || a.created_at || "").localeCompare(String(b.scheduled_date || b.created_at || ""))
    )
    .find((lesson) => {
      return !isStudentLessonComplete(lesson, progressByLesson);
    }) || null;
}

function getStudentAssessmentMetrics(items) {
  const scores = items
    .map((item) => item.score)
    .filter(Number.isFinite);
  const average = scores.length
    ? Math.round(scores.reduce((sum, score) => sum + score, 0) / scores.length)
    : null;
  const best = scores.length ? Math.max(...scores) : null;
  return {
    total: items.length,
    pending: items.filter((item) => !item.submitted).length,
    completed: items.filter((item) => item.submitted).length,
    averageLabel: average === null ? "-" : `${average}%`,
    bestLabel: best === null ? "-" : `${best}%`
  };
}

async function handleStudentAssessmentAction(event) {
  const pageButton = event.target.closest("[data-student-assessment-page]");
  if (pageButton) {
    studentState.assessmentPage = Number(pageButton.dataset.studentAssessmentPage) || 1;
    renderStudentAssessmentsDashboard();
    return;
  }
  const jumpButton = event.target.closest("[data-jump-student]");
  if (jumpButton) {
    showStudentView(jumpButton.dataset.jumpStudent);
    return;
  }
  const reviewButton = event.target.closest("[data-review-student-assessment]");
  if (reviewButton) {
    showStudentAssessmentReview(reviewButton.dataset.reviewStudentAssessment);
    return;
  }
  const lessonButton = event.target.closest("[data-open-student-lesson]");
  if (!lessonButton) return;
  if (lessonButton.disabled || lessonButton.dataset.locked === "true") {
    showLockedLessonMessage(lessonButton.dataset.lockReason);
    return;
  }
  await openStudentLesson(lessonButton.dataset.openStudentLesson, "auto");
}

function showStudentAssessmentReview(lessonId) {
  const lesson = studentState.lessons.find((item) => item.id === lessonId);
  const module = lesson ? studentState.modules.find((item) => item.id === lesson.module_id) : null;
  const attempt = getLatestAttemptsByLesson().find((item) => item.lesson_id === lessonId);
  if (!lesson || !attempt) {
    showToast({
      title: "No review yet",
      message: "Submit this practice or assessment first to see score feedback.",
      type: "info"
    });
    return;
  }

  const feedbackRows = normalizeAttemptFeedback(attempt.feedback).map((item, index) => `
    <article class="assessment-review-row ${item.is_correct ? "correct" : "incorrect"}">
      <span><i data-lucide="${item.is_correct ? "check-circle-2" : "alert-circle"}"></i></span>
      <div>
        <strong>Question ${index + 1}</strong>
        ${item.prompt ? `<p>${escapeHtml(item.prompt)}</p>` : ""}
        ${item.submitted_answer ? `<small>Your answer: ${escapeHtml(item.submitted_answer)}</small>` : ""}
        ${item.correct_answer ? `<small>Correct answer: ${escapeHtml(item.correct_answer)}</small>` : ""}
        <em>${escapeHtml(item.explanation || (item.is_correct ? "Correct answer." : "Review the correct answer and try the next activity carefully."))}</em>
      </div>
    </article>
  `).join("");

  createFeedbackModal({
    title: "Assessment Review",
    message: `${lesson.title} - ${attempt.score_percent ?? 0}% score`,
    tone: Number(attempt.score_percent || 0) >= 70 ? "primary" : "warning",
    icon: "clipboard-check",
    confirmText: "Close",
    hideCancel: true,
    content: `
      <div class="assessment-review-summary">
        <span><i data-lucide="${escapeAttribute(iconForLesson(lesson, module))}"></i></span>
        <div>
          <strong>${escapeHtml(attempt.score_percent ?? 0)}%</strong>
          <small>${escapeHtml(module?.title || "Assessment")} • Submitted ${escapeHtml(formatDateOnly(attempt.submitted_at))}</small>
        </div>
      </div>
      <div class="assessment-review-list">
        ${feedbackRows || `<p class="empty-text">No answer breakdown was saved for this attempt.</p>`}
      </div>
    `
  });
}

async function handleStudentHomeAction(event) {
  const jumpButton = event.target.closest("[data-jump-student]");
  if (jumpButton) {
    showStudentView(jumpButton.dataset.jumpStudent);
    return;
  }
  const lessonButton = event.target.closest("[data-open-student-lesson]");
  if (lessonButton) {
    if (lessonButton.disabled || lessonButton.dataset.locked === "true") {
      showLockedLessonMessage(lessonButton.dataset.lockReason);
      return;
    }
    await openStudentLesson(lessonButton.dataset.openStudentLesson, "auto");
  }
}

function assessmentStatusLabel(status) {
  if (status === "submitted") return "Submitted";
  if (status === "overdue") return "Overdue";
  if (status === "in_progress") return "In Progress";
  return "Not Started";
}

function assessmentSortDate(lesson) {
  const date = lesson.due_date || lesson.scheduled_date || lesson.created_at || "";
  const timestamp = date ? new Date(date).getTime() : Number.MAX_SAFE_INTEGER;
  return Number.isFinite(timestamp) ? timestamp : Number.MAX_SAFE_INTEGER;
}

function scoreBandClass(score) {
  return Number(score) >= 85 ? "good" : Number(score) >= 70 ? "warn" : "danger";
}

async function openStudentLesson(lessonId, view = "lesson") {
  const message = document.getElementById("studentMessage");
  const playerContainer = document.getElementById("studentLessonPlayer");
  try {
    const gate = getStudentLessonGate(lessonId);
    if (gate.locked) {
      showLockedLessonMessage(gate.reason);
      setMessage(message, gate.reason, "info");
      return;
    }
    setMessage(message, "Opening lesson...");
    if (playerContainer) {
      playerContainer.innerHTML = `
        <section class="lesson-loading-panel">
          <span class="button-spinner" aria-hidden="true"></span>
          <strong>Opening lesson...</strong>
          <p>Loading the content, questions, and progress for this part.</p>
        </section>
      `;
      showStudentView("lesson-player");
    }
    const detail = await apiGet(`/api/student/lessons?lesson_id=${encodeURIComponent(lessonId)}`);
    mergeStudentProgress(detail.progress);
    syncStudentCompletionSummaryProgress();
    detail.all_progress = studentState.progress;
    studentState.lessonPlayer = {
      detail,
      view: resolveLessonPlayerView(detail.lesson, view),
      feedback: null,
      layout: studentState.lessonPlayer?.layout || defaultLessonLayout()
    };
    renderStudentLessonPlayer();
    showStudentView("lesson-player");
    setMessage(message, "", "success");
  } catch (error) {
    setMessage(message, error.message, "error");
  }
}

function renderStudentLessonPlayer() {
  const container = document.getElementById("studentLessonPlayer");
  if (!container || !studentState.lessonPlayer) return;
  renderLessonPlayer(container, studentState.lessonPlayer.detail, {
    preview: false,
    view: studentState.lessonPlayer.view,
    feedback: studentState.lessonPlayer.feedback,
    layout: studentState.lessonPlayer.layout
  });
}

function renderLessonPlayer(container, detail, options = {}) {
  const view = options.view || "lesson";
  const lesson = detail.lesson;
  const module = detail.module || {};
  const moduleLessons = detail.module_lessons || [lesson];
  const progressRows = detail.all_progress || studentState.progress || [];
  const progressByLesson = new Map(progressRows.map((item) => [item.lesson_id, item]));
  const currentIndex = Math.max(0, moduleLessons.findIndex((item) => item.id === lesson.id));
  const completedCount = moduleLessons.filter((item) => isStudentLessonComplete(item, progressByLesson)).length;
  const progressPercent = moduleLessons.length ? Math.round((completedCount / moduleLessons.length) * 100) : 0;
  const quickTips = (detail.sections || []).filter((section) => section.section_type === "quick_tip");
  const contentSections = (detail.sections || []).filter((section) => section.section_type !== "quick_tip");
  const questions = detail.questions || [];
  const feedback = options.feedback || latestAttemptFeedback(detail.attempts);
  const preview = Boolean(options.preview);
  const previousLesson = moduleLessons[currentIndex - 1];
  const nextLesson = moduleLessons[currentIndex + 1];
  const currentComplete = preview || isStudentLessonComplete(lesson, progressByLesson);
  const nextGate = nextLesson ? getStudentLessonGate(nextLesson.id) : { locked: false, reason: "" };
  const canOpenNext = Boolean(nextLesson) && (preview || !nextGate.locked || !["practice", "assessment"].includes(lesson.lesson_type));
  const nextActionLabel = nextLesson
    ? !["practice", "assessment"].includes(lesson.lesson_type) && !currentComplete
      ? "Mark Done & Continue"
      : nextGate.locked
        ? "Complete Current First"
        : "Next Lesson"
    : "";
  const checkLessons = moduleLessons.filter((item) => ["practice", "assessment"].includes(item.lesson_type));
  const checkIndex = ["practice", "assessment"].includes(lesson.lesson_type) ? checkLessons.findIndex((item) => item.id === lesson.id) + 1 : 0;
  const currentPart = currentIndex + 1;
  const totalParts = Math.max(moduleLessons.length, 1);
  const partPercent = Math.round((currentPart / totalParts) * 100);
  const totalDuration = moduleLessons.reduce((sum, item) => sum + Number(item.duration_minutes || 0), 0);
  const lessonLabel = lesson.lesson_type === "assessment"
    ? "Assessment"
    : lesson.lesson_type === "practice"
      ? lesson.title.toLowerCase().includes("final") ? "Assessment" : "Practice Check"
      : "Lesson";
  const layout = normalizeLessonLayout(options.layout);
  const gridClasses = [
    "lesson-player-grid",
    layout.showSequence ? "" : "sequence-collapsed",
    layout.showTools ? "" : "tools-collapsed",
    !layout.showSequence && !layout.showTools ? "focus-mode" : ""
  ].filter(Boolean).join(" ");

  container.innerHTML = `
    ${preview ? `
      <div class="lesson-preview-toolbar">
        <span><i data-lucide="eye"></i> Teacher Preview - progress will not be saved</span>
        <div>
          <button class="${view === "lesson" ? "active" : ""}" type="button" data-player-view="lesson"><i data-lucide="book-open"></i><span>Lesson View</span></button>
          <button class="${view === "practice" ? "active" : ""}" type="button" data-player-view="practice" ${questions.length ? "" : "disabled"}><i data-lucide="flag"></i><span>Practice View</span></button>
        </div>
      </div>
    ` : ""}
    <div class="lesson-player-heading">
      <div>
        <span class="lesson-player-kicker">${escapeHtml(lessonLabel)} <b>Part ${currentPart} of ${totalParts}</b></span>
        <h1>${escapeHtml(view === "practice" ? `${lessonLabel}: ${lesson.title}` : lesson.title)}</h1>
        <p>${escapeHtml(module.title || "Lesson")} ${preview ? "- Teacher preview" : ""}</p>
      </div>
      <div class="lesson-player-top-actions">
        <button class="small-button rail-toggle ${layout.showSequence ? "active" : ""}" type="button" data-player-toggle="sequence" aria-pressed="${layout.showSequence}"><i data-lucide="panel-left"></i><span>Lesson Parts</span></button>
        <button class="small-button rail-toggle ${layout.showTools ? "active" : ""}" type="button" data-player-toggle="tools" aria-pressed="${layout.showTools}"><i data-lucide="panel-right"></i><span>Study Tools</span></button>
        <button class="small-button lesson-exit-button" type="button" data-player-back><i data-lucide="${preview ? "x" : "chevron-left"}"></i><span>${preview ? "Close Preview" : "Back to Lessons"}</span></button>
      </div>
    </div>
    <div class="lesson-course-progress" aria-label="Current module position">
      <span style="width:${partPercent}%"></span>
    </div>
    <div class="${gridClasses}">
      ${layout.showSequence ? `
      <aside class="lesson-sequence-card">
        ${preview ? `<div class="lesson-panel-label preview-label"><i data-lucide="monitor-check"></i> Student-style preview</div>` : ""}
        <div class="lesson-module-title">
          <span class="lesson-type-icon ${escapeHtml(moduleCategoryClass(module.category))}"><i data-lucide="${escapeHtml(iconForLesson(lesson, module))}"></i></span>
          <div><strong>${escapeHtml(module.title || "Module")}</strong><small>${escapeHtml(module.category || "TechWise 360")}</small></div>
        </div>
        <div class="lesson-sequence-top"><strong>Lessons</strong><span>${completedCount} of ${moduleLessons.length} Complete</span></div>
        <div class="lesson-sequence-list">
          ${moduleLessons.map((item, index) => {
            const itemProgress = progressByLesson.get(item.id);
            const isComplete = isStudentLessonComplete(item, progressByLesson);
            const isActive = item.id === lesson.id;
            const gate = preview ? { locked: false, reason: "" } : getStudentLessonGate(item.id);
            return `
              <button class="${isActive ? "active" : ""} ${isComplete ? "complete" : ""} ${gate.locked ? "locked" : ""}" type="button" data-player-open-lesson="${escapeHtml(item.id)}" data-locked="${gate.locked ? "true" : "false"}" data-lock-reason="${escapeAttribute(gate.reason)}" ${gate.locked ? "disabled" : ""}>
                <b>${index + 1}</b>
                <span><i data-lucide="${gate.locked ? "lock" : ["practice", "assessment"].includes(item.lesson_type) ? "flag" : "book-open"}"></i></span>
                <div class="lesson-sequence-copy">
                  <strong><small>${item.lesson_type === "assessment" ? "Assessment" : item.lesson_type === "practice" ? "Practice" : "Lesson"}</small>${escapeHtml(item.title)}</strong>
                  <em>${gate.locked ? "Locked" : isComplete ? "Complete" : isActive ? "In Progress" : "Not Started"}</em>
                </div>
              </button>
            `;
          }).join("")}
        </div>
      </aside>
      ` : ""}
      <main class="lesson-content-card">
        ${view === "practice" ? renderPracticePanel(questions, feedback, preview, checkIndex, checkLessons.length, lessonLabel) : renderLessonContentPanel(lesson, contentSections, detail.files || [], preview)}
      </main>
      ${layout.showTools ? `
      <aside class="lesson-tips-rail">
        <section class="lesson-progress-card">
          <h2>Lesson Progress</h2>
          <div class="lesson-ring" style="--progress:${progressPercent * 3.6}deg"><strong>${progressPercent}%</strong><span>Complete</span></div>
          <div class="lesson-progress-facts">
            <div><strong>${completedCount}/${moduleLessons.length}</strong><span>Parts Completed</span></div>
            <div><strong>${Number(lesson.duration_minutes || 0)} min</strong><span>This Part</span></div>
            <div><strong>${totalDuration} min</strong><span>Total Path</span></div>
          </div>
        </section>
        <section class="lesson-focus-card">
          <h2><i data-lucide="${["practice", "assessment"].includes(lesson.lesson_type) ? "target" : "compass"}"></i> Current Focus</h2>
          <p>${escapeHtml(lesson.description || "Read the lesson, review the examples, then complete the connected practice.")}</p>
        </section>
        <section class="lesson-quick-tips">
          <h2><i data-lucide="lightbulb"></i> Quick Tips</h2>
          ${quickTips.length ? quickTips.map((tip) => `
            <article>
              <span><i data-lucide="sparkles"></i></span>
              <p>${escapeHtml(tip.body || tip.title || "Review the key points before answering practice questions.")}</p>
            </article>
          `).join("") : `<p class="empty-text">No quick tips yet.</p>`}
        </section>
      </aside>
      ` : ""}
    </div>
    ${previousLesson || nextLesson ? `
      <div class="lesson-player-actions ${previousLesson && nextLesson ? "" : "single-action"}">
        ${previousLesson ? `<button class="small-button" type="button" data-player-open-lesson="${escapeHtml(previousLesson.id)}" data-player-nav="prev"><i data-lucide="arrow-left"></i><span>Previous Lesson</span></button>` : ""}
        ${nextLesson ? `<button class="primary-button next-lesson-button" type="button" data-player-open-lesson="${escapeHtml(nextLesson.id)}" data-player-nav="next" data-locked="${!canOpenNext ? "true" : "false"}" data-lock-reason="${escapeAttribute(nextGate.reason)}" ${canOpenNext ? "" : "disabled"}><span>${escapeHtml(nextActionLabel)}</span><i data-lucide="${canOpenNext ? "arrow-right" : "lock"}"></i></button>` : ""}
      </div>
    ` : ""}
  `;
  if (window.lucide) window.lucide.createIcons();
}

function defaultLessonLayout() {
  const compactViewport = typeof window !== "undefined" && window.matchMedia?.("(max-width: 980px)").matches;
  return {
    showSequence: !compactViewport,
    showTools: !compactViewport
  };
}

function normalizeLessonLayout(layout) {
  const defaults = defaultLessonLayout();
  return {
    showSequence: typeof layout?.showSequence === "boolean" ? layout.showSequence : defaults.showSequence,
    showTools: typeof layout?.showTools === "boolean" ? layout.showTools : defaults.showTools
  };
}

function renderLessonContentPanel(lesson, sections, files, preview = false) {
  const introSections = sections.filter((section) => ["paragraph", "key_points"].includes(section.section_type));
  const learningSections = sections.filter((section) => !["paragraph", "key_points"].includes(section.section_type));
  const orderedSections = [...introSections, ...learningSections];
  return `
    <div class="lesson-content-header">
      <span>Guided Lesson</span>
      <h2>${escapeHtml(lesson.title)}</h2>
      <p>${escapeHtml(lesson.description || "Read the lesson content, review examples, then complete the practice.")}</p>
    </div>
    <div class="lesson-content-blocks">
      ${orderedSections.length ? orderedSections.map(renderLessonSectionBlock).join("") : `
        <article class="lesson-content-block paragraph">
          <h3>Lesson Content</h3>
          <p>${escapeHtml(lesson.description || "Your teacher can add custom content blocks for this lesson.")}</p>
        </article>
      `}
    </div>
    <div class="lesson-resource-row">
      ${files.length ? `<button class="small-button" type="button" data-player-resource="${escapeHtml(lesson.id)}" ${preview && files[0]?.id === "preview-file" ? "disabled" : ""}><i data-lucide="file-text"></i><span>${preview && files[0]?.id === "preview-file" ? `Attached: ${escapeHtml(files[0].original_filename)}` : "Open Attached Resource"}</span></button>` : ""}
      ${lesson.resource_url ? `<a class="small-button" href="${escapeAttribute(lesson.resource_url)}" target="_blank" rel="noopener"><i data-lucide="external-link"></i><span>Open Link</span></a>` : ""}
    </div>
  `;
}

function renderLessonSectionBlock(section) {
  const icon = section.section_type === "key_points" ? "list-checks"
    : section.section_type === "example" ? "badge-check"
      : section.section_type === "resource" ? "file-text"
        : section.section_type === "steps" ? "list-ordered"
          : section.section_type === "safety_note" ? "shield-alert"
            : section.section_type === "vocabulary" ? "boxes"
              : section.section_type === "summary" ? "clipboard-check"
                : section.section_type === "reflection" ? "message-square-text"
                  : "book-open";
  return `
    <article class="lesson-content-block ${escapeHtml(section.section_type)}">
      <h3><i data-lucide="${icon}"></i>${escapeHtml(section.title || titleCase(section.section_type.replace("_", " ")))}</h3>
      ${section.body ? renderSectionBody(section) : ""}
      ${section.media_url ? renderSectionMedia(section.media_url, section.title) : ""}
    </article>
  `;
}

function renderSectionMedia(mediaUrl, title = "") {
  const cleanUrl = String(mediaUrl || "").trim();
  if (!cleanUrl) return "";
  const isImage = /\.(png|jpe?g|webp|gif|svg)(\?.*)?$/i.test(cleanUrl);
  if (isImage) {
    return `
      <figure class="lesson-section-media">
        <img src="${escapeAttribute(cleanUrl)}" alt="${escapeAttribute(title || "Lesson visual")}">
      </figure>
    `;
  }
  return `<a href="${escapeAttribute(cleanUrl)}" target="_blank" rel="noopener">${escapeHtml(cleanUrl)}</a>`;
}

function renderSectionBody(section) {
  const body = String(section.body || "").replace(/\\n/g, "\n");
  const lines = body.split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
  if (!lines.length) return "";
  if (["steps", "key_points", "vocabulary"].includes(section.section_type)) {
    return `<ol class="lesson-block-list ${escapeHtml(section.section_type)}">
      ${lines.map((line) => {
        const cleanLine = line.replace(/^\d+[\.)]\s*/, "").replace(/^[-*]\s*/, "");
        return `<li>${escapeHtml(cleanLine)}</li>`;
      }).join("")}
    </ol>`;
  }
  if (section.section_type === "summary") {
    return `<ul class="lesson-block-list summary">${lines.map((line) => `<li>${escapeHtml(line.replace(/^[-*]\s*/, ""))}</li>`).join("")}</ul>`;
  }
  return lines.map((line) => `<p>${escapeHtml(line)}</p>`).join("");
}

function renderPracticePanel(questions, feedback, preview, practiceIndex = 0, practiceTotal = 0, checkLabel = "Practice") {
  if (!questions.length) {
    return `<div class="lesson-content-header"><span>${escapeHtml(checkLabel)}</span><h2>No questions yet</h2><p>Your teacher can add guided questions from the lesson editor.</p></div>`;
  }
  const feedbackByQuestion = new Map((feedback || []).map((item) => [item.question_id, item]));
  return `
    <form id="lessonPracticeForm" class="practice-panel">
      <div class="lesson-content-header">
        <span>${practiceIndex ? `${escapeHtml(checkLabel)} ${practiceIndex} of ${practiceTotal}` : `${escapeHtml(checkLabel)} ${feedback ? "Completed" : "In Progress"}`}</span>
        <h2>Check Your Understanding</h2>
        <p>Work through each checkpoint. Use the hint when you need a clue, then check your answers for feedback.</p>
      </div>
      ${questions.map((question, index) => {
        const itemFeedback = feedbackByQuestion.get(question.id);
        return `
          <article class="practice-question ${itemFeedback ? itemFeedback.is_correct ? "correct" : "incorrect" : ""}" data-practice-question="${escapeAttribute(question.id)}">
            <div class="practice-task-title">
              <i data-lucide="target"></i>
              <div><strong>Task ${index + 1}</strong><span>${escapeHtml(question.prompt)}</span></div>
            </div>
            ${renderQuestionInput(question, Boolean(feedback) && !preview)}
            ${question.hint ? `<small class="practice-hint"><i data-lucide="lightbulb"></i>${escapeHtml(question.hint)}</small>` : ""}
            ${itemFeedback ? `<div class="practice-feedback"><strong>${itemFeedback.is_correct ? "Correct!" : "Review this one"}</strong><span>${escapeHtml(itemFeedback.explanation || `Correct answer: ${itemFeedback.correct_answer}`)}</span></div>` : ""}
          </article>
        `;
      }).join("")}
      <button class="primary-button" type="button" ${preview ? "data-submit-preview-practice" : "data-submit-practice"} ${feedback && !preview ? "disabled" : ""}><i data-lucide="check-circle-2"></i><span>${preview ? "Check Preview Answers" : feedback ? "Submitted" : `Submit ${escapeHtml(checkLabel)}`}</span></button>
    </form>
  `;
}

function renderQuestionInput(question, disabled) {
  if (["short_answer", "identification"].includes(question.question_type)) {
    return `<input class="practice-answer-input" data-practice-answer="${escapeHtml(question.id)}" type="text" placeholder="${question.question_type === "identification" ? "Identify the part or connector" : "Type your answer"}" ${disabled ? "disabled" : ""}>`;
  }
  if (question.question_type === "ordering") {
    const choices = question.choices || [];
    return `
      <p class="practice-order-help">Use the item numbers below and type the correct sequence separated by commas.</p>
      <div class="practice-order-list">
        ${choices.map((choice, index) => `<span><b>${index + 1}</b>${escapeHtml(choice)}</span>`).join("")}
      </div>
      <input class="practice-answer-input" data-practice-answer="${escapeHtml(question.id)}" type="text" placeholder="Enter the correct order, e.g. 2,4,1,3" ${disabled ? "disabled" : ""}>
    `;
  }
  const choices = question.question_type === "true_false" ? ["True", "False"] : question.choices || [];
  return `<div class="practice-choice-grid">
    ${choices.map((choice) => `
      <label><input data-practice-answer="${escapeHtml(question.id)}" type="radio" name="answer-${escapeHtml(question.id)}" value="${escapeAttribute(choice)}" ${disabled ? "disabled" : ""}><span>${escapeHtml(choice)}</span></label>
    `).join("")}
  </div>`;
}

async function handleLessonPlayerAction(event) {
  const back = event.target.closest("[data-player-back]");
  const toggleButton = event.target.closest("[data-player-toggle]");
  const viewButton = event.target.closest("[data-player-view]");
  const openLessonButton = event.target.closest("[data-player-open-lesson]");
  const resourceButton = event.target.closest("[data-player-resource]");
  const submitButton = event.target.closest("[data-submit-practice]");
  const submitPreviewButton = event.target.closest("[data-submit-preview-practice]");
  const isStudentPlayer = event.currentTarget.id === "studentLessonPlayer";
  const playerState = isStudentPlayer ? studentState.lessonPlayer : null;
  const detail = isStudentPlayer ? playerState?.detail : null;

  if (back) {
    if (isStudentPlayer) {
      renderStudentDashboard();
      showStudentView("lessons");
    } else {
      closeLessonPreview();
    }
    return;
  }
  if (toggleButton) {
    const key = toggleButton.dataset.playerToggle === "tools" ? "showTools" : "showSequence";
    if (isStudentPlayer && playerState) {
      const currentLayout = normalizeLessonLayout(playerState.layout);
      studentState.lessonPlayer = {
        ...playerState,
        layout: {
          ...currentLayout,
          [key]: !currentLayout[key]
        }
      };
      renderStudentLessonPlayer();
    } else if (!isStudentPlayer && teacherState.previewLesson) {
      const currentLayout = normalizeLessonLayout(teacherState.previewLayout);
      teacherState.previewLayout = {
        ...currentLayout,
        [key]: !currentLayout[key]
      };
      const activeView = event.currentTarget.querySelector("[data-player-view].active")?.dataset.playerView
        || resolveLessonPlayerView(teacherState.previewLesson.lesson, "auto");
      renderLessonPlayer(event.currentTarget, teacherState.previewLesson, {
        preview: true,
        view: activeView,
        layout: teacherState.previewLayout
      });
    }
    return;
  }
  if (viewButton && isStudentPlayer && playerState) {
    studentState.lessonPlayer = { ...playerState, view: viewButton.dataset.playerView };
    renderStudentLessonPlayer();
    return;
  }
  if (viewButton && !isStudentPlayer && teacherState.previewLesson) {
    renderLessonPlayer(event.currentTarget, teacherState.previewLesson, {
      preview: true,
      view: viewButton.dataset.playerView,
      layout: teacherState.previewLayout
    });
    return;
  }
  if (openLessonButton?.dataset.playerOpenLesson) {
    if (openLessonButton.disabled || openLessonButton.dataset.locked === "true") {
      showLockedLessonMessage(openLessonButton.dataset.lockReason);
      return;
    }
    if (isStudentPlayer) {
      if (openLessonButton.dataset.playerNav === "next" && !["practice", "assessment"].includes(detail?.lesson?.lesson_type)) {
        try {
          await completeStudentLessonPart(detail.lesson.id);
        } catch (error) {
          return;
        }
      }
      await openStudentLesson(openLessonButton.dataset.playerOpenLesson, "auto");
    } else {
      await previewLesson(openLessonButton.dataset.playerOpenLesson);
    }
    return;
  }
  if (resourceButton) {
    await openLessonResource(resourceButton.dataset.playerResource);
    return;
  }
  if (submitPreviewButton && !isStudentPlayer && teacherState.previewLesson) {
    submitPreviewPractice(event.currentTarget);
    return;
  }
  if (submitButton && isStudentPlayer && detail) {
    await submitPracticeAnswers(detail.lesson.id, submitButton);
  }
}

async function openLessonResource(lessonId) {
  try {
    const result = await apiGet(`/api/lesson-file-url?lesson_id=${encodeURIComponent(lessonId)}`);
    window.open(result.signed_url, "_blank", "noopener");
  } catch (error) {
    const lesson = studentState.lessonPlayer?.detail?.lesson || teacherState.lessons.find((item) => item.id === lessonId);
    if (lesson?.resource_url) {
      window.open(lesson.resource_url, "_blank", "noopener");
      return;
    }
    setMessage(document.getElementById("studentMessage") || document.getElementById("teacherMessage"), error.message, "error");
  }
}

function submitPreviewPractice(container) {
  const detail = teacherState.previewLesson;
  if (!detail) return;
  const answers = collectPracticeAnswers(container);
  const questions = detail.questions || [];
  const unanswered = getUnansweredPracticeQuestions(questions, answers);
  markPracticeValidation(container, unanswered);
  if (unanswered.length) {
    showToast({
      title: "Answer all questions",
      message: `Please answer ${unanswered.length} missing item${unanswered.length === 1 ? "" : "s"} before checking this preview.`,
      type: "warning"
    });
    return;
  }
  const feedback = questions.map((question) => {
    const submitted = answers[question.id] || "";
    const isCorrect = isPreviewAnswerCorrect(question, submitted);
    return {
      question_id: question.id,
      prompt: question.prompt,
      submitted_answer: submitted,
      correct_answer: question.correct_answer,
      is_correct: isCorrect,
      points: isCorrect ? Number(question.points || 1) : 0,
      max_points: Number(question.points || 1),
      explanation: question.explanation || `Correct answer: ${question.correct_answer}`
    };
  });
  const earned = feedback.reduce((sum, item) => sum + item.points, 0);
  const total = feedback.reduce((sum, item) => sum + item.max_points, 0);
  const score = total ? Math.round((earned / total) * 100) : 0;
  teacherState.previewLesson = {
    ...detail,
    attempts: [{
      id: "preview-attempt",
      feedback,
      score_percent: score,
      correct_count: feedback.filter((item) => item.is_correct).length,
      total_points: total,
      submitted_at: new Date().toISOString()
    }]
  };
  renderLessonPlayer(container, teacherState.previewLesson, {
    preview: true,
    view: "practice",
    layout: teacherState.previewLayout
  });
  setMessage(document.getElementById("teacherMessage"), `Preview checked. Score: ${score}%`, "success");
}

function collectPracticeAnswers(container) {
  const answers = {};
  container.querySelectorAll("[data-practice-answer]").forEach((input) => {
    if (input.type === "radio" && !input.checked) return;
    answers[input.dataset.practiceAnswer] = input.value;
  });
  return answers;
}

function getUnansweredPracticeQuestions(questions, answers) {
  return (questions || []).filter((question) => !String(answers[question.id] || "").trim());
}

function markPracticeValidation(container, unansweredQuestions) {
  const missingIds = new Set(unansweredQuestions.map((question) => question.id));
  container.querySelectorAll("[data-practice-question]").forEach((card) => {
    const isMissing = missingIds.has(card.dataset.practiceQuestion);
    card.classList.toggle("needs-answer", isMissing);
    card.querySelector(".practice-required-note")?.remove();
    if (isMissing) {
      card.insertAdjacentHTML("beforeend", `<small class="practice-required-note"><i data-lucide="alert-circle"></i>Please answer this item before submitting.</small>`);
    }
  });
  if (unansweredQuestions.length) {
    const firstMissing = container.querySelector(`[data-practice-question="${CSS.escape(unansweredQuestions[0].id)}"]`);
    firstMissing?.scrollIntoView({ behavior: "smooth", block: "center" });
  }
  if (window.lucide) window.lucide.createIcons();
}

function isPreviewAnswerCorrect(question, answer) {
  const submitted = String(answer || "").trim().toLowerCase();
  const correct = String(question.correct_answer || "").trim().toLowerCase();
  if (["short_answer", "identification"].includes(question.question_type)) {
    return correct.split("|").map((item) => item.trim()).includes(submitted);
  }
  if (question.question_type === "ordering") {
    return normalizeOrderAnswer(submitted) === normalizeOrderAnswer(correct);
  }
  return submitted === correct;
}

function normalizeOrderAnswer(value) {
  return String(value || "")
    .toLowerCase()
    .replace(/\s+/g, "")
    .replace(/[>;|]/g, ",")
    .replace(/,+/g, ",")
    .replace(/^,|,$/g, "");
}

async function submitPracticeAnswers(lessonId, submitButton = null) {
  const form = document.getElementById("lessonPracticeForm");
  const message = document.getElementById("studentMessage");
  if (!form || !studentState.lessonPlayer) return;
  const answers = {};
  form.querySelectorAll("[data-practice-answer]").forEach((input) => {
    if (input.type === "radio" && !input.checked) return;
    answers[input.dataset.practiceAnswer] = input.value;
  });
  const detail = studentState.lessonPlayer.detail;
  const questions = detail.questions || [];
  const unansweredQuestions = getUnansweredPracticeQuestions(questions, answers);
  markPracticeValidation(form, unansweredQuestions);
  if (unansweredQuestions.length) {
    const missingLabel = `${unansweredQuestions.length} missing item${unansweredQuestions.length === 1 ? "" : "s"}`;
    setMessage(message, `Please answer all questions before submitting. You still have ${missingLabel}.`, "error");
    showToast({
      title: "Answer all questions",
      message: `You still have ${missingLabel}. Complete every item before submitting.`,
      type: "warning"
    });
    return;
  }
  const submissionLabel = detail.lesson.lesson_type === "assessment" ? "assessment" : "practice";
  const confirmed = await showConfirmModal({
    title: `Submit ${submissionLabel}?`,
    message: "Your answers will be checked, recorded, and used to open the next part when complete.",
    tone: "primary",
    icon: "check-circle-2",
    confirmText: "Submit Answers",
    cancelText: "Review First"
  });
  if (!confirmed) return;
  try {
    setButtonLoading(submitButton, true, "Submitting...");
    setMessage(message, "Checking your answers...");
    const result = await apiPost("/api/student/lessons", {
      lesson_id: lessonId,
      answers,
      time_spent_seconds: Number(detail.lesson.duration_minutes || 0) * 60
    });
    mergeStudentProgress(result.progress);
    mergeStudentAttempt(result.attempt);
    syncStudentCompletionSummaryProgress();
    studentState.lessonPlayer = {
      ...studentState.lessonPlayer,
      feedback: result.feedback,
      detail: {
        ...studentState.lessonPlayer.detail,
        progress: result.progress,
        attempts: [result.attempt, ...(studentState.lessonPlayer.detail.attempts || [])],
        all_progress: studentState.progress
      }
    };
    renderStudentDashboard();
    showStudentView("lesson-player");
    renderStudentLessonPlayer();
    const submittedLabel = studentState.lessonPlayer.detail.lesson.lesson_type === "assessment" ? "Assessment" : "Practice";
    setMessage(message, `${submittedLabel} submitted. Score: ${result.attempt.score_percent}%`, "success");
  } catch (error) {
    setButtonLoading(submitButton, false);
    setMessage(message, error.message, "error");
  }
}

async function completeStudentLessonPart(lessonId) {
  if (!lessonId) return;
  const message = document.getElementById("studentMessage");
  try {
    const result = await apiPost("/api/student/lessons", {
      lesson_id: lessonId,
      action: "complete_lesson"
    });
    mergeStudentProgress(result.progress);
    syncStudentCompletionSummaryProgress();
    if (studentState.lessonPlayer?.detail) {
      studentState.lessonPlayer.detail.progress = result.progress;
      studentState.lessonPlayer.detail.all_progress = studentState.progress;
    }
  } catch (error) {
    setMessage(message, error.message, "error");
    throw error;
  }
}

function mergeStudentProgress(progress) {
  if (!progress) return;
  const index = studentState.progress.findIndex((item) => item.lesson_id === progress.lesson_id);
  if (index >= 0) {
    studentState.progress[index] = progress;
  } else {
    studentState.progress.push(progress);
  }
}

function syncStudentCompletionSummaryProgress() {
  studentState.completionSummary = {
    ...(studentState.completionSummary || {}),
    modules: studentState.completionSummary?.modules?.length ? studentState.completionSummary.modules : studentState.modules,
    lessons: studentState.completionSummary?.lessons?.length ? studentState.completionSummary.lessons : studentState.lessons,
    progress: studentState.progress
  };
}

function mergeStudentAttempt(attempt) {
  if (!attempt) return;
  const index = studentState.attempts.findIndex((item) => item.id === attempt.id);
  if (index >= 0) {
    studentState.attempts[index] = attempt;
  } else {
    studentState.attempts.unshift(attempt);
  }
  studentState.attempts.sort((a, b) => new Date(b.submitted_at || 0) - new Date(a.submitted_at || 0));
}

function latestAttemptFeedback(attempts = []) {
  const feedback = normalizeAttemptFeedback(attempts[0]?.feedback);
  return feedback.length ? feedback : null;
}

function normalizeAttemptFeedback(feedback) {
  if (!feedback) return [];
  if (Array.isArray(feedback)) return feedback;
  if (typeof feedback === "string") {
    try {
      const parsed = JSON.parse(feedback);
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }
  return [];
}

function renderStudentLeaderboardDashboard() {
  const container = document.getElementById("studentLeaderboardDashboard");
  if (!container) return;
  const data = studentState.leaderboard || { rows: [], summary: {} };
  const rows = data.rows || [];
  const summary = data.summary || {};
  container.innerHTML = `
    <div class="dashboard-page-header">
      <div>
        <span class="home-eyebrow"><i data-lucide="bar-chart-3"></i> VR Performance</span>
        <h1>Class VR Leaderboard</h1>
        <p>See assembly and disassembly records from real VR attempts in your class.</p>
      </div>
    </div>
    <div class="metric-grid">
      <article class="stat-card"><span class="stat-icon gold"><i data-lucide="bar-chart-3"></i></span><div><p>Your Result</p><strong>${summary.student_rank ? `#${summary.student_rank}` : "-"}</strong><small>Class position</small></div></article>
      <article class="stat-card"><span class="stat-icon blue"><i data-lucide="bar-chart-3"></i></span><div><p>Highest Score</p><strong>${formatProgressNumber(summary.top_score || 0)}%</strong><small>Best class attempt</small></div></article>
      <article class="stat-card"><span class="stat-icon green"><i data-lucide="users"></i></span><div><p>Students Recorded</p><strong>${summary.participants_ranked || 0}</strong><small>With VR attempts</small></div></article>
    </div>
    <section class="panel-card">
      <div class="leaderboard-table-wrap">
        <table class="leaderboard-table">
          <thead><tr><th>Rank</th><th>Student</th><th>Simulation</th><th>Score</th><th>Time</th><th>Mistakes</th><th>Last Attempt</th></tr></thead>
          <tbody>
            ${rows.length ? rows.map(renderStudentLeaderboardRow).join("") : `<tr><td colspan="7" class="empty-table-cell leaderboard-empty-cell"><strong>No VR attempts recorded for your class yet.</strong><span>The class leaderboard will show ranked VR attempts after students submit assembly or disassembly records.</span></td></tr>`}
          </tbody>
        </table>
      </div>
    </section>
  `;
}

function renderStudentLeaderboardRow(row) {
  const student = row.student || {};
  const attempt = row.attempt || {};
  return `
    <tr class="${row.is_current_student ? "current-student-row" : ""}">
      <td><span class="rank-badge rank-${Math.min(Number(row.rank || 0), 3)}">${row.rank}</span></td>
      <td><strong>${escapeHtml(student.full_name || student.username || "Student")}</strong><small>${row.is_current_student ? "You" : escapeHtml(student.section || "")}</small></td>
      <td>${escapeHtml(titleCase(attempt.simulation_type || "VR"))}${row.competition?.title ? `<small>${escapeHtml(row.competition.title)}</small>` : ""}</td>
      <td><strong>${formatProgressNumber(attempt.score_percent || 0)}%</strong></td>
      <td>${formatSeconds(attempt.duration_seconds || 0)}</td>
      <td>${Number(attempt.mistakes || 0)}</td>
      <td>${escapeHtml(formatDate(attempt.completed_at))}</td>
    </tr>
  `;
}

function renderStudentAchievementsDashboard() {
  const container = document.getElementById("studentAchievementsDashboard");
  if (!container) return;
  const summary = getStudentDashboardSummary();
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));
  const modulesById = new Map(studentState.modules.map((module) => [module.id, module]));
  const topicMastery = getStudentTopicMastery(modulesById, progressByLesson);
  const recommendation = getStudentRecommendation(topicMastery, progressByLesson);
  const milestones = getStudentAchievementMilestones(summary);
  const rewards = getStudentRecentRewards();

  container.innerHTML = `
    <div class="student-achievements-layout">
      <div class="student-achievements-main">
        <div class="section-heading achievements-heading">
          <div>
            <h1>Achievements</h1>
            <p>Review your awards, certificates, and ICT learning progress.</p>
          </div>
          <div class="achievement-tabs" role="tablist" aria-label="Achievement sections">
            <button class="${studentState.achievementTab === "badges" ? "active" : ""}" type="button" data-achievement-tab="badges">Badges</button>
            <button class="${studentState.achievementTab === "certificates" ? "active" : ""}" type="button" data-achievement-tab="certificates">Certificates</button>
          </div>
        </div>
        ${studentState.achievementTab === "certificates"
          ? renderStudentCertificateGallery()
          : renderStudentBadgeGallery(milestones)}
        <div class="student-achievement-bottom">
          ${renderStudentMilestoneTracker(milestones)}
          ${renderStudentAchievementMission(recommendation)}
        </div>
      </div>
      <aside class="student-achievements-side">
        ${renderStudentAchievementSummary(summary)}
        ${renderStudentRecentRewards(rewards)}
      </aside>
    </div>
  `;
  if (window.lucide) window.lucide.createIcons();
}

function renderStudentBadgeGallery(milestones) {
  const earned = studentState.badges.slice(0, 5);
  const locked = milestones.filter((item) => !item.done).slice(0, Math.max(0, 5 - earned.length));
  const cards = [
    ...earned.map((badge) => renderStudentBadgeCard({
      title: formatAwardDisplayTitle(badge.title, "Badge"),
      detail: `Awarded ${formatDateOnly(badge.awarded_at)}`,
      icon: badgeIconForTitle(formatAwardDisplayTitle(badge.title, "Badge")),
      color: badge.color || "blue",
      status: "Awarded",
      done: true
    })),
    ...locked.map((milestone) => renderStudentBadgeCard({
      title: milestone.title,
      detail: milestone.detail,
      icon: milestone.icon,
      color: milestone.color,
      status: `${milestone.current}/${milestone.target}`,
      done: false,
      percent: percentOf(Math.min(milestone.current, milestone.target), milestone.target)
    }))
  ];

  return `
    <section class="panel-card student-top-badges">
      <div class="student-card-title-row">
        <h2>Your Badges</h2>
        <button class="link-button" type="button" data-achievement-tab="badges">View All Badges</button>
      </div>
      <div class="student-badge-gallery">
        ${cards.length ? cards.join("") : `<p class="empty-text">Complete lessons and practices to receive your first badge.</p>`}
      </div>
    </section>
  `;
}

function renderStudentBadgeCard(card) {
  return `
    <article class="student-badge-card ${card.done ? "earned" : "locked"}">
      <div class="student-badge-shape ${escapeAttribute(card.color || "blue")}">
        <i data-lucide="${escapeAttribute(card.done ? card.icon : "lock")}"></i>
      </div>
      <strong>${escapeHtml(card.title)}</strong>
      <span>${escapeHtml(card.detail)}</span>
      ${card.done
        ? `<small class="earned"><i data-lucide="check-circle-2"></i>Awarded</small>`
        : `<div class="student-badge-progress"><b>${escapeHtml(card.status)}</b><div class="wide-progress"><span style="width:${card.percent || 0}%"></span></div></div>`}
    </article>
  `;
}

function renderStudentCertificateGallery() {
  const rows = studentState.certificates.length
    ? studentState.certificates.map((certificate) => `
      <article class="student-certificate-card">
        <i data-lucide="file-badge"></i>
        <div>
          <strong>${escapeHtml(certificate.title || "Certificate")}</strong>
          <span>${escapeHtml(certificate.achievement_name || "Achievement")} - Issued ${escapeHtml(formatDateOnly(certificate.issue_date || certificate.awarded_at))}</span>
        </div>
        <div class="student-certificate-actions">
          <button type="button" data-view-student-certificate="${escapeAttribute(certificate.id)}"><i data-lucide="eye"></i><span>View</span></button>
          <button type="button" data-print-student-certificate="${escapeAttribute(certificate.id)}"><i data-lucide="printer"></i><span>Print</span></button>
        </div>
      </article>
    `).join("")
    : `
      <article class="student-certificate-card locked">
        <i data-lucide="file-badge"></i>
        <div>
          <strong>No certificates yet</strong>
          <span>Complete teacher-assigned requirements to qualify for certificates.</span>
        </div>
      </article>
    `;

  return `
    <section class="panel-card student-top-badges">
      <div class="student-card-title-row">
        <h2>Your Certificates</h2>
        <button class="link-button" type="button" data-achievement-tab="certificates">View All</button>
      </div>
      <div class="student-certificate-list">${rows}</div>
    </section>
  `;
}

function renderStudentAchievementSummary(summary) {
  return `
    <section class="panel-card student-achievement-summary">
      <article><i data-lucide="award" class="blue"></i><strong>${summary.badgesEarned}</strong><span>Badges</span></article>
      <article><i data-lucide="file-badge" class="gray"></i><strong>${summary.certificatesEarned}</strong><span>Certificates</span></article>
      <article><i data-lucide="trophy" class="gold"></i><strong>${summary.overall}%</strong><span>Complete</span></article>
    </section>
  `;
}

function renderStudentMilestoneTracker(milestones) {
  return `
    <section class="panel-card student-milestone-card">
      <h2>Progress Requirements</h2>
      <div class="student-milestone-list">
        ${milestones.slice(0, 3).map((milestone, index) => {
          const percent = percentOf(Math.min(milestone.current, milestone.target), milestone.target);
          return `
            <article class="${milestone.done ? "done" : ""}">
              <span>${index + 1}</span>
              <div>
                <strong>${escapeHtml(milestone.title)}</strong>
                <p>${escapeHtml(milestone.done ? "Completed" : milestone.detail)}</p>
                ${milestone.done ? "" : `<div class="wide-progress"><span style="width:${percent}%"></span></div>`}
              </div>
              <i data-lucide="${milestone.done ? "check-circle-2" : "circle"}"></i>
            </article>
          `;
        }).join("")}
      </div>
    </section>
  `;
}

function renderStudentAchievementMission(recommendation) {
  if (!recommendation?.lesson) {
    return `
      <section class="panel-card student-achievement-mission">
        <div class="panel-title-row"><h2>Learning Task</h2><span class="soft-pill"><i data-lucide="check-circle-2"></i> Done</span></div>
        <p class="empty-text">All visible lessons are complete. Keep an eye out for new teacher assignments.</p>
      </section>
    `;
  }
  const { lesson, module, progress } = recommendation;
  const percent = Number(progress?.progress_percent || 0);
  const difficulty = getStudentLessonDifficulty(lesson);
  return `
    <section class="panel-card student-achievement-mission">
      <div class="panel-title-row"><h2>Learning Task</h2><span class="soft-pill"><i data-lucide="clock"></i> Active</span></div>
      <div class="achievement-mission-body">
        <i data-lucide="${escapeAttribute(iconForLesson(lesson, module))}" class="${escapeAttribute(moduleCategoryClass(module?.category || module?.title || ""))}"></i>
        <div>
          <strong>${escapeHtml(lesson.title)}</strong>
          <p>${escapeHtml(difficulty.label)} - ${escapeHtml(module?.title || "Module")}</p>
        </div>
      </div>
      <b>${percent}% completed</b>
      <div class="wide-progress"><span style="width:${percent}%"></span></div>
      <button class="primary-button" type="button" data-open-student-lesson="${escapeAttribute(lesson.id)}">${percent ? "Continue" : "Start Lesson"}</button>
    </section>
  `;
}

function renderStudentRecentRewards(rewards) {
  const rows = rewards.length
    ? rewards.slice(0, 4).map((reward) => `
      <article class="student-reward-row">
        <i data-lucide="${escapeAttribute(reward.icon)}" class="${escapeAttribute(reward.color)}"></i>
        <div>
          <strong>${escapeHtml(reward.title)}</strong>
          <span>${escapeHtml(reward.detail)}</span>
        </div>
        <time>${escapeHtml(formatRewardTime(reward.awarded_at))}</time>
      </article>
    `).join("")
    : `<p class="empty-text">Awarded badges and certificates will appear here.</p>`;
  return `
    <section class="panel-card student-recent-rewards">
      <div class="student-card-title-row">
        <h2>Recent Awards</h2>
        <button class="link-button" type="button" data-achievement-tab="badges">View All</button>
      </div>
      <div>${rows}</div>
    </section>
  `;
}

function getStudentAchievementMilestones(summary) {
  const latestAttempts = getLatestAttemptsByLesson();
  const attemptCount = latestAttempts.length;
  const scoredHundred = latestAttempts.filter((attempt) => Number(attempt.score_percent || 0) >= 100).length;
  return [
    {
      title: "Complete 1 Quiz",
      detail: "First practice record",
      icon: "puzzle",
      color: "blue",
      current: Math.min(attemptCount, 1),
      target: 1,
      done: attemptCount >= 1
    },
    {
      title: "Complete 5 Lessons",
      detail: "Learning progress",
      icon: "book-open",
      color: "gold",
      current: summary.completed,
      target: 5,
      done: summary.completed >= 5
    },
    {
      title: "Receive 1 Certificate",
      detail: "Certificate requirement",
      icon: "file-badge",
      color: "gray",
      current: summary.certificatesEarned,
      target: 1,
      done: summary.certificatesEarned >= 1
    },
    {
      title: "Full Score",
      detail: "Score 100% on a practice",
      icon: "target",
      color: "teal",
      current: Math.min(scoredHundred, 1),
      target: 1,
      done: scoredHundred >= 1
    },
    {
      title: "Lesson Progress",
      detail: "Complete 3 lessons",
      icon: "zap",
      color: "purple",
      current: Math.min(summary.completed, 3),
      target: 3,
      done: summary.completed >= 3
    }
  ];
}

function getStudentRecentRewards() {
  return [
    ...studentState.badges.map((badge) => ({
      title: `Badge Awarded: ${formatAwardDisplayTitle(badge.title, "Badge")}`,
      detail: "Award recorded",
      awarded_at: badge.awarded_at,
      icon: badgeIconForTitle(formatAwardDisplayTitle(badge.title, "Badge")),
      color: badge.color || "blue"
    })),
    ...studentState.certificates.map((certificate) => ({
      title: `Certificate Issued: ${certificate.title}`,
      detail: "Certificate issued",
      awarded_at: certificate.awarded_at,
      icon: "file-badge",
      color: "gray"
    }))
  ].sort((a, b) => new Date(b.awarded_at || 0) - new Date(a.awarded_at || 0));
}

function badgeIconForTitle(title = "") {
  const value = title.toLowerCase();
  if (value.includes("perfect") || value.includes("score")) return "target";
  if (value.includes("quick") || value.includes("starter")) return "book-open";
  if (value.includes("productivity")) return "trending-up";
  if (value.includes("safety") || value.includes("aware")) return "shield-check";
  if (value.includes("cable") || value.includes("network")) return "globe-2";
  if (value.includes("hardware") || value.includes("assembly")) return "settings";
  return "award";
}

function formatRewardTime(value) {
  if (!value) return "-";
  const date = startOfLocalDay(toDateInputValue(value));
  const today = startOfLocalDay(toDateInputValue(new Date()));
  const days = Math.round((today - date) / 86400000);
  if (days <= 0) return "Today";
  if (days === 1) return "Yesterday";
  if (days < 7) return `${days} days ago`;
  return formatDateOnly(value);
}

async function handleStudentAchievementAction(event) {
  const tabButton = event.target.closest("[data-achievement-tab]");
  if (tabButton) {
    studentState.achievementTab = tabButton.dataset.achievementTab || "badges";
    renderStudentAchievementsDashboard();
    return;
  }
  const jumpButton = event.target.closest("[data-jump-student]");
  if (jumpButton) {
    showStudentView(jumpButton.dataset.jumpStudent);
    return;
  }
  const lessonButton = event.target.closest("[data-open-student-lesson]");
  if (lessonButton) {
    await openStudentLesson(lessonButton.dataset.openStudentLesson, "auto");
    return;
  }
  const viewCertificate = event.target.closest("[data-view-student-certificate]");
  if (viewCertificate) {
    openCertificateViewer(viewCertificate.dataset.viewStudentCertificate);
    return;
  }
  const printCertificateButton = event.target.closest("[data-print-student-certificate]");
  if (printCertificateButton) {
    openCertificateViewer(printCertificateButton.dataset.printStudentCertificate, { printAfterOpen: true });
  }
}

function renderStudentProgressDashboard() {
  const container = document.getElementById("studentProgressDashboard");
  if (!container) return;
  const summary = getStudentDashboardSummary();
  const modulesById = new Map(studentState.modules.map((module) => [module.id, module]));
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));
  const range = getStudentProgressDateRange();
  const rangeLabel = `${formatDateOnly(range.startDate)} - ${formatDateOnly(range.endDate)}`;
  setText("studentProgressRange", rangeLabel);

  if (!studentState.lessons.length) {
    container.innerHTML = `
      <section class="panel-card empty-progress-state">
        <i data-lucide="bar-chart-3"></i>
        <h2>No progress yet</h2>
        <p>Your teacher's published lessons will appear here once they are available for your grade and active term.</p>
      </section>
    `;
    if (window.lucide) window.lucide.createIcons();
    return;
  }

  const topicMastery = getStudentTopicMastery(modulesById, progressByLesson);
  const recentScores = getLatestAttemptsByLesson()
    .sort((a, b) => new Date(b.submitted_at || 0) - new Date(a.submitted_at || 0))
    .slice(0, 4);
  const weeklyActivity = getStudentWeeklyActivity(range, modulesById);
  const hoursLearned = getStudentHoursLearned(range, progressByLesson);
  const goals = getStudentWeeklyGoals(range);
  const skillMap = getStudentSkillMap();
  const readiness = getStudentReadinessChecklist(skillMap);
  const recommendation = getStudentRecommendation(topicMastery, progressByLesson);

  container.innerHTML = `
    <div class="student-progress-grid">
      ${renderStudentOverallProgressCard(summary)}
      ${renderStudentTopicMasteryCard(topicMastery)}
      ${renderStudentSkillMapCard(skillMap)}
      ${renderStudentReadinessChecklistCard(readiness)}
      ${renderStudentCompletionProgressCard()}
      ${renderStudentWeeklyActivityCard(weeklyActivity)}
      ${renderStudentRecentScoresCard(recentScores, modulesById)}
      ${renderStudentHoursCard(hoursLearned)}
      ${renderStudentGoalsCard(goals)}
      ${renderStudentRecommendationCard(recommendation)}
      ${renderStudentEvidenceTimelineCard()}
    </div>
  `;
  renderStudentEvidenceTimeline();
  if (window.lucide) window.lucide.createIcons();
}

function renderStudentOverallProgressCard(summary) {
  return `
    <section class="panel-card student-progress-card student-progress-overall">
      <h2>Overall Progress</h2>
      <div class="student-overall-layout">
        <div class="student-progress-donut" style="--value:${summary.overall}">
          <strong>${summary.overall}%</strong>
          <span>Complete</span>
        </div>
        <div class="student-progress-kpis">
          ${renderStudentProgressKpi("book-open", "Lessons Completed", `${summary.completed}/${summary.total}`)}
          ${renderStudentProgressKpi("file-text", "Practice Completed", `${summary.practiceCompleted}/${summary.practiceTotal}`)}
          ${renderStudentProgressKpi("award", "Badges Awarded", summary.badgesEarned)}
          ${renderStudentProgressKpi("file-badge", "Certificates Issued", summary.certificatesEarned)}
        </div>
      </div>
    </section>
  `;
}

function renderStudentProgressKpi(icon, label, value) {
  return `
    <article>
      <i data-lucide="${icon}"></i>
      <span>${escapeHtml(label)}</span>
      <strong>${escapeHtml(value)}</strong>
    </article>
  `;
}

function renderStudentTopicMasteryCard(topicMastery) {
  const rows = topicMastery.length
    ? topicMastery.map((topic) => `
      <article class="student-mastery-row">
        <i data-lucide="${escapeAttribute(topic.icon)}" class="${escapeAttribute(topic.color)}"></i>
        <strong>${escapeHtml(topic.label)}</strong>
        <div class="student-mastery-track"><span style="width:${topic.score}%"></span></div>
        <b>${topic.score}%</b>
      </article>
    `).join("")
    : `<p class="empty-text">Complete lessons or practices to build topic mastery.</p>`;
  return `
    <section class="panel-card student-progress-card student-topic-mastery">
      <div class="student-card-title-row"><h2>Topic Mastery</h2><i data-lucide="info"></i></div>
      <div class="student-mastery-list">${rows}</div>
    </section>
  `;
}

function renderStudentSkillMapCard(skillMap) {
  const rows = skillMap.length
    ? skillMap.map((skill) => {
      const hasScore = Number.isFinite(skill.score);
      return `
        <article class="student-skill-map-row">
          <span class="student-skill-map-icon ${escapeAttribute(skill.color)}"><i data-lucide="${escapeAttribute(skill.icon)}"></i></span>
          <div>
            <strong>${escapeHtml(skill.label)}</strong>
            <small>${escapeHtml(skill.source)}${skill.relatedCount ? ` - ${skill.relatedCount} lesson${skill.relatedCount === 1 ? "" : "s"}` : ""}</small>
            <div class="student-mastery-track"><span style="width:${hasScore ? skill.score : 0}%"></span></div>
          </div>
          <b class="${hasScore ? scoreBandClass(skill.score) : ""}">${hasScore ? `${skill.score}%` : "-"}</b>
        </article>
      `;
    }).join("")
    : `<p class="empty-text">Complete a lesson to build your skill map.</p>`;
  return `
    <section class="panel-card student-progress-card student-skill-map-card">
      <div class="student-card-title-row"><h2>My Skill Map</h2><i data-lucide="map"></i></div>
      <div class="student-skill-map-list">${rows}</div>
    </section>
  `;
}

function renderStudentReadinessChecklistCard(items) {
  const rows = items.length
    ? items.map((item) => `
      <article class="student-readiness-row ${item.done ? "done" : ""}">
        <span><i data-lucide="${escapeAttribute(item.icon)}"></i></span>
        <div>
          <strong>${escapeHtml(item.label)}</strong>
          <small>${escapeHtml(item.detail)}</small>
        </div>
        <b>${item.done ? "Ready" : `${item.percent}%`}</b>
      </article>
    `).join("")
    : `<p class="empty-text">Complete lessons and checks to show readiness evidence.</p>`;
  return `
    <section class="panel-card student-progress-card student-readiness-card">
      <div class="student-card-title-row"><h2>Readiness Checklist</h2><i data-lucide="list-checks"></i></div>
      <div class="student-readiness-list">${rows}</div>
    </section>
  `;
}

function renderStudentCompletionProgressCard() {
  const data = studentState.completionSummary || {};
  const lessons = data.lessons || studentState.lessons || [];
  const modules = data.modules || studentState.modules || [];
  const progressByLesson = new Map((data.progress || studentState.progress || []).map((item) => [item.lesson_id, item]));
  const rows = modules
    .map((module) => {
      const moduleLessons = lessons.filter((lesson) => lesson.module_id === module.id);
      const completed = moduleLessons.filter((lesson) => isStudentLessonComplete(lesson, progressByLesson)).length;
      return {
        module,
        lessons: moduleLessons,
        completed,
        percent: percentOf(completed, moduleLessons.length)
      };
    })
    .filter((row) => row.lessons.length);

  return `
    <section class="panel-card student-progress-card student-completion-progress-card">
      <div class="student-card-title-row">
        <div><h2>Completion by Module</h2><p>Every lesson and check from your active term.</p></div>
        <i data-lucide="list-checks"></i>
      </div>
      <div class="student-completion-progress-list">
        ${rows.length ? rows.map((row) => `
          <article>
            <div>
              <strong>${escapeHtml(row.module.title)}</strong>
              <span>${row.completed}/${row.lessons.length} complete</span>
            </div>
            <b>${row.percent}%</b>
            <div class="wide-progress"><span style="width:${row.percent}%"></span></div>
          </article>
        `).join("") : `<p class="empty-text">No active lessons are available yet.</p>`}
      </div>
    </section>
  `;
}

function renderStudentWeeklyActivityCard(activity) {
  const maxValue = Math.max(1, ...activity.map((item) => item.count));
  const width = 680;
  const height = 238;
  const paddingX = 46;
  const top = 24;
  const plotHeight = 140;
  const plotWidth = width - paddingX * 2;
  const points = activity.map((item, index) => {
    const x = paddingX + (index / Math.max(1, activity.length - 1)) * plotWidth;
    const y = top + plotHeight - (item.count / maxValue) * plotHeight;
    return { ...item, x, y };
  });
  const line = points.map((point) => `${point.x},${point.y}`).join(" ");
  const area = points.length
    ? `${points[0].x},${top + plotHeight} ${line} ${points[points.length - 1].x},${top + plotHeight}`
    : "";
  const hasActivity = activity.some((item) => item.count > 0);
  return `
    <section class="panel-card student-progress-card student-weekly-card">
      <div class="student-card-title-row">
        <h2>Weekly Activity</h2>
        <span><i data-lucide="circle"></i> Activities Completed</span>
      </div>
      ${hasActivity ? `
        <div class="student-line-chart">
          <svg viewBox="0 0 ${width} ${height}" role="img" aria-label="Weekly activity chart">
            <g class="chart-grid">
              ${[0, 0.25, 0.5, 0.75, 1].map((ratio) => {
                const y = top + plotHeight - ratio * plotHeight;
                return `<line x1="${paddingX}" y1="${y}" x2="${width - paddingX}" y2="${y}"></line>`;
              }).join("")}
            </g>
            <polygon class="chart-area" points="${area}"></polygon>
            <polyline class="chart-line" points="${line}"></polyline>
            ${points.map((point) => `
              <circle class="chart-point" cx="${point.x}" cy="${point.y}" r="7"></circle>
              <text class="chart-value" x="${point.x}" y="${point.y - 14}">${point.count}</text>
              <text class="chart-label" x="${point.x}" y="205">${escapeHtml(point.day)}</text>
              <text class="chart-label sub" x="${point.x}" y="222">${escapeHtml(point.date)}</text>
            `).join("")}
          </svg>
        </div>
      ` : renderStudentChartEmpty("bar-chart-3", "Start a lesson or submit practice to see weekly activity.")}
    </section>
  `;
}

function renderStudentRecentScoresCard(attempts, modulesById) {
  const lessonsById = new Map(studentState.lessons.map((lesson) => [lesson.id, lesson]));
  const rows = attempts.length
    ? attempts.map((attempt) => {
      const lesson = lessonsById.get(attempt.lesson_id);
      const module = lesson ? modulesById.get(lesson.module_id) : null;
      const score = Number(attempt.score_percent || 0);
      return `
        <article class="student-score-row" role="button" tabindex="0" data-review-student-assessment="${escapeAttribute(lesson?.id || "")}" aria-label="Review ${escapeAttribute(lesson?.title || "practice score")}">
          <i data-lucide="${iconForLesson(lesson || {}, module)}" class="${moduleCategoryClass(module?.category || module?.title || "")}"></i>
          <div>
            <strong>${escapeHtml(lesson?.title || "Practice")}</strong>
            <span>${escapeHtml(formatDateOnly(attempt.submitted_at))}</span>
          </div>
          <b class="${score >= 85 ? "good" : score >= 70 ? "warn" : "danger"}">${score}%</b>
          <small>Review</small>
        </article>
      `;
    }).join("")
    : `<p class="empty-text">Submit practice or assessments to see recent scores.</p>`;
  return `
    <section class="panel-card student-progress-card student-recent-scores">
      <div class="student-card-title-row">
        <h2>Recent Scores</h2>
        <button class="link-button" type="button" data-jump-student="assessments">View All</button>
      </div>
      <div class="student-score-list">${rows}</div>
    </section>
  `;
}

function renderStudentHoursCard(hours) {
  const maxHours = Math.max(1, ...hours.days.map((day) => day.hours));
  return `
    <section class="panel-card student-progress-card student-hours-card">
      <div class="student-card-title-row"><h2>Hours Learned</h2><i data-lucide="info"></i></div>
      <div class="student-hours-total">
        <strong>${formatProgressNumber(hours.total)}</strong>
        <span>Hours This Week</span>
      </div>
      <div class="student-hour-bars">
        ${hours.days.map((day) => {
          const height = Math.max(8, Math.round((day.hours / maxHours) * 88));
          return `
            <article>
              <span>${formatProgressNumber(day.hours)}</span>
              <div><b style="height:${height}px"></b></div>
              <small>${escapeHtml(day.day)}</small>
            </article>
          `;
        }).join("")}
      </div>
    </section>
  `;
}

function renderStudentGoalsCard(goals) {
  return `
    <section class="panel-card student-progress-card student-goals-card">
      <h2>My Goals</h2>
      <div class="student-goal-list">
        ${goals.map((goal) => {
          const percent = percentOf(Math.min(goal.current, goal.target), goal.target);
          return `
            <article class="student-goal-row">
              <i data-lucide="${goal.icon}" class="${goal.done ? "complete" : ""}"></i>
              <div>
                <div><strong>${escapeHtml(goal.label)}</strong><b>${goal.current}/${goal.target}</b></div>
                <div class="wide-progress"><span style="width:${percent}%"></span></div>
              </div>
              <span class="${goal.done ? "complete" : ""}"><i data-lucide="${goal.done ? "check-circle-2" : "chevron-right"}"></i></span>
            </article>
          `;
        }).join("")}
      </div>
    </section>
  `;
}

function renderStudentRecommendationCard(recommendation) {
  if (!recommendation?.lesson) {
    return `
      <section class="panel-card student-progress-card student-recommend-card">
        <div class="student-card-title-row"><h2>Recommended for You</h2><i data-lucide="sparkles"></i></div>
        <p class="empty-text">All visible lessons are complete. Nice work.</p>
      </section>
    `;
  }
  const { lesson, module, progress } = recommendation;
  const percent = Number(progress?.progress_percent || 0);
  return `
    <section class="panel-card student-progress-card student-recommend-card">
      <div class="recommend-copy">
        <div class="student-card-title-row"><h2>Recommended for You</h2><i data-lucide="sparkles"></i></div>
        <p>Strengthen your skills in this area.</p>
        <article class="recommend-lesson">
          <i data-lucide="${iconForLesson(lesson, module)}" class="${moduleCategoryClass(module?.category || module?.title || "")}"></i>
          <div>
            <strong>${escapeHtml(lesson.title)}</strong>
            <span>${escapeHtml(module?.title || "Module")} - ${lesson.duration_minutes || 0} mins</span>
            <div class="wide-progress"><span style="width:${percent}%"></span></div>
          </div>
        </article>
      </div>
      <div class="recommend-action">
        <div class="recommend-visual"><i data-lucide="shield-check"></i></div>
        <button class="primary-button" type="button" data-open-student-lesson="${escapeAttribute(lesson.id)}">
          ${percent ? "Continue Lesson" : "Start Lesson"}
        </button>
      </div>
    </section>
  `;
}

function renderStudentEvidenceTimelineCard() {
  return `
    <section class="panel-card student-progress-card student-evidence-progress-card">
      <div class="student-card-title-row"><h2>Evidence Timeline</h2><i data-lucide="history"></i></div>
      <div id="studentEvidenceTimelineFull"></div>
    </section>
  `;
}

function renderStudentChartEmpty(icon, message) {
  return `
    <div class="student-chart-empty">
      <i data-lucide="${icon}"></i>
      <p>${escapeHtml(message)}</p>
    </div>
  `;
}

function getStudentProgressDateRange() {
  const filters = studentState.progressFilters || defaultStudentProgressFilters();
  return {
    startDate: startOfLocalDay(filters.startDate),
    endDate: endOfLocalDay(filters.endDate)
  };
}

function getStudentTopicMastery(modulesById, progressByLesson) {
  const latestAttempts = getLatestAttemptsByLesson();
  const attemptsByLesson = new Map(latestAttempts.map((attempt) => [attempt.lesson_id, attempt]));
  return studentState.modules.map((module) => {
    const lessons = getSortedLessonsForModule(module.id, studentState.lessons);
    const scores = lessons
      .map((lesson) => attemptsByLesson.get(lesson.id)?.score_percent)
      .map(Number)
      .filter(Number.isFinite);
    const completed = lessons.filter((lesson) => isStudentLessonComplete(lesson, progressByLesson)).length;
    const score = scores.length
      ? Math.round(scores.reduce((sum, item) => sum + item, 0) / scores.length)
      : percentOf(completed, lessons.length);
    const label = module.category || module.title;
    return {
      module_id: module.id,
      label,
      score,
      icon: iconForTopic(module),
      color: moduleCategoryClass(`${module.category || ""} ${module.title || ""}`),
      module,
      lessons
    };
  }).sort((a, b) => b.score - a.score || a.label.localeCompare(b.label));
}

function getStudentSkillDefinitions() {
  return [
    {
      id: "hardware",
      label: "Hardware",
      icon: "cpu",
      color: "blue",
      keywords: ["hardware", "computer", "parts", "component", "system unit", "motherboard", "ram", "cpu", "storage"]
    },
    {
      id: "safety",
      label: "Safety",
      icon: "shield-check",
      color: "green",
      keywords: ["safety", "security", "ohs", "esd", "static", "anti-static", "hazard", "protect"]
    },
    {
      id: "assembly",
      label: "Assembly",
      icon: "wrench",
      color: "orange",
      keywords: ["assembly", "assembling", "assemble", "install", "installation", "standoff"],
      exclude: ["disassembly", "disassembling", "disassemble"]
    },
    {
      id: "disassembly",
      label: "Disassembly",
      icon: "list-ordered",
      color: "purple",
      keywords: ["disassembly", "disassembling", "disassemble", "remove", "removing", "unplug"]
    },
    {
      id: "cables",
      label: "Cables/Ports",
      icon: "cable",
      color: "blue",
      keywords: ["cable", "port", "ports", "connector", "peripheral", "sata", "vga", "usb", "ps/2", "lan", "ethernet"]
    },
    {
      id: "productivity",
      label: "Productivity",
      icon: "monitor",
      color: "green",
      keywords: ["productivity", "spreadsheet", "word", "document", "presentation", "keyboard", "shortcuts", "typing"]
    }
  ];
}

function getStudentSkillMap() {
  const modulesById = new Map(studentState.modules.map((module) => [module.id, module]));
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));
  const attemptsByLesson = new Map(getLatestAttemptsByLesson().map((attempt) => [attempt.lesson_id, attempt]));

  return getStudentSkillDefinitions().map((definition) => {
    const relatedLessons = studentState.lessons.filter((lesson) => {
      const module = modulesById.get(lesson.module_id);
      return lessonMatchesSkillDefinition(lesson, module, definition);
    });
    const scores = relatedLessons
      .map((lesson) => Number(attemptsByLesson.get(lesson.id)?.score_percent))
      .filter(Number.isFinite);
    const completed = relatedLessons.filter((lesson) => isStudentLessonComplete(lesson, progressByLesson)).length;
    const score = scores.length
      ? Math.round(scores.reduce((sum, value) => sum + value, 0) / scores.length)
      : relatedLessons.length
        ? percentOf(completed, relatedLessons.length)
        : null;
    const source = scores.length
      ? "Latest scores"
      : relatedLessons.length
        ? `${completed}/${relatedLessons.length} lessons complete`
        : "No matching lessons yet";
    return {
      ...definition,
      score,
      source,
      relatedLessons,
      relatedCount: relatedLessons.length,
      completed
    };
  });
}

function getStudentReadinessChecklist(skillMap = getStudentSkillMap()) {
  const skillById = new Map(skillMap.map((skill) => [skill.id, skill]));
  return [
    { label: "Parts identification", icon: "cpu", skills: ["hardware"] },
    { label: "Safety awareness", icon: "shield-check", skills: ["safety"] },
    { label: "Correct procedure order", icon: "list-ordered", skills: ["disassembly"] },
    { label: "Assembly readiness", icon: "wrench", skills: ["assembly"] },
    { label: "Ports and cable matching", icon: "cable", skills: ["cables"] },
    { label: "Productivity basics", icon: "monitor-check", skills: ["productivity"] }
  ].map((item) => {
    const skills = item.skills.map((id) => skillById.get(id)).filter(Boolean);
    const scores = skills.map((skill) => skill.score).filter(Number.isFinite);
    const relatedCount = skills.reduce((sum, skill) => sum + Number(skill.relatedCount || 0), 0);
    const completed = skills.reduce((sum, skill) => sum + Number(skill.completed || 0), 0);
    const percent = scores.length
      ? Math.round(scores.reduce((sum, value) => sum + value, 0) / scores.length)
      : percentOf(completed, relatedCount);
    const done = percent >= 75 || (relatedCount > 0 && completed >= relatedCount);
    return {
      ...item,
      percent,
      done,
      detail: relatedCount
        ? `${completed}/${relatedCount} related lesson${relatedCount === 1 ? "" : "s"} complete`
        : "Waiting for matching lessons"
    };
  });
}

function lessonMatchesSkillDefinition(lesson, module, definition) {
  const lessonText = [
    lesson?.title,
    lesson?.description,
    lesson?.lesson_type
  ].filter(Boolean).join(" ").toLowerCase();
  const moduleText = [
    module?.title,
    module?.description,
    module?.category
  ].filter(Boolean).join(" ").toLowerCase();
  const text = `${lessonText} ${moduleText}`;
  const excluded = (definition.exclude || []).some((keyword) => lessonText.includes(keyword));
  if (excluded) return false;
  return definition.keywords.some((keyword) => text.includes(keyword));
}

function getStudentWeeklyActivity(range, modulesById) {
  const days = buildProgressWeekDays(range.startDate);
  const visibleLessonIds = new Set(studentState.lessons.map((lesson) => lesson.id));
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));
  const dayByKey = new Map(days.map((day) => [day.key, day]));
  const addToDay = (value) => {
    if (!isDateInRange(value, range.startDate, range.endDate)) return;
    const key = toDateInputValue(value);
    const day = dayByKey.get(key);
    if (day) day.count += 1;
  };

  studentState.progress.forEach((item) => {
    const lesson = studentState.lessons.find((row) => row.id === item.lesson_id);
    if (!lesson || !modulesById.has(lesson.module_id)) return;
    if (isStudentLessonComplete(lesson, progressByLesson)) {
      addToDay(item.completed_at || item.updated_at);
    }
  });
  studentState.attempts
    .filter((attempt) => visibleLessonIds.has(attempt.lesson_id))
    .forEach((attempt) => addToDay(attempt.submitted_at));

  return days;
}

function getStudentHoursLearned(range, progressByLesson) {
  const days = buildProgressWeekDays(range.startDate).map((day) => ({ ...day, hours: 0 }));
  const dayByKey = new Map(days.map((day) => [day.key, day]));
  studentState.lessons.forEach((lesson) => {
    const progress = progressByLesson.get(lesson.id);
    if (!isStudentLessonComplete(lesson, progressByLesson)) return;
    const completedAt = progress.completed_at || progress.updated_at;
    if (!isDateInRange(completedAt, range.startDate, range.endDate)) return;
    const day = dayByKey.get(toDateInputValue(completedAt));
    if (day) day.hours += Number(lesson.duration_minutes || 0) / 60;
  });
  return {
    total: days.reduce((sum, day) => sum + day.hours, 0),
    days
  };
}

function getStudentWeeklyGoals(range) {
  const lessonById = new Map(studentState.lessons.map((lesson) => [lesson.id, lesson]));
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));
  const weeklyLessons = studentState.progress.filter((item) => {
    const lesson = lessonById.get(item.lesson_id);
    return lesson
      && !["practice", "assessment"].includes(lesson.lesson_type)
      && isStudentLessonComplete(lesson, progressByLesson)
      && isDateInRange(item.completed_at || item.updated_at, range.startDate, range.endDate);
  }).length;
  const weeklyPractice = new Set(studentState.attempts
    .filter((attempt) => isDateInRange(attempt.submitted_at, range.startDate, range.endDate))
    .map((attempt) => attempt.lesson_id)).size;
  const weeklyAwards = [
    ...studentState.badges.map((item) => item.awarded_at),
    ...studentState.certificates.map((item) => item.awarded_at)
  ].filter((value) => isDateInRange(value, range.startDate, range.endDate)).length;
  return [
    { icon: "book-open", label: "Finish 2 lessons this week", current: weeklyLessons, target: 2, done: weeklyLessons >= 2 },
    { icon: "file-text", label: "Complete 2 practices", current: weeklyPractice, target: 2, done: weeklyPractice >= 2 },
    { icon: "award", label: "Earn 1 badge or certificate", current: weeklyAwards, target: 1, done: weeklyAwards >= 1 }
  ];
}

function getStudentNextAction() {
  const modulesById = new Map(studentState.modules.map((module) => [module.id, module]));
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));
  const assessmentItems = getStudentAssessmentItems(modulesById);
  const urgentCheck = assessmentItems.find((item) => !item.locked && item.status === "overdue")
    || assessmentItems.find((item) => !item.locked && item.status === "in_progress")
    || assessmentItems.find((item) => {
      if (item.locked || item.submitted || !item.lesson.due_date) return false;
      const due = endOfLocalDay(item.lesson.due_date);
      const soon = new Date();
      soon.setDate(soon.getDate() + 3);
      return due <= soon;
    });

  if (urgentCheck) {
    const overdue = urgentCheck.status === "overdue";
    const progressPercent = Number(urgentCheck.progressPercent || 0);
    return {
      lesson: urgentCheck.lesson,
      progress: urgentCheck.progress,
      progressPercent,
      title: urgentCheck.lesson.title,
      body: overdue
        ? "This check is overdue. Submit it first so your progress stays complete."
        : urgentCheck.status === "in_progress"
          ? "You already started this check. Finish it while the details are still fresh."
          : `This check is due ${formatDateOnly(urgentCheck.lesson.due_date)}.`,
      actionLabel: urgentCheck.status === "in_progress" ? "Continue Check" : "Open Check",
      icon: overdue ? "alarm-clock" : "clipboard-check",
      tone: overdue ? "danger" : "orange",
      kicker: urgentCheck.module?.title || "Assessment",
      pillIcon: overdue ? "alarm-clock" : "clipboard-check",
      pillLabel: overdue ? "Overdue" : "Assessment"
    };
  }

  const inProgressLesson = studentState.lessons.find((lesson) => {
    const progress = progressByLesson.get(lesson.id);
    return progress
      && !getStudentLessonGate(lesson.id).locked
      && !isStudentLessonComplete(lesson, progressByLesson)
      && (progress.status === "in_progress" || Number(progress.progress_percent || 0) > 0);
  });
  if (inProgressLesson) {
    const module = modulesById.get(inProgressLesson.module_id);
    const progress = progressByLesson.get(inProgressLesson.id);
    return {
      lesson: inProgressLesson,
      progress,
      progressPercent: Number(progress?.progress_percent || 0),
      title: inProgressLesson.title,
      body: "Pick up where you left off and keep your weekly evidence moving.",
      actionLabel: "Continue Lesson",
      icon: iconForLesson(inProgressLesson, module),
      tone: "blue",
      kicker: module?.title || "Lesson",
      pillIcon: "play-circle",
      pillLabel: "Continue"
    };
  }

  const weakestSkillLesson = getStudentWeakestSkillLesson(progressByLesson);
  if (weakestSkillLesson) {
    const { lesson, skill } = weakestSkillLesson;
    const module = modulesById.get(lesson.module_id);
    return {
      lesson,
      progress: progressByLesson.get(lesson.id) || null,
      progressPercent: Number(progressByLesson.get(lesson.id)?.progress_percent || 0),
      title: lesson.title,
      body: `This supports your ${skill.label.toLowerCase()} skill, currently your lowest visible area.`,
      actionLabel: "Start Lesson",
      icon: skill.icon,
      tone: skill.color,
      kicker: module?.title || skill.label,
      pillIcon: "target",
      pillLabel: "Weak Area"
    };
  }

  const nextLesson = getNextUnlockedStudentLesson();
  if (nextLesson) {
    const module = modulesById.get(nextLesson.module_id);
    return {
      lesson: nextLesson,
      progress: progressByLesson.get(nextLesson.id) || null,
      progressPercent: Number(progressByLesson.get(nextLesson.id)?.progress_percent || 0),
      title: nextLesson.title,
      body: "Start the next visible lesson for your active term.",
      actionLabel: "Start Lesson",
      icon: iconForLesson(nextLesson, module),
      tone: "blue",
      kicker: module?.title || "Lesson",
      pillIcon: "book-open",
      pillLabel: "Next Lesson"
    };
  }

  return null;
}

function getStudentWeakestSkillLesson(progressByLesson) {
  const skills = getStudentSkillMap()
    .filter((skill) => skill.relatedLessons.length && Number.isFinite(skill.score))
    .sort((a, b) => a.score - b.score || a.label.localeCompare(b.label));
  for (const skill of skills) {
    const lesson = skill.relatedLessons.find((item) => !getStudentLessonGate(item.id).locked && !isStudentLessonComplete(item, progressByLesson));
    if (lesson) return { skill, lesson };
  }
  return null;
}

function getStudentEvidenceItems() {
  const lessonsById = new Map(studentState.lessons.map((lesson) => [lesson.id, lesson]));
  const modulesById = new Map(studentState.modules.map((module) => [module.id, module]));
  const visibleLessonIds = new Set(studentState.lessons.map((lesson) => lesson.id));
  const items = [];

  studentState.progress.forEach((progress) => {
    if (!visibleLessonIds.has(progress.lesson_id) || !(progress.status === "completed" || Number(progress.progress_percent || 0) >= 100)) return;
    const lesson = lessonsById.get(progress.lesson_id);
    const module = lesson ? modulesById.get(lesson.module_id) : null;
    items.push({
      type: "lesson",
      title: "Lesson completed",
      detail: `${lesson?.title || "Lesson"}${module?.title ? ` - ${module.title}` : ""}`,
      date: progress.completed_at || progress.updated_at,
      lesson_id: progress.lesson_id,
      icon: "check-circle-2",
      color: "green"
    });
  });

  getLatestAttemptsByLesson().forEach((attempt) => {
    if (!visibleLessonIds.has(attempt.lesson_id)) return;
    const lesson = lessonsById.get(attempt.lesson_id);
    const module = lesson ? modulesById.get(lesson.module_id) : null;
    const score = Number(attempt.score_percent);
    items.push({
      type: "attempt",
      title: lesson?.lesson_type === "assessment" ? "Assessment submitted" : "Practice submitted",
      detail: `${lesson?.title || "Check"}${Number.isFinite(score) ? ` - ${score}%` : ""}${module?.title ? ` - ${module.title}` : ""}`,
      date: attempt.submitted_at,
      lesson_id: attempt.lesson_id,
      icon: "clipboard-check",
      color: Number.isFinite(score) && score >= 75 ? "blue" : "orange"
    });
  });

  studentState.badges.forEach((badge) => {
    items.push({
      type: "badge",
      title: "Badge awarded",
      detail: formatAwardDisplayTitle(badge.title || badge.badge_key, "Badge"),
      date: badge.awarded_at || badge.created_at,
      view: "achievements",
      icon: "shield-check",
      color: "purple"
    });
  });

  studentState.certificates.forEach((certificate) => {
    items.push({
      type: "certificate",
      title: "Certificate issued",
      detail: certificate.title || certificate.certificate_key || "Certificate",
      date: certificate.awarded_at || certificate.created_at,
      view: "achievements",
      icon: "file-badge",
      color: "orange"
    });
  });

  (studentState.notifications || []).forEach((notification) => {
    const meta = notification.metadata || {};
    const isLessonEvent = ["lesson_published", "lesson_updated", "assessment_published", "assessment_updated"].includes(notification.event_type);
    items.push({
      type: "notification",
      title: notification.title || "Notification",
      detail: meta.lesson_title || meta.reward_title || notification.body || "",
      date: notification.created_at,
      lesson_id: isLessonEvent ? notification.entity_id : null,
      view: notification.event_type === "badge_awarded" || notification.event_type === "certificate_awarded" ? "achievements" : "home",
      icon: iconForStudentNotification(notification.event_type),
      color: colorForStudentNotification(notification.event_type)
    });
  });

  return items
    .filter((item) => item.date && !Number.isNaN(new Date(item.date).getTime()))
    .sort((a, b) => new Date(b.date) - new Date(a.date));
}

function getStudentRecommendation(topicMastery, progressByLesson) {
  const moduleById = new Map(studentState.modules.map((module) => [module.id, module]));
  const assessmentItems = getStudentAssessmentItems(moduleById);
  const soon = new Date();
  soon.setDate(soon.getDate() + 3);
  const priorityCheck = assessmentItems.find((item) => item.status === "overdue")
    || assessmentItems.find((item) => item.status === "in_progress")
    || assessmentItems.find((item) => !item.submitted && item.lesson.due_date && endOfLocalDay(item.lesson.due_date) <= soon);
  if (priorityCheck) {
    return {
      lesson: priorityCheck.lesson,
      module: priorityCheck.module,
      progress: priorityCheck.progress || null
    };
  }

  const weakSkillLesson = getStudentWeakestSkillLesson(progressByLesson);
  if (weakSkillLesson) {
    const { lesson } = weakSkillLesson;
    return {
      lesson,
      module: moduleById.get(lesson.module_id),
      progress: progressByLesson.get(lesson.id) || null
    };
  }

  const weakestTopics = [...topicMastery].sort((a, b) => a.score - b.score);
  for (const topic of weakestTopics) {
    const lesson = topic.lessons.find((item) => {
      return !isStudentLessonComplete(item, progressByLesson);
    });
    if (lesson) {
      return { lesson, module: topic.module, progress: progressByLesson.get(lesson.id) || null };
    }
  }
  const fallback = studentState.lessons.find((lesson) => {
    return !isStudentLessonComplete(lesson, progressByLesson);
  }) || studentState.lessons[0];
  return fallback ? { lesson: fallback, module: moduleById.get(fallback.module_id), progress: progressByLesson.get(fallback.id) || null } : null;
}

function getLatestAttemptsByLesson() {
  const latest = new Map();
  [...studentState.attempts]
    .sort((a, b) => new Date(b.submitted_at || 0) - new Date(a.submitted_at || 0))
    .forEach((attempt) => {
      if (!latest.has(attempt.lesson_id)) latest.set(attempt.lesson_id, attempt);
    });
  return [...latest.values()];
}

function buildProgressWeekDays(startDate) {
  return Array.from({ length: 7 }, (_, index) => {
    const date = new Date(startDate);
    date.setDate(startDate.getDate() + index);
    return {
      key: toDateInputValue(date),
      day: new Intl.DateTimeFormat(undefined, { weekday: "short" }).format(date),
      date: new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric" }).format(date),
      count: 0
    };
  });
}

function iconForTopic(module) {
  const value = `${module?.category || ""} ${module?.title || ""}`.toLowerCase();
  if (value.includes("productivity") || value.includes("spreadsheet")) return "monitor";
  if (value.includes("network") || value.includes("internet")) return "globe-2";
  if (value.includes("safety") || value.includes("security")) return "shield-check";
  return "cpu";
}

function formatProgressNumber(value) {
  const number = Number(value || 0);
  if (number === 0) return "0";
  return number.toFixed(number >= 10 || Number.isInteger(number) ? 0 : 1).replace(/\.0$/, "");
}

function getTeacherStudentSummary(student) {
  const lessonIds = getRelevantLessonIds(student.grade_level);
  const progress = teacherState.progress.filter((item) => item.student_id === student.id && lessonIds.has(item.lesson_id));
  const badges = teacherState.badges.filter((badge) => badge.student_id === student.id);
  const certificates = teacherState.certificates.filter((certificate) => certificate.student_id === student.id);
  const total = lessonIds.size;
  const completed = progress.filter((item) => item.status === "completed" || item.progress_percent === 100).length;
  const scored = progress.filter((item) => Number.isFinite(item.score_percent));
  const average = scored.length
    ? Math.round(scored.reduce((sum, item) => sum + item.score_percent, 0) / scored.length)
    : total ? Math.round((completed / total) * 100) : 0;
  const progressPercent = total ? Math.round((completed / total) * 100) : 0;
  const latest = progress.map((item) => item.updated_at).filter(Boolean).sort().pop();
  const progressStatus = student.status !== "approved"
    ? titleCase(student.status)
    : (total === 0 || average < 75 || progressPercent < 50 ? "Needs Support" : "On Track");
  const performanceBand = average >= 90 ? "excellent" : average >= 75 ? "good" : average >= 60 ? "fair" : "needs";

  return {
    student,
    total,
    completed,
    average,
    progressPercent,
    progressStatus,
    performanceBand,
    badges,
    certificates,
    statusClass: progressStatus === "On Track" ? "good" : progressStatus === "Needs Support" ? "warning" : student.status,
    lastActive: latest ? formatDate(latest) : "No activity"
  };
}

function getRelevantLessonIds(gradeLevel) {
  const quarterId = teacherState.activeQuarter?.id || null;
  const moduleIds = new Set(teacherState.modules
    .filter((module) => module.status === "published" && module.grade_level === gradeLevel && (!quarterId || module.quarter_id === quarterId))
    .map((module) => module.id));
  return new Set(teacherState.lessons
    .filter((lesson) => lesson.status === "published" && moduleIds.has(lesson.module_id))
    .map((lesson) => lesson.id));
}

function getStudentDashboardSummary() {
  const total = studentState.lessons.length;
  const progressByLesson = new Map(studentState.progress.map((item) => [item.lesson_id, item]));
  const completed = studentState.lessons.filter((lesson) => isStudentLessonComplete(lesson, progressByLesson)).length;
  const overall = total ? Math.round((completed / total) * 100) : 0;
  const practiceLessonIds = new Set(studentState.lessons
    .filter((lesson) => ["practice", "assessment"].includes(lesson.lesson_type))
    .map((lesson) => lesson.id));
  const latestAttempts = getLatestAttemptsByLesson().filter((attempt) => practiceLessonIds.has(attempt.lesson_id));
  const practiceCompleted = new Set([
    ...latestAttempts.map((attempt) => attempt.lesson_id),
    ...studentState.progress
      .filter((item) => practiceLessonIds.has(item.lesson_id) && (item.status === "completed" || item.progress_percent === 100))
      .map((item) => item.lesson_id)
  ]).size;
  const attemptScores = latestAttempts
    .map((attempt) => Number(attempt.score_percent))
    .filter(Number.isFinite);
  const progressScores = studentState.progress
    .map((item) => Number(item.score_percent))
    .filter(Number.isFinite);
  const scored = attemptScores.length ? attemptScores : progressScores;
  const averageScore = scored.length ? Math.round(scored.reduce((sum, item) => sum + item, 0) / scored.length) : 0;
  const completedMinutes = studentState.lessons
    .filter((lesson) => isStudentLessonComplete(lesson, progressByLesson))
    .reduce((sum, lesson) => sum + Number(lesson.duration_minutes || 0), 0);
  return {
    total,
    completed,
    practiceTotal: practiceLessonIds.size,
    practiceCompleted,
    badgesEarned: studentState.badges.length,
    certificatesEarned: studentState.certificates.length,
    overall,
    averageScore,
    hours: (completedMinutes / 60).toFixed(1).replace(/\.0$/, "")
  };
}

function isLessonInActiveQuarter(lesson) {
  return !teacherState.activeQuarter || lesson.quarter_id === teacherState.activeQuarter.id;
}

function validateRegistrationPayload(payload) {
  if (payload.password !== payload.confirm_password) return "Password and confirm password must match.";
  if (!payload.terms) return "Please agree to the Terms and Privacy Policy.";
  if (!payload.grade_level || !payload.section) return "Please select your grade level and section.";
  const expectedAdviser = STUDENT_SECTION_ADVISERS[payload.grade_level]?.[payload.section];
  if (!expectedAdviser) return "Please select a valid section for your grade level.";
  if (payload.adviser !== expectedAdviser) return "Adviser must match the selected grade level and section.";
  if (!/^09\d{9}$/.test(payload.phone_number || "")) return "Phone Number must be exactly 11 digits and start with 09.";
  if (!isStrongPassword(payload.password || "")) return "Password must be at least 8 characters and include uppercase, lowercase, number, and symbol.";
  return "";
}

function isStrongPassword(value) {
  const password = String(value || "");
  return password.length >= 8
    && /[a-z]/.test(password)
    && /[A-Z]/.test(password)
    && /\d/.test(password)
    && /[^A-Za-z0-9]/.test(password);
}

async function requireSession() {
  const session = getSession();
  if (!session?.access_token) {
    throw new Error("Please log in first.");
  }
  const me = await apiGet("/api/me");
  setSession(session, me.profile);
  return { session, profile: me.profile };
}

async function apiGet(path) {
  const response = await fetch(path, {
    headers: authHeaders()
  });
  return parseApiResponse(response);
}

async function apiPost(path, body) {
  const response = await fetch(path, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...authHeaders()
    },
    body: JSON.stringify(body)
  });
  return parseApiResponse(response);
}

async function apiPostWithToken(path, body, token) {
  const response = await fetch(path, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`
    },
    body: JSON.stringify(body)
  });
  return parseApiResponse(response);
}

async function apiPatch(path, body) {
  const response = await fetch(path, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
      ...authHeaders()
    },
    body: JSON.stringify(body)
  });
  return parseApiResponse(response);
}

async function apiDelete(path, body = {}) {
  const response = await fetch(path, {
    method: "DELETE",
    headers: {
      "Content-Type": "application/json",
      ...authHeaders()
    },
    body: JSON.stringify(body)
  });
  return parseApiResponse(response);
}

async function parseApiResponse(response) {
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    throw new Error(data.error || "Something went wrong. Please try again.");
  }
  return data;
}

function authHeaders() {
  const session = getSession();
  return session?.access_token ? { Authorization: `Bearer ${session.access_token}` } : {};
}

function getSession() {
  try {
    return JSON.parse(localStorage.getItem(SESSION_KEY) || "null");
  } catch {
    return null;
  }
}

function setSession(session, profile) {
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    ...session,
    profile
  }));
}

function clearSession() {
  localStorage.removeItem(SESSION_KEY);
}

function setMessage(element, text, type = "") {
  if (!element) return;
  element.className = `status-message dashboard-status ${type}`.trim();
  const isDashboardActionMessage = ["teacherMessage", "studentMessage"].includes(element.id);
  if (isDashboardActionMessage && text && type) {
    const toastType = type === "error" ? "error" : type === "warning" ? "warning" : type === "success" ? "success" : "info";
    showToast({
      title: toastType === "error" ? "Action failed" : text,
      message: toastType === "error" ? text : "",
      type: toastType,
      duration: toastType === "error" ? null : undefined
    });
    element.textContent = "";
    return;
  }
  element.textContent = text;
}

function setText(id, value) {
  const element = document.getElementById(id);
  if (element) element.textContent = String(value);
}

function percentOf(value, total) {
  return total ? Math.round((value / total) * 100) : 0;
}

function defaultTeacherSettings() {
  return {
    notification_preferences: {
      student_accounts: true,
      lesson_completions: true,
      practice_submissions: true,
      achievement_awards: true
    },
    default_grade: "Grade 10",
    default_landing_view: "overview",
    compact_lessons: false,
    report_default_range: "week",
    report_export_format: "pdf",
    report_default_grade: ""
  };
}

function normalizeTeacherSettings(settings = {}) {
  const defaults = defaultTeacherSettings();
  return {
    ...defaults,
    ...settings,
    notification_preferences: {
      ...defaults.notification_preferences,
      ...(settings.notification_preferences || {})
    },
    default_grade: settings.default_grade || defaults.default_grade,
    default_landing_view: settings.default_landing_view || defaults.default_landing_view,
    compact_lessons: Boolean(settings.compact_lessons),
    report_default_range: settings.report_default_range || defaults.report_default_range,
    report_export_format: settings.report_export_format || defaults.report_export_format,
    report_default_grade: settings.report_default_grade || ""
  };
}

function defaultTeacherEvaluationData() {
  return {
    active_quarter: null,
    survey_categories: [],
    assessment_options: [],
    cycles: [],
    summaries: [],
    overall: {
      student_count: 0,
      pretest_average: null,
      posttest_average: null,
      growth: null,
      growth_percent: null,
      missing_pretest_count: 0,
      missing_posttest_count: 0,
      student_response_count: 0,
      teacher_response_count: 0,
      survey_average: null,
      survey_rating: "Not collected"
    }
  };
}

function normalizeTeacherEvaluationData(data = {}) {
  const defaults = defaultTeacherEvaluationData();
  return {
    ...defaults,
    ...data,
    survey_categories: Array.isArray(data.survey_categories) ? data.survey_categories : [],
    assessment_options: Array.isArray(data.assessment_options) ? data.assessment_options : [],
    cycles: Array.isArray(data.cycles) ? data.cycles : [],
    summaries: Array.isArray(data.summaries) ? data.summaries : [],
    overall: {
      ...defaults.overall,
      ...(data.overall || {})
    }
  };
}

function defaultStudentEvaluationData() {
  return {
    survey_categories: [],
    cycle: null,
    survey_open: false,
    response: null
  };
}

function normalizeStudentEvaluationData(data = {}) {
  const defaults = defaultStudentEvaluationData();
  return {
    ...defaults,
    ...data,
    survey_categories: Array.isArray(data.survey_categories) ? data.survey_categories : [],
    cycle: data.cycle || null,
    survey_open: Boolean(data.survey_open ?? data.cycle?.survey_is_open),
    response: data.response || null
  };
}

function defaultStudentProgressFilters() {
  const now = new Date();
  const start = new Date(now);
  const day = start.getDay() || 7;
  start.setDate(start.getDate() - day + 1);
  const end = new Date(start);
  end.setDate(start.getDate() + 6);
  return {
    startDate: toDateInputValue(start),
    endDate: toDateInputValue(end)
  };
}

function defaultReportFilters() {
  const now = new Date();
  const start = new Date(now);
  const day = start.getDay() || 7;
  start.setDate(start.getDate() - day + 1);
  const end = new Date(start);
  end.setDate(start.getDate() + 6);
  return {
    startDate: toDateInputValue(start),
    endDate: toDateInputValue(end),
    grade: "",
    section: "",
    template: REPORT_TEMPLATE_NAME
  };
}

function reportFiltersFromSettings(settings = {}) {
  const normalized = normalizeTeacherSettings(settings);
  const now = new Date();
  if (normalized.report_default_range === "month") {
    const start = new Date(now.getFullYear(), now.getMonth(), 1);
    const end = new Date(now.getFullYear(), now.getMonth() + 1, 0);
    return normalizeReportFilters({
      startDate: toDateInputValue(start),
      endDate: toDateInputValue(end),
      grade: normalized.report_default_grade || "",
      section: "",
      template: REPORT_TEMPLATE_NAME
    });
  }
  return normalizeReportFilters({
    ...defaultReportFilters(),
    grade: normalized.report_default_grade || "",
    section: "",
    template: REPORT_TEMPLATE_NAME
  });
}

function normalizeReportFilters(filters = {}) {
  const defaults = defaultReportFilters();
  const startDate = /^\d{4}-\d{2}-\d{2}$/.test(filters.startDate || "") ? filters.startDate : defaults.startDate;
  const endDate = /^\d{4}-\d{2}-\d{2}$/.test(filters.endDate || "") ? filters.endDate : defaults.endDate;
  return {
    startDate: startDate <= endDate ? startDate : endDate,
    endDate: endDate >= startDate ? endDate : startDate,
    ...normalizeGradeSectionFilter(filters),
    template: filters.template || REPORT_TEMPLATE_NAME
  };
}

function toDateInputValue(value) {
  const date = value instanceof Date ? value : new Date(value);
  const offset = date.getTimezoneOffset() * 60000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 10);
}

function startOfLocalDay(value) {
  const [year, month, day] = String(value).slice(0, 10).split("-").map(Number);
  return new Date(year, month - 1, day, 0, 0, 0, 0);
}

function endOfLocalDay(value) {
  const [year, month, day] = String(value).slice(0, 10).split("-").map(Number);
  return new Date(year, month - 1, day, 23, 59, 59, 999);
}

function isDateInRange(value, startDate, endDate) {
  if (!value) return false;
  const date = new Date(value);
  return date >= startDate && date <= endDate;
}

function redirectToLoginSoon(delay = 900) {
  window.setTimeout(() => {
    clearSession();
    window.location.href = "index.html";
  }, delay);
}

function formToObject(form) {
  const data = {};
  new FormData(form).forEach((value, key) => {
    if (data[key] !== undefined) return;
    const input = form.elements[key];
    data[key] = input?.type === "checkbox" ? input.checked : value;
  });
  return data;
}

function getInitials(name) {
  return String(name || "")
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("") || "ST";
}

function formatDate(value) {
  if (!value) return "-";
  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit"
  }).format(new Date(value));
}

function formatDateOnly(value) {
  if (!value) return "-";
  const date = /^\d{4}-\d{2}-\d{2}$/.test(String(value))
    ? startOfLocalDay(value)
    : new Date(value);
  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric"
  }).format(date);
}

function titleCase(value) {
  return String(value).replace(/[_-]/g, " ").replace(/\b\w/g, (letter) => letter.toUpperCase());
}

function isPackageModule(module) {
  return PACKAGE_MODULE_TITLES.has(module?.title || "");
}

function getSortedLessonsForModule(moduleId, lessons = []) {
  return lessons
    .filter((lesson) => lesson.module_id === moduleId)
    .sort((a, b) => (Number(a.sort_order || 0) - Number(b.sort_order || 0)) || String(a.created_at || "").localeCompare(String(b.created_at || "")));
}

function resolveLessonPlayerView(lesson, requestedView = "lesson") {
  if (requestedView && requestedView !== "auto") return requestedView;
  return ["practice", "assessment"].includes(lesson?.lesson_type) ? "practice" : "lesson";
}

function getLessonFile(lessonId) {
  return teacherState.lessonFiles.find((file) => file.lesson_id === lessonId) || null;
}

function moduleCategoryClass(category = "") {
  const value = category.toLowerCase();
  if (value.includes("productivity") || value.includes("spreadsheet")) return "green";
  if (value.includes("safety") || value.includes("online")) return "orange";
  if (value.includes("network")) return "purple";
  return "blue";
}

function iconForLesson(lesson, module) {
  if (lesson.lesson_type === "practice") return "puzzle";
  if (lesson.lesson_type === "assessment") return "clipboard-check";
  const category = `${module?.category || ""} ${lesson.title || ""}`.toLowerCase();
  if (category.includes("spreadsheet")) return "table-2";
  if (category.includes("network")) return "git-fork";
  if (category.includes("safety")) return "shield";
  if (category.includes("productivity")) return "bar-chart-3";
  return "cpu";
}

function fileIcon(mimeType) {
  if (mimeType === "video/mp4") return "video";
  if (mimeType.includes("presentation")) return "file-sliders";
  if (mimeType.includes("wordprocessing")) return "file-text";
  return "file-type";
}

function fileIconClass(mimeType) {
  if (mimeType === "video/mp4") return "purple";
  if (mimeType.includes("presentation")) return "orange";
  if (mimeType.includes("wordprocessing")) return "blue";
  return "red";
}

function fileLabel(file) {
  const ext = file.original_filename.split(".").pop()?.toUpperCase() || "FILE";
  return `${ext} - ${formatBytes(file.size_bytes)}`;
}

function formatBytes(value) {
  const bytes = Number(value || 0);
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(bytes >= 10 * 1024 * 1024 ? 0 : 1)} MB`;
  if (bytes >= 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${bytes} B`;
}

function relativeTime(value) {
  if (!value) return "-";
  const seconds = Math.max(1, Math.round((Date.now() - new Date(value).getTime()) / 1000));
  if (seconds < 60) return "just now";
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} hours ago`;
  const days = Math.round(hours / 24);
  return `${days} day${days === 1 ? "" : "s"} ago`;
}

function shiftMonth(value, amount) {
  const [year, month] = value.split("-").map(Number);
  const date = new Date(year, month - 1 + amount, 1);
  return date.toISOString().slice(0, 7);
}

function escapeHtml(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

function escapeAttribute(value) {
  return escapeHtml(value);
}
