using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleFruitLoop.Pooling
{
    public sealed class ComponentPool<T> where T : Component, IPoolable
    {
        private readonly Queue<T> inactive = new();
        private readonly HashSet<T> active = new();
        private readonly Func<T> factory;
        private readonly Transform root;

        public int ActiveCount => active.Count;
        public int TotalCount => active.Count + inactive.Count;

        public ComponentPool(Func<T> factory, Transform root, int warmCount)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.root = root != null ? root : throw new ArgumentNullException(nameof(root));
            Warm(warmCount);
        }

        public T Spawn()
        {
            T item = inactive.Count > 0 ? inactive.Dequeue() : Create();
            active.Add(item);
            item.gameObject.SetActive(true);
            item.OnSpawned();
            return item;
        }

        public void AdoptActive(T item)
        {
            if (item == null || active.Contains(item)) return;
            item.transform.SetParent(null, true);
            item.gameObject.SetActive(true);
            active.Add(item);
            item.OnSpawned();
        }

        public void Despawn(T item)
        {
            if (item == null || !active.Remove(item)) return;
            item.OnDespawned();
            item.transform.SetParent(root, false);
            item.gameObject.SetActive(false);
            inactive.Enqueue(item);
        }

        public void DespawnAll()
        {
            if (active.Count == 0) return;
            // Snapshot once because Despawn mutates the active set.
            T[] snapshot = new T[active.Count];
            active.CopyTo(snapshot);
            for (int index = 0; index < snapshot.Length; index++) Despawn(snapshot[index]);
        }

        private void Warm(int count)
        {
            for (int index = 0; index < Mathf.Max(0, count); index++) inactive.Enqueue(Create());
        }

        private T Create()
        {
            T item = factory();
            item.transform.SetParent(root, false);
            item.gameObject.SetActive(false);
            return item;
        }
    }
}
