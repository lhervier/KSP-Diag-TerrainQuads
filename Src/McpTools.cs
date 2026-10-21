using System;
using System.Collections.Generic;
using UnityEngine;
using com.github.lhervier.ksp.util.mcpserver;

namespace com.github.lhervier.ksp.diag.terrainquads
{
    /// <summary>
    /// What KSP-Util-MCPServer, when it is installed, offers of this mod as tools: the reading of the largest gap,
    /// the buttons that choose what is drawn, and the moving of the window. Each method works on the mod
    /// loaded in the flight scene. Nothing here is needed to play by hand, and this mod runs the same without
    /// KSP-Util-MCPServer: only that server reads the attribute.
    /// </summary>
    internal static class McpTools
    {
        [McpTool("terrainquads_read",
            "Reads KSP Diag - Terrain Quads, as its window shows it: body, level (the highest), sideVertices, seams, " +
                "fineQuads, coarseQuads, sharedVertices, noCounterpart, meanGapMm, maxGapMm, and for the largest gap " +
                "gapMm, verticalMm, finerAbove (whether the finer quad stands above the coarser one), horizontalMm, " +
                "the latitude, longitude, height above sea level (altitude) and underSea of its vertex, fineQuad, " +
                "fineSide, coarseQuad, coarseSide, coarseLevel, distanceKm and headingFromVessel from the active " +
                "vessel. Empty while the body of the active vessel has no terrain.")]
        internal static object Read()
        {
            return Mod().Reading();
        }

        [McpTool("terrainquads_log",
            "Writes a Log of KSP Diag - Terrain Quads, as its Log button does: one line in its file of Logs, and one " +
                "line per quad of the highest level, and per coarser quad against one of them, with the distance " +
                "from the centre of the body to each of its vertices in millimetres. Returns the number of the Log, " +
                "quads, originShifts (since the previous Log), logsFile and quadsFile.")]
        internal static object Log()
        {
            return Mod().LogVertices();
        }

        [McpTool("terrainquads_set_display",
            "Chooses what KSP Diag - Terrain Quads draws, as the buttons of its window do: everything (the seams " +
                "and the largest gap), marker (the yellow line of the largest gap only) or nothing.")]
        internal static object SetDisplay(string display)
        {
            KSPDiagTerrainQuads mod = Mod();
            switch ((display ?? "").ToLowerInvariant())
            {
                case "everything":
                    mod.SetDisplay(KSPDiagTerrainQuads.Display.Everything);
                    break;
                case "marker":
                    mod.SetDisplay(KSPDiagTerrainQuads.Display.MarkerOnly);
                    break;
                case "nothing":
                    mod.SetDisplay(KSPDiagTerrainQuads.Display.Nothing);
                    break;
                default:
                    throw new ArgumentException("display: everything, marker or nothing");
            }
            return mod.Current.ToString();
        }

        [McpTool("terrainquads_move_window",
            "Moves the window of KSP Diag - Terrain Quads, as dragging it does: x and y in pixels from the top left " +
            "corner of the screen. Returns its position and size (x, y, width, height).")]
        internal static object MoveWindow(double x, double y)
        {
            KSPDiagTerrainQuads mod = Mod();
            Rect rect = mod.WindowRect;
            rect.x = (float)x;
            rect.y = (float)y;
            mod.WindowRect = rect;
            return new Dictionary<string, object>
            {
                { "x", (double)rect.x },
                { "y", (double)rect.y },
                { "width", (double)rect.width },
                { "height", (double)rect.height }
            };
        }

        [McpTool("terrainquads_show_window",
            "Shows or hides the window of KSP Diag - Terrain Quads, as Mod+F6 does; what it measures goes on either " +
            "way. Returns whether it shows (visible).")]
        internal static object ShowWindow(bool visible)
        {
            KSPDiagTerrainQuads.WindowVisible = visible;
            return new Dictionary<string, object> { { "visible", KSPDiagTerrainQuads.WindowVisible } };
        }

        private static KSPDiagTerrainQuads Mod()
        {
            KSPDiagTerrainQuads mod = UnityEngine.Object.FindObjectOfType<KSPDiagTerrainQuads>();
            if (mod == null)
            {
                throw new InvalidOperationException("KSP Diag - Terrain Quads only runs in flight");
            }
            return mod;
        }
    }
}
