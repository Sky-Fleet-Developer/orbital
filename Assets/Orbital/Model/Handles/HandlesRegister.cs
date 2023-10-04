using Zenject;

namespace Orbital.Model.Handles
{
    public static class HandlesRegister
    {
        public static void RegisterHandlers<T>(T target, DiContainer container)
        { 
            if(target is IUpdateHandler iupdatehandler)
            {
                HandlerCollection.GetOrCreateCollection<IUpdateHandler>(container).AddItem(iupdatehandler);
            }
 
            if(target is IFixedUpdateHandler ifixedupdatehandler)
            {
                HandlerCollection.GetOrCreateCollection<IFixedUpdateHandler>(container).AddItem(ifixedupdatehandler);
            }
 
            if(target is IUpdateByFrequencyHandler iupdatebyfrequencyhandler)
            {
                HandlerCollection.GetOrCreateCollection<IUpdateByFrequencyHandler>(container).AddItem(iupdatebyfrequencyhandler);
            }
 
            if(target is IObserverTriggerHandler iobservertriggerhandler)
            {
                HandlerCollection.GetOrCreateCollection<IObserverTriggerHandler>(container).AddItem(iobservertriggerhandler);
            }
        }
        public static void UnregisterHandlers<T>(T target, DiContainer container)
        { 
            if(target is IUpdateHandler iupdatehandler)
            {
                HandlerCollection.GetOrCreateCollection<IUpdateHandler>(container).RemoveItem(iupdatehandler);
            }
 
            if(target is IFixedUpdateHandler ifixedupdatehandler)
            {
                HandlerCollection.GetOrCreateCollection<IFixedUpdateHandler>(container).RemoveItem(ifixedupdatehandler);
            }
 
            if(target is IUpdateByFrequencyHandler iupdatebyfrequencyhandler)
            {
                HandlerCollection.GetOrCreateCollection<IUpdateByFrequencyHandler>(container).RemoveItem(iupdatebyfrequencyhandler);
            }
 
            if(target is IObserverTriggerHandler iobservertriggerhandler)
            {
                HandlerCollection.GetOrCreateCollection<IObserverTriggerHandler>(container).RemoveItem(iobservertriggerhandler);
            }
        }
    }
}