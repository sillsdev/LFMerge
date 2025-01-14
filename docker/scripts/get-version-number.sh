#!/bin/bash

REV=${GITHUB_REF:-$(git rev-parse --symbolic-full-name HEAD)}
echo "Calculating name from ${REV}"
DESCRIBE=$(git describe --long --match "v*")
MAJOR=$(echo "$DESCRIBE" | sed -E 's/^v([0-9]+)\.([0-9]+)\.([0-9]+).*$/\1/')
MINOR=$(echo "$DESCRIBE" | sed -E 's/^v([0-9]+)\.([0-9]+)\.([0-9]+).*$/\2/')
PATCH=$(echo "$DESCRIBE" | sed -E 's/^v([0-9]+)\.([0-9]+)\.([0-9]+).*$/\3/')
# TODO: Detect need for minor/major updates and increment those instead of PATCH
COMMIT_COUNT=$(echo "$DESCRIBE" | sed -E 's/^[^-]+-([^-]+)-.*$/\1/')
COMMIT_HASH=$(echo "$DESCRIBE" | sed -E 's/^[^-]+-[^-]+-g(.*)$/\1/')
if [ -n "$COMMIT_COUNT" -a "$COMMIT_COUNT" -gt 0 ]; then
  # If we're building from a tagged version, rebuild precisely that version
  PATCH=$((${PATCH} + 1))
fi
echo "Build number before: $BUILD_NUMBER"
export BUILD_NUMBER=${BUILD_NUMBER:-${COMMIT_COUNT}}
echo "Build number after: $BUILD_NUMBER"
if [ -z ${REV} ]; then
  echo Failed to get a meaningful commit name
fi
echo Got commit name ${REV}
RESULT=notfound
if echo "${REV}" | grep -E '^refs/pull/'; then
  echo Found PR
  RESULT=$(echo "${REV}" | sed -E 's/^refs\/pull\/([0-9]+)\/merge/\1/')
fi
if echo "${REV}" | grep -E '^refs/heads/'; then
  echo Found branch
  RESULT=$(echo "${REV}" | sed -E 's/^refs\/heads\///')
fi
if echo "${REV}" | grep -E '^refs/tags/'; then
  echo Found tag
  RESULT=$(echo "${REV}" | sed -E 's/^refs\/tags\///')
fi
echo Will calculate version from "${RESULT}" and "${MAJOR}.${MINOR}.${PATCH} with $COMMIT_COUNT commits since then, and current hash $COMMIT_HASH"

case "$REV" in
  refs/heads/develop)
    PRERELEASE="~alpha.${BUILD_NUMBER}"
    ;;

  refs/heads/master)
    PRERELEASE=
    ;;

  refs/pull/*)
    PR_NUMBER=$(echo "${REV}" | sed -E 's/^refs\/pull\/([0-9]+)\/merge/\1/')
    PRERELEASE="~PR${PR_NUMBER}.${BUILD_NUMBER}"
    ;;

  refs/heads/*)
    BRANCH=$(echo "${REV##refs/heads/}" | sed 's/\//-/')
    PRERELEASE="~${BRANCH}.${BUILD_NUMBER}"
    ;;

  *)
    echo "Could not determine version number from ${REV}"
    echo "::error ::Could not determine version number from ${REV}"
    exit 1

esac
export MajorMinorPatch="${MAJOR}.${MINOR}.${PATCH}"
export AssemblySemVer="${MajorMinorPatch}.${BUILD_NUMBER}"
export AssemblySemFileVer="${MajorMinorPatch}.0"
export DebPackageVersion="${MajorMinorPatch}${PRERELEASE}"
export MsBuildVersion=$(echo "${DebPackageVersion}" | sed 's/~/-/')
if [ -n "${DbVersion}" ]; then
  INFO_SUFFIX=".${DbVersion}"
else
  INFO_SUFFIX=""
fi
GIT_SHA=${GITHUB_SHA:-$(git rev-parse ${REV})}
TAG_SUFFIX="$(date +%Y%m%d)-${GIT_SHA}"
export InformationalVersion="${MsBuildVersion}${INFO_SUFFIX}:${TAG_SUFFIX}"

# If not running on GitHub Actions, GITHUB_OUTPUT is empty which would cause "ambiguous redirect" errors below
GITHUB_OUTPUT=${GITHUB_OUTPUT:-/dev/stdout}

echo "Calculated version number ${MsBuildVersion} for ${DbVersion}"
echo "DebPackageVersion=${DebPackageVersion}" >> $GITHUB_OUTPUT
echo "MsBuildVersion=${MsBuildVersion}" >> $GITHUB_OUTPUT
echo "MajorMinorPatch=${MajorMinorPatch}" >> $GITHUB_OUTPUT
echo "AssemblySemVer=${AssemblySemVer}" >> $GITHUB_OUTPUT
echo "AssemblySemFileVer=${AssemblySemFileVer}" >> $GITHUB_OUTPUT
echo "InformationalVersion=${InformationalVersion}" >> $GITHUB_OUTPUT
