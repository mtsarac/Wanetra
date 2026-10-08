#!/usr/bin/env bash
set -euo pipefail

valid_semver() { [[ "$1" =~ ^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; }
version_not_older() {
  local latest="$1" candidate="$2"
  [[ -z "$latest" || "$candidate" == "$latest" || "$(printf '%s\n%s\n' "$latest" "$candidate" | sort -V | tail -n 1)" == "$candidate" ]]
}
alias_not_older() { version_not_older "$1" "$2"; }

for tag in v0.0.0 v1.2.3; do valid_semver "$tag"; done
for tag in v01.2.3 v1.2.3-rc.1 v1.2; do valid_semver "$tag" && exit 1; done
version_not_older v1.2.2 v1.2.3
version_not_older v1.2.3 v1.2.3
version_not_older v1.2.3 v1.2.2 && exit 1
alias_not_older v1.2.9 v1.2.8 && exit 1
alias_not_older v1.2.8 v1.2.9

echo 'release smoke OK'
