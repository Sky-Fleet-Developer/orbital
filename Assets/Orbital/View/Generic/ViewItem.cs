namespace Orbital.View.Generic
{
    public class ViewItem<TModel, TViewGroup> where TViewGroup : ViewGroup
    {
        protected TModel Model;
        protected TViewGroup Group;
        public ViewItem(TModel model, TViewGroup group)
        {
            Model = model;
            Group = group;
        }

        public virtual void Update(){}
    }
}
