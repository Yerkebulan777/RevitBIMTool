using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Database.Providers
{
    /// <summary>
    /// Полностью функциональная коллекция параметров для InMemory провайдера
    /// Эта реализация показывает, как правильно реализовать интерфейс IDataParameterCollection
    /// </summary>
    internal class InMemoryParameterCollection : IDataParameterCollection
    {
        private readonly List<IDataParameter> _parameters = [];

        /// <summary>
        /// Индексатор по строковому имени параметра
        /// Это позволяет писать код типа: collection["@printerName"] = value
        /// </summary>
        public object this[string parameterName]
        {
            get
            {
                IDataParameter parameter = _parameters.FirstOrDefault(p => p.ParameterName == parameterName);
                return parameter?.Value;
            }
            set
            {
                IDataParameter parameter = _parameters.FirstOrDefault(p => p.ParameterName == parameterName);
                if (parameter != null)
                {
                    parameter.Value = value;
                }
                else
                {
                    // Если параметр не найден, создаем новый
                    InMemoryParameter newParameter = new()
                    {
                        ParameterName = parameterName,
                        Value = value
                    };
                    _parameters.Add(newParameter);
                }
            }
        }

        /// <summary>
        /// Индексатор по числовому индексу
        /// Это позволяет писать код типа: collection[0] = value
        /// </summary>
        public object this[int index]
        {
            get => index >= 0 && index < _parameters.Count
                    ? _parameters[index].Value
                    : throw new ArgumentOutOfRangeException(nameof(index), $"Parameter index {index} is out of range");
            set
            {
                if (index < 0 || index >= _parameters.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), $"Parameter index {index} is out of range");
                }

                _parameters[index].Value = value;
            }
        }

        public int Count => _parameters.Count;
        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public bool IsSynchronized => false;
        public object SyncRoot => _parameters;

        public int Add(object value)
        {
            if (value is IDataParameter parameter)
            {
                _parameters.Add(parameter);
                return _parameters.Count - 1;
            }
            throw new ArgumentException("Value must be an IDataParameter", nameof(value));
        }

        public void Clear()
        {
            _parameters.Clear();
        }

        public bool Contains(object value)
        {
            return value is IDataParameter param && _parameters.Contains(param);
        }

        public bool Contains(string parameterName)
        {
            return _parameters.Any(p => p.ParameterName == parameterName);
        }

        public void CopyTo(Array array, int index)
        {
            if (array is IDataParameter[] paramArray)
            {
                _parameters.CopyTo(paramArray, index);
            }
            else
            {
                throw new ArgumentException("Array must be of type IDataParameter[]");
            }
        }

        public IEnumerator GetEnumerator()
        {
            return _parameters.GetEnumerator();
        }

        public int IndexOf(object value)
        {
            return value is IDataParameter param ? _parameters.IndexOf(param) : -1;
        }

        public int IndexOf(string parameterName)
        {
            return _parameters.FindIndex(p => p.ParameterName == parameterName);
        }

        public void Insert(int index, object value)
        {
            if (value is IDataParameter parameter)
            {
                _parameters.Insert(index, parameter);
            }
            else
            {
                throw new ArgumentException("Value must be an IDataParameter", nameof(value));
            }
        }

        public void Remove(object value)
        {
            if (value is IDataParameter parameter)
            {
                _ = _parameters.Remove(parameter);
            }
        }

        public void RemoveAt(int index)
        {
            _parameters.RemoveAt(index);
        }

        public void RemoveAt(string parameterName)
        {
            int index = IndexOf(parameterName);
            if (index >= 0)
            {
                RemoveAt(index);
            }
        }
    }
}