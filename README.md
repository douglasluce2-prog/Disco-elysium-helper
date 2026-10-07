# Disco Dictionary

A mod for **Disco Elysium: The Final Cut** that explains the game while you play it: its invented
places, people, politics, slang and history, the 24 skills talking in your head, and any ordinary
English word you don't know.

Disco Elysium drops you into a whole made-up world with no introduction. Words like *the Pale*,
*Revachol*, *Mazovian*, *Moralintern*, *Innocence* or *Esprit de Corps* come up constantly, and the
game assumes you'll pick them up as you go. This mod fills that gap without spoiling the story.

> **Want to read the dictionary without the game?** Everything it knows is in
> [GLOSSARY.md](GLOSSARY.md), readable on GitHub or your phone.

## What it does

| Feature | How it works |
|---|---|
| **Sidebar** | As dialogue appears, terms the dictionary knows are listed on the left of the screen with a one-line explanation, newest first. When a skill or a known character is speaking, that's explained too. Click any term for the full entry. |
| **Dictionary window** (`F1`) | Search everything, or browse by category: Skills, Game Mechanics, People, Places, World, Factions, Politics & Ideas, History, Slang & Jargon, Vocabulary. Blue words inside an explanation are links to other entries. |
| **Look up any word** (`F3` or middle-click) | Point the mouse at any word on screen (dialogue, tooltips, journal, Thought Cabinet, item descriptions) and press `F3`. Game terms open their entry, even multi-word ones like *Inland Empire*. Ordinary English words are looked up in a free online dictionary. |
| **No spoilers** | Explanations are written to be safe at any point in the game. Plot details sit behind a *"Contains spoilers. Click here to reveal."* line. Names that would themselves give the story away don't appear in the list until you've met them in the game. |
| **Real-world context** | Many entries say what the thing is based on (the Paris Commune, Marx, Lynch's *Inland Empire*...), because a lot of the game is political satire of our world. |
| **Add your own** | Drop your own entries into a JSON file and reload them in game, no rebuild needed. |

## Installing (one time, about 10 minutes)

The mod has to be built on your own PC, because it's compiled against files MelonLoader generates from
your copy of the game. A script does all of it for you.

**You need:** Disco Elysium: The Final Cut on Windows (Steam, GOG or Epic) and an internet connection.

1. **Install MelonLoader** (the mod loader).
   Download `MelonLoader.Installer.exe` from the
   [latest MelonLoader release](https://github.com/LavaGang/MelonLoader/releases/latest), run it, pick
   `disco.exe` in your Disco Elysium folder, and click **Install**.
   (Finding the folder in Steam: right-click Disco Elysium > *Manage* > *Browse local files*.)
2. **Start Disco Elysium once** and wait until you reach the main menu, then quit. The first start
   with MelonLoader takes a few minutes while it prepares the game's files.
3. **Install the .NET 8 SDK** (free, from Microsoft):
   [dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0).
   Choose the **SDK**, *Windows x64* installer.
4. **Download this mod:** on this GitHub page, click **Code** > **Download ZIP**, and unzip it anywhere.
5. **Double-click `build.bat`** in the unzipped folder. It finds your game, builds the mod, and copies
   `DiscoDictionary.dll` into the game's `Mods` folder. If it can't find the game, it asks you to drag
   the game folder into the window.
6. **Play.** A MelonLoader console window opens next to the game; it should say
   `Disco Dictionary` ... `Ready.`

After a game update, run the game once (so MelonLoader refreshes its files) and double-click
`build.bat` again.

## Using it

| Key | What it does |
|---|---|
| `F1` | Open or close the dictionary window (`Esc` also closes it) |
| `F2` | Show or hide the sidebar |
| `F3` or **middle mouse button** | Look up the word under the mouse cursor |

In the dictionary window: type to search, click a term or use the arrow keys, click the category
names to filter, press `Enter` to look up what you typed in the English dictionary, scroll with the
mouse wheel. **Seen in game** shows only terms you've already come across.

The sidebar hides itself after two minutes without new terms, and while the dictionary window is open.

## Settings

Settings are in `UserData\MelonPreferences.cfg` inside the game folder, under `[DiscoDictionary]`. They
appear after you've started the game once with the mod. Close the game before editing them.

| Setting | Default | Meaning |
|---|---|---|
| `OpenDictionaryKey`, `ToggleSidebarKey`, `LookupWordKey` | `F1`, `F2`, `F3` | Keys, as [Unity KeyCode names](https://docs.unity3d.com/ScriptReference/KeyCode.html) (`F9`, `BackQuote`, `Insert`...) |
| `MiddleClickLookup` | `true` | Middle mouse button also looks up the word under the cursor |
| `ShowSidebar` | `true` | Show the sidebar at all |
| `SidebarOnRight` | `false` | Put the sidebar on the right instead of the left |
| `SidebarMaxItems` | `6` | How many recent terms the sidebar lists |
| `SidebarHideAfterSeconds` | `120` | Hide the sidebar after this long without new terms (`0` = never) |
| `ShowSpeakerEntries` | `true` | Also explain who's speaking (skills and known characters) |
| `OnlineEnglishLookup` | `true` | Use the free online dictionary for ordinary words |
| `RevealSpoilers` | `false` | Show spoiler sections without clicking |
| `ShowUnseenEntries` | `false` | List spoiler-sensitive names before you've met them |
| `UiScale` | `1.0` | Make the dictionary bigger or smaller (e.g. `1.25`) |
| `FontName` | *(empty)* | Use a specific font instead of the game's (names are listed in the MelonLoader console) |
| `LogDialogue` | `false` | Print every dialogue line and the terms found in it to the console |

## Adding or correcting entries

The first time the mod runs it creates `UserData\DiscoDictionary\glossary\my-entries.json` in the game
folder, with an example inside. Every `.json` file in that folder is loaded after the built-in
dictionary, and an entry with the same `id` as a built-in one replaces it. In game, press `F1` and click
**Reload glossary files** at the bottom to see your changes immediately.

An entry looks like this:

```json
{
  "term": "Kineema",
  "aliases": ["Kineemas"],
  "category": "World",
  "short": "Kim Kitsuragi's motor carriage (car), which he adores.",
  "details": "Longer explanation. Optional.",
  "inspiredBy": "Optional: the real-world thing it's based on.",
  "spoiler": "Optional: hidden until the player clicks it.",
  "seeAlso": ["kim-kitsuragi", "motor-carriage"],
  "caseSensitive": true
}
```

`caseSensitive: true` means the term is only recognised with that exact capitalisation, which is what
you want for names that are also ordinary words ("the Pale" vs "pale"). Add `"commonWord": true` too if
the name is an everyday word, so it isn't matched just because it starts a sentence.

To improve the built-in dictionary for everyone, edit the files in [`data/glossary`](data/glossary) and
open a pull request. The automatic checks will tell you about mistakes like a misspelled `seeAlso` id or
two entries claiming the same name.

## Troubleshooting

- **Look in the MelonLoader console** (the black window that opens with the game). Disco Dictionary
  reports there what it loaded and anything that went wrong.
- **The sidebar never shows anything.** The console should say `Listening to the dialogue log`. If it
  says it couldn't hook the dialogue log, a game update may have renamed something; please open an issue
  with the console text. The `F1` window and the `F3` lookup still work.
- **Typing in the search box also triggers game shortcuts.** The mod tries to block the game's
  keyboard input while the window is open. If something slips through, please report which key.
- **English lookups say they can't reach the dictionary.** They need an internet connection and use
  [dictionaryapi.dev](https://dictionaryapi.dev/). Words you've looked up once are saved and work offline.
  Turn lookups off with `OnlineEnglishLookup = false`.
- **Steam Deck / Linux (Proton):** MelonLoader needs the Steam launch option
  `WINEDLLOVERRIDES="version=n,b" %command%`. Build with `./build.sh "/path/to/Disco Elysium"`
  (needs the .NET 8 SDK).
- **Uninstalling:** delete `Mods\DiscoDictionary.dll` (and `UserData\DiscoDictionary` if you want to
  remove your saved data too).

## Project status

The dictionary logic is unit-tested, and the mod is compile-checked automatically on every change. It
has **not yet been run against the live game**, because that can't happen on a build server. If
something doesn't work the first time, the MelonLoader console output is the most useful thing to send.

## For developers

```
data/glossary/            The dictionary content (JSON, comments allowed). Embedded into the DLL.
src/DiscoDictionary.Core  All logic with no game dependency: loading, term matching, search,
                          formatting, spoiler tracking, the English dictionary client.
src/DiscoDictionary.Mod   The MelonLoader mod: dialogue hook, UI (uGUI + TextMeshPro), input, settings.
tests/                    xUnit tests, including validation of every glossary file.
tools/CompileCheck        Builds the mod against stand-ins for the game's assemblies (used by CI).
tools/generate_glossary_md.py   Regenerates GLOSSARY.md from the JSON.
```

- `dotnet test tests/DiscoDictionary.Core.Tests` runs the tests (no game needed).
- `dotnet build tools/CompileCheck/CompileCheck.csproj` checks the mod compiles (no game needed).
- `dotnet build src/DiscoDictionary.Mod -p:GamePath="..."` builds the real mod and installs it.
- After editing the glossary, run `python3 tools/generate_glossary_md.py` and commit `GLOSSARY.md`.

How it hooks the game: a Harmony postfix on `LogRenderer.AddToLog(FinalEntry)` receives every line added
to the dialogue log (speaker and text), which is the approach used by the
[Disco Elysium accessibility mod](https://github.com/LordLuceus/disco-accessibility). Word lookup
uses TextMeshPro's own word hit-testing on whatever text is under the cursor.

## Credits

- Disco Elysium is made by ZAUM. This is an unofficial fan project, not affiliated with or endorsed by ZAUM.
  The glossary is original writing that describes the game; it doesn't contain the game's text.
- English definitions come from the [Free Dictionary API](https://dictionaryapi.dev/), which is built on
  [Wiktionary](https://www.wiktionary.org/) (CC BY-SA).
- Runs on [MelonLoader](https://github.com/LavaGang/MelonLoader).
