namespace Orbital.Navigation
{
    public static class PathBuilder
    {
        private const int MaxElementsCount = 10;
        public static void BuildTransitions(this NavigationPath root, double timeAhead)
        {
            /*SampleHolderNode element = root.FirstNextOfType<SampleHolderNode>(true);
            while (element != null && element.Ending.Type != OrbitEndingType.Cycle)
            {
                element = element.FirstNextOfType<SampleHolderNode>();
            }
            if (element != null && element.Next != null)
            {
                element.Next = null;
            }*/
            
            PathElement last = root;
            int elementsCount = root.GetElementsCount();
            while (last.Ending.Type != OrbitEndingType.Cycle && elementsCount < MaxElementsCount && last.Ending.Time < timeAhead)
            {
                if (last.Next == null)
                {
                    var newNode = new PathNode();
                    root.AddElement(newNode);
                    last = newNode;
                    elementsCount++;
                }
                else
                {
                    last = last.Next;
                }
            }

            if (last.Next != null)
            {
                root.RemoveAtElement(last.Next);
            }

            root.RefreshDirty();
        }
    }
}
