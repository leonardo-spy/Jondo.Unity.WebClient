import { createHash, randomUUID } from 'node:crypto';
import { createReadStream, createWriteStream } from 'node:fs';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import path from 'node:path';
import { parseArgs } from 'node:util';
import { pathToFileURL } from 'node:url';

const { values } = parseArgs({ options: {
  'package-root': { type: 'string' },
  audit: { type: 'string', default: 'Generated/output/DecompilationCoverage.json' },
  output: { type: 'string', default: 'Generated/output' },
  'check-only': { type: 'boolean', default: false },
  'java-heap': { type: 'string', default: '8G' },
} });
if (!values['package-root']) throw new Error('--package-root is required');
if (!/^(?:[1-9]|1[0-6])G$/.test(values['java-heap'])) throw new Error('--java-heap must be 1G through 16G');
const packageRoot = path.resolve(values['package-root']);
const requireFromRea = createRequire(path.join(packageRoot, 'package.json'));
const { Client } = await import(pathToFileURL(requireFromRea.resolve('@modelcontextprotocol/client')).href);
const { StdioClientTransport } = await import(pathToFileURL(requireFromRea.resolve('@modelcontextprotocol/client/stdio')).href);
const packageInfo = JSON.parse(await readFile(path.join(packageRoot, 'package.json'), 'utf8'));
if (packageInfo.name !== 'rea-agents' || packageInfo.version !== '6.8.0') throw new Error('Expected installed REA 6.8.0');

async function digest(file) {
  const hash = createHash('sha256');
  for await (const chunk of createReadStream(file)) hash.update(chunk);
  return hash.digest('hex');
}
async function save(file, value) {
  await writeFile(file, `${JSON.stringify(value, null, 2)}\n`, 'utf8');
}
function evidenceFrom(value, operation, depth = 0) {
  if (!value || typeof value !== 'object' || depth > 5) return null;
  if (value.operation === operation && typeof value.evidence_id === 'string') return value;
  for (const child of Object.values(value)) {
    const found = evidenceFrom(child, operation, depth + 1);
    if (found) return found;
  }
  return null;
}
function toolEvidence(response, operation) {
  const structured = evidenceFrom(response.structuredContent, operation);
  if (structured) return structured;
  for (const content of response.content ?? []) {
    if (content.type !== 'text') continue;
    try {
      const found = evidenceFrom(JSON.parse(content.text), operation);
      if (found) return found;
    } catch { }
  }
  return null;
}

const outputRoot = path.resolve(values.output);
await mkdir(outputRoot, { recursive: true });
const runDirectory = path.join(outputRoot, `decompilation-recovery-${randomUUID()}`);
await mkdir(runDirectory);
const config = JSON.parse(await readFile(path.join(process.env.USERPROFILE, '.claude.json'), 'utf8'));
const environment = { ...process.env };
for (const name of ['GHIDRA_INSTALL_DIR', 'JAVA_HOME', 'REA_GHIDRA_STARTUP_TIMEOUT_MS', 'REA_MCP_MAX_RESPONSE_BYTES']) {
  if (config.mcpServers?.rea?.env?.[name] !== undefined) environment[name] = String(config.mcpServers.rea.env[name]);
}
environment.NODE_OPTIONS = '--max-old-space-size=8192';
environment.REA_GHIDRA_STARTUP_TIMEOUT_MS = '18000000';
environment.REA_GHIDRA_DECOMPILE_TIMEOUT_SECONDS = '900';
environment.REA_MCP_MAX_RESPONSE_BYTES = '536870912';
environment.GHIDRA_HEADLESS_MAXMEM = values['java-heap'];
const launcher = path.join(packageRoot, 'scripts', 'rea.mjs');
const bridge = path.join(packageRoot, 'bridge', 'ghidra', 'ReaGhidraBridge.java');
const transport = new StdioClientTransport({
  command: process.execPath, args: [launcher, '--mcp'], env: environment,
  cwd: process.cwd(), stderr: 'pipe', maxBufferSize: 536870912,
});
const stderr = createWriteStream(path.join(runDirectory, 'stderr.log'));
transport.stderr?.pipe(stderr);
const client = new Client({ name: 'jondo-decompilation-recovery', version: '1' }, { capabilities: {} });
const journal = {
  schemaVersion: 1, status: 'starting', startedAt: new Date().toISOString(),
  packageVersion: packageInfo.version, launcher, launcherSha256: await digest(launcher),
  bridge, bridgeSha256: await digest(bridge), checkOnly: values['check-only'], results: [],
  limits: { ghidraStartupMs: 18000000, decompileSeconds: 900, nodeHeapMiB: 8192,
    javaHeap: values['java-heap'], transportBytes: 536870912 },
};
const journalPath = path.join(runDirectory, 'run.json');
let targetOpened = false;
await save(journalPath, journal);
console.log(`REA batch journal: ${journalPath}`);

try {
  await client.connect(transport, { timeout: 60000 });
  const catalog = await client.listTools();
  const names = ['open_binary', 'procedure_pseudo_code', 'export_evidence_bundle', 'binary_session', 'close_binary'];
  const contracts = Object.fromEntries(names.map(name => {
    const tool = catalog.tools.find(item => item.name === name);
    if (!tool) throw new Error(`REA does not advertise ${name}`);
    return [name, tool.inputSchema];
  }));
  await save(path.join(runDirectory, 'tool-contracts.json'), contracts);
  if (values['check-only']) {
    journal.status = 'ready';
    console.log(JSON.stringify({ status: 'ready', contracts }, null, 2));
  } else {
    const audit = JSON.parse(await readFile(path.resolve(values.audit), 'utf8'));
    const binary = audit.metadata?.binary;
    if (!binary?.path || !binary.sha256) throw new Error('Audit must include retained metadata/binary identity');
    if (await digest(binary.path) !== binary.sha256) throw new Error('Binary changed since the audit');
    if (await digest(audit.source.path) !== audit.source.sha256) throw new Error('Export changed since the audit');
    const bridgeText = await readFile(bridge, 'utf8');
    if (!bridgeText.includes('options.setMaxPayloadMBytes(512);')) throw new Error('Decompiler payload patch is not applied');
    const targets = [...(audit.recovery?.remainingFailures ?? audit.failedFunctions)].sort((left, right) => {
      const priority = item => item.symbol.includes('Core.Engine.Compositor.GameCompositor$$bkqa') ? 0 : 1;
      return priority(left) - priority(right) || Number(BigInt(left.address) - BigInt(right.address));
    });
    if (!targets.length || new Set(targets.map(item => item.address)).size !== targets.length) throw new Error('Audit failure targets are empty or ambiguous');
    journal.audit = { path: path.resolve(values.audit), sha256: await digest(path.resolve(values.audit)) };
    journal.binary = binary;
    journal.targets = targets;
    journal.status = 'opening';
    await save(journalPath, journal);
    const opened = await client.callTool({ name: 'open_binary', arguments: { path: binary.path, provider_id: 'ghidra' } }, { timeout: 18060000 });
    await save(path.join(runDirectory, 'open-result.json'), opened);
    if (opened.isError) throw new Error('REA could not open the selected binary; see open-result.json');
    targetOpened = true;
    const openingExport = await client.callTool({ name: 'export_evidence_bundle', arguments: { path: path.join(runDirectory, 'evidence-open.json') } }, { timeout: 18060000 });
    await save(path.join(runDirectory, 'export-open.json'), openingExport);
    if (openingExport.isError) throw new Error('Could not preserve opening Evidence');
    journal.status = 'recovering';
    await save(journalPath, journal);
    for (const [index, target] of targets.entries()) {
      journal.currentTarget = target;
      await save(journalPath, journal);
      const response = await client.callTool({ name: 'procedure_pseudo_code', arguments: { procedure: target.address } }, { timeout: 18060000 });
      const filename = `${String(index).padStart(3, '0')}-${target.address}.json`;
      await save(path.join(runDirectory, filename), response);
      const evidence = toolEvidence(response, 'procedure_pseudo_code');
      const outcome = { ...target, response: filename, status: response.isError ? 'failed' : 'tool-completed', evidenceId: evidence?.evidence_id ?? null };
      journal.results.push(outcome);
      await save(journalPath, journal);
      const bundlePath = path.join(runDirectory, `evidence-${String(index).padStart(3, '0')}.json`);
      const exported = await client.callTool({ name: 'export_evidence_bundle', arguments: { path: bundlePath } }, { timeout: 18060000 });
      await save(path.join(runDirectory, `export-${String(index).padStart(3, '0')}.json`), exported);
      if (exported.isError) throw new Error('Could not preserve session Evidence; stopping before more work');
      console.log(`${index + 1}/${targets.length}: ${target.address} ${outcome.status}`);
      if (response.isError) {
        const state = await client.callTool({ name: 'binary_session', arguments: {} }, { timeout: 60000 });
        await save(path.join(runDirectory, `session-after-${String(index).padStart(3, '0')}.json`), state);
        throw new Error('Pseudocode recovery failed; preserved Evidence and session state. Inspect before continuing.');
      }
    }
    journal.status = 'tools-completed';
    journal.limitations = ['Tool completion is not proof of correct pseudocode; inspect retained records and audit recovered bodies before claiming complete coverage.'];
  }
} catch (error) {
  journal.status = 'failed';
  journal.error = String(error?.stack ?? error);
  process.exitCode = 1;
} finally {
  if (targetOpened) {
    try {
      const closed = await client.callTool({ name: 'close_binary', arguments: {} }, { timeout: 300000 });
      await save(path.join(runDirectory, 'close-result.json'), closed);
      if (closed.isError) {
        journal.cleanupError = 'REA close_binary returned an error; inspect close-result.json';
        process.exitCode = 1;
      }
    } catch (error) { journal.cleanupError = String(error); process.exitCode = 1; }
  }
  try { await client.close(); } catch (error) { journal.cleanupError = String(error); process.exitCode = 1; }
  stderr.end();
  journal.finishedAt = new Date().toISOString();
  await save(journalPath, journal);
  console.log(JSON.stringify({ status: journal.status, journal: journalPath, results: journal.results.length, error: journal.error }, null, 2));
}