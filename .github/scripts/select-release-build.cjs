'use strict';

// A tag can only consume a successful default-branch push build of its exact commit.
module.exports = async function selectReleaseBuild({ github, context, commit }) {
    const match = /^refs\/tags\/v(\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?)$/.exec(context.ref);
    if (!match) throw new Error('Release tags must use v<major>.<minor>.<patch>[-prerelease].');
    if (!/^[0-9a-f]{40}$/.test(commit)) throw new Error('Invalid release commit.');

    const branch = context.payload.repository.default_branch;
    const repository = `${context.repo.owner}/${context.repo.repo}`;
    const runs = await github.paginate(github.rest.actions.listWorkflowRuns, {
        ...context.repo, workflow_id: 'build.yml', head_sha: commit,
        branch, event: 'push', status: 'success', per_page: 100
    });
    for (const run of runs) {
        // Check the returned provenance too; never accept a PR or another repository.
        if (run.head_sha !== commit || run.head_branch !== branch || run.event !== 'push' ||
            run.status !== 'completed' || run.conclusion !== 'success' ||
            run.path !== '.github/workflows/build.yml' || run.head_repository?.full_name !== repository) continue;
        const artifacts = await github.paginate(github.rest.actions.listWorkflowRunArtifacts, {
            ...context.repo, run_id: run.id, per_page: 100
        });
        const artifact = artifacts.find(item => item.name === 'release-package' && !item.expired);
        if (artifact) return { runId: run.id, artifactId: artifact.id, version: match[1], url: run.html_url };
    }
    throw new Error(`No successful default-branch Build with a retained release-package artifact for ${commit}. ` +
        'Wait for Build to pass, or rerun the original Build if its artifact expired, then rerun this publish workflow.');
};
