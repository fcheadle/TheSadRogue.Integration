using System;
using System.Collections.Generic;
using System.Linq;
using GoRogue.GameFramework;
using SadRogue.Primitives.GridViews;
using GoRogue.SpatialMaps;
using SadConsole;
using SadConsole.Entities;
using SadRogue.Primitives;

namespace TheSadRogue.Integration
{
    /// <summary>
    /// A Map that contains the necessary function to render to a ScreenSurface
    /// </summary>
    public class RogueLikeMap : Map
    {
        private readonly List<ScreenSurface> _renderers;

        /// <summary>
        /// List of renderers (ScreenSurfaces) that currently render the map.
        /// </summary>
        public IReadOnlyList<ScreenSurface> Renderers => _renderers.AsReadOnly();

        /// <summary>
        /// An IGridView of the ColoredGlyphs on the Terrain layer (0) that will produce a fully
        /// transparent appearance if there is no terrain object at the requested location.
        /// </summary>
        public readonly IGridView<ColoredGlyph> TerrainView;

        private static readonly ColoredGlyph _transparentAppearance =
            new ColoredGlyph(Color.Transparent, Color.Transparent, 0, Mirror.None);

        /// <summary>
        /// Creates a new RogueLikeMap
        /// </summary>
        /// <param name="width">Desired width of map</param>
        /// <param name="height">Desired Height of Map</param>
        /// <param name="numberOfEntityLayers">How many entity layers to include</param>
        /// <param name="distanceMeasurement">How to measure the distance of a single-tile movement</param>
        /// <param name="layersBlockingWalkability">Layers which should factor into move logic</param>
        /// <param name="layersBlockingTransparency">Layers which should factor into transparency</param>
        /// <param name="entityLayersSupportingMultipleItems">How many entity layers support multiple entities per layer</param>
        public RogueLikeMap(int width, int height, int numberOfEntityLayers, Distance distanceMeasurement,
            uint layersBlockingWalkability = uint.MaxValue, uint layersBlockingTransparency = uint.MaxValue,
            uint entityLayersSupportingMultipleItems = uint.MaxValue) : base(width, height, numberOfEntityLayers,
            distanceMeasurement, layersBlockingWalkability, layersBlockingTransparency,
            entityLayersSupportingMultipleItems)
        {
            ObjectAdded += Object_Added;
            ObjectRemoved += Object_Removed;

            _renderers = new List<ScreenSurface>();
            TerrainView = new LambdaTranslationGridView<IGameObject?, ColoredGlyph>(Terrain, GetTerrainAppearance);
        }

        /// <summary>
        /// Creates a renderer that renders this Map.  When no longer used, <see cref="DisposeOfRenderer"/> must
        /// be called.
        /// </summary>
        /// <param name="viewSize">Viewport size for the renderer.</param>
        /// <param name="font">Font to use for the renderer.</param>
        /// <param name="fontSize">Size of font to use for the renderer.</param>
        /// <returns>A renderer configured with the given parameters.</returns>
        public ScreenSurface CreateRenderer(Point? viewSize = null, Font? font = null, Point? fontSize = null)
        {
            // Default view size is entire Map
            var (viewWidth, viewHeight) = viewSize ?? (Width, Height);

            // Create surface representing the terrain layer of the map
            var cellSurface = new SettableCellSurface(this, viewWidth, viewHeight);

            // Create screen surface that renders that cell surface and keep track of it
            var renderer = new ScreenSurface(cellSurface, font, fontSize);
            _renderers.Add(renderer);

            // Create an EntityRenderer and configure it with all the appropriate entities,
            // then add it to the main surface
            var entityRenderer = new Renderer();
            // TODO: Reverse this order when it won't cause NullReferenceException
            renderer.SadComponents.Add(entityRenderer);
            entityRenderer.AddRange(Entities.Items.Cast<Entity>());

            // Return renderer
            return renderer;
        }

        /// <summary>
        /// Removes a renderer from the list of renders displaying the map.  This must be called when a renderer is no
        /// longer used, in order to ensure that the renderer resources are freed
        /// </summary>
        /// <param name="renderer">The renderer to unlink.</param>
        public void DisposeOfRenderer(ScreenSurface renderer) => _renderers.Remove(renderer);

        private void Object_Added(object? sender, ItemEventArgs<IGameObject> e)
        {
            switch (e.Item)
            {
                case RogueLikeCell terrain:
                    // Ensure we flag the surfaces of renderers as dirty on the add and on subsequent appearance changed events
                    terrain.AppearanceChanged += Terrain_AppearanceChanged;
                    Terrain_AppearanceChanged(terrain, EventArgs.Empty);
                    break;

                case RogueLikeEntity entity:
                    // Add to any entity renderers we have
                    foreach (var renderer in _renderers)
                        renderer.GetSadComponent<Renderer>()?.Add(entity);

                    break;

                default:
                    throw new InvalidOperationException(
                        $"Objects added to a {nameof(RogueLikeMap)} must be of type {nameof(RogueLikeCell)} for terrain, or {nameof(RogueLikeEntity)} for non-terrain");
            }
        }

        private void Object_Removed(object? sender, ItemEventArgs<IGameObject> e)
        {
            switch (e.Item)
            {
                case RogueLikeCell terrain:
                    // Ensure we flag the surfaces of renderers as dirty on the remove and unlike our changed handler
                    terrain.AppearanceChanged -= Terrain_AppearanceChanged;
                    Terrain_AppearanceChanged(terrain, EventArgs.Empty);
                    break;

                case RogueLikeEntity entity:
                    // Remove from any entity renderers we have
                    foreach (var renderer in _renderers)
                    {
                        var entityRenderer = renderer.GetSadComponent<Renderer>();
                        entityRenderer?.Remove(entity);
                    }
                    break;
            }
        }

        private void Terrain_AppearanceChanged(object? sender, EventArgs e)
        {
            foreach (var surface in _renderers)
                surface.IsDirty = true;
        }

        private static ColoredGlyph GetTerrainAppearance(IGameObject? gameObject)
            => ((RogueLikeCell?) gameObject)?.Appearance ?? _transparentAppearance;
    }
}
