using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace com.github.lhervier.ksp.diag.terrainquads
{
    /// <summary>
    /// Seam viewer. In flight, draws over the terrain of the active vessel's body, wherever a quad of the
    /// highest subdivision level lies against a coarser quad, the triangles both quads have along the
    /// side they share: green for the finer quad, red for the coarser one, filled and outlined. A yellow
    /// vertical line rises from the shared vertex where the two quads are furthest apart. The drawing
    /// shows through the terrain; the buttons of a small window choose between all of it, the yellow line
    /// only, and nothing, and Mod+F6 shows or hides that window. The window also shows how far apart the two
    /// quads put the vertices they share (see <see cref="SeamReading"/>). Nothing is written unless the
    /// tester asks: the Log button of the window writes that reading to a CSV file, and to another the
    /// distance from the centre of the body to every vertex of every quad of the highest level, and of the
    /// coarser quads against them (see <see cref="VertexLog"/>).
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KSPDiagTerrainQuads : MonoBehaviour
    {
        private const string LOG_PREFIX = "[KSPDiagTerrainQuads] ";

        // Unity's own shader for its debug lines: unlike the unlit shaders of the game, it lets a script
        // turn the depth test off.
        private const string SHADER_NAME = "Hidden/Internal-Colored";

        // How often the quads are looked at again, in seconds of real time. The drawing itself follows
        // the quads every frame.
        private const float REFRESH_PERIOD = 0.25f;

        private static readonly Color FINE_FILL = new Color(0f, 1f, 0f, 0.25f);
        private static readonly Color FINE_LINE = new Color(0f, 0.45f, 0f, 1f);
        private static readonly Color COARSE_FILL = new Color(1f, 0f, 0f, 0.25f);
        private static readonly Color COARSE_LINE = new Color(0.5f, 0f, 0f, 1f);
        private static readonly Color MARKER = new Color(1f, 0.9f, 0f, 1f);

        // Drawn after everything else, the lines after the fills so that no fill covers them, and the
        // marker last.
        private const int FILL_QUEUE = 4000;
        private const int LINE_QUEUE = 4001;
        private const int MARKER_QUEUE = 4002;

        private Material fineFill;
        private Material fineLine;
        private Material coarseFill;
        private Material coarseLine;
        private Material markerLine;

        // Mod+F6 shows or hides the window, the same key for every KSP Diag.
        private static readonly KeyBinding WINDOW_KEY = new KeyBinding(KeyCode.F6);
        private const int WINDOW_ID = 0x47485004;

        /// <summary>What is drawn. Measuring goes on whatever is chosen.</summary>
        internal enum Display
        {
            Everything,
            MarkerOnly,
            Nothing,
        }

        // Static, so that the choices survive a scene change or a reload.
        private static Display display = Display.Everything;
        private static bool windowVisible = true;

        /// <summary>Whether the window shows, as Mod+F6 toggles it; the measures go on either way.</summary>
        internal static bool WindowVisible
        {
            get { return windowVisible; }
            set { windowVisible = value; }
        }

        private static Rect windowRect = new Rect(60f, 60f, 520f, 0f);

        private CelestialBody body;
        private PQS sphere;
        private readonly WorstMarker marker = new WorstMarker();

        // The seam with the largest gap, at the last refresh; null when no vertex was matched.
        private Seam worst;

        // What the seams showed at the last refresh; null while the active vessel's body has no terrain.
        private SeamReading reading;

        private readonly Dictionary<PQ, EdgeOverlay> fineOverlays = new Dictionary<PQ, EdgeOverlay>();
        private readonly Dictionary<PQ, EdgeOverlay> coarseOverlays = new Dictionary<PQ, EdgeOverlay>();
        private readonly List<Seam> seams = new List<Seam>();
        private float nextRefresh;

        // Scratch buffers of a refresh.
        private readonly List<PQ> leaves = new List<PQ>();
        private readonly Dictionary<PQ, int> fineSides = new Dictionary<PQ, int>();
        private readonly Dictionary<PQ, int> coarseSides = new Dictionary<PQ, int>();

        // Shifts of the origin since the last Log, and what the window says of that Log.
        private int shiftsSinceLog;
        private string lastVertexLog = "";

        private void Awake()
        {
            Shader shader = Shader.Find(SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError(LOG_PREFIX + "shader " + SHADER_NAME + " not found: nothing will be drawn");
                enabled = false;
                return;
            }
            fineFill = MakeMaterial(shader, FINE_FILL, FILL_QUEUE);
            fineLine = MakeMaterial(shader, FINE_LINE, LINE_QUEUE);
            coarseFill = MakeMaterial(shader, COARSE_FILL, FILL_QUEUE);
            coarseLine = MakeMaterial(shader, COARSE_LINE, LINE_QUEUE);
            markerLine = MakeMaterial(shader, MARKER, MARKER_QUEUE);

            // Shader.Find answers with whatever carries the name: without a depth test to turn off, the
            // terrain hides the drawing, which the tester should know.
            if (!fineFill.HasProperty("_ZTest"))
            {
                Debug.LogWarning(LOG_PREFIX + "shader " + shader.name
                    + " has no depth test to turn off: the terrain will hide the drawing");
            }

            // An instance method: EventData refuses a static handler.
            GameEvents.onFloatingOriginShift.Add(OnOriginShift);
        }

        private void OnDestroy()
        {
            GameEvents.onFloatingOriginShift.Remove(OnOriginShift);
            Clear();
            marker.Destroy();

            // None of them exists when the shader was not found.
            foreach (Material material in new[] { fineFill, fineLine, coarseFill, coarseLine, markerLine })
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
        }

        /// <summary>A material of <paramref name="color"/> that ignores the depth of what is already
        /// drawn, drawn at <paramref name="queue"/>.</summary>
        private static Material MakeMaterial(Shader shader, Color color, int queue)
        {
            Material material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetColor("_Color", color);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)CompareFunction.Always);
            material.renderQueue = queue;
            return material;
        }

        private void OnOriginShift(Vector3d offset, Vector3d nonFrame)
        {
            // The quads have just been moved: measure them again at once.
            nextRefresh = 0f;
            shiftsSinceLog++;
        }

        private void Update()
        {
            if (GameSettings.MODIFIER_KEY.GetKey() && WINDOW_KEY.GetKeyDown())
            {
                windowVisible = !windowVisible;
            }

            if (Time.unscaledTime < nextRefresh)
            {
                return;
            }
            nextRefresh = Time.unscaledTime + REFRESH_PERIOD;
            Refresh();
        }

        private void OnGUI()
        {
            if (!windowVisible)
            {
                return;
            }
            GUI.skin = HighLogic.Skin;

            // Laid out anew at every frame, so that the window shrinks when the reading has fewer lines.
            windowRect.height = 0f;
            windowRect = GUILayout.Window(WINDOW_ID, windowRect, DrawWindow, "KSP Diag - Terrain Quads");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            foreach (Display choice in new[] { Display.Everything, Display.MarkerOnly, Display.Nothing })
            {
                // The choice in force is the one that cannot be pressed.
                GUI.enabled = choice != display;
                if (GUILayout.Button(Describe(choice)))
                {
                    SetDisplay(choice);
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (reading == null)
            {
                GUILayout.Label("No terrain under the active vessel");
            }
            else
            {
                foreach (string line in reading.Lines())
                {
                    GUILayout.Label(line);
                }
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Log", GUILayout.ExpandWidth(false)))
            {
                try
                {
                    LogVertices();
                }
                catch (InvalidOperationException e)
                {
                    lastVertexLog = "no Log: " + e.Message;
                }
            }
            GUILayout.Label(lastVertexLog);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private static string Describe(Display choice)
        {
            return choice == Display.Everything ? "seams and largest gap"
                : choice == Display.MarkerOnly ? "largest gap only"
                : "nothing";
        }

        /// <summary>Chooses what is drawn, as the buttons of the window do.</summary>
        internal void SetDisplay(Display choice)
        {
            display = choice;
        }

        /// <summary>What is drawn.</summary>
        internal Display Current
        {
            get { return display; }
        }

        /// <summary>Where the window is on the screen, and how big.</summary>
        internal Rect WindowRect
        {
            get { return windowRect; }
            set { windowRect = value; }
        }

        /// <summary>
        /// Writes a Log as the Log button does: the reading of the seams as the window shows it, and the
        /// quads as they are now, every quad of the highest level and every coarser quad against one of
        /// them. Returns the number of the Log, how many quads it holds and the two files it went to, ready
        /// to be written as JSON; throws when the active vessel's body has no terrain.
        /// </summary>
        internal Dictionary<string, object> LogVertices()
        {
            // Measured again first, so that the Log holds the quads of this very frame.
            Refresh();
            if (reading == null)
            {
                throw new InvalidOperationException("the body of the active vessel has no terrain");
            }

            // The leaves of the highest level whether they show or not, then the coarse side of each seam;
            // a set keeps a coarse quad met by several seams to one line.
            HashSet<PQ> quads = new HashSet<PQ>();
            foreach (PQ leaf in leaves)
            {
                if (leaf.subdivision == sphere.maxLevel)
                {
                    quads.Add(leaf);
                }
            }
            foreach (Seam seam in seams)
            {
                quads.Add(seam.Coarse);
            }

            int shifts = shiftsSinceLog;
            int log = VertexLog.Write(body, reading, quads, shifts);
            shiftsSinceLog = 0;
            lastVertexLog = "Log " + log + ": " + quads.Count + " quad(s), " + shifts + " origin shift(s) since the previous one";
            Debug.Log(LOG_PREFIX + lastVertexLog + ", written to " + VertexLog.LogsPath + " and " + VertexLog.QuadsPath);
            return new Dictionary<string, object>
            {
                { "log", log },
                { "quads", quads.Count },
                { "originShifts", shifts },
                { "logsFile", VertexLog.LogsPath },
                { "quadsFile", VertexLog.QuadsPath }
            };
        }

        /// <summary>
        /// The seams as last measured, as the window shows them, ready to be written as JSON (see
        /// <see cref="SeamReading.ToJson"/>); empty while the active vessel's body has no terrain.
        /// </summary>
        internal Dictionary<string, object> Reading()
        {
            return reading != null ? reading.ToJson() : new Dictionary<string, object>();
        }

        private void LateUpdate()
        {
            if (display == Display.Everything)
            {
                foreach (EdgeOverlay overlay in fineOverlays.Values)
                {
                    overlay.Draw(fineFill, fineLine);
                }
                foreach (EdgeOverlay overlay in coarseOverlays.Values)
                {
                    overlay.Draw(coarseFill, coarseLine);
                }
            }
            if (display != Display.Nothing)
            {
                marker.Draw(markerLine, body);
            }
        }

        /// <summary>
        /// Finds the seams of the active vessel's body as they are now, brings the drawing in line with
        /// them, and measures them.
        /// </summary>
        private void Refresh()
        {
            body = FlightGlobals.currentMainBody;
            PQS current = body != null ? body.pqsController : null;
            if (current != sphere)
            {
                Clear();
                sphere = current;
            }
            if (sphere == null || sphere.quads == null)
            {
                return;
            }

            FindSeams();
            UpdateOverlays(fineOverlays, fineSides);
            UpdateOverlays(coarseOverlays, coarseSides);
            worst = null;
            foreach (Seam seam in seams)
            {
                seam.Measure();
                if (seam.WorstVertex >= 0 && (worst == null || seam.MaxGap > worst.MaxGap))
                {
                    worst = seam;
                }
            }
            marker.PointAt(worst?.Fine, worst != null ? worst.WorstVertex : 0);
            reading = new SeamReading(body, sphere, seams, fineOverlays.Count, coarseOverlays.Count, worst);
        }

        /// <summary>
        /// Fills <see cref="seams"/>, <see cref="fineSides"/> and <see cref="coarseSides"/> with the
        /// seams of <see cref="sphere"/> as they are now.
        /// </summary>
        private void FindSeams()
        {
            seams.Clear();
            fineSides.Clear();
            coarseSides.Clear();

            leaves.Clear();
            foreach (PQ root in sphere.quads)
            {
                CollectLeaves(root);
            }

            int level = sphere.maxLevel;
            foreach (PQ fine in leaves)
            {
                if (fine.subdivision != level || !fine.isVisible)
                {
                    continue;
                }
                foreach (Side side in QuadGrid.SIDES)
                {
                    // The same test as PQ.GetEdgeState, the one that makes stock stitch this side.
                    PQ coarse = QuadGrid.Neighbour(fine, side);
                    if (coarse == null || coarse.subdivision >= fine.subdivision)
                    {
                        continue;
                    }
                    Side? facing = QuadGrid.FacingSide(coarse, fine);
                    if (facing == null || coarse.isSubdivided || !coarse.isVisible)
                    {
                        continue;
                    }
                    seams.Add(new Seam(fine, side, coarse, facing.Value));
                    AddSide(fineSides, fine, side);
                    AddSide(coarseSides, coarse, facing.Value);
                }
            }
        }

        /// <summary>Adds to <see cref="leaves"/> the quads under <paramref name="quad"/> that are not
        /// split into smaller ones, <paramref name="quad"/> included.</summary>
        private void CollectLeaves(PQ quad)
        {
            if (quad == null)
            {
                return;
            }
            if (!quad.isSubdivided || quad.subNodes == null)
            {
                leaves.Add(quad);
                return;
            }
            foreach (PQ child in quad.subNodes)
            {
                CollectLeaves(child);
            }
        }

        private static void AddSide(Dictionary<PQ, int> sides, PQ quad, Side side)
        {
            sides.TryGetValue(quad, out int mask);
            sides[quad] = mask | (int)side;
        }

        /// <summary>
        /// Makes <paramref name="overlays"/> hold one overlay per quad of <paramref name="sides"/>,
        /// drawing the sides it maps that quad to, and nothing else.
        /// </summary>
        private static void UpdateOverlays(Dictionary<PQ, EdgeOverlay> overlays, Dictionary<PQ, int> sides)
        {
            List<PQ> gone = null;
            foreach (KeyValuePair<PQ, EdgeOverlay> entry in overlays)
            {
                if (entry.Key == null || !sides.ContainsKey(entry.Key))
                {
                    (gone ?? (gone = new List<PQ>())).Add(entry.Key);
                }
            }
            if (gone != null)
            {
                foreach (PQ quad in gone)
                {
                    overlays[quad].Destroy();
                    overlays.Remove(quad);
                }
            }

            foreach (KeyValuePair<PQ, int> entry in sides)
            {
                if (!overlays.TryGetValue(entry.Key, out EdgeOverlay overlay))
                {
                    overlay = new EdgeOverlay(entry.Key);
                    overlays.Add(entry.Key, overlay);
                }
                overlay.Refresh(entry.Value);
            }
        }

        /// <summary>Drops every overlay and seam.</summary>
        private void Clear()
        {
            foreach (EdgeOverlay overlay in fineOverlays.Values)
            {
                overlay.Destroy();
            }
            foreach (EdgeOverlay overlay in coarseOverlays.Values)
            {
                overlay.Destroy();
            }
            fineOverlays.Clear();
            coarseOverlays.Clear();
            seams.Clear();
            worst = null;
            reading = null;
            marker.PointAt(null, 0);
        }
    }
}
