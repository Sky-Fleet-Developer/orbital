using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Orbital.Core.Serialization.Sqlite
{
    public class TableSet<T> : IEnumerable<T> where T : ModelBase
    {
        private Dictionary<int, DifferenceType> differences = new ();
        private Dictionary<int, T> _elements = new ();
        public string TableName { get; }
        public TableSet(string tableName)
        {
            TableName = tableName;
        }
        
        public T this[int id]
        {
            get => _elements[id];
            set
            {
                _elements[id] = value;
                OverwriteDiff(id, DifferenceType.Update);
            }
        }

        public void Fill(DataTable dataTable, ModelType model)
        {
            var rows = dataTable.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                T element = (T) model.CreateModelByRow(rows[i]);
                if (_elements.ContainsKey(element.Id))
                {
                    _elements[element.Id] = element;
                }
                else
                {
                    _elements.Add(element.Id, element);
                }
            }
        }

        public bool HasChanges() => differences.Count > 0;
        public IEnumerable<(DifferenceType differenceType, T element, int id)> GetDifference(ModelType model)
        {
            foreach (KeyValuePair<int, DifferenceType> pair in differences)
            {
                switch (pair.Value)
                {
                    case DifferenceType.Update: case DifferenceType.Add:
                        yield return (pair.Value, _elements[pair.Key], pair.Key);
                        break;
                    case DifferenceType.Remove:
                        yield return (pair.Value, null, pair.Key);
                        break;
                }
            }
        }
        
        public void Remove(int id)
        {
            _elements[id] = null;
            OverwriteDiff(id, DifferenceType.Remove);
        }
        
        public void Remove(T value)
        {
            int id = _elements.First(x => x.Value == value).Key;
            Remove(id);
        }

        public void Add(T value)
        {
            if (!_elements.ContainsKey(value.Id))
            {
                _elements.Add(value.Id, value);
                OverwriteDiff(value.Id, DifferenceType.Add);
            }
            else
            {
                _elements[value.Id] = value;
                OverwriteDiff(value.Id, DifferenceType.Update);
            }
        }

        public void Clear()
        {
            foreach (int key in _elements.Keys.ToArray())
            {
                _elements[key] = null;
                OverwriteDiff(key, DifferenceType.Remove);
            }
        }

        private void OverwriteDiff(int id, DifferenceType difference)
        {
            if (differences.TryGetValue(id, out DifferenceType oldDiff))
            {
                if(oldDiff == difference) return;

                if (oldDiff == DifferenceType.Remove && difference == DifferenceType.Add)
                {
                    differences[id] = DifferenceType.Update;
                }
                else if (oldDiff == DifferenceType.Add && difference == DifferenceType.Remove)
                {
                    differences.Remove(id);
                }
                else if (oldDiff == DifferenceType.Add && difference == DifferenceType.Update)
                {
                    return;
                }
                else
                {
                    differences[id] = difference;
                }
            }
            else
            {
                differences.Add(id, difference);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            foreach (T elementsValue in _elements.Values)
            {
                if (elementsValue != null) yield return elementsValue;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}