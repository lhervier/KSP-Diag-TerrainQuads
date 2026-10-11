# What it shows

Part of [KSP Diag - Terrain Quads](../README.md): the seam between two subdivision levels, what the mod
draws over it, what its window says of it, and what its *Log* button writes to its two files.

## The seam

The terrain of a body is made of square patches, the quads, split into four smaller ones as the craft
gets closer. Around the active craft, the quads are split down to the highest level the body allows;
further away, they are coarser.

Every quad has the same grid of vertices, 15 by 15 in stock. A quad one level coarser covers twice the
width, so along the edge they share, the finer quad has twice as many vertices: every other one has no
counterpart on the coarser edge, and would leave a crack.

The game does not move those vertices. It changes the triangles of the finer quad along that edge:
`PQ.GetEdgeState` marks each side whose neighbour has a lower subdivision level, and the quad takes one
of sixteen precomputed lists of triangles, one per combination of marked sides (`PQS.cacheIndices`).
Along a marked side, each pair of cells gets three triangles instead of four, and the vertex between
them is left out of all of them:

![A quad of the highest level under a quad one level coarser, four by four cells: along their common edge, every other vertex of the finer quad is left out, and the triangles around it are rebuilt on the vertices both quads share](../imgs/stitching.svg)

The finer edge then runs along the same segments as the coarser one — provided the vertices both quads
share land on the same point. The triangles this mod draws are these ones, along the edges of both
quads.

The quads of the highest level follow the active craft: as it moves, the quads ahead of it are split
and the ones far behind it are merged back. The seam therefore stays about the same distance from the
craft whatever it does, and a craft cannot be driven up to it.

## The drawing

For every side where a quad of the highest level lies against a coarser quad, the mod draws, over the
terrain:

- **in green**, the triangles the finer quad has along that side, as the game draws them at that moment;
- **in red**, the triangles the coarser quad has along the side that faces it;
- **a yellow vertical line**, 1 km tall, rising from the shared vertex where the two quads are furthest
  apart: the one the window talks about.

Each triangle is filled, see-through, and outlined in a darker shade of the same colour, one pixel wide.
The drawing ignores the depth of the terrain: it stays visible behind a hill, and every vertex is drawn
where the game puts it, not lifted towards the camera. The edges between two quads of the highest level
are not drawn, and nothing is drawn while no quad of the highest level is shown, high above the ground
for instance.

<p>
<img src="../imgs/drawing-kerbin.png" width="49%" alt="The drawing seen from 10 km above a landed capsule: a green band inside, a red band outside, all around the zone of the highest level, and a short yellow line on its lower left edge">
<img src="../imgs/drawing-earth.jpg" width="49%" alt="The drawing seen from low over grassland: the red and green bands of the seam zigzag across the ground in the foreground and along the horizon, each band made of triangles, and a yellow line stands on the nearer seam">
</p>

*Left: stock KSP 1.12.5 with KSP Community Fixes, a capsule landed a few kilometres from the Space
Center, the camera pulled back 10 km from it. Right: Real Solar System as released on KSP 1.12.5, with
KSP Community Fixes, a craft on the launchpad at Cape Canaveral, the camera pulled back beyond the edge
of the zone of the highest level and looking back towards the craft.*

**Three buttons** in the mod's window choose the mode: everything, the yellow line only, nothing. The
triangles cover the ground they are drawn on; the yellow line alone shows where to look while leaving the
ground itself in sight. The button of the mode in force is greyed out; `Alt+F6` hides the window. The
choice is kept across scene changes and reloads, and measuring goes on whatever it is.

## The reading

Under its buttons, the window shows what the seams are at that moment, measured again four times a
second and right after every shift of the origin of the world:

```
<body>, highest level <level>, <n> vertices along a side of a quad
<n> seam(s) between <n> quad(s) of level <level> and <n> coarser one(s), <n> shared vertices
Gap: mean <gap> mm, max <gap> mm
Largest gap: the finer quad <gap> mm above|below the coarser one, <gap> mm beside it
At <latitude>° lat, <longitude>° lon, <height> m above sea level|under the sea; <distance> km from the vessel, heading <heading>°
On <quad> (<side>, level <level>) against <quad> (<side>, level <level>)
```

- **Highest level**: the subdivision level of the finest quads of the body, the ones around the craft.
- **Shared vertices**: along a side it has changed, the finer quad keeps one vertex in two, and each of
  them should land on a vertex of the coarser quad. The mod pairs each one with the nearest vertex of
  the coarser side.
- **Gap**: the distance between the two vertices of a pair, averaged over all pairs, and the largest
  one. Both vertices are taken through the matrix their quad is drawn with, and the distance is computed
  in double precision: it is the distance between the two places the game puts them.
- **Above, below, beside**: that largest gap split along the vertical of the body at the vertex. The
  vertical part is a step in the ground; the part beside it is a crack or an overlap, which hardly shows
  from above.
- **At**: where that vertex is, whether the sea covers it, and how far and in which direction it lies
  from the active vessel, the heading counted from north towards east. The yellow line rises from it.
- **On**: the two quads and sides of the largest gap.
- **Vertices with no counterpart**, after the gaps when there are any: a vertex of the finer side further
  than a quarter of the coarser side's spacing from every vertex of it. This should never appear; if it
  does, the mod has misread the grid, and its gaps should not be trusted.

After a load, the figures change for a second or two while the game builds the quads around the craft,
then stay put as long as nothing moves: the reading of that load is the one they settle on.

The mod writes none of this anywhere by itself. To keep a reading, press *Log*: it goes to a file, with
the time and where the craft is ([The Log button](#the-log-button)). In `KSP.log`, the mod writes only
an error if the drawing cannot be made, and one line each time *Log* is pressed, saying where it wrote.

## The Log button

The *Log* button, under the reading in the window, writes that reading, and where every vertex of the
terrain is at that moment: for each quad of the highest level, shown or not, and for each coarser quad
against one of them, the distance from the centre of the body to each of its vertices. It goes to two
files, created at
the first *Log* of a run of KSP in `GameData/KSPDiagTerrainQuads/PluginData/`, named after the time they
are created, each *Log* adding its lines to them. Columns are separated by `;`, and numbers written with
`.` as their decimal separator: a spreadsheet opens them once told so.

`logs-<date>.csv` gets one line per *Log*:

| Column | What it holds |
|---|---|
| Log | the number of the *Log*, from 1 at the first one of the run of KSP |
| UT | the time of the game, in seconds |
| Body | the body of the active craft, whose terrain is written |
| Body x, y, z (m) | where the centre of the body is in the world of the game, whose origin is the floating origin |
| Vessel, Latitude, Longitude, Altitude (m) | the active craft and where it is |
| Surface speed, Orbital speed (m/s) | how fast it goes |
| Krakensbane (m/s) | the speed the game takes off the craft and gives the world instead, 0 when it does not |
| Origin shifts since previous Log | how many times the floating origin was moved since the previous *Log* |
| Quads | how many lines this *Log* added to the other file |
| Level, Seams, Finer quads, Coarser quads, Shared vertices, Vertices with no counterpart | the second line of the reading, and the count of vertices with no counterpart, 0 when there are none |
| Gap mean (mm), Gap max (mm) | the gaps of the reading |
| Finer quad at largest gap, Step (mm), Beside (mm) | the largest gap: `above` or `below`, its vertical part and the part beside it |
| Largest gap latitude, longitude, altitude (m), under the sea | where its vertex is, and `yes` when the sea covers it |
| Largest gap from vessel (km), heading from vessel | how far it lies from the active craft, and in which direction, in degrees from north towards east |
| Finer quad, Finer side, Coarser quad, Coarser side, Coarser level | the two quads and sides of the largest gap |

The columns of the largest gap are left empty when no vertex is shared. The figures are those of the
window, with all their digits.

`quads-<date>.csv` gets one line per quad, for each *Log*:

| Column | What it holds |
|---|---|
| Log | the number of the *Log* the line belongs to |
| Quad | the name the game gives the quad |
| Level | its subdivision level |
| Visible | whether the game draws it |
| v0 (mm) to v224 (mm) | the distance from the centre of the body to each of its vertices, in millimetres |

**The name of a quad says where it is.** The six quads the game starts from are named after the body
and the face of a cube they cover, `Kerbin Xp` to `Kerbin Zn`; each time a quad is split in four, its
children take its name with one more digit, 0 to 3 (`PQS.CreateQuads`, `PQ.Subdivide`). A name is therefore
the path to the quad, as long as its level: the same quad, built again at the same place, takes the same
name, and the lines of one quad can be followed from one *Log* to the next. Two quads whose names differ
only by their last digit are split from the same quad, at the same moment.

**The vertices are in the order of the game's own grid**, 15 by 15, row by row (`PQS.vi(x, z)`, the
vertex of column x and row z at x + 15 z). The first row and the last one, the first column and the
last one are the four edges of the quad.

Each vertex is taken through the matrix its quad is drawn with, computed in double precision, as the
gaps of the seams are: the distance is the one from the centre of the body to the place the game puts
the vertex. It does not depend on where the floating origin is, so the same vertex can be compared from
one *Log* to the next, whatever the origin did in between.
