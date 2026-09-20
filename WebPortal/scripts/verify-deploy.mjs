import { readFile } from "node:fs/promises";

const productionUrl = String(
  process.env.TECHWISE_DEPLOY_URL || "https://techwise360-web-portal.pages.dev",
).replace(/\/+$/, "");

const filesToVerify = [
  "index.html",
  "create-account.html",
  "auth.css",
  "teacher-dashboard.html",
  "teacher-dashboard.css",
  "student-dashboard.html",
  "student-dashboard.css",
  "vr-connect.html",
  "theme.css",
  "app.js",
];

const wait = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds));

async function compareFile(filename) {
  const localUrl = new URL(`../${filename}`, import.meta.url);
  const localContent = await readFile(localUrl);
  const remoteUrl = `${productionUrl}/${filename}?deploy-check=${Date.now()}`;
  const response = await fetch(remoteUrl, {
    cache: "no-store",
    headers: {
      "Cache-Control": "no-cache",
    },
    redirect: "follow",
  });

  if (!response.ok) {
    throw new Error(`${filename} returned HTTP ${response.status}`);
  }

  const remoteContent = Buffer.from(await response.arrayBuffer());
  return localContent.equals(remoteContent);
}

let lastMismatches = [];

for (let attempt = 1; attempt <= 6; attempt += 1) {
  const results = await Promise.all(
    filesToVerify.map(async (filename) => ({
      filename,
      matches: await compareFile(filename),
    })),
  );

  lastMismatches = results.filter(({ matches }) => !matches).map(({ filename }) => filename);

  if (lastMismatches.length === 0) {
    console.log(`[deploy:verify] Production matches local files at ${productionUrl}`);
    process.exit(0);
  }

  if (attempt < 6) {
    console.log(`[deploy:verify] Waiting for Cloudflare propagation (attempt ${attempt}/6)...`);
    await wait(2000);
  }
}

throw new Error(
  `Cloudflare production does not match the local deployment: ${lastMismatches.join(", ")}`,
);
