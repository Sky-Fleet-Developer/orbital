using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Orbital.Core.Bodies
{
    /// <summary>One row of the <see cref="BodyTable"/>: a body name and the facets it has.</summary>
    [Serializable]
    [MovedFrom(false, sourceNamespace: "Orbital.View.Map", sourceAssembly: "Orbital", sourceClassName: "BodyViewRecord")]
    public class BodyRecord : ISerializationCallbackReceiver
    {
        public const int MaxFacets = 64;

        public string name;
        [SerializeReference] public List<IBodyFacet> facets = new ();

        private ulong _mask;
        private readonly IBodyFacet[] _byId = new IBodyFacet[MaxFacets];

        public bool HasFacet<T>() where T : class, IBodyFacet
        {
            return (_mask & (1UL << FacetId<T>.Value)) != 0;
        }

        /// <summary>The facet cast to <typeparamref name="T"/>, or null when the body does not have it.</summary>
        public T GetFacet<T>() where T : class, IBodyFacet
        {
            int id = FacetId<T>.Value;
            return (_mask & (1UL << id)) != 0 ? (T) _byId[id] : null;
        }

        public bool TryGetFacet<T>(out T facet) where T : class, IBodyFacet
        {
            facet = GetFacet<T>();
            return facet != null;
        }

        /// <summary>Rebuilds the mask and the id index from the list.</summary>
        public void Rebuild()
        {
            _mask = 0;
            Array.Clear(_byId, 0, _byId.Length);
            if (facets == null) return;
            foreach (IBodyFacet facet in facets)
            {
                if (facet == null) continue;
                int id = facet.Id;
                if (id < 0 || id >= MaxFacets)
                {
                    Debug.LogError($"Body '{name}': facet {facet.GetType().Name} has id {id}, expected 0..{MaxFacets - 1}");
                    continue;
                }
                if ((_mask & (1UL << id)) != 0)
                {
                    Debug.LogError($"Body '{name}': more than one facet with id {id} ({facet.GetType().Name})");
                    continue;
                }
                _mask |= 1UL << id;
                _byId[id] = facet;
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            Rebuild();
        }
    }
}
