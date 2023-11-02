namespace Orbital.Navigation
{
    public static class PathBuilder
    {
        private const int MaxElementsCount = 10;
        public static void BuildTransitions(this PathRoot root)
        {
            root.CallRefresh();

            SampleHolderNode element = root.FirstNextOfType<SampleHolderNode>(true);
            while (element != null && element.Ending.Type != OrbitEndingType.Cycle)
            {
                element = element.FirstNextOfType<SampleHolderNode>();
            }
            if (element != null && element.Next != null)
            {
                element.Next = null;
            }
            
            SampleHolderNode last = root.GetLastElement().FirstPreviousOfType<SampleHolderNode>(true);
            int elementsCount = root.GetElementsCount();
            while (last.Ending.Type != OrbitEndingType.Cycle && elementsCount < MaxElementsCount)
            {
                var newNode = new PathNode();
                root.AddElement(newNode);
                last = newNode;
                elementsCount++;
            }
        }
    }
}
