using System;
using GoRogue.Components;
using GoRogue.GameFramework;
using GoRogue.GameFramework.Components;
using SadConsole;
using SadConsole.Components;
using SadConsole.Entities;
using SadRogue.Primitives;

namespace TheSadRogue.Integration
{
    public class RogueLikeEntity : Entity, IGameObject
    {
        private bool _isTransparent;
        private bool _isWalkable;

        public uint ID { get; }
        public int Layer => ZIndex;
        public Map? CurrentMap { get; private set; }
        public ITaggableComponentCollection GoRogueComponents { get; private set; }

        /// <summary>
        /// Each and every component on this entity
        /// </summary>
        /// <remarks>
        /// Confused about which collection to add a component to?
        /// Add it here.
        /// </remarks>
        public ITaggableComponentCollection AllComponents => GoRogueComponents;

        /// <inheritdoc />
        Point IGameObject.Position
        {
            get => base.Position;
            set => base.Position = value;
        }

        /// <inheritdoc />
        public bool IsTransparent
        {
            get => _isTransparent;
            set => this.SafelySetProperty(ref _isTransparent, value, TransparencyChanged);
        }

        /// <inheritdoc />
        public bool IsWalkable
        {
            get => _isWalkable;
            set => this.SafelySetProperty(ref _isWalkable, value, WalkabilityChanged);
        }

        #region initialization

        public RogueLikeEntity(Point position, int glyph, bool walkable = true, bool transparent = true, int layer = 1)
            : this(position, Color.White, Color.Black, glyph, walkable, transparent, layer)
        { }

        public RogueLikeEntity(Point position, Color foreground, int glyph, bool walkable = true, bool transparent = true, int layer = 1)
            : this(position, foreground, Color.Black, glyph, walkable, transparent, layer)
        { }

        public RogueLikeEntity(Point position, Color foreground, Color background, int glyph, bool walkable = true, bool transparent = true, int layer = 1)
            : base(foreground, background, glyph, layer != 0 ? layer : throw new ArgumentException($"{nameof(RogueLikeEntity)} objects may not reside on the terrain layer.", nameof(layer)))
        {
            Position = position;
            PositionChanged += Position_Changed;

            Appearance = new ColoredGlyph(foreground, background, glyph);

            IsWalkable = walkable;
            IsTransparent = transparent;

            GoRogueComponents = new ComponentCollection();
            AllComponents.ComponentAdded += On_GoRogueComponentAdded;
            AllComponents.ComponentRemoved += On_GoRogueComponentRemoved;

            ID = GoRogue.Random.GlobalRandom.DefaultRNG.NextUInt();
        }
        #endregion

        public void OnMapChanged(Map? newMap)
            => CurrentMap = newMap;

        #region event handlers

        public event EventHandler<GameObjectPropertyChanged<Point>>? Moved;
        public event EventHandler<GameObjectPropertyChanged<bool>>? TransparencyChanged;
        public event EventHandler<GameObjectPropertyChanged<bool>>? WalkabilityChanged;


        private void On_GoRogueComponentAdded(object? s, ComponentChangedEventArgs e)
        {
            if (e.Component is IComponent sadComponent)
                SadComponents.Add(sadComponent);
            if (e.Component is IGameObjectComponent goRogueComponent)
            {
                if (goRogueComponent.Parent != null)
                    throw new ArgumentException(
                        $"Components implementing {nameof(IGameObjectComponent)} cannot be added to multiple objects at once.");

                goRogueComponent.Parent = this;
            }
        }

        private void On_GoRogueComponentRemoved(object? s, ComponentChangedEventArgs e)
        {
            if (e.Component is IComponent sadComponent)
                SadComponents.Remove(sadComponent);

            if (e.Component is IGameObjectComponent goRogueComponent)
            {
                goRogueComponent.Parent = null;
            }
        }

        private void Position_Changed(object? sender, ValueChangedEventArgs<Point> e)
            => Moved?.Invoke(sender, new GameObjectPropertyChanged<Point>(this, e.OldValue, e.NewValue));

        #endregion
    }
}
