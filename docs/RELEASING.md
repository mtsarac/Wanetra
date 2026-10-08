# Releasing Wanetra

This page is for maintainers. Users only need the [Docker section of the README](../README.md#docker).

## Images from `main`

Pull requests run the backend and frontend checks and publish nothing. On `main`, once both checks pass, CI builds one multi-platform image (linux/amd64 and linux/arm64) and tags it `latest` and `sha-<full commit SHA>`.

Image publishing is serialized and rechecks the current commit before building, so an older build cannot publish over a newer queued one.

## Cutting a release

Releases are tags. After CI has passed on the commit you want, tag it on `main`:

```sh
git switch main
git pull --ff-only
git tag -a v0.3.0 -m "Wanetra v0.3.0"
git push origin v0.3.0
```

Pushing the tag starts the `Release` workflow (there is no `workflow_dispatch`). It does not rebuild anything. It checks that:

- the tag is plain SemVer (`vX.Y.Z`) and points at a commit that is an ancestor of `main`,
- CI succeeded for that commit on `main`,
- the tag is not older than the newest existing release, unless it is a retry of the same tag,
- the `sha-<commit>` image exists for both architectures.

It then promotes that image to `X.Y.Z` and `X.Y`, and creates the GitHub Release with generated notes.

- `X.Y.Z` tags are immutable. A retry only succeeds if the digest still matches the source image.
- `X.Y` moves only for the newest release in that minor line, so re-running an older patch cannot roll it back.
- The GitHub Release is marked Latest only when its tag is the highest SemVer release on `main`. A backport to an older line does not take the badge.
- The image's own `latest` tag follows `main` and is never moved by a release.
- OCI labels written at build time, including the version label from the `main` build, are not rewritten during promotion.

If promotion succeeds but creating the GitHub Release fails, use **Re-run jobs** on the failed run. Promotion is idempotent for the same digest. A draft release, or one that does not match the tag, makes the workflow stop instead of overwriting it.

Do not move or re-push an existing version tag.

## Package visibility

The GHCR package must be public and allow anonymous pulls. If a new package is created private, change its visibility in the GitHub package settings.
