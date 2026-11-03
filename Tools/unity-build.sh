#!/usr/bin/env bash
# Batch-mode helper for the Unity project (Unity CLI `unity`, Git Bash on Windows).
# The Unity Editor must NOT have this project open (batch mode refuses a locked project).
#
#   Tools/unity-build.sh test [EditMode|PlayMode] [filter]   run Test Runner tests, NUnit XML in Build/test-results/
#   Tools/unity-build.sh compile                             open the project headless, fail on compile errors
#   Tools/unity-build.sh build [--dev]                       Windows64 player -> Build/Windows/Orbital.exe
#   Tools/unity-build.sh smoke [--dev] [delaySeconds]        build, run the player windowed, take a screenshot, quit
#   Tools/unity-build.sh run-player [delaySeconds]           run an existing player build only (screenshot + log)
#
# Every Editor/player log goes to Build/logs/<command>-<timestamp>.log; the script prints the paths and the last
# error lines. Exit code: 0 ok, non-zero failure (unity test: 8 = tests failed, 6 = no verdict).
set -u

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/Build"
LOGS="$OUT/logs"
PLAYER="$OUT/Windows/Orbital.exe"
STAMP="$(date +%Y%m%d-%H%M%S)"
EDITOR_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
export UNITY_NO_CONSENT_PROMPT=1 UNITY_NO_UPDATE_CHECK=1 UNITY_NO_PAGER=1
mkdir -p "$LOGS"

die() { echo "error: $*" >&2; exit 1; }

check_unity() {
  command -v unity >/dev/null 2>&1 || die "Unity CLI 'unity' not found on PATH"
  [ -n "$EDITOR_VERSION" ] || die "cannot read m_EditorVersion from ProjectSettings/ProjectVersion.txt"
  if [ -f "$ROOT/Temp/UnityLockfile" ] && ! rm -f "$ROOT/Temp/UnityLockfile" 2>/dev/null; then
    die "the project is open in the Unity Editor (Temp/UnityLockfile is locked); close it first"
  fi
}

# Print compile errors and other error lines from a Unity log.
summarize_log() {
  local log="$1"
  [ -f "$log" ] || return 0
  echo "log: $log"
  grep -E "error CS[0-9]+|Scripts have compiler errors|\[BuildScript\]|\[AutomationHook\]" "$log" | sort -u | head -40
  # Debug.LogError / LogException / uncaught exceptions: the message is the last text line before the stack trace.
  awk '
    /^UnityEngine\.Debug:(LogError|LogException|LogErrorFormat)/ { if (msg != "" && !(msg in seen)) { seen[msg]=1; print "ERROR: " msg } next }
    /^[A-Za-z_.]+Exception: / { if (!($0 in seen)) { seen[$0]=1; print "EXCEPTION: " $0 } next }
    /^(UnityEngine\.|\(Filename|Rethrow|  at |$)/ { next }
    { msg = $0 }
  ' "$log" | head -20
}

cmd_test() {
  check_unity
  local mode="${1:-EditMode}" filter="${2:-}"
  local results="$OUT/test-results/${mode,,}-$STAMP.xml"
  local log="$LOGS/test-${mode,,}-$STAMP.log"
  mkdir -p "$OUT/test-results"
  local args=(--no-banner test "$ROOT" --editor-version "$EDITOR_VERSION" --mode "$mode" --output "$results" --timeout 1800 --format json)
  [ -n "$filter" ] && args+=(--filter "$filter")
  unity "${args[@]}" -- -logFile "$log" > "$log.console" 2>&1
  local code=$?
  summarize_log "$log"
  if [ -f "$results" ]; then
    echo "results: $results"
    head -c 600 "$results" | grep -o '<test-run [^>]*' | grep -oE '(total|passed|failed|skipped)="[0-9]+"' | tr '\n' ' '; echo
    grep -oE '<test-case [^>]*result="Failed"[^>]*' "$results" | grep -oE 'fullname="[^"]*"' | head -30
  fi
  return $code
}

cmd_compile() {
  check_unity
  local log="$LOGS/compile-$STAMP.log"
  unity --no-banner run "$ROOT" --editor-version "$EDITOR_VERSION" --timeout 1800 -- \
    -nographics -logFile "$log" -executeMethod Orbital.Automation.BuildScript.CompileCheck > "$log.console" 2>&1
  local code=$?
  summarize_log "$log"
  echo "compile exit code: $code"
  return $code
}

cmd_build() {
  check_unity
  local dev=()
  [ "${1:-}" = "--dev" ] && dev=(-development)
  local log="$LOGS/build-$STAMP.log"
  unity --no-banner run "$ROOT" --editor-version "$EDITOR_VERSION" --timeout 3600 -- \
    -logFile "$log" -buildTarget Win64 -executeMethod Orbital.Automation.BuildScript.BuildWindows -buildOutput "$PLAYER" "${dev[@]}" > "$log.console" 2>&1
  local code=$?
  summarize_log "$log"
  [ $code -eq 0 ] && [ -f "$PLAYER" ] && echo "player: $PLAYER"
  echo "build exit code: $code"
  return $code
}

cmd_run_player() {
  local delay="${1:-15}"
  [ -f "$PLAYER" ] || die "no player at $PLAYER; run: Tools/unity-build.sh build"
  local shot="$OUT/screenshots/smoke-$STAMP.png"
  local log="$LOGS/player-$STAMP.log"
  local limit=$(( ${delay%.*} + 60 ))
  mkdir -p "$OUT/screenshots"
  echo "running player for about ${delay}s (window 1280x720), hard limit ${limit}s"
  "$PLAYER" -logFile "$(cygpath -w "$log" 2>/dev/null || echo "$log")" \
    -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
    -orbitalScreenshot "$(cygpath -w "$shot" 2>/dev/null || echo "$shot")" -orbitalScreenshotDelay "$delay" &
  local pid=$!
  local waited=0
  while kill -0 "$pid" 2>/dev/null && [ $waited -lt $limit ]; do sleep 1; waited=$((waited + 1)); done
  if kill -0 "$pid" 2>/dev/null; then echo "player did not quit in ${limit}s, killing"; kill "$pid" 2>/dev/null; taskkill //F //IM Orbital.exe >/dev/null 2>&1; fi
  summarize_log "$log"
  if [ -f "$shot" ]; then echo "screenshot: $shot"; return 0; fi
  echo "no screenshot written"; return 1
}

cmd_smoke() {
  local dev="" delay=""
  for a in "$@"; do
    case "$a" in --dev) dev="--dev" ;; *) delay="$a" ;; esac
  done
  cmd_build $dev || return $?
  cmd_run_player "${delay:-15}"
}

case "${1:-}" in
  test) shift; cmd_test "$@" ;;
  compile) shift; cmd_compile "$@" ;;
  build) shift; cmd_build "$@" ;;
  smoke) shift; cmd_smoke "$@" ;;
  run-player) shift; cmd_run_player "$@" ;;
  *) sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'; exit 2 ;;
esac
