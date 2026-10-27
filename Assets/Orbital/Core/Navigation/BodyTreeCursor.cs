using System.Collections.Generic;
using System.Linq;

namespace Orbital.Core.Navigation
{
    /// <summary>
    /// Selection over the tree of static bodies with hierarchy-list semantics: only expanded nodes are walkable by Up/Down.
    /// Engine-free, so it can be covered by EditMode tests.
    /// </summary>
    public class BodyTreeCursor
    {
        private readonly HashSet<IStaticBody> _expanded = new ();
        private readonly List<IStaticBody> _visible = new ();
        private readonly List<int> _depths = new ();

        public IStaticBody Root { get; }
        public IStaticBody Current { get; private set; }
        public IReadOnlyList<IStaticBody> Visible => _visible;
        public IReadOnlyList<int> Depths => _depths;

        public BodyTreeCursor(IStaticBody root)
        {
            Root = root;
            Current = root;
            _expanded.Add(root);
            Rebuild();
        }

        public bool IsExpanded(IStaticBody body) => _expanded.Contains(body);

        public bool HasChildren(IStaticBody body) => body.Children != null && body.Children.Any();

        /// <summary>Next item of the visible list.</summary>
        public bool MoveDown() => MoveBy(1);

        /// <summary>Previous item of the visible list.</summary>
        public bool MoveUp() => MoveBy(-1);

        /// <summary>Expands the current item, or steps to its first child when it is already expanded.</summary>
        public bool MoveInside()
        {
            if (!HasChildren(Current)) return false;
            if (_expanded.Add(Current))
            {
                Rebuild();
                return true;
            }
            Current = Current.Children.First();
            return true;
        }

        /// <summary>Collapses the current item, or steps to its parent when it is already collapsed.</summary>
        public bool MoveOutside()
        {
            if (_expanded.Remove(Current))
            {
                Rebuild();
                return true;
            }
            if (Current.Parent == null) return false;
            Current = Current.Parent;
            return true;
        }

        private bool MoveBy(int delta)
        {
            int index = _visible.IndexOf(Current) + delta;
            if (index < 0 || index >= _visible.Count) return false;
            Current = _visible[index];
            return true;
        }

        private void Rebuild()
        {
            _visible.Clear();
            _depths.Clear();
            Append(Root, 0);
        }

        private void Append(IStaticBody body, int depth)
        {
            _visible.Add(body);
            _depths.Add(depth);
            if (!_expanded.Contains(body) || body.Children == null) return;
            foreach (IStaticBody child in body.Children)
            {
                Append(child, depth + 1);
            }
        }
    }
}
