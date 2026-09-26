'use strict';

const { test } = require('node:test');
const assert = require('node:assert/strict');
const select = require('./select-release-build.cjs');

const commit = 'a'.repeat(40);
const context = {
    repo: { owner: 'owner', repo: 'PNFmt' }, ref: 'refs/tags/v0.1.0-alpha.12',
    payload: { repository: { default_branch: 'master' } }
};
const passed = {
    id: 42, head_sha: commit, head_branch: 'master', event: 'push',
    status: 'completed', conclusion: 'success', path: '.github/workflows/build.yml',
    head_repository: { full_name: 'owner/PNFmt' }, html_url: 'https://example.test/runs/42'
};
const packageArtifact = { id: 100, name: 'release-package', expired: false };

function api(runs = [passed], artifacts = [packageArtifact]) {
    const rest = { actions: { listWorkflowRuns: Symbol('runs'), listWorkflowRunArtifacts: Symbol('artifacts') } };
    return {
        rest,
        async paginate(method, options) {
            if (method === rest.actions.listWorkflowRuns) {
                assert.equal(options.workflow_id, 'build.yml');
                assert.equal(options.head_sha, commit);
                assert.equal(options.branch, 'master');
                assert.equal(options.event, 'push');
                assert.equal(options.status, 'success');
                return runs;
            }
            assert.equal(method, rest.actions.listWorkflowRunArtifacts);
            assert.equal(options.run_id, 42);
            return artifacts;
        }
    };
}

test('selects the exact successful build and retained package', async () => {
    assert.deepEqual(await select({ github: api(), context, commit }), {
        runId: 42, artifactId: 100, version: '0.1.0-alpha.12', url: passed.html_url
    });
});

for (const [name, change] of Object.entries({
    'different commit': { head_sha: 'b'.repeat(40) },
    'release branch': { head_branch: 'release/example' },
    'pull request': { event: 'pull_request' },
    'running build': { status: 'in_progress' },
    'failed build': { conclusion: 'failure' },
    'cancelled build': { conclusion: 'cancelled' },
    'other workflow': { path: '.github/workflows/unrelated.yml' },
    'fork build': { head_repository: { full_name: 'fork/PNFmt' } },
    'missing source repository': { head_repository: null }
})) {
    test(`rejects ${name}`, async () => {
        await assert.rejects(select({ github: api([{ ...passed, ...change }]), context, commit }), /No successful/);
    });
}

for (const [name, artifacts] of Object.entries({
    missing: [], expired: [{ ...packageArtifact, expired: true }],
    unrelated: [{ ...packageArtifact, name: 'coverage-linux' }]
})) {
    test(`rejects ${name} package artifacts`, async () => {
        await assert.rejects(select({ github: api([passed], artifacts), context, commit }), /rerun the original Build/);
    });
}

test('rejects a commit without a completed successful build', async () => {
    await assert.rejects(select({ github: api([]), context, commit }), /Wait for Build to pass/);
});

test('ignores unrelated runs before selecting the eligible one', async () => {
    const result = await select({ github: api([{ ...passed, head_sha: 'b'.repeat(40) }, passed]), context, commit });
    assert.equal(result.runId, 42);
});

for (const ref of ['refs/heads/master', 'refs/tags/0.1.0', 'refs/tags/v1.2', 'refs/tags/v1.2.3-']) {
    test(`rejects invalid release ref ${ref}`, async () => {
        await assert.rejects(select({ github: api(), context: { ...context, ref }, commit }), /Release tags must use/);
    });
}

test('accepts a stable version', async () => {
    const result = await select({ github: api(), context: { ...context, ref: 'refs/tags/v1.2.3' }, commit });
    assert.equal(result.version, '1.2.3');
});

test('rejects a malformed commit before calling GitHub', async () => {
    await assert.rejects(select({ github: api(), context, commit: 'main' }), /Invalid release commit/);
});
