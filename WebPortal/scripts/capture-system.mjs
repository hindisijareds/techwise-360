import { chromium } from "playwright";
import { readFile, writeFile, mkdir, readdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const projectDirectory = path.resolve(scriptDirectory, "..");

await loadEnvironmentFile(path.join(projectDirectory, "capture.env"));
const pdfOnly = process.argv.includes("--pdf-only");

const configuration = {
  baseUrl: String(process.env.TECHWISE_BASE_URL || "https://techwise360-web-portal.pages.dev").replace(/\/+$/, ""),
  teacherIdentifier: String(process.env.TECHWISE_TEACHER_IDENTIFIER || "").trim(),
  teacherPassword: String(process.env.TECHWISE_TEACHER_PASSWORD || ""),
  studentIdentifier: String(process.env.TECHWISE_STUDENT_IDENTIFIER || "").trim(),
  studentPassword: String(process.env.TECHWISE_STUDENT_PASSWORD || ""),
  publicOnly: String(process.env.TECHWISE_PUBLIC_ONLY || "").toLowerCase() === "true",
  browserChannel: String(process.env.TECHWISE_BROWSER_CHANNEL || "").trim(),
  viewport: {
    width: positiveInteger(process.env.TECHWISE_VIEWPORT_WIDTH, 1600),
    height: positiveInteger(process.env.TECHWISE_VIEWPORT_HEIGHT, 1000)
  }
};

const missingCredentials = [
  ["TECHWISE_TEACHER_IDENTIFIER", configuration.teacherIdentifier],
  ["TECHWISE_TEACHER_PASSWORD", configuration.teacherPassword],
  ["TECHWISE_STUDENT_IDENTIFIER", configuration.studentIdentifier],
  ["TECHWISE_STUDENT_PASSWORD", configuration.studentPassword]
].filter(([, value]) => !value).map(([name]) => name);

if (missingCredentials.length && !configuration.publicOnly && !pdfOnly) {
  throw new Error(`Complete WebPortal/capture.env before capturing. Missing: ${missingCredentials.join(", ")}`);
}

const runStamp = new Date().toISOString().replace(/[:.]/g, "-");
const pdfOnlyArgumentIndex = process.argv.indexOf("--pdf-only") + 1;
const pdfOnlyDirectory = pdfOnlyArgumentIndex > 0 ? process.argv[pdfOnlyArgumentIndex] : "";
const outputDirectory = pdfOnly
  ? await findCaptureDirectory(pdfOnlyDirectory)
  : path.resolve(projectDirectory, process.env.TECHWISE_SCREENSHOT_DIR || path.join("artifacts", "system-screenshots", runStamp));
const pdfOutputDirectory = path.join(projectDirectory, "output", "pdf");
const vrScreenshotDirectory = path.join(projectDirectory, "artifacts", "vr-screenshots");
await mkdir(outputDirectory, { recursive: true });
await mkdir(pdfOutputDirectory, { recursive: true });

const browser = await launchBrowser();
const records = [];

try {
  if (pdfOnly) {
    const manifest = JSON.parse(await readFile(path.join(outputDirectory, "manifest.json"), "utf8"));
    records.push(...(manifest.screenshots || []));
  } else {
    await capturePublicPages();
    if (!configuration.publicOnly) {
      await captureRole({
        role: "teacher",
        identifier: configuration.teacherIdentifier,
        password: configuration.teacherPassword,
        dashboardPath: "teacher-dashboard.html",
        viewAttribute: "data-teacher-view",
        sectionSelector: ".teacher-section",
        extraCaptures: captureTeacherAchievementTabs
      });
      await captureRole({
        role: "student",
        identifier: configuration.studentIdentifier,
        password: configuration.studentPassword,
        dashboardPath: "student-dashboard.html",
        viewAttribute: "data-student-view",
        sectionSelector: ".student-section",
        extraCaptures: captureStudentAchievementTabs
      });
    }
    await writeFile(path.join(outputDirectory, "manifest.json"), JSON.stringify({
      generatedAt: new Date().toISOString(),
      baseUrl: configuration.baseUrl,
      viewport: configuration.viewport,
      screenshots: records
    }, null, 2));
  }
  await createSoftcopyReport();
} finally {
  await browser.close();
}

console.log(`${pdfOnly ? "Included" : "Captured"} ${records.length} system screens.`);
console.log(`Screenshots: ${outputDirectory}`);
console.log(`PDF softcopy: ${path.join(pdfOutputDirectory, "TechWise360-System-Softcopy.pdf")}`);

async function launchBrowser() {
  const options = { headless: true };
  if (configuration.browserChannel) options.channel = configuration.browserChannel;
  try {
    return await chromium.launch(options);
  } catch (error) {
    if (configuration.browserChannel || process.platform !== "win32") throw error;
    console.warn("Playwright Chromium is unavailable; using the installed Microsoft Edge browser.");
    return chromium.launch({ ...options, channel: "msedge" });
  }
}

async function findCaptureDirectory(requestedDirectory) {
  if (requestedDirectory) return path.resolve(projectDirectory, requestedDirectory);
  const captureRoot = path.join(projectDirectory, "artifacts", "system-screenshots");
  const directories = (await readdir(captureRoot, { withFileTypes: true }))
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort((left, right) => right.localeCompare(left));
  for (const directory of directories) {
    const candidate = path.join(captureRoot, directory);
    try {
      await readFile(path.join(candidate, "manifest.json"), "utf8");
      return candidate;
    } catch {
      // Continue until a completed capture with a manifest is found.
    }
  }
  throw new Error("No completed screenshot run was found. Run npm run screenshots first.");
}

async function createPage(context) {
  const page = await context.newPage();
  await page.emulateMedia({ reducedMotion: "reduce", colorScheme: "light" });
  page.setDefaultTimeout(20000);
  return page;
}

async function capturePublicPages() {
  const context = await browser.newContext({ viewport: configuration.viewport, deviceScaleFactor: 1 });
  const page = await createPage(context);
  const publicPages = [
    ["login", "index.html"],
    ["create-account", "create-account.html"],
    ["forgot-password", "forgot-password.html"]
  ];

  for (let index = 0; index < publicPages.length; index += 1) {
    const [name, target] = publicPages[index];
    await page.goto(`${configuration.baseUrl}/${target}`, { waitUntil: "domcontentloaded" });
    await settlePage(page);
    await saveScreenshot(page, "public", `${pad(index + 1)}-${name}`, `Public - ${titleFromSlug(name)}`);
  }
  await context.close();
}

async function captureRole({ role, identifier, password, dashboardPath, viewAttribute, sectionSelector, extraCaptures }) {
  const context = await browser.newContext({ viewport: configuration.viewport, deviceScaleFactor: 1 });
  const page = await createPage(context);
  await login(page, role, identifier, password, dashboardPath);

  const navigationSelector = `[${viewAttribute}]`;
  const views = await page.locator(navigationSelector).evaluateAll((buttons, attribute) =>
    [...new Set(buttons.map((button) => button.getAttribute(attribute)).filter(Boolean))],
  viewAttribute);

  for (let index = 0; index < views.length; index += 1) {
    const view = views[index];
    await openView(page, navigationSelector, viewAttribute, sectionSelector, view);
    await saveScreenshot(page, role, `${pad(index + 1)}-${view}`, `${titleFromSlug(role)} - ${titleFromSlug(view)}`);
  }

  await extraCaptures(page, views.length + 1);
  await context.close();
}

async function login(page, role, identifier, password, dashboardPath) {
  const expectedDashboardPath = `/${dashboardPath.replace(/\.html$/, "")}`;
  await page.goto(`${configuration.baseUrl}/index.html`, { waitUntil: "domcontentloaded" });
  await page.locator('input[name="identifier"]').fill(identifier);
  await page.locator('input[name="password"]').fill(password);
  await Promise.all([
    page.waitForURL((url) => url.pathname.replace(/\/$/, "").replace(/\.html$/, "") === expectedDashboardPath, { timeout: 30000 }),
    page.locator('#loginForm button[type="submit"]').click()
  ]).catch(async (error) => {
    const message = (await page.locator("#loginMessage").textContent().catch(() => ""))?.trim();
    throw new Error(`${titleFromSlug(role)} login failed${message ? `: ${message}` : "."}`, { cause: error });
  });
  await page.locator(`body[data-page="${role}"]`).waitFor({ state: "visible" });
  await settlePage(page, 1500);
}

async function openView(page, navigationSelector, viewAttribute, sectionSelector, view) {
  const button = page.locator(`${navigationSelector}[${viewAttribute}="${view}"]`).first();
  await button.click();
  await page.locator(`${sectionSelector}[data-view="${view}"].active`).waitFor({ state: "visible" });
  await settlePage(page);
}

async function captureTeacherAchievementTabs(page, startingIndex) {
  await openView(page, "[data-teacher-view]", "data-teacher-view", ".teacher-section", "achievements");
  const tabs = ["tools", "students", "history"];
  for (let index = 0; index < tabs.length; index += 1) {
    const tab = tabs[index];
    await page.locator(`[data-achievement-teacher-tab="${tab}"]`).first().click();
    await page.locator(`[data-achievement-teacher-tab="${tab}"].active`).waitFor({ state: "visible" });
    await settlePage(page);
    await saveScreenshot(page, "teacher", `${pad(startingIndex + index)}-achievements-${tab}`, `Teacher - Achievements - ${titleFromSlug(tab)}`);
  }
}

async function captureStudentAchievementTabs(page, startingIndex) {
  await openView(page, "[data-student-view]", "data-student-view", ".student-section", "achievements");
  const tabs = ["certificates"];
  let captureIndex = startingIndex;
  for (let index = 0; index < tabs.length; index += 1) {
    const tab = tabs[index];
    await page.locator(`[data-achievement-tab="${tab}"]`).first().click();
    await page.locator(`[data-achievement-tab="${tab}"].active`).waitFor({ state: "visible" });
    await settlePage(page);
    await saveScreenshot(page, "student", `${pad(captureIndex)}-achievements-${tab}`, `Student - Achievements - ${titleFromSlug(tab)}`);
    captureIndex += 1;
  }

  await page.evaluate(() => window.showStudentView?.("evaluation"));
  await page.locator('.student-section[data-view="evaluation"].active').waitFor({ state: "visible" });
  await settlePage(page);
  await saveScreenshot(page, "student", `${pad(captureIndex)}-evaluation-contextual`, "Student - Evaluation - Contextual Survey");
  captureIndex += 1;

  await openView(page, "[data-student-view]", "data-student-view", ".student-section", "lessons");
  const firstLesson = page.locator('[data-open-student-lesson]:not([disabled])').first();
  if (await firstLesson.count()) {
    await firstLesson.click();
    await page.locator('.student-section[data-view="lesson-player"].active').waitFor({ state: "visible" });
    await settlePage(page, 1200);
    await saveScreenshot(page, "student", `${pad(captureIndex)}-lesson-player`, "Student - Lesson Player");
    captureIndex += 1;

    const practicePart = page.locator('.lesson-sequence-list button:not([disabled])').filter({ hasText: /Practice|Assessment/i }).first();
    if (await practicePart.count()) {
      await practicePart.click();
      await settlePage(page, 1000);
      await saveScreenshot(page, "student", `${pad(captureIndex)}-lesson-practice`, "Student - Lesson Player - Practice");
      captureIndex += 1;
    }
  }

  await openView(page, "[data-student-view]", "data-student-view", ".student-section", "home");
  const collapseToggle = page.locator("#studentSidebarToggle");
  if (await collapseToggle.count()) {
    await collapseToggle.click();
    await settlePage(page, 500);
    await saveScreenshot(page, "student", `${pad(captureIndex)}-home-sidebar-collapsed`, "Student - Home - Collapsed Sidebar");
    captureIndex += 1;
  }

  const responsiveViewports = [
    [1024, 900, "tablet"],
    [768, 900, "narrow-tablet"],
    [390, 844, "mobile"]
  ];
  for (const [width, height, label] of responsiveViewports) {
    await page.setViewportSize({ width, height });
    await settlePage(page, 400);
    await saveScreenshot(page, "student", `${pad(captureIndex)}-home-${label}`, `Student - Home - ${titleFromSlug(label)}`);
    captureIndex += 1;
  }
}

async function settlePage(page, delay = 800) {
  await page.evaluate(() => document.fonts?.ready).catch(() => {});
  await page.waitForTimeout(delay);
  await page.addStyleTag({ content: `
    *, *::before, *::after { animation: none !important; transition: none !important; caret-color: transparent !important; }
    .notification-panel, .modal-overlay, .drawer-overlay { visibility: hidden !important; }
  ` }).catch(() => {});
  await page.evaluate(() => window.scrollTo(0, 0));
}

async function saveScreenshot(page, group, filename, title) {
  const groupDirectory = path.join(outputDirectory, group);
  await mkdir(groupDirectory, { recursive: true });
  const relativePath = path.posix.join(group, `${filename}.png`);
  await page.screenshot({
    path: path.join(outputDirectory, ...relativePath.split("/")),
    fullPage: true,
    animations: "disabled"
  });
  records.push({ title, file: relativePath });
  console.log(`Captured ${title}`);
}

async function createSoftcopyReport() {
  const context = await browser.newContext({ viewport: { width: 1240, height: 1754 } });
  const page = await createPage(context);
  const figures = [];
  const reportRecords = [...records, ...(await loadVrScreenshotRecords())];
  for (const record of reportRecords) {
    const imagePath = record.sourcePath || path.join(outputDirectory, ...record.file.split("/"));
    const bytes = await readFile(imagePath);
    figures.push(`<figure><h2>${escapeHtml(record.title)}</h2><img src="data:image/png;base64,${bytes.toString("base64")}" alt="${escapeHtml(record.title)}"></figure>`);
  }
  await page.setContent(`<!doctype html><html><head><meta charset="utf-8"><style>
    @page { size: A4 landscape; margin: 10mm; }
    * { box-sizing: border-box; }
    body { margin: 0; color: #172033; font-family: Arial, sans-serif; }
    figure { min-height: 180mm; margin: 0; display: grid; grid-template-rows: auto 1fr; gap: 5mm; break-after: page; page-break-after: always; }
    figure:last-child { break-after: auto; page-break-after: auto; }
    h2 { margin: 0; font-size: 17px; }
    img { width: 100%; height: 170mm; object-fit: contain; object-position: top center; border: 1px solid #dce6f3; }
  </style></head><body>
    ${figures.join("\n")}
  </body></html>`, { waitUntil: "load" });
  await page.pdf({
    path: path.join(pdfOutputDirectory, "TechWise360-System-Softcopy.pdf"),
    format: "A4",
    landscape: true,
    printBackground: true,
    margin: { top: "10mm", right: "10mm", bottom: "10mm", left: "10mm" }
  });
  await context.close();
}

async function loadVrScreenshotRecords() {
  const screenshots = [
    ["VR - Main Menu", "01-vr-main-menu.png"],
    ["VR - Real Life Simulation", "02-vr-real-life-simulation.png"],
    ["VR - Knowledge Corner", "03-vr-knowledge-corner.png"]
  ];
  const available = [];
  for (const [title, filename] of screenshots) {
    const sourcePath = path.join(vrScreenshotDirectory, filename);
    try {
      await readFile(sourcePath);
      available.push({ title, sourcePath });
    } catch (error) {
      if (error.code !== "ENOENT") throw error;
    }
  }
  return available;
}

async function loadEnvironmentFile(filename) {
  let contents = "";
  try {
    contents = await readFile(filename, "utf8");
  } catch (error) {
    if (error.code === "ENOENT") return;
    throw error;
  }
  for (const line of contents.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) continue;
    const separator = trimmed.indexOf("=");
    if (separator < 1) continue;
    const key = trimmed.slice(0, separator).trim();
    let value = trimmed.slice(separator + 1).trim();
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }
    if (process.env[key] === undefined) process.env[key] = value;
  }
}

function positiveInteger(value, fallback) {
  const number = Number(value);
  return Number.isInteger(number) && number > 0 ? number : fallback;
}

function pad(value) {
  return String(value).padStart(2, "0");
}

function titleFromSlug(value) {
  return String(value).replace(/[-_]/g, " ").replace(/\b\w/g, (letter) => letter.toUpperCase());
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}
