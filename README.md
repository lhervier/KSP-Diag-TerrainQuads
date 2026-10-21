# KSP Diag - Terrain Quads

**⚠️ Work in progress.** This is an active investigation, not a finished mod. The code and this page can still change, and several questions are still open.

**How this was made.** Written with Claude, Anthropic's AI assistant, and reviewed line by line by a
human — me. I am saying so up front, because contributions made with an AI deserve a closer look than
others, and because some people would rather stop reading here. That look is easy to give here: this
mod changes nothing in the game, it only reads the vertices and triangles the game already holds for
each quad, and the source is a few hundred lines with no dependency of any kind.

A viewing and measuring instrument for KSP 1.12, for the quads the terrain is built of. In flight, it
draws over the terrain the seam where the quads of the highest subdivision level meet coarser quads:
the triangles each of the two quads has along the edge they share, green for the finer quad, red for
the coarser one, and a yellow line where the two are furthest apart. The drawing shows through the
hills, so the whole seam can be followed from far away. Its window shows how far apart the two quads
put the vertices they share, and, at the press of a button, the mod writes that reading to a file along
with where every vertex of every quad around the craft is, so that a quad can be followed from one press
to the next and compared with its neighbours.

## What it shows

Where quads of the highest level meet coarser ones, the mod draws the triangles of both along the seam,
marks with a yellow line where they are furthest apart, and shows the gaps in its window. Its *Log*
button writes those gaps, and how far every vertex around the craft is from the centre of the body.

![The drawing seen from 10 km above a landed capsule: a green band inside, a red band outside, all around the zone of the highest level](imgs/drawing-kerbin.png)

*Stock KSP 1.12.5 with KSP Community Fixes: a capsule landed a few kilometres from the Space Center,
the camera pulled back 10 km from it.*

**→ Full chapter: [What it shows](docs/what-it-shows.md)**

## Taking a reading

The drawing is there in every flight: fly, or land, and look at the terrain around the craft. The
buttons of the window switch between the whole drawing, the yellow line alone, and nothing; under them,
the window gives the gaps of the seam, as numbers. The mod writes nothing by itself: press *Log* to keep
what the window shows.

The *Log* button writes two files, in `GameData/KSPDiagTerrainQuads/PluginData/`: copy them somewhere
before starting KSP again, which starts a new pair at its first *Log*. Both are plain text and open in
any spreadsheet: import them as text, with `;` as the separator of columns and `.` as the decimal
separator. Each line of the file of Logs is one press, with the reading of the window at that moment.
Each line of the file of quads is one quad at one *Log*: filtering on its name follows a quad from one
*Log* to the next, and filtering on a *Log* gives the terrain at that moment.

## Driving it by script

With [KSP-Util-MCPServer](https://github.com/lhervier/KSP-Util-MCPServer) installed next to it, this mod offers
its window to a program, as tools of that server: a script presses its buttons and reads what it shows,
as a player would. Without the server, this mod runs the same.

| tool | what it does |
|---|---|
| `terrainquads_read` | reads what the window shows: the body, the highest level, the seams, quads and shared vertices counted, the mean and largest gaps, and where the largest gap lies; empty while the body of the active vessel has no terrain |
| `terrainquads_log` | presses *Log*: writes a Log and returns its number, the quads written, the shifts of the floating origin since the previous Log, and the two files |
| `terrainquads_set_display` | chooses what the mod draws, as the buttons of the window do: `everything`, `marker` (the yellow line alone) or `nothing` |
| `terrainquads_move_window` | moves the window, as dragging it does: `x` and `y` in pixels from the top left corner of the screen; returns its position and size |
| `terrainquads_show_window` | shows or hides the window, as `Alt+F6` does; the drawing goes on either way |

## Get it

Either way you end up with the same `GameData/KSPDiagTerrainQuads/` folder.

**Download it** — from the assets of the
[latest release](https://github.com/lhervier/KSP-Diag-TerrainQuads/releases/latest).

**Or compile it** — clone this repository, set `KSPDIR` to your KSP install folder and run
`build.bat`. It needs the .NET SDK and
[KSP-Util-MCPServer](https://github.com/lhervier/KSP-Util-MCPServer) installed in that KSP, takes a few seconds,
reads the KSP assemblies straight from your install, and puts the DLL in
`GameData/KSPDiagTerrainQuads/` inside the repository. It does not install anything.
KSP-Util-MCPServer is only needed to compile: it provides the attribute that marks what this mod offers to
it, and this mod runs the same without it. Worth doing if you would rather not run a binary you have no source for while
reporting a measurement.

## Install

Drop `GameData/KSPDiagTerrainQuads` into the `GameData` of KSP, so that you end up with
`GameData/KSPDiagTerrainQuads/KSPDiagTerrainQuads.dll`. It runs on a stock install:
no Harmony, no ModuleManager, no dependency of any kind.

The drawing is there in every flight, the window's *nothing* hides it, and `Alt+F6` hides the window. The mod reads the terrain
and writes nothing but the two files of its *Log* button, in its own `PluginData` folder, and a line in
`KSP.log` at each press; your saves are never touched. Removing the folder removes
the mod.

## License

MIT
