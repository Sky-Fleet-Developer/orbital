using System;
using UnityEngine;

namespace Orbital.Navigation
{
    [Serializable]
    public class Path
    {
        private PathNode _begin;
        public PathNode GetFirstElement() => _begin;
        public Element GetLastElement() => _begin.GetLastElement();
        
        [Obsolete("For json only")]
        public Path(){}

        public Path(PathNode firstElement)
        {
            _begin = firstElement;
            _begin.SetDirty();
        }

        public void AddElement(Element element)
        {
            _begin.GetLastElement().Next = element;
            element.SetDirty();
        }
    }
}