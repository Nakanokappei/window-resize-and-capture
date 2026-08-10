# store-shots

Everything the store pictures need, except the code that draws them.

The listing wants the same handful of screens in every language the app speaks.
That is dozens of pictures which have to be identical in everything but the
language, so **the app photographs itself**: one run of the app produces one
picture, told which pose, which language and what size on the command line.

This app lives in the tray and has no main window, so there is nothing to
photograph until one is made. It therefore draws a **studio set**: a borderless
window, exactly the size of the picture, holding the brand color where the
wallpaper goes, a taskbar, its own tray icon, a clock in the shoot's language,
and the marketing line at the top left. The tray menu and the settings window
are then shown on top of it and the whole thing is copied off the screen in one
pass.

**The taskbar is measured, not photographed.** At the moment a picture is taken
the app finds the real taskbar, reads its height and three of its colors, and
pulls the icons of File Explorer and Edge from the installed programs. No
picture of anybody's desktop is stored here or shipped inside the product.

## Layout

```
store-shots/
  README.md                  this file - what to run, and what is specific to this app
  copy/                      the marketing line per language
  take-store-screenshots.sh  walks languages x poses
  out/                       finished pictures (ignored by git)
```

The drawing code is the one part that cannot live here, because it has to
compile with the app. It is kept together under `WindowResize/Studio/` rather
than spread through the UI.

## Running a shoot

**Run these from a shell, not from PowerShell's `Start-Process`.** The app is a
WinExe, so `--list-views` prints to the console it was started from and shows
nothing when there is none. `Start-Process` also joins its argument list with
spaces, which cuts a headline off at its first word and a path off at its
first folder with a space in it. Both failures look like app bugs and are not.

```bash
exe=WindowResize/bin/Release/net8.0-windows10.0.17763.0/WindowResizeCapture.exe

# What the app can hold, and in which languages. Never keep a second copy of this list.
"$exe" --list-views

# One picture. Copy given here wins over the files in copy/.
"$exe" --language ja --screenshot view=choose-a-size \
  "headline=ウィンドウのサイズを、いつでも同じに。" \
  "out=store-shots/out/store-choose-a-size-ja.png"
```

**Do not touch the machine while a shoot runs.** The picture is a copy of the
screen, so a notification or another window taking the foreground lands in it.

## Writing the copy

A space is where a line may break. English already has them between its words.
Japanese, Chinese and Korean have none, so the app puts one after every clause
mark itself - nothing has to be written into the copy, and an unused break
disappears rather than leaving a gap.

**Thai is the exception.** It separates neither its words with spaces nor its
sentences with a mark, so the only breaks it gets are the ones its own
convention puts between phrases. Copy written in Thai has to carry them, or a
line runs on until it leaves the picture. Finding word boundaries there needs a
dictionary, which is far more than this is worth.

Arabic is laid out from the right, which the app does on its own from the
culture.

## Checking a language nobody here reads

Point a phone's camera translation at the picture. If it can read the text
back, the shaping is right - Arabic that has lost its letter joining is not
recognized as words at all, and neither is Devanagari whose marks have come
apart. It catches in seconds what an unfamiliar script hides.

## Rules that matter here

- **The clock is the shoot's date at 10:08, with no seconds**, formatted for the
  language being shot. It is laid out from the right edge, because the text
  width changes with the language.
- **The marketing line is translated like any other string.** It is not burned
  into an image by hand: a new language must not need a graphics editor.
- **Poses are arranged by calling what a click calls.** The tray menu in a
  picture is the same menu a user opens, built by the same code.
- **The screen must be at least as large as the picture**, because the picture
  is copied off the screen. Sizes are physical pixels.
- The studio runs per-monitor DPI aware while the product runs DPI unaware.
  That is deliberate: only an aware process can draw a set that fills its own
  window. It is also why `SettingsForm` scales itself explicitly.
- After a shoot, check every file has the same size, then **open several and
  look at them** - especially German, Russian and Vietnamese. What breaks is a
  wrapped label or a clipped column, and no file listing shows that.
- Store captions are limited to 200 characters. Count them before uploading.
