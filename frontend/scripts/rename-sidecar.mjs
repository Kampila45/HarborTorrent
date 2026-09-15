#!/usr/bin/env node
/**
 * Renames the published .NET API binary to match the Tauri sidecar naming convention.
 *
 * Tauri sidecar docs: https://tauri.app/develop/sidecar/
 * The binary must be named: `HarborTorrent.Api-<target-triple>[.exe]`
 * e.g. `HarborTorrent.Api-x86_64-unknown-linux-gnu`
 *      `HarborTorrent.Api-x86_64-pc-windows-msvc.exe`
 *
 * Usage (run from the HarborTorrent-Frontend directory):
 *   node scripts/rename-sidecar.mjs
 */

import { execSync } from 'child_process';
import fs from 'fs';
import path from 'path';

const extension = process.platform === 'win32' ? '.exe' : '';
const targetTriple = execSync('rustc --print host-tuple').toString().trim();

if (!targetTriple) {
  console.error('[rename-sidecar] ERROR: Failed to determine platform target triple.');
  process.exit(1);
}

const binariesDir = path.join('src-tauri', 'binaries');
const sourcePath = path.join(binariesDir, `HarborTorrent.Api${extension}`);
const destPath = path.join(binariesDir, `HarborTorrent.Api-${targetTriple}${extension}`);

if (!fs.existsSync(sourcePath)) {
  console.error(`[rename-sidecar] ERROR: Source binary not found at: ${sourcePath}`);
  console.error('[rename-sidecar] Make sure you have published the .NET API first:');
  console.error('  dotnet publish ../backend/HarborTorrent.Api \\');
  console.error('    --runtime linux-x64 --self-contained true \\');
  console.error('    -p:PublishSingleFile=true -o src-tauri/binaries/');
  process.exit(1);
}

fs.copyFileSync(sourcePath, destPath);
if (extension === '') {
  fs.chmodSync(destPath, '755');
}

console.log(`[rename-sidecar] ✓ Renamed to: ${destPath}`);
console.log(`[rename-sidecar]   Target triple: ${targetTriple}`);
