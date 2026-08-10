#!/usr/bin/env bash
#
# Takes every picture the store listing needs: one run of the app per language
# per pose. The shot list comes from the app itself, so a pose or a language
# added in the code is photographed here without this file being touched.
#
# Run it from a shell. Started through PowerShell's Start-Process the app gets
# an argument list joined with spaces, which cuts a path off at its first
# space, and the shot list has no console to print to. Both failures look like
# app bugs and are not.
#
#   bash store-shots/take-store-screenshots.sh              every language
#   bash store-shots/take-store-screenshots.sh de ru vi     only those three
#
# Started with bash rather than run directly: this repository is shared with a
# Windows volume that cannot keep an executable bit, so the file is not stored
# with one.
#
# Naming languages shoots only those, which is how a single language is taken
# again after its wording changes.
#
# Do not touch the machine while it runs. Every picture is a copy of the
# screen, so a notification or another window taking the foreground lands in
# one of them. Expect a few minutes: each picture is a separate launch that
# waits for the compositor twice.

set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repo/WindowResize/WindowResize.csproj"
pictureFolder="$repo/store-shots/out"

# The app writes one line per picture here, and it is the only place the reason
# for a failed shot appears.
studioLog="$(cygpath -u "$TEMP")/WindowResizeCapture-studio.log"

# ── The binary that gets photographed ──────────────────────────────────────

# Build first, so the pictures show the current source rather than whatever was
# left in bin from an older session.
echo "Building Release..."
dotnet build "$project" -c Release --nologo -v:quiet

# Ask MSBuild where it just wrote, instead of searching bin. Three copies of
# the executable live under bin at once - the framework build, the win-x64
# runtime build and the published one - and a search picks whichever comes
# first alphabetically, which is regularly the stalest of them.
target="$(dotnet msbuild "$project" -getProperty:TargetPath -p:Configuration=Release -v:quiet)"
exe="$(cygpath -u "${target%$'\r'}")"
exe="${exe%.dll}.exe"

if [ ! -f "$exe" ]; then
  echo "No executable at $exe" >&2
  exit 1
fi

# A build with nothing to do leaves the old timestamps, so being newer than
# every source file is what says the build holds the current code.
#
# Against the newest thing the build wrote, not against the executable. The
# executable is only the launcher: MSBuild rewrites it when the main assembly
# changes and leaves it alone when only a translation changes, so a Vietnamese
# fix that landed in a satellite assembly was reported here as a build that did
# not take, on the strength of a file nobody had asked it to rewrite.
newestBuilt="$(find "$(dirname "$exe")" -type f \( -name '*.dll' -o -name '*.exe' \) \
  -printf '%T@ %p\n' | sort -n | tail -1 | cut -d' ' -f2-)"

newerSource="$(find "$repo/WindowResize" \
  -name bin -prune -o -name obj -prune -o \
  -type f -newer "$newestBuilt" -print -quit)"

if [ -n "$newerSource" ]; then
  echo "$newerSource is newer than anything the build wrote - the build did not take" >&2
  exit 1
fi

echo "Photographing $exe"
echo "  built $(date -r "$newestBuilt" '+%Y-%m-%d %H:%M:%S') ($(basename "$newestBuilt"))"

# ── The shot list ──────────────────────────────────────────────────────────

# --list-views is the single copy of what to shoot. Keeping a second list here
# would drift away from the app the first time a pose was added.
listing="$("$exe" --list-views)"

views="$(echo "$listing" | awk '/^views:/{take=1;next} /^[a-z]+:/{take=0} take{print $1}')"
languages="$(echo "$listing" | awk '/^languages:/{getline; print}')"
size="$(echo "$listing" | awk '/^size:/{getline; print $1}')"

if [ -z "$views" ] || [ -z "$languages" ] || [ -z "$size" ]; then
  echo "Could not read the shot list from --list-views" >&2
  exit 1
fi

# A language asked for on the command line has to be one the app speaks. It
# would otherwise be photographed in English, which is a picture nobody can
# tell is wrong from its file name.
if [ $# -gt 0 ]; then
  for wanted in "$@"; do
    case " $languages " in
      *" $wanted "*) ;;
      *) echo "The app does not speak $wanted. It speaks:$languages" >&2; exit 1 ;;
    esac
  done
  languages="$*"
fi

total=$(( $(echo "$views" | wc -w) * $(echo "$languages" | wc -w) ))
echo "  $total pictures at $size"
echo

# ── Taking the pictures ────────────────────────────────────────────────────

# Width and height sit in the PNG header, at bytes 16 to 23, most significant
# byte first. Reading them here keeps the check from needing an image tool.
pictureSize() {
  local header
  header="$(od -An -tx1 -j16 -N8 "$1" | tr -d ' \n')"
  printf '%dx%d' "0x${header:0:8}" "0x${header:8:8}"
}

# Everything the app logged during the shot that just failed.
reportLog() {
  local from=$1
  if [ -f "$studioLog" ]; then
    echo "  the app said:" >&2
    tail -n "+$((from + 1))" "$studioLog" >&2
  fi
}

mkdir -p "$pictureFolder"
taken=0

for language in $languages; do
  for view in $views; do
    taken=$((taken + 1))
    picture="$pictureFolder/store-$view-$language.png"
    logLines=$( [ -f "$studioLog" ] && wc -l < "$studioLog" || echo 0 )

    printf '[%2d/%d] %-10s %s\n' "$taken" "$total" "$language" "$view"

    # Clear the target first, so a file being there afterwards is proof that
    # this run wrote it. Left in place, a picture from an earlier shoot passes
    # every check below and the listing goes out with last week's wording on
    # one of its frames.
    rm -f "$picture"

    # Stop at the first bad picture. The operator has to stay away from the
    # machine while this runs, and a shoot that is already producing wrong
    # files should not spend another ten minutes doing it. Re-running after
    # the cause is fixed is one command.
    if ! "$exe" --language "$language" --screenshot "view=$view" "out=$picture"; then
      echo "  the app exited with an error" >&2
      reportLog "$logLines"
      exit 1
    fi

    if [ ! -f "$picture" ]; then
      echo "  wrote no file" >&2
      reportLog "$logLines"
      exit 1
    fi

    written="$(pictureSize "$picture")"
    if [ "$written" != "$size" ]; then
      echo "  wanted $size, wrote $written" >&2
      echo "  the screen has to be at least $size in physical pixels" >&2
      reportLog "$logLines"
      exit 1
    fi
  done
done

echo
echo "$taken pictures in store-shots/out, all $size."
echo "Now open several of them - German, Russian and Vietnamese first."
echo "A wrapped label or a clipped column is the failure no file listing shows."
