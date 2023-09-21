using System.Collections.Generic;

namespace Patterns.AbstractFactory
{
    public interface IGenerator<DefineT, ObjectT>
    {
        public ObjectT Generate(DefineT define);

        public bool CheckDefine(DefineT define);
    }

    public abstract class AbstractFactory<DefineT, ObjectT>
    {
        private List<IGenerator<DefineT, ObjectT>> generators;
        private IGenerator<DefineT, ObjectT> cashGenerator;

        public AbstractFactory()
        {
            generators = new List<IGenerator<DefineT, ObjectT>>();
            cashGenerator = null;
        }

        public void RegisterNewType(IGenerator<DefineT, ObjectT> generator)
        {
            generators.Add(generator);
        }

        public ObjectT Generate(DefineT define)
        {
            if(cashGenerator != null && cashGenerator.CheckDefine(define))
            {
                return cashGenerator.Generate(define);
            }
            for(int i = 0; i < generators.Count; i++)
            {
                if(generators[i].CheckDefine(define))
                {
                    cashGenerator = generators[i];
                    return generators[i].Generate(define);
                }
            }
            return GetDefault();
        }

        protected abstract ObjectT GetDefault();
    }
}
