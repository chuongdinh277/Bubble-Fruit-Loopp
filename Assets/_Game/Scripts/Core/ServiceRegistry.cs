using System;
using System.Collections.Generic;

namespace BubbleFruitLoop.Core
{
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> services = new();

        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            services[typeof(T)] = service;
        }

        public T Resolve<T>() where T : class
        {
            if (services.TryGetValue(typeof(T), out object value)) return (T)value;
            throw new InvalidOperationException($"Service {typeof(T).Name} was not registered.");
        }
    }
}
