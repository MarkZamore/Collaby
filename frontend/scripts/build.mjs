import { spawn } from 'node:child_process';

const resolveBuildEnv = (env = process.env) => {
  const builtAt = env.VITE_BUILD_TIMESTAMP || new Date().toISOString();
  const buildId = env.VITE_BUILD_ID || env.GITHUB_SHA || `local-${Date.now()}`;
  const commitSha = env.VITE_COMMIT_SHA || env.GITHUB_SHA || 'local';
  const refName = env.VITE_REF_NAME || env.GITHUB_REF_NAME || 'local';

  return {
    ...env,
    VITE_BUILD_TIMESTAMP: builtAt,
    VITE_BUILD_ID: buildId,
    VITE_COMMIT_SHA: commitSha,
    VITE_REF_NAME: refName,
  };
};

const run = (command, args, env) =>
  new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd: process.cwd(),
      env,
      shell: process.platform === 'win32',
      stdio: 'inherit',
    });

    child.on('exit', (code) => {
      if (code === 0) {
        resolve();
        return;
      }

      reject(new Error(`${command} ${args.join(' ')} exited with code ${code ?? 'null'}`));
    });

    child.on('error', reject);
  });

const buildEnv = resolveBuildEnv();

await run('node', ['./scripts/write-build-meta.mjs'], buildEnv);
await run('vite', ['build'], buildEnv);
