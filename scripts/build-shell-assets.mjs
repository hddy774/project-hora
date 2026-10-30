#!/usr/bin/env node
/**
 * Offline Android API 24 / Chromium 51 shell asset.
 * Setup: npm ci --prefix scripts/shell-assets --ignore-scripts --no-audit --no-fund
 * Build: node scripts/build-shell-assets.mjs
 * Verify committed bytes: node scripts/build-shell-assets.mjs --check
 * VERSION remains the application-version authority; this tooling only records compiler provenance.
 */
import {readFile, writeFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
import esbuild from './shell-assets/node_modules/esbuild/lib/main.js';
import * as acorn from './shell-assets/node_modules/acorn/dist/acorn.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const sourcePath = path.join(root, 'src/Chronicles.Shell/index.html');
const outputPath = path.join(root, 'src/Chronicles.Shell/index.compat.html');
const normalize = text => text.replace(/\r\n?/g, '\n');
const sha = text => createHash('sha256').update(text, 'utf8').digest('hex');
const manifest = JSON.parse(await readFile(path.join(root, 'scripts/shell-assets/package.json'), 'utf8'));
const pinned = manifest.devDependencies.esbuild;
if (!/^\d+\.\d+\.\d+$/.test(pinned) || esbuild.version !== pinned) {
  throw new Error(`Install the exact locked esbuild compiler (${pinned}).`);
}
const [source, polyfills, fallbackCss] = await Promise.all([
  readFile(sourcePath, 'utf8').then(normalize),
  readFile(path.join(root, 'scripts/build-shell-assets.polyfills.js'), 'utf8').then(normalize),
  readFile(path.join(root, 'scripts/build-shell-assets.compat.css'), 'utf8').then(normalize)
]);
acorn.parse(polyfills, {ecmaVersion: 5, sourceType: 'script'});
let blocks = 0;
const pattern = /<script\b([^>]*)>([\s\S]*?)<\/script\s*>/gi;
const replacements = [];
for (const match of source.matchAll(pattern)) {
  if (/\bsrc\s*=|\btype\s*=\s*["']?module/i.test(match[1])) throw new Error('Only inline classic authoring scripts are supported.');
  const compiled = await esbuild.transform(match[2], {
    loader: 'js', target: 'chrome51', charset: 'utf8', legalComments: 'none',
    sourcemap: false, minify: false, supported: {'inline-script': false}
  });
  // The targeted compiler also guarantees Chromium-specific lowerings such as exponentiation.
  // Parsing as ES2016 catches accidental modern syntax in the delivered script independently.
  acorn.parse(compiled.code, {ecmaVersion: 2016, sourceType: 'script'});
  if (/<\/script/i.test(compiled.code)) throw new Error('The compiled script contains an unsafe HTML terminator.');
  replacements.push({start: match.index, end: match.index + match[0].length,
    text: `<script>\n${blocks++ === 0 ? polyfills + '\n' : ''}${compiled.code}</script>`});
}
if (blocks !== 1) throw new Error('Expected one classic authoring script; review bootstrap ordering before changing it.');
let output = source;
for (const replacement of replacements.reverse()) output = output.slice(0, replacement.start) + replacement.text + output.slice(replacement.end);
if (!output.includes('</head>')) throw new Error('The authoring document has no head boundary.');
output = output.replace('</head>', `<style>\n${fallbackCss}</style>\n</head>`);
const marker = `<!-- seonro-compat source-sha256-lf=${sha(source)} shims-sha256-lf=${sha(polyfills)} css-sha256-lf=${sha(fallbackCss)} compiler=esbuild@${esbuild.version} target=chrome51 -->`;
output = output.replace('<!doctype html>', `<!doctype html>\n${marker}`);
if (process.argv.includes('--check')) {
  let committed;
  try { committed = normalize(await readFile(outputPath, 'utf8')); }
  catch { throw new Error('Committed compatibility asset is missing. Run the build command and commit it.'); }
  if (committed !== output) throw new Error('Compatibility asset is stale. Rebuild it from the authoring source and commit it.');
  process.stdout.write(`Verified offline chrome51 asset; authoring SHA256 ${sha(source)}\n`);
} else {
  await writeFile(outputPath, output, 'utf8');
  process.stdout.write(`Built offline chrome51 asset; authoring SHA256 ${sha(source)}\n`);
}
