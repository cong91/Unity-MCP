import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { runCliAsync } from './helpers/cli.js';

describe('setup-mcp hermes-agent', () => {
  let tmpDir: string;
  let tmpHome: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'unity-mcp-hermes-project-'));
    tmpHome = fs.mkdtempSync(path.join(os.tmpdir(), 'unity-mcp-hermes-home-'));

    fs.mkdirSync(path.join(tmpDir, 'Assets'), { recursive: true });
    fs.mkdirSync(path.join(tmpDir, 'UserSettings'), { recursive: true });
    fs.writeFileSync(
      path.join(tmpDir, 'UserSettings', 'AI-Game-Developer-Config.json'),
      JSON.stringify(
        {
          host: 'http://localhost:24446',
          timeoutMs: 10000,
          authOption: 'none',
          connectionMode: 'Custom',
        },
        null,
        2,
      ) + '\n',
    );
  });

  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
    fs.rmSync(tmpHome, { recursive: true, force: true });
  });

  it('--list includes hermes-agent', async () => {
    const { stdout, exitCode } = await runCliAsync(['setup-mcp', '--list']);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('hermes-agent');
    expect(stdout).toContain('~/.hermes/config.yaml');
  });

  it('writes Hermes YAML config for stdio transport', async () => {
    const env = { HOME: tmpHome, USERPROFILE: tmpHome };
    const { stdout, exitCode } = await runCliAsync(
      ['setup-mcp', 'hermes-agent', tmpDir, '--transport', 'stdio'],
      env,
    );

    expect(exitCode).toBe(0);
    expect(stdout).toContain('Hermes Agent configured successfully');

    const configPath = path.join(tmpHome, '.hermes', 'config.yaml');
    expect(fs.existsSync(configPath)).toBe(true);

    const yaml = fs.readFileSync(configPath, 'utf-8');
    expect(yaml).toContain('mcp_servers:');
    expect(yaml).toContain('ai-game-developer:');
    expect(yaml).toContain('command:');
    expect(yaml).toContain('client-transport=stdio');
    expect(yaml).toContain('skills:');
    expect(yaml).toContain('external_dirs:');
    expect(yaml).toContain(path.join(tmpDir, '.hermes', 'skills').replace(/\\/g, '/'));
  });
});
