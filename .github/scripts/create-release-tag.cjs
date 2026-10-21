'use strict';

const { execFileSync } = require('node:child_process');
const path = require('node:path');
const selectReleaseBuild = require('./select-release-build.cjs');
const remote = 'https://github.com/stefanegli/PNFmt.git';
const repository = { owner: 'stefanegli', repo: 'PNFmt' };

// Public metadata only. Git owns authentication for the single tag push.
function publicGitHub(fetchPage = fetch) {
    const actions = { listWorkflowRuns: 'runs', listWorkflowRunArtifacts: 'artifacts' };
    return {
        rest: { actions },
        async paginate(method, options) {
            const endpoint = method === actions.listWorkflowRuns
                ? `actions/workflows/build.yml/runs?head_sha=${options.head_sha}&branch=master&event=push&status=success`
                : `actions/runs/${options.run_id}/artifacts`;
            const items = [];
            for (let page = 1; page <= 10; page++) {
                const separator = endpoint.includes('?') ? '&' : '?';
                const response = await fetchPage(`https://api.github.com/repos/stefanegli/PNFmt/${endpoint}${separator}per_page=100&page=${page}`, {
                    headers: { Accept: 'application/vnd.github+json', 'User-Agent': 'PNFmt-release-tag', 'X-GitHub-Api-Version': '2022-11-28' },
                    redirect: 'error', signal: AbortSignal.timeout(30000)
                });
                if (!response.ok) throw new Error(`GitHub release metadata request failed (HTTP ${response.status}). Retry after resolving access or rate limits.`);
                const body = await response.json();
                const batch = body[method === actions.listWorkflowRuns ? 'workflow_runs' : 'artifacts'];
                if (!Array.isArray(batch)) throw new Error('GitHub returned invalid release metadata.');
                items.push(...batch);
                if (batch.length < 100) return items;
            }
            throw new Error('GitHub release metadata exceeded the bounded page limit.');
        }
    };
}

function remoteTag(git, ref) {
    const rows = git(['ls-remote', remote, ref, `${ref}^{}`]).trim().split(/\r?\n/).filter(Boolean);
    // ls-remote prints <object> <ref>; retain both the tag object and peeled commit.
    return new Map(rows.map(row => { const [object, name] = row.split(/\s+/); return [name, object]; }));
}

async function createReleaseTag({ commit, version, buildNumber, git, github, checkOnly = false }) {
    if (!/^[0-9a-f]{40}$/.test(commit)) throw new Error('Invalid release commit.');
    if (!/^\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/.test(version)) throw new Error('Invalid release version.');
    if (!/^[1-9][0-9]*$/.test(String(buildNumber))) throw new Error('Invalid Forge build number.');
    if (git(['rev-parse', 'HEAD']).trim() !== commit) throw new Error('Checkout does not match the validated source commit.');
    if (git(['status', '--porcelain', '--untracked-files=no']).trim()) throw new Error('Release checkout has tracked changes.');
    const tag = `v${version}`;
    const ref = `refs/tags/${tag}`;
    const existing = remoteTag(git, ref);
    if (existing.size && (existing.get(`${ref}^{}`) !== commit || existing.get(ref) === commit)) {
        throw new Error(`Release tag ${tag} already exists with another identity. Choose a new version; tags are never moved.`);
    }
    const head = git(['ls-remote', '--refs', remote, 'refs/heads/master']).trim();
    if (head !== `${commit}\trefs/heads/master`) throw new Error('Release commit must be the current GitHub master commit. Push normal development changes and wait for Build first.');
    const build = await selectReleaseBuild({
        github, context: { repo: repository, ref, payload: { repository: { default_branch: 'master' } } }, commit
    });
    const receipt = { tag, commit, githubBuildRunId: build.runId, githubArtifactId: build.artifactId, githubBuildUrl: build.url };
    if (existing.size) return { ...receipt, status: 'already-exists' };
    if (checkOnly) return { ...receipt, status: 'checked' };

    // Fresh Forge checkouts have no user identity. Keep identity/signing overrides command-scoped.
    git(['-c', 'user.name=PNForge', '-c', 'user.email=pnforge@localhost', 'tag', '--annotate', '--no-sign',
        '--message', `Release ${version}\n\nValidated by PNForge build ${buildNumber}; GitHub Build ${build.runId}.`, tag, commit]);
    // An absent expected value enforces create-only semantics, including a concurrent tag creation.
    git(['-c', 'push.followTags=false', '-c', 'push.recurseSubmodules=no', 'push', '--porcelain',
        `--force-with-lease=${ref}:`, remote, `${ref}:${ref}`]);
    if (remoteTag(git, ref).get(`${ref}^{}`) !== commit) throw new Error('Could not confirm the pushed release tag. Inspect GitHub before retrying.');
    return { ...receipt, status: 'created' };
}

module.exports = { createReleaseTag, publicGitHub };

if (require.main === module) {
    const [commit, version, buildNumber, option, ...extra] = process.argv.slice(2);
    if (extra.length || (option !== undefined && option !== '--check-only')) throw new Error('Unexpected release-tag arguments.');
    const root = path.resolve(__dirname, '../..');
    const git = args => execFileSync('git', ['--git-dir=.git', '--work-tree=.', ...args], {
        cwd: root, encoding: 'utf8', timeout: 60000, maxBuffer: 1024 * 1024,
        env: { ...process.env, GIT_TERMINAL_PROMPT: '0', GCM_INTERACTIVE: 'Never' }, stdio: ['ignore', 'pipe', 'pipe']
    });
    createReleaseTag({ commit, version, buildNumber, git, github: publicGitHub(), checkOnly: option === '--check-only' })
        .then(receipt => process.stdout.write(`${JSON.stringify(receipt)}\n`))
        .catch(error => { process.stderr.write(`${error.message}\n`); process.exitCode = 1; });
}
