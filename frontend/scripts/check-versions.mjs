#!/usr/bin/env node
/**
 * Verifies that the version field is consistent across all three version sources:
 *   - frontend/package.json
 *   - frontend/src-tauri/tauri.conf.json
 *   - backend/HarborTorrent.Api/HarborTorrent.Api.csproj
 *
 * Exits with a non-zero code if any version disagrees, causing the CI build to fail.
 */

import { readFileSync } from 'fs';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';
import { DOMParser } from '@xmldom/xmldom';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = resolve(__dirname, '..');

// ── 1. package.json ──────────────────────────────────────────────────────────
const packageJson = JSON.parse(readFileSync(resolve(root, 'package.json'), 'utf8'));
const packageVersion = packageJson.version;

// ── 2. tauri.conf.json ───────────────────────────────────────────────────────
const tauriConf = JSON.parse(readFileSync(resolve(root, 'src-tauri/tauri.conf.json'), 'utf8'));
const tauriVersion = tauriConf.version;

// ── 3. HarborTorrent.Api.csproj ─────────────────────────────────────────────
const csprojPath = resolve(root, '../backend/HarborTorrent.Api/HarborTorrent.Api.csproj');
const csprojContent = readFileSync(csprojPath, 'utf8');
const doc = new DOMParser().parseFromString(csprojContent, 'text/xml');
const versionNode = doc.getElementsByTagName('Version')[0];
const csprojVersion = versionNode?.textContent?.trim() ?? null;

// ── Report ────────────────────────────────────────────────────────────────────
console.log(`package.json   : ${packageVersion}`);
console.log(`tauri.conf.json: ${tauriVersion}`);
console.log(`HarborTorrent.Api.csproj: ${csprojVersion}`);

const versions = new Set([packageVersion, tauriVersion, csprojVersion]);

if (versions.size !== 1) {
  console.error('\nVersion mismatch detected. All three files must share the same version string.');
  process.exit(1);
}

console.log('\nAll versions match. ✓');
