using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using GoRogue.Components;
using GoRogue.GameFramework;
using GoRogue.GameFramework.Components;
using SadConsole;
using SadConsole.Components;
using SadConsole.Input;
using SadRogue.Primitives;
using TheSadRogue.Integration.Components;

namespace TheSadRogue.Integration
{
    /// <summary>
    /// Everything that will be rendered to the screen, except for written text
    /// </summary>
    public class RogueLikeEntity : ColoredGlyph, IGameObject, IScreenObject
    {
        #region properties

        public bool IsTransparent { get; set; }
        public bool UsePixelPositioning { get; set; }
        private bool _isVisible = true;
        private bool _isEnabled = true;
        private bool _isfocused;
        public string Name { get; set; }
        public int ZIndex { get; }
        public uint ID { get; }
        public int Layer { get; }
        public Map? CurrentMap { get; private set; }
        private ColoredGlyph _glyph;
        private IScreenObject[] _childrenSerialized;
        private Point _position;
        private IScreenObject _parentObject;
        private IComponent[] _componentsSerialized;
        
        public RogueLikeComponentCollection Components { get; }
        public ObservableCollection<IComponent> SadComponents { get; }
        public ITaggableComponentCollection GoRogueComponents { get; }

        public FocusBehavior FocusedMode { get; set; } = FocusBehavior.Set;
        public bool IsExclusiveMouse { get; set; }
        public bool UseKeyboard { get; set; }
        public bool UseMouse { get; set; }
        public ScreenObjectCollection Children { get; }

        public bool IsWalkable { get; set; }
        public event EventHandler<GameObjectPropertyChanged<bool>>? TransparencyChanged;
        public event EventHandler<GameObjectPropertyChanged<bool>>? WalkabilityChanged;
        public event EventHandler<GameObjectPropertyChanged<Point>>? Moved;
        public event EventHandler<NewOldValueEventArgs<IScreenObject>> ParentChanged;
        public event EventHandler<NewOldValueEventArgs<Point>> PositionChanged;
        public event EventHandler IsDirtyChanged;
        public event EventHandler VisibleChanged;
        public event EventHandler EnabledChanged;
        public event EventHandler FocusLost;
        public event EventHandler Focused;
        public Point AbsolutePosition { get; protected set; }
        public ColoredGlyph Appearance
        {
            get => _glyph;
            set
            {
                if (value == null) throw new System.NullReferenceException();
                _glyph = value;
                IsDirty = true;
            }
        }
        public Point Position
        {
            get => _position;
            set
            {
                if (_position == value) return;

                Point oldPoint = _position;
                _position = value;
                OnPositionChanged(oldPoint, _position);
            }
        }
        public bool IsDirty
        {
            get => _glyph.IsDirty;
            set => _glyph.IsDirty = value;
        }

        public IScreenObject Parent
        {
            get => _parentObject;
            set
            {
                if (value == this) throw new Exception("Cannot set parent to itself.");
                if (_parentObject == value) return;

                if (_parentObject == null)
                {
                    _parentObject = value;
                    _parentObject.Children.Add(this);
                    OnParentChanged(null, _parentObject);
                }
                else
                {
                    IScreenObject oldParent = _parentObject;
                    _parentObject = null;
                    oldParent.Children.Remove(this);
                    _parentObject = value;

                    _parentObject?.Children.Add(this);
                    OnParentChanged(oldParent, _parentObject);
                }
            }
        }
        
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;

                _isEnabled = value;
                OnEnabledChanged();
            }
        }
        public bool IsFocused
        {
            get => _isfocused;
            set
            {
                if ((_isfocused && value) || (!_isfocused && !value)) return;

                _isfocused = value;

                if (value)
                {
                    switch (FocusedMode)
                    {
                        case FocusBehavior.Set:
                            GameHost.Instance.FocusedScreenObjects.Set(this);
                            break;
                        case FocusBehavior.Push:
                            GameHost.Instance.FocusedScreenObjects.Push(this);
                            break;
                        default:
                            break;
                    }

                    Focused?.Invoke(this, EventArgs.Empty);
                    OnFocused();
                }
                else
                {
                    if (GameHost.Instance.FocusedScreenObjects.ScreenObject == this && FocusedMode != FocusBehavior.None)
                        GameHost.Instance.FocusedScreenObjects.Pop(this);

                    FocusLost?.Invoke(this, EventArgs.Empty);
                    OnFocusLost();
                }
            }
        }
        #endregion

        #region initialization
        public RogueLikeEntity(Point position, int glyph, bool walkable = true, bool transparent = true, int layer = 0) : this(Color.White, Color.Black, glyph)
        {
            Position = position;
            IsWalkable = walkable;
            IsTransparent = transparent;
            Layer = layer;
        }

        public RogueLikeEntity(Point position, Color foreground, int glyph) : this(foreground, Color.Black, glyph)
        {
            Position = position;
        }
        public RogueLikeEntity(Color foreground, Color background, int glyph) : base(foreground, background, glyph)
        {
            IsWalkable = true;
            IsTransparent = true;
            UseMouse = Settings.DefaultScreenObjectUseMouse;
            UseKeyboard = Settings.DefaultScreenObjectUseKeyboard;
            
            Components = new RogueLikeComponentCollection();
            SadComponents = Components;
            GoRogueComponents = Components;
            Children = new ScreenObjectCollection(this);
            
            Moved += SadConsole_Moved;
            Moved += GoRogue_Moved;

            Components.ComponentAdded += OnComponentAdded;
            Components.ComponentRemoved += OnComponentRemoved;
        }
        #endregion
        
        #region motion
        public void OnMapChanged(Map? newMap)
        {
            if (newMap != null)
            {
                if (Layer == 0)
                {
                    if (newMap.Terrain[Position] != this)
                    {
                        return; //do nothing
                    }
                }
                else if (!newMap.Entities.Contains(this)) // It's an entity
                {
                    return;//do nothing
                }
            }
            CurrentMap = newMap;
        }

        public bool CanMove(Point position) => CurrentMap.GetTerrainAt(position).IsWalkable;
        public bool CanMoveIn(Direction direction) => CanMove(Position + direction);

        #endregion
        
        #region event handlers
        protected void OnSerializingMethod(StreamingContext context)
        {
            _childrenSerialized = Children.ToArray();
            _componentsSerialized = SadComponents.ToArray();
        }
        private void OnSerialized(StreamingContext context)
        {
            _childrenSerialized = null;
            _componentsSerialized = null;
        }
        private void OnDeserialized(StreamingContext context)
        {
            foreach (var item in _childrenSerialized)
                Children.Add(item);

            foreach (var item in _componentsSerialized)
                SadComponents.Add(item);

            _componentsSerialized = null;
            _childrenSerialized = null;

            UpdateAbsolutePosition();
        }
        public virtual void LostMouse(MouseScreenObjectState state) { }
        public virtual void OnFocusLost() { }
        public virtual void OnFocused() { }
        private void GoRogue_Moved(object? sender, GameObjectPropertyChanged<Point> change)
        {
            if (Position != change.NewValue)
            {
                Position = change.NewValue;
                if (((IScreenObject)this).Position != change.NewValue)
                    Position = change.OldValue;
            }
        }
        private void SadConsole_Moved(object? sender, GameObjectPropertyChanged<Point> change)
        {
            if (Position != change.NewValue)
            {
                Position = change.NewValue;
                if (((IGameObject)this).Position != change.NewValue)
                    Position = change.OldValue;
            }
        }
        private void OnComponentAdded(object? s, ComponentChangedEventArgs e)
        {
            if (!(e.Component is IGameObjectComponent c))
                return;

            if (c.Parent != null)
                throw new ArgumentException(
                    $"Components implementing {nameof(IGameObjectComponent)} cannot be added to multiple objects at once.");

            c.Parent = this;
        }
        
        private void OnComponentRemoved(object? s, ComponentChangedEventArgs e)
        {
            if (e.Component is IGameObjectComponent c)
                c.Parent = null;
        }
        protected virtual void OnParentChanged(IScreenObject oldParent, IScreenObject newParent)
        {
            UpdateAbsolutePosition();
            ParentChanged?.Invoke(this, new NewOldValueEventArgs<IScreenObject>(oldParent, newParent));
        }
        protected virtual void OnPositionChanged(Point oldPosition, Point newPosition)
        {
            UpdateAbsolutePosition();
            PositionChanged?.Invoke(this, new NewOldValueEventArgs<Point>(oldPosition, newPosition));
        }
        protected virtual void OnVisibleChanged() =>
            VisibleChanged?.Invoke(this, EventArgs.Empty);

        protected virtual void OnEnabledChanged() =>
            EnabledChanged?.Invoke(this, EventArgs.Empty);

        #endregion
        
        #region ScreenObject
        public virtual void Render(TimeSpan delta)
        {
            if (IsVisible)
                Components.Render(delta);
        }
        public virtual void Update(TimeSpan delta)
        {
            if (IsEnabled)
                Components.Update(delta);
        }
        public virtual bool ProcessKeyboard(Keyboard keyboard)
        {
            if (UseKeyboard)
                return Components.ProcessKeyboard(keyboard);
            
            return false;
        }

        public virtual bool ProcessMouse(MouseScreenObjectState state)
        {
            if (IsVisible && UseMouse)
                return Components.ProcessMouse(state);
            
            return false;
        }
        
        public virtual void UpdateAbsolutePosition()
        {
            AbsolutePosition = Position + (Parent?.AbsolutePosition ?? new Point(0, 0));

            foreach (IScreenObject child in Children)
                child.UpdateAbsolutePosition();
        }

        #endregion
        #region components
        public void AddComponent(IRogueLikeComponent component)
        {
            SadComponents.Add(component);
            GoRogueComponents.Add(component);
        }
        public void AddComponents(IEnumerable<IRogueLikeComponent> components)
        {
            foreach (var component in components)
                SadComponents.Add(component);
            
            GoRogueComponents.Add(components);
        }

        public T GetComponent<T>(string tag = "")
        {
            if (tag is "")
            {
                return GetComponents<T>().Distinct().FirstOrDefault();
            }
            else
            {
                //temporary
                return GetComponents<T>().Distinct().FirstOrDefault();
            }
        }
        public IEnumerable<IRogueLikeComponent> GetComponents()
            => GetGoRogueComponents().Concat(GetSadComponents<IRogueLikeComponent>());

        public IEnumerable<T> GetComponents<T>()
        {
            foreach (var component in GetComponents())
            {
                if (component is T rlComponent)
                {
                    yield return rlComponent;
                }
            }
        }
        private IEnumerable<IRogueLikeComponent> GetGoRogueComponents()
        {
            foreach (var pair in GoRogueComponents)
            {
                yield return (IRogueLikeComponent)pair.Component;
            }
        }

        public IEnumerable<TComponent> GetSadComponents<TComponent>() where TComponent : class, IComponent
        {
            foreach (IComponent component in SadComponents)
            {
                if (component is TComponent tComponent)
                {
                    yield return tComponent;
                }
            }
        }

        public TComponent GetSadComponent<TComponent>() where TComponent : class, IComponent
        {
            foreach (IComponent component in SadComponents)
            {
                if (component is TComponent)
                    return (TComponent)component;
            }

            return null;
        }

        public bool HasSadComponent<TComponent>(out TComponent component)
            where TComponent: class, IComponent
        {
            foreach (IComponent comp in SadComponents)
            {
                if (comp is TComponent)
                {
                    component = (TComponent)comp;
                    return true;
                }
            }

            component = null;
            return false;
        }
        public void SortComponents()
        {
            
        }

        static int CompareComponent(IComponent left, IComponent right)
        {
            if (left.SortOrder > right.SortOrder)
                return 1;

            if (left.SortOrder < right.SortOrder)
                return -1;

            return 0;
        }
        //todo - HasComponent<T>()
        //todo - HasComponents(???)
        //todo - HasComponent(string name)
        //todo - GetComponent<T>()
        //todo - GetComponents(???)
        //todo - RemoveComponent<T>()
        //todo - RemoveComponents(???)
        #endregion
    }
}