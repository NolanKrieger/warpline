#!/bin/bash
# Build the store trailer from real gameplay (dev routes played by the game, recorded with Godot's Movie Maker).
# Output: docs/store/trailer/warpline-trailer.mp4 (1920x1080, 60 fps, H.264 + AAC). Needs ffmpeg and a display.
set -euo pipefail
cd "$(dirname "$0")/.."
work=${WORK:-$(mktemp -d)}
font=assets/fonts/NotoSans-Black.ttf
mkdir -p "$work" docs/store/trailer
# A save with the music muted: clips carry only sound effects; one music bed goes under the whole cut.
python3 - "$work/save.json" <<'PY'
import json, sys
json.dump({"Version": 1, "Settings": {"MasterVolume": 90, "SfxVolume": 90, "MusicVolume": 0}}, open(sys.argv[1], "w"))
PY
clips=(02-speedy 04-terminal 08-afterburner 06-beamline 16-trampoline 17-ferris 20-warpline)
for id in "${clips[@]}"; do
  [ -f "$work/$id.avi" ] && continue
  godot --path . --resolution 1920x1080 --write-movie "$work/$id.avi" --fixed-fps 60 -- \
    --save="$work/save.json" --level=$id --autoplay --trailer --quit-after-finish=0.8 >/dev/null 2>&1
done
godot --headless --path . -- --export-music="$work" >/dev/null 2>&1

# segment: id start duration caption
seg() {
  ffmpeg -hide_banner -loglevel error -y -ss "$2" -t "$3" -i "$work/$1.avi" \
    -vf "scale=1920:1080,fps=60,fade=t=in:st=0:d=0.15,fade=t=out:st=$(python3 -c "print($3-0.15)"):d=0.15,drawtext=fontfile=$font:text='$4':fontsize=64:fontcolor=white:borderw=6:bordercolor=0x0d0f15:x=80:y=h-150" \
    -af "afade=t=in:d=0.1,afade=t=out:st=$(python3 -c "print($3-0.15)"):d=0.15" -c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -c:a aac -ar 44100 -ac 2 "$work/s_$5.mp4"
}
card() {  # image duration name caption
  local text=""
  [ -n "$4" ] && text=",drawtext=fontfile=$font:text='$4':fontsize=58:fontcolor=white:borderw=6:bordercolor=0x0d0f15:x=(w-tw)/2:y=h-140"
  ffmpeg -hide_banner -loglevel error -y -loop 1 -t "$2" -i "$1" -f lavfi -t "$2" -i anullsrc=r=44100:cl=stereo \
    -vf "scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:color=0x0d0f15,fps=60,fade=t=in:st=0:d=0.3,fade=t=out:st=$(python3 -c "print($2-0.3)"):d=0.3$text" \
    -c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -c:a aac -ar 44100 -ac 2 -shortest "$work/s_$3.mp4"
}
card docs/store/capsules/main_capsule.png 2.6 00 ""
seg 02-speedy 0.8 5.6 "Speed in. Speed out." 01
seg 04-terminal 0.0 3.2 "Fall until nothing falls faster." 02
seg 08-afterburner 0.5 4.8 "Turn speed into height." 03
seg 06-beamline 5.0 4.8 "Redirect lasers." 04
seg 16-trampoline 2.0 7.5 "Bounce higher than you fell." 05
seg 17-ferris 4.0 7.5 "Ride everything. Portal onto it." 06
seg 20-warpline 8.0 6.5 "20 levels. Every medal. Your ghost." 07
card docs/store/screenshots/07-editor.png 3.0 08 "Build your own. Share it as a code."
card docs/store/capsules/main_capsule.png 3.2 09 "Coming to Steam"
ls "$work"/s_*.mp4 | sort | sed "s/^/file '/; s/$/'/" > "$work/list.txt"
ffmpeg -hide_banner -loglevel error -y -f concat -safe 0 -i "$work/list.txt" -c copy "$work/cut.mp4"
dur=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$work/cut.mp4")
# Music bed (act 3 on a loop) under the effects, faded out at the end.
ffmpeg -hide_banner -loglevel error -y -i "$work/cut.mp4" -stream_loop -1 -i "$work/act3.wav" \
  -filter_complex "[1:a]atrim=0:$dur,afade=t=in:d=1.0,afade=t=out:st=$(python3 -c "print($dur-2.5)"):d=2.5,volume=0.9[m];[0:a]volume=0.8[s];[s][m]amix=inputs=2:normalize=0[a]" \
  -map 0:v -map "[a]" -c:v copy -c:a aac -b:a 192k -shortest docs/store/trailer/warpline-trailer.mp4
ffprobe -v error -show_entries format=duration:stream=codec_name,width,height -of compact docs/store/trailer/warpline-trailer.mp4
