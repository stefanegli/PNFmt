'use strict';

const { test } = require('node:test');
const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const { randomUUID } = require('node:crypto');
const { createReleaseTag, publicGitHub } = require('./create-release-tag.cjs');

const remoteUrl = 'https://github.com/stefanegli/PNFmt.git';
const tag = 'v0.1.0-alpha.12';
const ref = `refs/tags/${tag}`;
const root = path.resolve(__dirname, '../../artifacts/release-tag-tests', randomUUID());

// Exercise real Git tag objects, refspecs and concurrent creation against private bare repositories.
function fixture() {
    const directory = path.join(root, randomUUID());
    const checkout = path.join(directory, 'checkout');
    const remote = path.join(directory, 'remote.git');
    fs.mkdirSync(directory, { recursive: true });
    const run = args => execFileSync('git', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'],
        env: { ...process.env, GIT_TERMINAL_PROMPT: '0', GCM_INTERACTIVE: 'Never' } }).trim();
    run(['init', '--bare', '--initial-branch=master', remote]);
    run(['init', '--initial-branch=master', checkout]);
    const local = args => run(['-C', checkout, ...args]);
    local(['config', 'user.name', 'Test']);
    local(['config', 'user.email', 'test@example.invalid']);
    local(['-c', 'commit.gpgSign=false', 'commit', '--allow-empty', '-m', 'candidate']);
    const commit = local(['rev-parse', 'HEAD']);
    local(['push', remote, 'HEAD:refs/heads/master']);
    const calls = [];
    const git = args => { calls.push(args); return local(args.map(arg => arg === remoteUrl ? remote : arg)); };
    const actions = { listWorkflowRuns: Symbol('runs'), listWorkflowRunArtifacts: Symbol('artifacts') };
    const runs = [{ id: 42, head_sha: commit, head_branch: 'master', event: 'push', status: 'completed',
        conclusion: 'success', path: '.github/workflows/build.yml', head_repository: { full_name: 'stefanegli/PNFmt' },
        html_url: 'https://github.com/stefanegli/PNFmt/actions/runs/42' }];
    const artifacts = [{ id: 10, name: 'release-package', expired: false }];
    const github = { rest: { actions }, async paginate(method) { return method === actions.listWorkflowRuns ? runs : artifacts; } };
    const options = { commit, version: '0.1.0-alpha.12', buildNumber: 5, git, github };
    return { local, run, remote, checkout, commit, calls, runs, artifacts, options };
}

function assertNoWrite(f) {
    assert.equal(f.calls.some(args => args.includes('tag') || args.includes('push')), false);
    assert.equal(f.run(['--git-dir', f.remote, 'tag', '--list', tag]), '');
}

test('creates exactly one annotated tag on the validated commit, without pushing branches or other tags', async () => {
    const f = fixture();
    f.local(['tag', '--annotate', '--no-sign', '-m', 'unrelated', 'unrelated-tag']);
    f.local(['config', 'push.followTags', 'true']);
    const receipt = await createReleaseTag(f.options);
    assert.equal(receipt.status, 'created');
    assert.equal(receipt.githubBuildRunId, 42);
    assert.equal(receipt.githubArtifactId, 10);
    assert.equal(f.run(['--git-dir', f.remote, 'cat-file', '-t', ref]), 'tag');
    assert.equal(f.run(['--git-dir', f.remote, 'rev-parse', `${ref}^{}`]), f.commit);
    assert.equal(f.run(['--git-dir', f.remote, 'rev-parse', 'refs/heads/master']), f.commit);
    assert.equal(f.run(['--git-dir', f.remote, 'tag', '--list']), tag);
    assert.match(f.run(['--git-dir', f.remote, 'cat-file', '-p', ref]), /PNForge build 5; GitHub Build 42/);
});

test('check-only validates the release without creating any tag', async () => {
    const f = fixture();
    assert.equal((await createReleaseTag({ ...f.options, checkOnly: true })).status, 'checked');
    assertNoWrite(f);
    assert.equal(f.local(['tag', '--list', tag]), '');
});

test('a retry of the same annotated tag does not push again', async () => {
    const f = fixture();
    await createReleaseTag(f.options);
    f.calls.length = 0;
    assert.equal((await createReleaseTag(f.options)).status, 'already-exists');
    assert.equal(f.calls.some(args => args.includes('tag') || args.includes('push')), false);
});

test('rejects an existing tag at another commit without changing it', async () => {
    const f = fixture();
    f.local(['-c', 'commit.gpgSign=false', 'commit', '--allow-empty', '-m', 'different']);
    f.local(['tag', '--annotate', '--no-sign', '-m', 'existing', tag]);
    f.local(['push', f.remote, ref]);
    const existing = f.run(['--git-dir', f.remote, 'rev-parse', ref]);
    f.local(['checkout', '--detach', f.commit]);
    await assert.rejects(createReleaseTag(f.options), /already exists with another identity/);
    assert.equal(f.run(['--git-dir', f.remote, 'rev-parse', ref]), existing);
    assert.equal(f.calls.some(args => args.includes('tag') || args.includes('push')), false);
});

test('does not replace an existing lightweight tag even at the same commit', async () => {
    const f = fixture();
    f.local(['tag', tag]);
    f.local(['push', f.remote, ref]);
    await assert.rejects(createReleaseTag(f.options), /already exists with another identity/);
    assert.equal(f.run(['--git-dir', f.remote, 'cat-file', '-t', ref]), 'commit');
});

test('a concurrent tag creation is not overwritten by the push', async () => {
    const f = fixture();
    const git = args => {
        if (args.includes('push')) f.run(['--git-dir', f.remote, 'update-ref', ref, f.commit]);
        return f.options.git(args);
    };
    await assert.rejects(createReleaseTag({ ...f.options, git }), error => {
        assert.equal(error.status, 1);
        assert.match(error.stdout, /stale info/);
        return true;
    });
    assert.equal(f.run(['--git-dir', f.remote, 'cat-file', '-t', ref]), 'commit');
});

test('a credential or transport failure does not report tag creation', async () => {
    const f = fixture();
    const git = args => { if (args.includes('push')) throw new Error('authentication failed'); return f.options.git(args); };
    await assert.rejects(createReleaseTag({ ...f.options, git }), /authentication failed/);
    assert.equal(f.run(['--git-dir', f.remote, 'tag', '--list']), '');
});

test('rejects a checkout for a different commit before creating a tag', async () => {
    const f = fixture();
    await assert.rejects(createReleaseTag({ ...f.options, commit: 'b'.repeat(40) }), /Checkout does not match/);
    assertNoWrite(f);
});

test('rejects tracked changes', async () => {
    const f = fixture();
    fs.writeFileSync(path.join(f.checkout, 'dirty.txt'), 'change');
    f.local(['add', 'dirty.txt']);
    await assert.rejects(createReleaseTag(f.options), /tracked changes/);
    assertNoWrite(f);
});

test('requires the exact current GitHub master commit', async () => {
    const f = fixture();
    f.local(['-c', 'commit.gpgSign=false', 'commit', '--allow-empty', '-m', 'new master']);
    f.local(['push', f.remote, 'HEAD:refs/heads/master']);
    f.local(['checkout', '--detach', f.commit]);
    await assert.rejects(createReleaseTag(f.options), /current GitHub master/);
    assertNoWrite(f);
});

for (const [name, change] of Object.entries({
    'no successful build': f => f.runs.splice(0),
    'failed build': f => { f.runs[0].conclusion = 'failure'; },
    'PR build': f => { f.runs[0].event = 'pull_request'; },
    'wrong source build': f => { f.runs[0].head_sha = 'b'.repeat(40); },
    'missing package': f => f.artifacts.splice(0),
    'expired package': f => { f.artifacts[0].expired = true; }
})) {
    test(`does not tag with ${name}`, async () => {
        const f = fixture();
        change(f);
        await assert.rejects(createReleaseTag(f.options), /No successful/);
        assertNoWrite(f);
    });
}

test('rejects malformed release arguments before invoking Git or the API', async () => {
    for (const change of [{ commit: 'master' }, { version: '../escape' }, { version: 'v1.2.3' }, { buildNumber: 0 }]) {
        await assert.rejects(createReleaseTag({ commit: 'a'.repeat(40), version: '1.2.3', buildNumber: 1,
            git() { assert.fail('Git must not be called'); }, ...change }), /Invalid/);
    }
});

test('public GitHub metadata adapter follows full pages and uses the exact build query', async () => {
    const urls = [];
    const github = publicGitHub(async (url, options) => {
        urls.push(url);
        assert.equal(options.redirect, 'error');
        assert.equal(options.headers.Authorization, undefined);
        return { ok: true, json: async () => ({ workflow_runs: urls.length === 1 ? Array(100).fill({ id: 1 }) : [{ id: 2 }] }) };
    });
    const result = await github.paginate(github.rest.actions.listWorkflowRuns, { head_sha: 'a'.repeat(40) });
    assert.equal(result.length, 101);
    assert.match(urls[0], /head_sha=a{40}&branch=master&event=push&status=success&per_page=100&page=1$/);
    assert.match(urls[1], /page=2$/);
});

test('public GitHub adapter loads artifacts from the selected run', async () => {
    const github = publicGitHub(async url => {
        assert.match(url, /actions\/runs\/42\/artifacts\?per_page=100&page=1$/);
        return { ok: true, json: async () => ({ artifacts: [{ id: 10 }] }) };
    });
    assert.deepEqual(await github.paginate(github.rest.actions.listWorkflowRunArtifacts, { run_id: 42 }), [{ id: 10 }]);
});

test('public GitHub adapter fails closed for HTTP errors, malformed results and excessive pages', async () => {
    for (const response of [{ ok: false, status: 403 }, { ok: true, json: async () => ({}) },
        { ok: true, json: async () => ({ artifacts: Array(100).fill({ id: 10 }) }) }]) {
        const github = publicGitHub(async () => response);
        await assert.rejects(github.paginate(github.rest.actions.listWorkflowRunArtifacts, { run_id: 42 }), /failed|invalid|bounded/);
    }
});

test('Forge keeps automatic/required checks at build and gates tagging behind the successful artifact on master', () => {
    const pipeline = JSON.parse(fs.readFileSync(path.resolve(__dirname, '../../.forge/pipelines/verify.json'), 'utf8'));
    assert.deepEqual(pipeline.runThrough, { push: 'build', manual: 'build', schedule: 'build' });
    assert.equal(pipeline.requiredCheck.throughStage, 'build');
    const job = pipeline.stages.find(stage => stage.id === 'tag').jobs[0];
    assert.equal(job.manualTrigger, true);
    assert.deepEqual(job.constraints, { sourceBranches: ['master'], triggers: ['manual', 'push'] });
    assert.deepEqual(job.inputs, [{ job: 'verify', artifact: 'package', path: 'incoming/validated-package' }]);
});
